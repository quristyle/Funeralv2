using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 계정에 매달린 <b>차량</b>. 게이트웨이의 <c>cargotrust/admin/vehicles</c> 로 나간다.
///
/// <para>
/// [왜 AdminClient 에 얹지 않았나]
/// </para>
///
/// <para>
/// <see cref="AdminClient"/> 도 이미 세 서비스를 넘나들지만, 그 셋은 포털을 쓰면
/// 반드시 있는 것들이다(계정 · 푸시 · 알림). 차량은 다르다 — <b>운송관리를 쓰는
/// 곳에만 있는 개념</b>이고, 안 쓰는 설치본에서는 메뉴를 끄면 그만이어야 한다.
/// 통로를 따로 두면 그 선택이 파일 하나로 보인다.
/// </para>
///
/// <para>
/// [자료의 주인은 여기가 아니다]
/// </para>
///
/// <para>
/// 차량 줄은 cargotrust DB 에 있다. 계정 표(scom.accounts)에 붙이지 않은 까닭은
/// 그 표가 전사 공용이기 때문이다 — 장례식장·헬프데스크 계정까지 축수와
/// 하이패스 여부를 지게 할 수 없다. <b>관리하는 자리와 자료의 주인은 다른 문제다.</b>
/// 계정과 차량을 잇는 열쇠는 로그인 아이디(<c>LoginId</c>)이고, 그것이 곧
/// 게이트웨이가 각 서비스에 보내는 <c>X-User-Id</c> 다.
/// </para>
/// </summary>
public sealed class CargoVehicleClient(GatewayClient gateway)
{
    private const string Prefix = "cargotrust/admin";

    /// <summary>
    /// 번호판 읽기. <b>저장하지 않는다</b> — 칸을 채우기 전에 「이렇게 읽었다」를
    /// 먼저 보여 주고, 사람이 그 위에서 고치게 한다.
    /// </summary>
    public Task<PlateReadDto?> ReadPlateAsync(string plateNo, CancellationToken ct = default)
        => gateway.GetOneAsync<PlateReadDto>(
            $"{Prefix}/vehicles/plate?no={Uri.EscapeDataString(plateNo)}", ct);

    /// <summary>차량 목록. <paramref name="q"/> 는 계정 아이디·이름·번호판·별칭을 함께 본다.</summary>
    public Task<IReadOnlyList<CargoVehicleDto>> GetVehiclesAsync(
        string? q = null, string? externalUserId = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrWhiteSpace(externalUserId)) query.Add($"externalUserId={Uri.EscapeDataString(externalUserId)}");
        var path = query.Count == 0 ? $"{Prefix}/vehicles" : $"{Prefix}/vehicles?{string.Join('&', query)}";
        return gateway.GetListAsync<CargoVehicleDto>(path, ct);
    }

    /// <summary>
    /// 계정을 지정해 등록한다. 그 계정에 운송관리 사용자 줄이 아직 없어도 된다 —
    /// 서버가 만들어 준다(그래서 이름도 함께 보낸다).
    /// </summary>
    public Task<CargoVehicleDto?> CreateVehicleAsync(CargoVehicleSaveDto vehicle, CancellationToken ct = default)
        => gateway.PostAsync<CargoVehicleDto>($"{Prefix}/vehicles", vehicle, ct);

    public Task<CargoVehicleDto?> UpdateVehicleAsync(long vehicleId, CargoVehicleSaveDto vehicle, CancellationToken ct = default)
        => gateway.PutAsync<CargoVehicleDto>($"{Prefix}/vehicles/{vehicleId}", vehicle, ct);

    public Task DeleteVehicleAsync(long vehicleId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/vehicles/{vehicleId}", ct);
}
