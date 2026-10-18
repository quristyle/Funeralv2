using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace JSini.Web.Components.Security;

/// <summary>
/// 갱신으로 받은 토큰과 「세션 거절」 표시를 <b>로그인 토큰마다</b> 들고 있는 통.
///
/// <para>
/// [왜 <see cref="TokenStore"/> 의 칸이 아니라 따로인가]
/// </para>
///
/// <para>
/// <c>AuthTokenHandler</c> 는 <c>HttpClientFactory</c> 가 <b>자기 DI 스코프</b>에서
/// 만들고 그 핸들러 줄을 몇 분씩 <b>모든 사용자에게 돌려 쓴다.</b> 그래서 핸들러가
/// 받은 <see cref="TokenStore"/> 는 사람의 것이 아니라 공용 한 벌이다. 거기에
/// 갱신한 토큰을 담아 두면 그다음부터 <b>모든 사람의 요청이 그 토큰으로 나간다</b>.
/// 서버를 다시 띄운 직후처럼 갱신이 몰리는 자리에서 남의 화면이 뜬다.
/// </para>
///
/// <para>
/// 열쇠를 <b>지금 요청의 인증 쿠키에 든 토큰</b>(의 지문)으로 두면, 어느 스코프의
/// 통이 묻든 자기 것만 꺼낸다. 다시 로그인하면 쿠키의 토큰이 바뀌므로 표시도
/// 저절로 갈린다.
/// </para>
/// </summary>
public sealed class RefreshedTokens
{
    /// <summary>갱신한 토큰을 들고 있는 시간. 접근 토큰 수명(7일)보다 조금 길게.</summary>
    public static readonly TimeSpan RefreshedTtl = TimeSpan.FromDays(8);

    /// <summary>
    /// 거절 표시를 들고 있는 시간. 그 사이 사람은 로그인으로 보내지고, 다시
    /// 로그인하면 열쇠가 바뀐다 — 오래 들고 있을 까닭이 없다.
    /// </summary>
    public static readonly TimeSpan RejectedTtl = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private readonly TimeProvider _clock;

    public RefreshedTokens() : this(TimeProvider.System) { }

    public RefreshedTokens(TimeProvider clock) => _clock = clock;

    /// <summary>
    /// 이 로그인 토큰에 대해 적어 둔 것. <paramref name="rejected"/> 가 참이면
    /// 토큰을 쓰지 말라는 뜻이다.
    /// </summary>
    public bool TryGet(string original, out string? refreshed, out bool rejected)
    {
        refreshed = null;
        rejected = false;

        var key = KeyOf(original);
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= _clock.GetUtcNow())
        {
            _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            return false;
        }

        refreshed = entry.Token;
        rejected = entry.Rejected;
        return true;
    }

    public void SetRefreshed(string original, string token) =>
        Put(original, new Entry(token, false, _clock.GetUtcNow() + RefreshedTtl));

    public void Reject(string original) =>
        Put(original, new Entry(null, true, _clock.GetUtcNow() + RejectedTtl));

    private void Put(string original, Entry entry)
    {
        _entries[KeyOf(original)] = entry;
        Sweep();
    }

    /// <summary>시효가 지난 줄을 걷는다. 쓰는 자리에서만 돌아 읽기는 가볍다.</summary>
    private void Sweep()
    {
        var now = _clock.GetUtcNow();
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _entries.TryRemove(pair);
            }
        }
    }

    /// <summary>토큰 자체는 열쇠로 담지 않는다(지문만 쓴다).</summary>
    private static string KeyOf(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record Entry(string? Token, bool Rejected, DateTimeOffset ExpiresAt);
}
