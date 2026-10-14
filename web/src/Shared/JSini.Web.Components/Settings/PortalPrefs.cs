using System.Text.Json;
using JSini.Web.Http;

namespace JSini.Web.Components.Settings;

/// <summary>
/// 화면 환경설정 중 <b>계정에 담기는 것들</b>. 기기를 가리지 않고 따라온다.
///
/// <para>
/// [무엇이 브라우저를 떠났나]
/// </para>
///
/// <para>
/// 레이아웃의 개인 설정은 오래 <b>브라우저 저장소</b>에 있었다
/// (<see cref="Layout.PortalBoot"/>). 그 자리가 맞는 값이 있다 — 사이드바를
/// 끌어 넓힌 폭, 이 탭에서 공지를 닫았다는 표시, 고정해 둔 탭. 창마다·기기마다
/// 다른 것이 자연스러운 값들이다.
/// </para>
///
/// <para>
/// 그런데 <b>휴대폰 아래 띠</b>는 그렇지 않다. 그것은 「내가 쓰는 메뉴 다섯」을
/// 고르는 일이고, 사람이 손에 드는 기기는 하나가 아니다 — 회사 휴대폰과
/// 개인 휴대폰, 태블릿, 홈 화면에 깐 앱(PWA 는 저장소가 또 따로다). 브라우저에
/// 담아 두면 <b>기기마다 다른 띠가 뜨고</b>, 한 곳에서 고친 것이 다른 곳에
/// 영영 안 간다. 캐시를 지우면 통째로 사라지는 것은 덤이다.
/// </para>
///
/// <para>
/// 그래서 아래 것들을 계정으로 올렸다.
/// </para>
///
/// <list type="table">
///   <item><term><see cref="BottomNavItemsKey"/></term><description>아래 띠에 놓을 칸들</description></item>
///   <item><term><see cref="BottomNavHiddenKey"/></term><description>그 띠를 쓸지</description></item>
///   <item><term><see cref="BottomNavLabelsHiddenKey"/></term><description>그 띠에 이름을 적을지</description></item>
///   <item><term><see cref="BottomNavDesktopHiddenKey"/></term><description>같은 칸들을 <b>넓은 화면 헤더</b>에도 세울지</description></item>
///   <item><term><see cref="FabHiddenKey"/></term><description>메뉴 바로가기 단추를 쓸지</description></item>
///   <item><term><see cref="HelpDeskFabHiddenKey"/></term><description>요청등록 바로가기 단추를 쓸지</description></item>
///   <item><term><see cref="ToastPositionKey"/></term><description>알림(토스트)이 뜨는 자리</description></item>
/// </list>
///
/// <para>
/// <b>떠다니는 단추의 「자리」는 안 올렸다.</b> 그 둘은 손가락으로 끌어 옮기는
/// 값이라(휴대폰에서 길게 눌러 끈다) 기기마다 다른 것이 맞다 — 휴대폰에서
/// 왼손 엄지에 맞춰 옮긴 자리를 책상 화면이 따라갈 까닭이 없다. 「쓸지 말지」만
/// 사람의 뜻이고, 그래서 그쪽만 계정에 담는다.
/// </para>
///
/// <para>
/// [왕복을 내지 않는다]
/// </para>
///
/// <para>
/// 값은 부트스트랩이 실어 온다(<c>PortalBootstrapDto.Preferences</c>) —
/// <see cref="Seed"/>. 그 응답은 프론트가 2분간 들고 있으므로
/// (<see cref="Layout.PortalBootstrapStore"/>) 업무를 넘나들어도 왕복이 없다.
/// 씨가 안 뿌려진 채로 누가 먼저 읽으면 그때만 혼자 한 번 읽는다.
/// </para>
///
/// <para>
/// [고칠 때는 <b>그 칸만</b> 보낸다]
/// </para>
///
/// <para>
/// <c>PATCH</c> 다. 한 벌을 통째로 보내면, 들고 있던 한 벌이 낡았을 때
/// <b>다른 기기가 그 사이에 바꾼 것을 조용히 되돌린다</b> — 휴대폰에서 띠를
/// 고치고 책상에서 토스트 위치를 고르면 바로 그 일이 난다.
/// </para>
/// </summary>
public sealed class PortalPrefs(GatewayClient gateway, ILogger<PortalPrefs> logger)
{
    /// <summary>휴대폰 아래 띠를 <b>쓰지 않을지</b>. 없으면 쓴다.</summary>
    public const string BottomNavHiddenKey = "portal.bottomNav.hidden";

    /// <summary>
    /// 그 띠에 놓을 칸들. <b>배열 그대로</b> 담는다 — 글자로 싸서 담으면
    /// DB 에서 눈으로 못 읽고, 읽는 쪽이 <c>JSON.parse</c> 를 한 번 더 한다.
    /// 모양은 <c>BottomNavItem</c> 이고 푸는 일은 <c>BottomNav.Parse</c> 가 한다.
    /// </summary>
    public const string BottomNavItemsKey = "portal.bottomNav.items";

    /// <summary>
    /// 그 띠의 칸에서 <b>이름을 감출지</b>(아이콘만 보기). 없으면 보인다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「보인다」가 아니라 「감춘다」를 적는다.</b> 읽는 쪽이 「열쇠가 있으면
    /// 그렇다는 뜻」으로 보므로(<see cref="PortalPrefsData.Flag"/>), 기본값과
    /// 반대되는 쪽을 적어야 한 번도 안 고친 사람이 기본값을 받는다 —
    /// <see cref="BottomNavHiddenKey"/> 와 같은 꼴이다.
    /// </para>
    /// <para>
    /// <b>브라우저에 있던 적이 없다.</b> 위 넷과 달리 이 값은 처음부터 계정에
    /// 났으므로 옛 값을 들고 올라갈 일도 없다 — <see cref="AdoptFromBrowserAsync"/>
    /// 가 이 열쇠를 모르는 까닭이다.
    /// </para>
    /// </remarks>
    public const string BottomNavLabelsHiddenKey = "portal.bottomNav.labelsHidden";

    /// <summary>
    /// 같은 칸들을 <b>넓은 화면 헤더 오른쪽에 세우지 않을지</b>. 없으면 선다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 띠에 고른 칸은 「내가 자주 여는 화면」이다. 그 뜻은 화면 폭에 딸린 것이
    /// 아닌데, 한동안 <b>쓰이는 자리가 ≤767px 하나뿐</b>이었다 — 책상에 앉으면
    /// 같은 사람이 같은 화면을 메뉴에서 다시 찾아 들어갔다. 그래서 넓은 화면에서는
    /// 그 칸들을 헤더 오른쪽 도구 줄에 아이콘으로 세운다
    /// (<c>HeaderNavTools</c>).
    /// </para>
    /// <para>
    /// <b>「세울지」가 아니라 「감출지」를 적는다</b> — 읽는 쪽이 「열쇠가 있으면
    /// 그렇다는 뜻」으로 보므로(<see cref="PortalPrefsData.Flag"/>), 기본값과
    /// 반대되는 쪽을 적어야 한 번도 안 고친 사람이 기본값(=선다)을 받는다.
    /// <see cref="BottomNavHiddenKey"/> 와 같은 꼴이다.
    /// </para>
    /// <para>
    /// <b>휴대폰 쪽 스위치와 따로다.</b> 한 열쇠로 묶으면 띠만 끄거나 헤더만
    /// 끄는 길이 없어지는데, 둘은 서로 다른 자리에서 서로 다른 이유로 거슬린다 —
    /// 휴대폰에서 엄지 자리를 먹는 것과 책상에서 헤더가 붐비는 것.
    /// </para>
    /// <para>
    /// <b>브라우저에 있던 적이 없다.</b> 처음부터 계정에 났으므로 옛 값을 들고
    /// 올라갈 일이 없다 — <see cref="AdoptFromBrowserAsync"/> 가 이 열쇠를 모르는
    /// 까닭이다(<see cref="BottomNavLabelsHiddenKey"/> 와 같다).
    /// </para>
    /// </remarks>
    public const string BottomNavDesktopHiddenKey = "portal.bottomNav.desktopHidden";

    /// <summary>모바일 메뉴 바로가기 단추를 <b>감출지</b>. 없으면 보인다.</summary>
    public const string FabHiddenKey = "portal.fab.hidden";

    /// <summary>헬프데스크 요청등록 바로가기 단추를 <b>감출지</b>. 없으면 보인다.</summary>
    public const string HelpDeskFabHiddenKey = "portal.helpDeskFab.hidden";

    /// <summary>알림(토스트)이 뜨는 자리. 없으면 <c>bottom-right</c>.</summary>
    public const string ToastPositionKey = "portal.toast.position";

    /// <summary>게이트웨이에서 이 값을 다루는 경로.</summary>
    private const string Path = "auth/user/preferences";

    private Task<PortalPrefsData>? _reading;

    /// <summary>
    /// 담긴 것이 바뀌었을 때, <b>바뀐 뒤의 한 벌 전체</b>를 알린다.
    /// </summary>
    /// <remarks>
    /// 듣는 쪽은 <c>PortalBootstrap</c> 하나다 — 2분짜리 통에 든 부트스트랩
    /// 응답의 같은 칸을 맞춰 둔다. 안 맞추면 <b>업무를 한 번 옮기는 순간 옛
    /// 값이 돌아온다</b>: 모듈이 갈리면 레이아웃이 새로 생기고, 그때 이 통은
    /// 게이트웨이 대신 그 응답을 다시 읽는다.
    /// </remarks>
    public event Action<JsonElement>? Changed;

    /// <summary>
    /// 부트스트랩이 실어 온 것을 심는다. <b>이미 읽었으면 건드리지 않는다</b> —
    /// 먼저 읽은 쪽이 고쳐 둔 값을, 늦게 도착한 응답이 되돌리면 안 된다.
    /// </summary>
    public void Seed(JsonElement payload) =>
        _reading ??= Task.FromResult(PortalPrefsData.From(payload));

    /// <summary>
    /// 담긴 것을 읽는다. 몇 번을 불러도 왕복은 많아야 한 번이고,
    /// <see cref="Seed"/> 가 먼저 들었으면 <b>0 번</b>이다.
    /// </summary>
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
            // **빈 것으로 넘어간다.** 못 읽었을 때의 올바른 동작은 전부
            // 「고른 적이 없다」 — 그때는 기본값이고, 화면은 그대로 뜬다.
            logger.LogDebug(ex, "계정에 담긴 환경설정을 읽지 못했다. 기본값으로 본다.");
            return PortalPrefsData.Empty;
        }
    }

    /// <summary>
    /// 칸 하나를 고친다. <paramref name="value"/> 가 <c>null</c> 이면
    /// <b>지운다</b> — 「기본값으로 되돌리기」가 그 길이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>회로가 든 값을 먼저 고친다.</b> 게이트웨이가 실패해도 이 화면은
    /// 방금 고른 대로 움직여야 한다 — 되돌려 놓으면 사람은 스위치가 저절로
    /// 되돌아가는 것을 본다.
    /// </para>
    /// <para>
    /// <b>실패를 위로 던지지 않는다.</b> 부르는 자리가 전부 스위치·고르개라,
    /// 저장이 안 됐다고 화면을 세울 일이 아니다. 다음에 고칠 때 다시 간다.
    /// </para>
    /// </remarks>
    public async Task SetAsync(string key, object? value)
    {
        var next = (await ReadAsync()).With(key, value);

        _reading = Task.FromResult(next);
        Changed?.Invoke(next.Payload);

        await PatchAsync(new Dictionary<string, object?>(StringComparer.Ordinal) { [key] = value });
    }

    /// <summary>
    /// 브라우저에만 남아 있던 옛 값을 <b>계정으로 한 번 올린다</b>.
    /// </summary>
    /// <param name="bottomNavHidden">브라우저에 「띠를 안 쓴다」가 적혀 있었나.</param>
    /// <param name="bottomNavItemsJson">브라우저에 적혀 있던 띠의 칸들(날것 JSON). 없으면 <c>null</c>.</param>
    /// <param name="fabHidden">브라우저에 「메뉴 단추를 감춘다」가 적혀 있었나.</param>
    /// <param name="helpDeskFabHidden">브라우저에 「요청등록 단추를 감춘다」가 적혀 있었나.</param>
    /// <param name="toastPosition">브라우저에 적혀 있던 토스트 자리. 없으면 <c>null</c>.</param>
    /// <returns>옮겼으면 옮긴 뒤의 한 벌, 옮길 것이 없었으면 <c>null</c>.</returns>
    /// <remarks>
    /// <para>
    /// [왜 필요한가]
    /// </para>
    /// <para>
    /// 이 다섯은 오래 브라우저에 있었고 사람들이 거기서 골라 두었다. 계정으로
    /// 옮기면서 그것을 안 들고 오면, <b>쓰던 띠가 어느 날 기본값으로
    /// 돌아간다.</b> 고장으로 읽히는 변화다.
    /// </para>
    /// <para>
    /// [계정에 <b>열쇠가 아예 없을 때만</b> 올린다]
    /// </para>
    /// <para>
    /// 값이 같은지가 아니라 <b>열쇠가 있는지</b>를 본다 — 「감추지 않는다」를
    /// 고른 사람과 아무것도 안 고른 사람은 값이 같고 뜻이 다르다.
    /// </para>
    /// <para>
    /// <b>그래도 한 번뿐이어야 한다.</b> 계정 쪽에서 기본값으로 되돌리면
    /// 열쇠가 지워지므로(서버의 <c>Merge</c>), 브라우저에 옛 값이 남아 있으면
    /// 다음 로그인에 <b>되돌린 것이 되살아난다.</b> 그래서 부르는 쪽이 올린
    /// 직후에 브라우저 쪽을 걷는다(<c>PortalBoot</c>) — 이 함수가
    /// <c>null</c> 이 아닌 것을 돌려주는 것이 그 신호다.
    /// </para>
    /// <para>
    /// 기기가 둘이면 <b>먼저 로그인한 기기가 이긴다.</b> 그 뒤로는 계정에
    /// 열쇠가 있으므로 다른 기기의 옛 값은 올라오지 않는다 — 요구가
    /// 「어느 기기에서나 같게」이므로 이것이 맞는 쪽이다.
    /// </para>
    /// </remarks>
    public async Task<PortalPrefsData?> AdoptFromBrowserAsync(
        bool bottomNavHidden,
        string? bottomNavItemsJson,
        bool fabHidden,
        bool helpDeskFabHidden,
        string? toastPosition)
    {
        var data = await ReadAsync();
        var patch = new Dictionary<string, object?>(StringComparer.Ordinal);

        // 감추기 셋은 **감춰 둔 사람만** 올린다. 안 감춘 것이 기본값이라,
        // 거짓을 올리면 「아무것도 안 고친 사람」이 고친 사람이 된다.
        Adopt(BottomNavHiddenKey, bottomNavHidden ? true : null);
        Adopt(FabHiddenKey, fabHidden ? true : null);
        Adopt(HelpDeskFabHiddenKey, helpDeskFabHidden ? true : null);

        Adopt(ToastPositionKey, toastPosition);

        // 띠에 놓을 칸들만 날것 JSON 이다. **배열일 때만** 올린다 —
        // 사람이 저장소를 고쳐 둔 글자를 그대로 계정에 박아 넣지 않는다.
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

/// <summary>
/// 계정에 담긴 한 벌. <b>고친 것은 새 것을 만든다</b> — 들고 있는 쪽이
/// 바뀐 줄 모르고 옛 참조를 계속 읽는 일을 막는다.
/// </summary>
public sealed class PortalPrefsData
{
    /// <summary>담긴 것이 없을 때. 전부 「고른 적이 없다」다.</summary>
    public static readonly PortalPrefsData Empty = new([]);

    private readonly Dictionary<string, JsonElement> _values;

    private PortalPrefsData(Dictionary<string, JsonElement> values) => _values = values;

    /// <summary>
    /// 담아 둘 모양 그대로. 부트스트랩 통을 맞출 때 쓴다
    /// (<see cref="PortalPrefs.Changed"/>).
    /// </summary>
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

    /// <summary>
    /// 날것 JSON 으로. 배열·객체를 그대로 받는 쪽이 쓴다
    /// (띠에 놓을 칸들 — <c>BottomNav.Parse</c>).
    /// 고른 적이 없거나 비어 있으면 <c>null</c>.
    /// </summary>
    public string? Raw(string key) =>
        _values.TryGetValue(key, out var value)
            && value.ValueKind is JsonValueKind.Array or JsonValueKind.Object
            ? value.GetRawText()
            : null;

    /// <summary>
    /// 칸 하나만 갈아 끼운 새 한 벌. <paramref name="value"/> 가 <c>null</c>
    /// 이면 뺀다 — 서버가 같은 판단을 한다(<c>AccountPreferenceService.Merge</c>).
    /// </summary>
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

    /// <summary>
    /// 서버가 준 객체를 옮겨 담는다. 객체가 아니면 <see cref="Empty"/> 다 —
    /// 담긴 것이 깨졌다고 화면을 세우지 않는다.
    /// </summary>
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
