using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>계정에 매달린 차량. 게이트웨이의 <c>cargotrust/admin/vehicles</c> 로 나간다. <see cref="AdminClient"/> 도 이미 세 서비스를
/// 넘나들지만, 그 셋은 포털을 쓰면 반드시 있는 것들이다(계정 · 푸시 · 알림). 차량은 다르다</summary>
public sealed class CargoVehicleClient(GatewayClient gateway)
{
    private const string Prefix = "cargotrust/admin";

    /// <summary>번호판 읽기. 저장하지 않는다 — 칸을 채우기 전에 「이렇게 읽었다」를 먼저 보여 주고, 사람이 그 위에서 고치게 한다.</summary>
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

    /// <summary>계정을 지정해 등록한다. 그 계정에 운송관리 사용자 줄이 아직 없어도 된다 — 서버가 만들어 준다(그래서 이름도 함께 보낸다).</summary>
    public Task<CargoVehicleDto?> CreateVehicleAsync(CargoVehicleSaveDto vehicle, CancellationToken ct = default)
        => gateway.PostAsync<CargoVehicleDto>($"{Prefix}/vehicles", vehicle, ct);

    public Task<CargoVehicleDto?> UpdateVehicleAsync(long vehicleId, CargoVehicleSaveDto vehicle, CancellationToken ct = default)
        => gateway.PutAsync<CargoVehicleDto>($"{Prefix}/vehicles/{vehicleId}", vehicle, ct);

    public Task DeleteVehicleAsync(long vehicleId, CancellationToken ct = default)
        => gateway.DeleteAsync($"{Prefix}/vehicles/{vehicleId}", ct);
}
