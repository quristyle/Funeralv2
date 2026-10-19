using System.Text.Json;
using JSini.Web.Http;

namespace JSini.Web.Components.Settings;

/// <summary>화면 환경설정 중 계정에 담기는 것들. 기기를 가리지 않고 따라온다. 띠·단추의 사용 여부·토스트 자리처럼 사람의 뜻인 값만 올리고, 끌어 옮긴 단추 자리는 기기마다 달라도 되므로 브라우저에 둔다.
/// 고칠 때는 PATCH 로 그 칸만 보낸다 — 한 벌을 통째로 보내면 다른 기기가 방금 바꾼 칸을 낡은 한 벌이 되돌린다.</summary>
public sealed class PortalPrefs(GatewayClient gateway, ILogger<PortalPrefs> logger)
{
    /// <summary>휴대폰 아래 띠를 <b>쓰지 않을지</b>. 없으면 쓴다.</summary>
    public const string BottomNavHiddenKey = "portal.bottomNav.hidden";

    /// <summary>그 띠에 놓을 칸들. 배열 그대로 담는다 — 글자로 싸면 읽는 쪽이 JSON.parse 를 한 번 더 한다. 푸는 일은 BottomNav.Parse 가 한다.</summary>
    public const string BottomNavItemsKey = "portal.bottomNav.items";

    /// <summary>그 띠의 칸에서 이름을 감출지(아이콘만 보기). 「감춘다」로 적는다 — 읽는 쪽이 열쇠가 있으면 그렇다고 보므로 기본값과 반대쪽을 적어야 한 번도 안 고친 사람이 기본값을 받는다. 브라우저에 있던 적이 없어 옛 값 올림 대상도 아니다.</summary>
    public const string BottomNavLabelsHiddenKey = "portal.bottomNav.labelsHidden";

    /// <summary>같은 칸들을 넓은 화면 헤더 오른쪽에 세우지 않을지. 「감춘다」로 적는 까닭은 BottomNavHiddenKey 와 같다. 휴대폰 띠 스위치와 따로 둔다 — 한 열쇠로 묶으면 띠만 끄거나 헤더만 끄는 길이 없어진다. 브라우저에 있던 적이 없다.</summary>
    public const string BottomNavDesktopHiddenKey = "portal.bottomNav.desktopHidden";

    /// <summary>모바일 메뉴 바로가기 단추를 <b>감출지</b>. 없으면 보인다.</summary>
    public const string FabHiddenKey = "portal.fab.hidden";

    /// <summary>헬프데스크 요청등록 바로가기 단추를 <b>감출지</b>. 없으면 보인다.</summary>
    public const string HelpDeskFabHiddenKey = "portal.helpDeskFab.hidden";

    /// <summary>알림(토스트)이 뜨는 자리. 없으면 <c>bottom-right</c>.</summary>
    public const string ToastPositionKey = "portal.toast.position";

    /// <summary>데스크탑 오른쪽 서랍(빠른 지시 등)의 기억된 창 너비(px).</summary>
    public const string RightDrawerWidthKey = "portal.rightDrawer.width";

    /// <summary>게이트웨이에서 이 값을 다루는 경로.</summary>
    private const string Path = "auth/user/preferences";

    private Task<PortalPrefsData>? _reading;

    /// <summary>담긴 것이 바뀌었을 때 바뀐 뒤의 한 벌 전체를 알린다. 듣는 쪽은 PortalBootstrap 이 2분짜리 통의 같은 칸을 맞춘다 — 안 맞추면 업무를 옮기는 순간 옛 값이 돌아온다.</summary>
    public event Action<JsonElement>? Changed;

    /// <summary>부트스트랩이 실어 온 것을 심는다. 이미 읽었으면 건드리지 않는다 — 먼저 읽은 쪽이 고친 값을 늦게 온 응답이 되돌리면 안 된다.</summary>
    public void Seed(JsonElement payload) =>
        _reading ??= Task.FromResult(PortalPrefsData.From(payload));

    /// <summary>담긴 것을 읽는다. Seed 가 먼저 들었으면 왕복은 0 번이다.</summary>
    public Task<PortalPrefsData> ReadAsync() => _reading ??= FetchAsync();

    private async Task<PortalPrefsData> FetchAsync()
    {
        try
        {
            var wire = await gateway.GetOneAsync<PrefsWire>(Path);
            return PortalPrefsData.From(wire?.Payload ?? default);
        }
        catch (ApiException ex)
        {
            // 못 읽었을 때는 전부 「고른 적이 없다」로 떨어진다 — 기본값으로 화면은 그대로 뜬다.
            logger.LogDebug(ex, "계정에 담긴 환경설정을 읽지 못했다. 기본값으로 본다.");
            return PortalPrefsData.Empty;
        }
    }

    /// <summary>칸 하나를 고친다. value 가 null 이면 지운다 — 기본값으로 되돌리기가 그 길이다. 회로가 든 값을 먼저 고쳐 게이트웨이가 실패해도 화면은 방금 고른 대로 움직인다. 실패를 위로 던지지 않는다 — 부르는 곳이 스위치·고르개라 저장 실패로 화면을 세울 일이 아니다.</summary>
    public async Task SetAsync(string key, object? value)
    {
        var next = (await ReadAsync()).With(key, value);

        _reading = Task.FromResult(next);
        Changed?.Invoke(next.Payload);

        await PatchAsync(new Dictionary<string, object?>(StringComparer.Ordinal) { [key] = value });
    }

    /// <summary>브라우저에만 남아 있던 옛 값을 계정으로 한 번 올린다. 계정에 그 열쇠가 아예 없을 때만 올린다 — 값이 같은지가 아니라 열쇠가 있는지를 본다(안 감춘 것과 아무것도 안 고른 것은 뜻이 다르다). 올린 것이 있으면 null 이 아닌 한 벌을 돌려주고, 부르는 쪽이 브라우저 쪽을 걷는다 — 안 걷으면 기본값으로 되돌린 것이 다음 로그인에 되살아난다.</summary>
    public async Task<PortalPrefsData?> AdoptFromBrowserAsync(
        bool bottomNavHidden,
        string? bottomNavItemsJson,
        bool fabHidden,
        bool helpDeskFabHidden,
        string? toastPosition)
    {
        var data = await ReadAsync();
        var patch = new Dictionary<string, object?>(StringComparer.Ordinal);

        // 감추기 셋은 감춰 둔 사람만 올린다 — 안 감춘 것이 기본값이라 거짓을 올리면 아무것도 안 고친 사람이 고친 사람이 된다.
        Adopt(BottomNavHiddenKey, bottomNavHidden ? true : null);
        Adopt(FabHiddenKey, fabHidden ? true : null);
        Adopt(HelpDeskFabHiddenKey, helpDeskFabHidden ? true : null);

        Adopt(ToastPositionKey, toastPosition);

        // 띠 칸은 배열일 때만 올린다 — 저장소를 사람이 고쳐 둔 글자를 그대로 계정에 박지 않는다.
        Adopt(BottomNavItemsKey, Array(bottomNavItemsJson));

        if (patch.Count == 0)
        {
            return null;
        }

        foreach (var (key, value) in patch)
        {
            data = data.With(key, value);
        }

        _reading = Task.FromResult(data);
        Changed?.Invoke(data.Payload);

        logger.LogInformation(
            "브라우저에만 있던 화면 설정 {Count}칸을 계정으로 옮겼다.", patch.Count);

        await PatchAsync(patch);

        return data;

        void Adopt(string key, object? value)
        {
            if (value is not null && !data.Has(key))
            {
                patch[key] = value;
            }
        }

        static object? Array(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                var element = JsonDocument.Parse(text).RootElement.Clone();
                return element.ValueKind == JsonValueKind.Array ? element : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private async Task PatchAsync(Dictionary<string, object?> patch)
    {
        try
        {
            await gateway.PatchAsync(Path, new { payload = patch });
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "화면 설정을 계정에 저장하지 못했다. 이 화면에만 남는다.");
        }
    }

    /// <summary><c>GET /auth/user/preferences</c> 응답.</summary>
    private sealed class PrefsWire
    {
        public JsonElement Payload { get; set; }
    }
}

/// <summary>계정에 담긴 한 벌. 고친 것은 새 것을 만든다 — 들고 있는 쪽이 바뀐 줄 모르고 옛 참조를 읽는 일을 막는다.</summary>
public sealed class PortalPrefsData
{
    /// <summary>담긴 것이 없을 때. 전부 「고른 적이 없다」다.</summary>
    public static readonly PortalPrefsData Empty = new([]);

    private readonly Dictionary<string, JsonElement> _values;

    private PortalPrefsData(Dictionary<string, JsonElement> values) => _values = values;

    /// <summary>담아 둘 모양 그대로. 부트스트랩 통을 맞출 때 쓴다.</summary>
    public JsonElement Payload => JsonSerializer.SerializeToElement(_values);

    /// <summary>이 열쇠를 고른 적이 있는가. <b>값이 기본값과 같아도 참이다.</b></summary>
    public bool Has(string key) => _values.ContainsKey(key);

    /// <summary>참·거짓으로. 고른 적이 없거나 참·거짓이 아니면 <c>null</c>.</summary>
    public bool? Flag(string key) =>
        _values.TryGetValue(key, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    /// <summary>글자로. 고른 적이 없거나 글자가 아니면 <c>null</c>.</summary>
    public string? Text(string key) =>
        _values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>정수로. 고른 적이 없거나 양의 정수가 아니면 <c>null</c>.</summary>
    public int? Number(string key)
    {
        if (!_values.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var num) && num > 0)
        {
            return num;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    /// <summary>날것 JSON. 띠 칸만 쓴다(BottomNav.Parse). 고른 적이 없거나 비어 있으면 null.</summary>
    public string? Raw(string key) =>
        _values.TryGetValue(key, out var value)
            && value.ValueKind is JsonValueKind.Array or JsonValueKind.Object
            ? value.GetRawText()
            : null;

    /// <summary>칸 하나만 갈아 끼운 새 한 벌. value 가 null 이면 뺀다 — 서버 Merge 가 같은 판단을 한다.</summary>
    public PortalPrefsData With(string key, object? value)
    {
        var copy = new Dictionary<string, JsonElement>(_values, StringComparer.Ordinal);

        if (value is null)
        {
            copy.Remove(key);
        }
        else
        {
            copy[key] = value as JsonElement? ?? JsonSerializer.SerializeToElement(value);
        }

        return new PortalPrefsData(copy);
    }

    /// <summary>서버가 준 객체를 옮겨 담는다. 객체가 아니면 Empty — 담긴 것이 깨졌다고 화면을 세우지 않는다.</summary>
    internal static PortalPrefsData From(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return Empty;
        }

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var prop in payload.EnumerateObject())
        {
            values[prop.Name] = prop.Value.Clone();
        }

        return new PortalPrefsData(values);
    }
}
