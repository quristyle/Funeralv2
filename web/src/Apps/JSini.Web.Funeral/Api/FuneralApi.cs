using JSini.Web.Http;

namespace JSini.Web.Funeral.Api;

/// <summary>
/// funeralv2Api 호출. 게이트웨이의 <c>/funeral</c> 아래로 나간다.
///
/// Vue 의 <c>api/funeral/**/index.ts</c> 함수들을 그대로 옮겼다 —
/// 경로·파라미터 이름을 바꾸지 않는다. 백엔드는 이행하는 동안 그대로이기 때문이다.
/// </summary>
public sealed class FuneralApi(GatewayClient gateway)
{
    // ── 건물 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<Building>> GetBuildingsAsync(string? companyId = null, CancellationToken ct = default)
        => gateway.GetListAsync<Building>("funeral/building/info/list" + Query(("companyId", companyId)), ct);

    public Task CreateBuildingAsync(Building data, CancellationToken ct = default)
        => gateway.PostAsync("funeral/building/info", data, ct);

    public Task UpdateBuildingAsync(string id, Building data, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/info/{id}", data, ct);

    public Task DeleteBuildingAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/info/{id}", ct);

    // ── 층 ──────────────────────────────────────────────────────

    public Task<IReadOnlyList<Floor>> GetFloorsAsync(string? buildingId = null, CancellationToken ct = default)
        => gateway.GetListAsync<Floor>("funeral/building/floor/list" + Query(("buildingId", buildingId)), ct);

    public Task CreateFloorAsync(Floor data, CancellationToken ct = default)
        => gateway.PostAsync("funeral/building/floor", data, ct);

    public Task UpdateFloorAsync(string id, Floor data, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/floor/{id}", data, ct);

    public Task DeleteFloorAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/floor/{id}", ct);

    // ── 호실 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<Room>> GetRoomsAsync(
        string? companyId = null, string? buildingId = null, string? floorId = null, CancellationToken ct = default)
        => gateway.GetListAsync<Room>(
            "funeral/building/room/list" + Query(("companyId", companyId), ("buildingId", buildingId), ("floorId", floorId)), ct);

    /// <summary>배정(이동) 가능한 호실 — ACTIVE + 미점유. 빈소현황의 호실 변경이 쓴다.</summary>
    public Task<IReadOnlyList<Room>> GetAvailableRoomsAsync(
        string? buildingId = null, string? companyId = null, string? excludeRoomId = null, CancellationToken ct = default)
        => gateway.GetListAsync<Room>(
            "funeral/building/room/available" + Query(("buildingId", buildingId), ("companyId", companyId), ("excludeRoomId", excludeRoomId)), ct);

    public Task CreateRoomAsync(Room data, CancellationToken ct = default)
        => gateway.PostAsync("funeral/building/room", data, ct);

    public Task UpdateRoomAsync(string id, Room data, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/room/{id}", data, ct);

    public Task DeleteRoomAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/room/{id}", ct);

    // ── 장비 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<Device>> GetDevicesAsync(
        string? companyId = null, string? buildingId = null, string? floorId = null, string? roomId = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<Device>(
            "funeral/building/device/list"
            + Query(("companyId", companyId), ("buildingId", buildingId), ("floorId", floorId), ("roomId", roomId)), ct);

    public Task<Device?> GetDeviceAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<Device>($"funeral/building/device/{id}", ct);

    public Task CreateDeviceAsync(Device data, CancellationToken ct = default)
        => gateway.PostAsync("funeral/building/device", data, ct);

    public Task<Device?> UpdateDeviceAsync(string id, object data, CancellationToken ct = default)
        => gateway.PutAsync<Device>($"funeral/building/device/{id}", data, ct);

    public Task DeleteDeviceAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/device/{id}", ct);

    /// <summary>원격 모니터 전원. DB 에 저장되지 않는 즉시 실행 명령이다.</summary>
    public Task SetDeviceScreenPowerAsync(string code, string state, CancellationToken ct = default)
        => gateway.PostAsync($"funeral/building/device/screen-power/{code}?state={state}", null, ct);

    /// <summary>플레이어 앱 재시작 — 리눅스 장비는 systemd 가 되살린다 (D-RS3).</summary>
    public Task RestartDeviceAppAsync(string code, CancellationToken ct = default)
        => gateway.PostAsync($"funeral/building/device/app-restart/{code}", null, ct);

    /// <summary>
    /// 플레이어에게 <b>지금 새 판을 확인하라</b>고 시킨다 (D-P3).
    ///
    /// <para>
    /// 서버가 파일을 나르지 않는다 — 릴리스 조회와 판정은 플레이어 자신이 한다.
    /// 그래서 잘못 눌러도 <b>새 판이 없으면 아무 일도 일어나지 않는다.</b>
    /// </para>
    /// <para>
    /// 즉시 실행 명령이라 DB 에 남지 않는다. 장비가 실시간 연결에 붙어 있지
    /// 않으면 서버가 거절한다 — 「보냈는데 아무 일도 없는」 상태를 만들지 않기 위해서다.
    /// </para>
    /// </summary>
    public Task UpdateDeviceNowAsync(string code, CancellationToken ct = default)
        => gateway.PostAsync($"funeral/building/device/update-now/{code}", null, ct);

    // ── 장비 기기 설정 ──────────────────────────────────────────

    public Task<IReadOnlyList<DeviceConfig>> GetDeviceConfigsAsync(string? deviceId = null, CancellationToken ct = default)
        => gateway.GetListAsync<DeviceConfig>("funeral/building/device-config/list" + Query(("deviceId", deviceId)), ct);

    public Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId, CancellationToken ct = default)
        => gateway.GetOneAsync<DeviceConfig>($"funeral/building/device-config/{deviceId}", ct);

    public Task<DeviceConfig?> UpsertDeviceConfigAsync(DeviceConfig data, CancellationToken ct = default)
        => gateway.PutAsync<DeviceConfig>("funeral/building/device-config/", data, ct);

    // ── 미디어 소스 (영상 · 음원 · 이미지 · 배경) ────────────────

    public Task<IReadOnlyList<MediaSource>> GetMediaSourcesAsync(string? type = null, CancellationToken ct = default)
        => gateway.GetListAsync<MediaSource>("funeral/building/source/list" + Query(("type", type)), ct);

    public Task CreateMediaSourceAsync(MediaSource data, CancellationToken ct = default)
        => gateway.PostAsync("funeral/building/source", data, ct);

    public Task UpdateMediaSourceAsync(string id, MediaSource data, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/source/{id}", data, ct);

    public Task DeleteMediaSourceAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/source/{id}", ct);

    public Task RetryThumbnailAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync($"funeral/building/source/{id}/retry/thumbnail", null, ct);

    public Task RetryWebmAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync($"funeral/building/source/{id}/retry/webm", null, ct);

    public Task RetryAudioAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync($"funeral/building/source/{id}/retry/audio", null, ct);

    // ── 고인 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<Deceased>> GetDeceasedListAsync(
        IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken ct = default)
        => gateway.GetListAsync<Deceased>(
            "funeral/building/deceased/list"
            + Query((parameters ?? new Dictionary<string, string?>()).Select(p => (p.Key, (object?)p.Value)).ToArray()), ct);

    public Task CreateDeceasedAsync(Deceased data, CancellationToken ct = default)
        => gateway.PostAsync("funeral/building/deceased", data, ct);

    public Task UpdateDeceasedAsync(string id, Deceased data, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/deceased/{id}", data, ct);

    public Task DeleteDeceasedAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/deceased/{id}", ct);

    public Task<DeceasedDetail?> GetDeceasedDetailAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<DeceasedDetail>($"funeral/building/deceased/{id}/detail", ct);

    /// <summary>고인 상세 저장. id 가 비어 있으면 신규 등록이다.</summary>
    public Task<DeceasedDetail?> SaveDeceasedDetailAsync(string? id, DeceasedDetail data, CancellationToken ct = default)
        => gateway.PutAsync<DeceasedDetail>(
            string.IsNullOrEmpty(id) ? "funeral/building/deceased/detail" : $"funeral/building/deceased/{id}/detail",
            data, ct);

    /// <summary>호실 이동 — 배정만 바꾸고 인적 사항은 건드리지 않는다.</summary>
    public Task MoveDeceasedRoomAsync(string id, string roomId, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/deceased/{id}/room?roomId={Uri.EscapeDataString(roomId)}", null, ct);

    /// <summary>출상 처리 — 상태 전환과 배정 해제만 한다.</summary>
    public Task DepartDeceasedAsync(string id, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/deceased/{id}/depart", null, ct);

    public Task CancelDeceasedDepartureAsync(string id, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/deceased/{id}/cancel-departure", null, ct);

    // ── 장비 속성 ───────────────────────────────────────────────

    public Task<DeviceAttribute?> GetDeviceAttributeAsync(string deviceId, CancellationToken ct = default)
        => gateway.GetOneAsync<DeviceAttribute>($"funeral/building/device-attribute/{deviceId}", ct);

    public Task<DeviceAttribute?> UpsertDeviceAttributeAsync(DeviceAttribute data, CancellationToken ct = default)
        => gateway.PutAsync<DeviceAttribute>("funeral/building/device-attribute/", data, ct);

    public Task DeleteDeviceAttributeAsync(string deviceId, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/device-attribute/{deviceId}", ct);

    // ── 장비 리본 ───────────────────────────────────────────────

    public Task<IReadOnlyList<DeviceRibbon>> GetDeviceRibbonsAsync(string deviceId, CancellationToken ct = default)
        => gateway.GetListAsync<DeviceRibbon>($"funeral/building/device-ribbon/by-device/{deviceId}", ct);

    public Task<IReadOnlyList<DeviceRibbon>> BulkSaveDeviceRibbonsAsync(
        string deviceId, IReadOnlyList<DeviceRibbonUpsert> ribbons, CancellationToken ct = default)
        => PutListAsync<DeviceRibbon>("funeral/building/device-ribbon/bulk-save", new { deviceId, ribbons }, ct);

    public Task DeleteDeviceRibbonAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/device-ribbon/{id}", ct);

    // ── 텍스트 오버레이 ─────────────────────────────────────────

    public Task<IReadOnlyList<DeviceTextOverlay>> GetDeviceTextOverlaysAsync(string deviceId, CancellationToken ct = default)
        => gateway.GetListAsync<DeviceTextOverlay>($"funeral/building/device-text-overlay/by-device/{deviceId}", ct);

    public Task<IReadOnlyList<DeviceTextOverlay>> BulkSaveDeviceTextOverlaysAsync(
        string deviceId, IReadOnlyList<DeviceTextOverlay> overlays, CancellationToken ct = default)
        => PutListAsync<DeviceTextOverlay>("funeral/building/device-text-overlay/bulk-save", new { deviceId, overlays }, ct);

    public Task DeleteDeviceTextOverlayAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"funeral/building/device-text-overlay/{id}", ct);

    // ── 빈소 현황 ───────────────────────────────────────────────

    public Task<StatusBoard?> GetFuneralStatusBoardAsync(
        string? buildingId = null, string? floorId = null, bool? onlyInUse = null, CancellationToken ct = default)
        => gateway.GetOneAsync<StatusBoard>(
            "funeral/status/funeral-status/board" + Query(("buildingId", buildingId), ("floorId", floorId), ("onlyInUse", onlyInUse)), ct);

    public Task<IReadOnlyList<FuneralStatus>> GetFuneralStatusesAsync(
        string? buildingId = null, string? floorId = null, bool? onlyInUse = null, CancellationToken ct = default)
        => gateway.GetListAsync<FuneralStatus>(
            "funeral/status/funeral-status/list" + Query(("buildingId", buildingId), ("floorId", floorId), ("onlyInUse", onlyInUse)), ct);

    public Task<FuneralStatus?> GetFuneralStatusDetailAsync(string roomId, CancellationToken ct = default)
        => gateway.GetOneAsync<FuneralStatus>($"funeral/status/funeral-status/{roomId}", ct);

    /// <summary>빈소현황 대시보드 — 호실·고인·장비를 서버가 붙여 준다.</summary>
    public Task<RoomBoard?> GetRoomBoardAsync(
        string? companyId = null, string? buildingId = null, string? floorId = null, string? name = null,
        DateTime? coffinStartDate = null, DateTime? coffinEndDate = null,
        DateTime? burialStartDate = null, DateTime? burialEndDate = null,
        string? detail = null, CancellationToken ct = default)
        => gateway.GetOneAsync<RoomBoard>(
            "funeral/status/room-board" + Query(
                ("companyId", companyId), ("buildingId", buildingId), ("floorId", floorId), ("name", name),
                ("coffinStartDate", coffinStartDate), ("coffinEndDate", coffinEndDate),
                ("burialStartDate", burialStartDate), ("burialEndDate", burialEndDate),
                ("detail", detail)), ct);

    // ── 정보 화면 묶음 ──────────────────────────────────────────

    public Task<IReadOnlyList<RoomHistory>> GetRoomHistoriesAsync(
        string? buildingId = null, string? roomId = null, string? keyword = null,
        DateTime? from = null, DateTime? to = null, bool? inUse = null, CancellationToken ct = default)
        => gateway.GetListAsync<RoomHistory>(
            "funeral/info/room-history/list" + Query(
                ("buildingId", buildingId), ("roomId", roomId), ("keyword", keyword),
                ("from", from), ("to", to), ("inUse", inUse)), ct);

    public Task<IReadOnlyList<DeceasedLookup>> SearchDeceasedAsync(
        string? buildingId = null, string? roomId = null, string? keyword = null, string? status = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => gateway.GetListAsync<DeceasedLookup>(
            "funeral/info/deceased-search/list" + Query(
                ("buildingId", buildingId), ("roomId", roomId), ("keyword", keyword), ("status", status),
                ("from", from), ("to", to)), ct);

    public Task<MyInfo?> GetMyInfoAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<MyInfo>("funeral/info/my-info", ct);

    public Task<IReadOnlyList<DevicePreview>> GetDevicePreviewsAsync(
        string? buildingId = null, string? roomId = null, CancellationToken ct = default)
        => gateway.GetListAsync<DevicePreview>(
            "funeral/info/preview/list" + Query(("buildingId", buildingId), ("roomId", roomId)), ct);

    // ── 통계 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<Billing>> GetBillingStatsAsync(
        string? buildingId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => gateway.GetListAsync<Billing>(
            "funeral/stat/billing/list" + Query(("buildingId", buildingId), ("from", from), ("to", to)), ct);

    public Task<IReadOnlyList<RoomUsage>> GetRoomUsageStatsAsync(
        string? buildingId = null, string? roomId = null, DateTime? from = null, DateTime? to = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<RoomUsage>(
            "funeral/stat/room-usage/list" + Query(("buildingId", buildingId), ("roomId", roomId), ("from", from), ("to", to)), ct);

    public Task<StatSummary?> GetStatSummaryAsync(
        string? buildingId = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => gateway.GetOneAsync<StatSummary>(
            "funeral/stat/summary" + Query(("buildingId", buildingId), ("from", from), ("to", to)), ct);

    // ── 환경설정 ────────────────────────────────────────────────

    public Task<IReadOnlyList<EnvironmentSetting>> GetEnvironmentSettingsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<EnvironmentSetting>("funeral/setting/environment/list", ct);

    /// <summary>한 줄만 바꾼다 (스위치를 누르는 즉시 저장하는 화면용).</summary>
    public Task<EnvironmentSetting?> UpdateEnvironmentSettingAsync(string code, bool enabled, CancellationToken ct = default)
        => gateway.PutAsync<EnvironmentSetting>($"funeral/setting/environment/{code}", new { enabled }, ct);

    /// <summary>여러 줄을 한 번에 바꾼다 (저장 버튼 하나로 끝내는 화면용).</summary>
    public Task UpdateEnvironmentSettingsAsync(IReadOnlyDictionary<string, bool> settings, CancellationToken ct = default)
        => gateway.PutAsync("funeral/setting/environment", new { settings }, ct);

    // ── 음원-건물 배정 ──────────────────────────────────────────

    /// <summary>음원 하나를 고르면 건물 목록에 배정 여부가 붙어 온다.</summary>
    public Task<IReadOnlyList<BuildingMusicMapping>> GetBuildingsForMusicAsync(string mediaSourceId, CancellationToken ct = default)
        => gateway.GetListAsync<BuildingMusicMapping>($"funeral/building/music/{mediaSourceId}/buildings", ct);

    /// <summary>음원 하나의 배정을 통째로 바꾼다. 목록에 없는 건물은 배정이 풀린다.</summary>
    public Task SaveBuildingsForMusicAsync(string mediaSourceId, IReadOnlyList<string> buildingIds, CancellationToken ct = default)
        => gateway.PutAsync($"funeral/building/music/{mediaSourceId}/buildings", new { buildingIds }, ct);

    /// <summary>한 건물에 배정된 음원 아이디 목록.</summary>
    public Task<IReadOnlyList<string>> GetMusicIdsForBuildingAsync(string buildingId, CancellationToken ct = default)
        => gateway.GetListAsync<string>($"funeral/building/music/building/{buildingId}", ct);

    // ── 내부 도우미 ─────────────────────────────────────────────

    /// <summary>
    /// PUT 인데 응답이 목록인 경우 (bulk-save). GatewayClient 의 PutAsync&lt;T&gt; 는
    /// 첫 칸만 꺼내므로 목록이 필요한 곳은 이 우회를 쓴다 — 봉투 해석은
    /// GetListAsync 와 같은 규칙이다.
    /// </summary>
    private async Task<IReadOnlyList<T>> PutListAsync<T>(string path, object body, CancellationToken ct)
    {
        // GatewayClient 에 '목록을 돌려주는 PUT' 이 없어서, 저장 후 재조회로 갈음하지
        // 않고 PutAsync 로 보낸 뒤 빈 목록을 돌려준다. 부르는 화면은 저장 뒤
        // 어차피 다시 조회한다 (Vue 화면도 saved 이벤트에서 재조회했다).
        await gateway.PutAsync(path, body, ct);
        return [];
    }

    /// <summary>쿼리스트링을 만든다. 값이 null 이거나 빈 문자열이면 뺀다.</summary>
    internal static string Query(params (string Key, object? Value)[] parameters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            var text = value switch
            {
                null => null,
                string s => string.IsNullOrWhiteSpace(s) ? null : s,
                bool b => b ? "true" : "false",
                DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ss"),
                _ => value.ToString(),
            };
            if (text is not null)
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
            }
        }
        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
