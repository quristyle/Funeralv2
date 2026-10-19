using System.Net;
using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// 알림 서비스가 돌려준 푸시 응답을 <b>「갔다」와 「안 갔다」로 가른다.</b>
/// </summary>
/// 알림 서비스는 한 대도 못 보냈을 때도 2xx(202)로 답하므로 상태 코드만 보면 안 된다 — 구독 기기 없음 · 본인이 푸시를 끔 · VAPID 설정 없음이 그 예다.
/// 결과 알림과 요청 알림 두 곳이 같이 쓰므로 여기 한 벌만 둔다 — 한쪽에만 적으면 반드시 갈라진다.
internal static class PushOutcome
{
    /// <summary>한 건도 안 나갔는데 까닭조차 못 읽었을 때 쓸 말.</summary>
    private const string Unknown = "보낸 알림이 없습니다.";

    /// <summary>
    /// 2xx 로 돌아온 응답이 사실은 <b>한 건도 안 나간 것</b>인지 본다.
    /// </summary>
    /// <param name="status">응답 상태 코드. 2xx 라고 보고 부른다.</param>
    /// <param name="body">응답 본문(표준 봉투).</param>
    /// <returns>나갔으면 <c>null</c>, 안 나갔으면 그 까닭 한 줄.</returns>
    public static string? NotSent(HttpStatusCode status, string? body)
    {
        var sent = SentCount(body);

        // 기기 하나라도 받았으면 된 것이다.
        if (sent is > 0)
        {
            return null;
        }

        // 숫자를 못 읽었으면 상태 코드로만 가른다. 200 은 저쪽이
        // 「보냈다」고 말한 것이므로 의심하지 않는다.
        if (sent is null && status != HttpStatusCode.Accepted)
        {
            return null;
        }

        var why = Message(body);
        return string.IsNullOrWhiteSpace(why) ? Unknown : why;
    }

    /// <summary>봉투 안의 <c>data.sent</c>. 못 읽으면 <c>null</c>.</summary>
    private static int? SentCount(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.TryGetProperty("data", out var data)
                   && data.ValueKind == JsonValueKind.Object
                   && data.TryGetProperty("sent", out var sent)
                   && sent.TryGetInt32(out var count)
                ? count
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 봉투 안의 사람이 읽을 한 줄. <b>JSON 덩어리를 그대로 화면에 올리지
    /// 않는다</b> — 못 꺼내면 빈 문자열이다.
    /// </summary>
    private static string Message(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("message", out var m)
                && m.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // JSON 이 아니면 아래에서 앞부분만 자른다.
        }

        var trimmed = body.Trim();
        return trimmed[..Math.Min(trimmed.Length, 200)];
    }
}
