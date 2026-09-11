using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 공개 공지를 <b>회로 바깥에서</b> 잠깐 들고 있는 통.
///
/// [무엇을 고친 것인가]
///
/// 로그인 화면의 공개 공지는 <b>사람을 가리지 않는다</b> — 로그인하기 전이라
/// 가릴 사람 자체가 없다. 그런데도 로그인 화면이 열릴 때마다 게이트웨이 →
/// AuthServer → DB 를 한 번씩 다녀왔다. 아침에 백 명이 들어오면 같은 답을
/// 백 번 읽는다.
///
/// 그리고 그 왕복이 이제 <b>로그인 화면 HTML 을 만드는 길 위에</b> 있다
/// (<see cref="PublicNoticePopup"/> — 팝업을 첫 HTML 에 함께 실어 보낸다).
/// 통이 없으면 공지 때문에 로그인 화면 자체가 늦어진다.
///
/// [싱글턴이어야 한다]
///
/// scoped 로 두면 요청 하나가 곧 수명이라 아무것도 막지 못한다. 담기는 것이
/// <b>모두에게 같은 값</b>이라 사람을 섞을 위험도 없다 — 그것이
/// <see cref="Data.ReferenceDataStore"/> 처럼 열쇠를 받지 않고 통 하나로
/// 끝내는 이유다.
///
/// [얼마나 들고 있나]
///
/// 30초다. 관리자가 공지를 올리고 로그인 화면을 열어 확인하는 흐름이 있어서
/// 분 단위로 잡지 않았다. 늦게 반영되는 방향이 「방금 올린 공지가 아직 안
/// 뜬다」 쪽이라 위험하지도 않다.
///
/// [못 읽었을 때]
///
/// <b>빈 목록으로 넘어간다.</b> 공지를 못 읽은 것과 로그인이 안 되는 것은
/// 무게가 다르다 — 그 판단은 옛 <c>NoticeAutoPopup</c> 이 하던 것과 같고,
/// 자리만 이쪽으로 옮겨 왔다. 실패는 <see cref="RetryAfter"/> 만큼만 기억한다.
/// 30초를 기억하면 잠깐 끊긴 것 때문에 그 뒤 서른 초 동안 공지가 사라진다.
/// </summary>
public sealed class PublicNoticeStore
{
    /// <summary>읽어 둔 것을 그대로 쓰는 시간.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    /// <summary>못 읽었을 때 다시 물어보기까지. 짧게 잡는 이유는 머리말에 있다.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 게이트웨이를 기다리는 한도.
    ///
    /// <para>
    /// <b>이 한도가 곧 로그인 화면이 늦어질 수 있는 최대치다.</b> 게이트웨이가
    /// 대답을 안 하는 동안 로그인 화면이 함께 멈춰 서면, 고쳐야 할 것은
    /// 공지인데 사용자가 보는 증상은 「로그인이 안 된다」가 된다.
    /// </para>
    /// </summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    private readonly GatewayClient _anonymous;
    private readonly ILogger<PublicNoticeStore> _logger;

    /// <summary>
    /// 읽고 있는(또는 읽어 둔) 것.
    ///
    /// <para>
    /// 값이 아니라 <b><c>Task</c></b> 를 담는다. 그래야 처음 열리는 순간 요청이
    /// 여럿 겹쳐도 왕복은 하나다 — 뒤에 온 요청은 앞사람이 낸 왕복을 함께
    /// 기다린다(<c>PortalBoot</c> 와 같은 구도).
    /// </para>
    /// </summary>
    private Task<IReadOnlyList<NoticeDto>>? _reading;

    /// <summary>위 <c>Task</c> 를 그대로 써도 되는 시각의 끝.</summary>
    private DateTimeOffset _until;

    private readonly object _gate = new();

    public PublicNoticeStore(IHttpClientFactory factory, ILogger<PublicNoticeStore> logger)
    {
        _logger = logger;

        // 토큰을 붙이지 않는 클라이언트로 부른다. 이유는 `NoticeClient` 머리말과
        // 같다 — 로그인 화면에는 붙일 토큰이 없고, 만료된 토큰 하나 때문에
        // 401 → 갱신 → 로그인으로 튕기는 길이 열린다. 여기는 그보다 더한데,
        // **싱글턴이라 붙일 사람도 없다**(요청 맥락 밖이다).
        _anonymous = new GatewayClient(
            factory.CreateClient(ServiceCollectionExtensions.AnonymousClientName));
    }

    /// <summary>
    /// 로그인 전에도 보이는 팝업 공지. 통에 있으면 왕복 없이 돌려준다.
    /// </summary>
    public Task<IReadOnlyList<NoticeDto>> GetAsync()
    {
        lock (_gate)
        {
            if (_reading is not null && DateTimeOffset.UtcNow < _until)
            {
                return _reading;
            }

            // **먼저 시각을 잡고 나서 읽는다.** 읽는 동안 들어온 요청이 또
            // 왕복을 내지 않게 하려는 것이다 — 아침처럼 동시에 열리는 때가
            // 바로 이 통이 필요한 때다.
            _until = DateTimeOffset.UtcNow + Ttl;

            return _reading = ReadAsync();
        }
    }

    private async Task<IReadOnlyList<NoticeDto>> ReadAsync()
    {
        using var deadline = new CancellationTokenSource(Deadline);

        try
        {
            return await _anonymous.GetListAsync<NoticeDto>(
                "auth/notices/popup/public", deadline.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException
                                      or System.Text.Json.JsonException)
        {
            // 실패는 짧게만 기억한다. 이 `lock` 은 `GetAsync` 의 것과 겹치지
            // 않는다 — 위쪽은 첫 `await` 에서 이미 빠져나간 뒤다.
            lock (_gate)
            {
                _until = DateTimeOffset.UtcNow + RetryAfter;
            }

            _logger.LogWarning(ex, "공개 공지를 읽지 못했습니다. 이번에는 공지 없이 넘어갑니다.");
            return [];
        }
    }
}
