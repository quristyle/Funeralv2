using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

/// <summary>공개 공지를 회로 바깥에서 잠깐 들고 있는 통. 로그인 화면의 공개 공지는 사람을 가리지 않는데(로그인 전이라 가릴 사람이 없다) 열릴 때마다 게이트웨이 → AuthServer → DB 를 다녀왔다. 이제 그 왕복이 로그인 화면 HTML 을 만드는 길 위에 있어 통이 없으면 공지 때문에 로그인 화면이 늦어진다.
/// 담기는 것이 모두에게 같은 값이라 싱글턴이다 — 사람을 섞을 위험이 없어 ReferenceDataStore 처럼 열쇠 없이 통 하나로 끝낸다. 30초를 들고, 실패는 RetryAfter 만큼만 기억한다(30초를 기억하면 잠깐 끊긴 것 때문에 그 뒤 서른 초 공지가 사라진다). 못 읽으면 빈 목록으로 넘어간다 — 공지를 못 읽은 것과 로그인이 안 되는 것은 무게가 다르다.</summary>
public sealed class PublicNoticeStore
{
    /// <summary>읽어 둔 것을 그대로 쓰는 시간.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    /// <summary>못 읽었을 때 다시 물어보기까지. 짧게 잡는 이유는 머리말에 있다.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(3);

    /// <summary>게이트웨이를 기다리는 한도. 이 한도가 곧 로그인 화면이 늦어질 수 있는 최대치다 — 게이트웨이가 대답을 안 하는 동안 로그인 화면이 함께 멈추면 고칠 것은 공지인데 사용자에게는 로그인이 안 되는 증상이 된다.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    private readonly GatewayClient _anonymous;
    private readonly ILogger<PublicNoticeStore> _logger;

    /// <summary>읽고 있는(또는 읽어 둔) 것. 값이 아니라 Task 를 담는다 — 처음 열리는 순간 요청이 여럿 겹쳐도 왕복은 하나이고 뒤에 온 요청은 앞사람이 낸 왕복을 함께 기다린다(PortalBoot 와 같은 구도).</summary>
    private Task<IReadOnlyList<NoticeDto>>? _reading;

    /// <summary>위 <c>Task</c> 를 그대로 써도 되는 시각의 끝.</summary>
    private DateTimeOffset _until;

    private readonly object _gate = new();

    public PublicNoticeStore(IHttpClientFactory factory, ILogger<PublicNoticeStore> logger)
    {
        _logger = logger;

        // 토큰을 붙이지 않는 클라이언트로 부른다 — 로그인 화면에는 붙일 토큰이 없고 만료된 토큰 하나 때문에 401 → 갱신 → 로그인으로 튕기는 길이 열린다. 싱글턴이라 붙일 사람도 없다(요청 맥락 밖이다).
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
