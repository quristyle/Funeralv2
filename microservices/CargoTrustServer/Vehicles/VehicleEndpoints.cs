using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Toll;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Vehicles;

/// <summary>번호판이 말한 것. 화면이 그대로 띄우고, 저장된 값과 견주는 데 쓴다.</summary>
public record PlateReadDto(
    string PlateNo,
    string? Region,
    int? ClassNumber,
    string? UsageChar,
    string Kind,
    string KindName,
    string Usage,
    string UsageName,
    bool? IsBusiness,
    bool? IsFreight,
    string? SuggestedClass,
    bool Readable,
    string Summary);

/// <summary>차량 한 대.</summary>
public record VehicleDto(
    long VehicleId,
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
    DateTimeOffset CreatedAt);

/// <summary>차량 등록·수정에 받는 것.</summary>
public record VehicleSaveRequest(
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
/// 내 차량 — <c>/vehicles/*</c>.
///
/// <para>
/// [차량번호는 남에게 안 간다]
/// </para>
///
/// <para>
/// 이 묶음이 내보내는 줄은 <b>부른 사람 자신의 차량</b>뿐이다. 거래·후기처럼
/// 남이 보는 자리에는 차량이 실리지 않는다 — 번호판은 사람과 차를 한 번에
/// 가리키는 값이라 이름을 가리는 것(<c>PersonName</c>)만으로는 모자란다.
/// 관리자는 계정관리 쪽 <c>/admin/vehicles</c> 로 본다.
/// </para>
/// </summary>
public static class VehicleEndpoints
{
    public static void MapVehicleEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/vehicles").WithTags("Vehicle");

        group.MapGet("/plate", ReadPlate).WithSummary("번호판 읽기 — 화물차인가 · 사업용인가");
        group.MapGet("/", List).WithSummary("내 차량");
        group.MapPost("/", Create).WithSummary("차량 등록");
        group.MapPut("/{id:long}", Update).WithSummary("차량 수정");
        group.MapDelete("/{id:long}", Delete).WithSummary("차량 삭제(표시만)");
    }

    /// <summary>
    /// 번호판만 읽어 돌려준다. <b>저장하지 않는다</b> — 화면이 칸을 채우기 전에
    /// 「이렇게 읽었다」를 먼저 보여 주고, 사람이 그 위에서 고치게 한다.
    /// </summary>
    private static IResult ReadPlate(string? no) =>
        Results.Ok(VehicleMap.Plate(PlateNumber.Read(no)));

    private static async Task<IResult> List(CargoTrustDbContext db, CurrentUser me, CancellationToken ct)
    {
        var rows = await Mine(db, me).OrderByDescending(v => v.IsDefault).ThenBy(v => v.VehicleId).ToListAsync(ct);
        return Results.Ok(rows.Select(Map).ToList());
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, CurrentUser me, AuditService audit, VehicleSaveRequest req, CancellationToken ct)
    {
        if (Validate(req, out var plate, out var cls) is { } problem) return ApiError.BadRequest(problem);

        if (await db.Vehicles.AnyAsync(v => v.UserId == me.UserId && v.PlateNo == plate && !v.IsDeleted, ct))
            return ApiError.Conflict("이미 등록한 차량번호입니다.");

        var vehicle = new Vehicle
        {
            UserId = me.UserId,
            PlateNo = plate,
            Nickname = Check.Clean(req.Nickname),
            VehicleClass = cls,
            AxleCount = req.AxleCount,
            Tonnage = req.Tonnage,
            IsBusiness = req.IsBusiness,
            HasHipass = req.HasHipass,
            Memo = Check.Clean(req.Memo),
        };
        // 번호판은 **서버가 읽는다.** 화면이 읽어 보낸 값을 그대로 믿으면 두 곳의
        // 해석이 갈릴 수 있고, 갈린 쪽을 나중에 가려낼 방법이 없다.
        Stamp(vehicle);

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        // 첫 대는 말없이 기본 차량이 된다 — 한 대뿐인데 「기본으로 지정」을 한 번 더
        // 누르게 할 이유가 없다.
        var onlyOne = await db.Vehicles.CountAsync(v => v.UserId == me.UserId && !v.IsDeleted, ct) == 1;
        if (req.IsDefault || onlyOne) await MakeDefaultAsync(db, me.UserId, vehicle, ct);

        audit.Add(VehicleAudit.Create, VehicleAudit.Target, vehicle.VehicleId, null, audit.Snapshot(vehicle));
        await db.SaveChangesAsync(ct);
        return Results.Ok(Map(vehicle));
    }

    private static async Task<IResult> Update(
        CargoTrustDbContext db, CurrentUser me, AuditService audit, long id, VehicleSaveRequest req, CancellationToken ct)
    {
        if (Validate(req, out var plate, out var cls) is { } problem) return ApiError.BadRequest(problem);

        var vehicle = await Mine(db, me).FirstOrDefaultAsync(v => v.VehicleId == id, ct);
        if (vehicle is null) return ApiError.NotFound("차량을 찾을 수 없습니다.");

        if (await db.Vehicles.AnyAsync(v => v.UserId == me.UserId && v.PlateNo == plate && v.VehicleId != id && !v.IsDeleted, ct))
            return ApiError.Conflict("이미 등록한 차량번호입니다.");

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
        Stamp(vehicle);

        if (req.IsDefault) await MakeDefaultAsync(db, me.UserId, vehicle, ct);

        audit.AddChange(VehicleAudit.Update, VehicleAudit.Target, id, before, vehicle);
        await db.SaveChangesAsync(ct);
        return Results.Ok(Map(vehicle));
    }

    private static async Task<IResult> Delete(
        CargoTrustDbContext db, CurrentUser me, AuditService audit, long id, CancellationToken ct)
    {
        var vehicle = await Mine(db, me).FirstOrDefaultAsync(v => v.VehicleId == id, ct);
        if (vehicle is null) return ApiError.NotFound("차량을 찾을 수 없습니다.");

        // 계산 이력이 이 줄을 가리키고 있다. 실제로 지우면 이력이 끊긴다.
        var before = audit.Snapshot(vehicle);
        vehicle.IsDeleted = true;
        vehicle.IsDefault = false;
        vehicle.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange(VehicleAudit.Delete, VehicleAudit.Target, id, before, vehicle);
        await db.SaveChangesAsync(ct);
        return ApiError.Empty();
    }

    // ── 함께 쓰는 것 ─────────────────────────────────────────

    internal static IQueryable<Vehicle> Mine(CargoTrustDbContext db, CurrentUser me) =>
        db.Vehicles.Where(v => v.UserId == me.UserId && !v.IsDeleted);

    /// <summary>기본 차량은 한 사람에 한 대. 다른 것을 내리고 이것을 올린다.</summary>
    internal static async Task MakeDefaultAsync(CargoTrustDbContext db, long userId, Vehicle vehicle, CancellationToken ct)
    {
        var others = await db.Vehicles
            .Where(v => v.UserId == userId && v.VehicleId != vehicle.VehicleId && v.IsDefault)
            .ToListAsync(ct);
        foreach (var other in others) other.IsDefault = false;
        vehicle.IsDefault = true;
    }

    /// <summary>받은 값이 쓸 만한가. 통과하면 다듬은 번호판과 차종을 내보낸다.</summary>
    internal static string? Validate(VehicleSaveRequest req, out string plate, out VehicleClass cls)
    {
        plate = string.Empty;
        cls = VehicleClass.C4;

        // 번호판은 사람마다 「12가3456」·「12 가 3456」처럼 띄어 적는다. 공백을 걷어 내
        // 한 모양으로 저장한다 — 안 그러면 같은 차가 두 줄이 된다.
        var cleaned = new string((req.PlateNo ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (cleaned.Length == 0) return "차량번호를 입력하세요.";
        if (cleaned.Length > 20) return "차량번호는 20자까지입니다.";
        plate = cleaned;

        if (!Code.TryParse<VehicleClass>(req.VehicleClass, out var parsed))
            return $"vehicleClass 는 {Code.Allowed<VehicleClass>()} 중 하나입니다.";
        cls = parsed ?? VehicleClass.C4;

        if (req.AxleCount is { } axles && (axles < 2 || axles > 10))
            return "축수는 2에서 10 사이입니다.";
        if (req.Tonnage is { } tons && (tons < 0 || tons > 9999))
            return "톤수가 올바르지 않습니다.";
        if (Check.MaxLength(req.Nickname, 50, "차량 별칭") is { } nick) return nick;

        return null;
    }

    /// <summary>번호판을 읽어 그 결과를 줄에 찍는다. 등록·수정 양쪽이 쓴다.</summary>
    internal static void Stamp(Vehicle vehicle)
    {
        var read = PlateNumber.Read(vehicle.PlateNo);
        vehicle.PlateRegion = read.Region;
        vehicle.PlateKind = read.Kind;
        vehicle.PlateUsage = read.Usage;
    }

    internal static VehicleDto Map(Vehicle v) => new(
        v.VehicleId, v.PlateNo, v.Nickname,
        v.VehicleClass.ToString(), TollEligibility.VehicleClassName(v.VehicleClass),
        v.AxleCount, v.Tonnage, v.IsBusiness, v.HasHipass, v.IsDefault, v.Memo,
        VehicleMap.Plate(PlateNumber.Read(v.PlateNo)), v.CreatedAt);
}

/// <summary>차량의 감사 기록 이름 — 한 곳에 둔다.</summary>
public static class VehicleAudit
{
    public const string Target = "VEHICLE";
    public const string Create = "VEHICLE_CREATE";
    public const string Update = "VEHICLE_UPDATE";
    public const string Delete = "VEHICLE_DELETE";
    public const string AdminCreate = "ADMIN_VEHICLE_CREATE";
    public const string AdminUpdate = "ADMIN_VEHICLE_UPDATE";
    public const string AdminDelete = "ADMIN_VEHICLE_DELETE";
}

/// <summary>번호판 해석을 바깥 모양으로 옮기는 한 곳 — 사용자·관리자 양쪽이 쓴다.</summary>
public static class VehicleMap
{
    public static PlateReadDto Plate(PlateNumber.Reading r) => new(
        r.Normalized,
        r.Region,
        r.ClassNumber,
        r.UsageChar,
        r.Kind.ToString(),
        PlateNumber.KindName(r.Kind),
        r.Usage.ToString(),
        PlateNumber.UsageName(r.Usage),
        r.IsBusiness,
        r.IsFreight,
        r.SuggestedClass?.ToString(),
        !r.Unreadable,
        r.Summary());
}
