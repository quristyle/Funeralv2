using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using CargoTrustServer.Toll;
using CargoTrustServer.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Admin;

/// <summary>관리자가 보는 차량 — 누구 것인지가 함께 온다.</summary>
public record AdminVehicleDto(
    long VehicleId,
    long UserId,
    string ExternalUserId,
    string? DisplayName,
    string PlateNo,
    string? Nickname,
    string VehicleClass,
    string VehicleClassName,
    short? AxleCount,
    decimal? Tonnage,
    bool IsBusiness,
    bool HasHipass,
    bool IsDefault,
    string? Memo,
    PlateReadDto Plate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 관리자가 계정을 지정해 차량을 올린다.
///
/// <para>
/// <c>ExternalUserId</c> 는 포털 로그인 아이디 — 게이트웨이가 보내는 <c>X-User-Id</c> 와
/// 같은 값이다. <c>DisplayName</c> 은 계정 이름으로, 운송관리 줄이 아직 없을 때 함께 넣어 둔다.
/// </para>
/// </summary>
public record AdminVehicleCreateRequest(
    string ExternalUserId,
    string? DisplayName,
    string PlateNo,
    string? Nickname,
    string? VehicleClass,
    short? AxleCount,
    decimal? Tonnage,
    bool IsBusiness = true,
    bool HasHipass = true,
    bool IsDefault = false,
    string? Memo = null);

/// <summary>
/// 관리자 — 차량 (<c>/admin/vehicles</c>).
///
/// <para>
/// [계정에 차량을 매다는 자리다]
/// </para>
///
/// <para>
/// 화면은 <b>계정관리</b>(<c>/admin/system/vehicle</c>)에 있다. 그런데 자료는
/// 여기(cargotrust)에 있다 — 계정 표(scom.accounts)는 전사 공용이라
/// 장례식장·헬프데스크 계정까지 차량 개념을 지게 할 수 없다.
/// <b>관리하는 자리와 자료의 주인은 다른 문제다.</b>
/// </para>
///
/// <para>
/// [운송관리를 한 번도 안 연 계정에도 달 수 있어야 한다]
/// </para>
///
/// <para>
/// 운송관리의 사용자 줄(<c>app_user</c>)은 그 사람이 처음 화면을 열 때 생긴다.
/// 그래서 차량을 미리 등록해 두려면 줄이 없을 수 있다 — 여기서 만들어 준다.
/// 안 그러면 「먼저 그 사람에게 운송관리를 한 번 열어 달라고 하세요」가 되는데,
/// 그건 관리 화면이 할 말이 아니다.
/// </para>
/// </summary>
public static class AdminVehicleEndpoints
{
    public static void MapAdminVehicleEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/vehicles/plate", ReadPlate).WithSummary("번호판 읽기 — 화물차인가 · 사업용인가");
        admin.MapGet("/vehicles", List).WithSummary("차량 목록 — 계정 아이디·이름·번호판으로 찾는다");
        admin.MapPost("/vehicles", Create).WithSummary("계정을 지정해 차량 등록");
        admin.MapPut("/vehicles/{id:long}", Update).WithSummary("차량 수정");
        admin.MapDelete("/vehicles/{id:long}", Delete).WithSummary("차량 삭제(표시만)");
    }

    private static IResult ReadPlate(string? no) =>
        Results.Ok(VehicleMap.Plate(PlateNumber.Read(no)));

    private static async Task<IResult> List(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options,
        string? q, string? externalUserId, CancellationToken ct)
    {
        var query = db.Vehicles.AsNoTracking().Include(v => v.User).Where(v => !v.IsDeleted);

        if (Check.Clean(externalUserId) is { } owner)
            query = query.Where(v => v.User.ExternalUserId == owner);

        if (Check.Clean(q) is { } text)
        {
            var pattern = "%" + CompanyEndpoints.EscapeLike(text) + "%";
            query = query.Where(v =>
                EF.Functions.ILike(v.PlateNo, pattern, "\\")
                || EF.Functions.ILike(v.User.ExternalUserId, pattern, "\\")
                || (v.User.DisplayName != null && EF.Functions.ILike(v.User.DisplayName, pattern, "\\"))
                || (v.Nickname != null && EF.Functions.ILike(v.Nickname, pattern, "\\")));
        }

        var rows = await query
            .OrderBy(v => v.User.ExternalUserId).ThenByDescending(v => v.IsDefault).ThenBy(v => v.VehicleId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);

        return Results.Ok(rows.Select(Map).ToList());
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, AuditService audit, AdminVehicleCreateRequest req, CancellationToken ct)
    {
        var externalId = Check.Clean(req.ExternalUserId);
        if (externalId is null) return ApiError.BadRequest("계정 아이디를 지정하세요.");
        if (externalId.Length > 100) return ApiError.BadRequest("계정 아이디는 100자까지입니다.");

        var save = new VehicleSaveRequest(
            req.PlateNo, req.Nickname, req.VehicleClass, req.AxleCount, req.Tonnage,
            req.IsBusiness, req.HasHipass, req.IsDefault, req.Memo);
        if (VehicleEndpoints.Validate(save, out var plate, out var cls) is { } problem)
            return ApiError.BadRequest(problem);

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalId, ct);
        if (user is null)
        {
            user = new AppUser
            {
                ExternalUserId = externalId,
                DisplayName = Check.Clean(req.DisplayName),
                UserType = UserType.DRIVER,
                Status = UserStatus.ACTIVE,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
        else if (user.DisplayName is null && Check.Clean(req.DisplayName) is { } name)
        {
            user.DisplayName = name;
        }

        if (await db.Vehicles.AnyAsync(v => v.UserId == user.UserId && v.PlateNo == plate && !v.IsDeleted, ct))
            return ApiError.Conflict("이 계정에 이미 등록된 차량번호입니다.");

        var vehicle = new Vehicle
        {
            UserId = user.UserId,
            PlateNo = plate,
            Nickname = Check.Clean(req.Nickname),
            VehicleClass = cls,
            AxleCount = req.AxleCount,
            Tonnage = req.Tonnage,
            IsBusiness = req.IsBusiness,
            HasHipass = req.HasHipass,
            Memo = Check.Clean(req.Memo),
        };
        // 번호판은 서버가 읽는다 — 사용자 쪽과 같은 한 벌을 쓴다.
        VehicleEndpoints.Stamp(vehicle);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        var onlyOne = await db.Vehicles.CountAsync(v => v.UserId == user.UserId && !v.IsDeleted, ct) == 1;
        if (req.IsDefault || onlyOne)
        {
            await VehicleEndpoints.MakeDefaultAsync(db, user.UserId, vehicle, ct);
        }

        audit.Add(VehicleAudit.AdminCreate, VehicleAudit.Target, vehicle.VehicleId, null, audit.Snapshot(vehicle));
        await db.SaveChangesAsync(ct);

        vehicle.User = user;
        return Results.Ok(Map(vehicle));
    }

    private static async Task<IResult> Update(
        CargoTrustDbContext db, AuditService audit, long id, VehicleSaveRequest req, CancellationToken ct)
    {
        if (VehicleEndpoints.Validate(req, out var plate, out var cls) is { } problem)
            return ApiError.BadRequest(problem);

        var vehicle = await db.Vehicles.Include(v => v.User).FirstOrDefaultAsync(v => v.VehicleId == id && !v.IsDeleted, ct);
        if (vehicle is null) return ApiError.NotFound("차량을 찾을 수 없습니다.");

        if (await db.Vehicles.AnyAsync(v => v.UserId == vehicle.UserId && v.PlateNo == plate && v.VehicleId != id && !v.IsDeleted, ct))
            return ApiError.Conflict("이 계정에 이미 등록된 차량번호입니다.");

        var before = audit.Snapshot(vehicle);
        vehicle.PlateNo = plate;
        vehicle.Nickname = Check.Clean(req.Nickname);
        vehicle.VehicleClass = cls;
        vehicle.AxleCount = req.AxleCount;
        vehicle.Tonnage = req.Tonnage;
        vehicle.IsBusiness = req.IsBusiness;
        vehicle.HasHipass = req.HasHipass;
        vehicle.Memo = Check.Clean(req.Memo);
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;
        VehicleEndpoints.Stamp(vehicle);

        if (req.IsDefault) await VehicleEndpoints.MakeDefaultAsync(db, vehicle.UserId, vehicle, ct);

        audit.AddChange(VehicleAudit.AdminUpdate, VehicleAudit.Target, id, before, vehicle);
        await db.SaveChangesAsync(ct);
        return Results.Ok(Map(vehicle));
    }

    private static async Task<IResult> Delete(
        CargoTrustDbContext db, AuditService audit, long id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.Include(v => v.User).FirstOrDefaultAsync(v => v.VehicleId == id && !v.IsDeleted, ct);
        if (vehicle is null) return ApiError.NotFound("차량을 찾을 수 없습니다.");

        var before = audit.Snapshot(vehicle);
        vehicle.IsDeleted = true;
        vehicle.IsDefault = false;
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange(VehicleAudit.AdminDelete, VehicleAudit.Target, id, before, vehicle);
        await db.SaveChangesAsync(ct);
        return ApiError.Empty();
    }

    private static AdminVehicleDto Map(Vehicle v) => new(
        v.VehicleId, v.UserId, v.User.ExternalUserId, v.User.DisplayName,
        v.PlateNo, v.Nickname,
        v.VehicleClass.ToString(), TollEligibility.VehicleClassName(v.VehicleClass),
        v.AxleCount, v.Tonnage, v.IsBusiness, v.HasHipass, v.IsDefault, v.Memo,
        VehicleMap.Plate(PlateNumber.Read(v.PlateNo)),
        v.CreatedAt, v.UpdatedAt);
}
