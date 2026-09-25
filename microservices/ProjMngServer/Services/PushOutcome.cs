using System.Net;
using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// 알림 서비스가 돌려준 푸시 응답을 <b>「갔다」와 「안 갔다」로 가른다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>2xx 가 곧 배달은 아니다.</b> <c>POST /notifications/push</c> 는 한 대도
/// 못 보냈을 때 <b>202</b> 와 까닭을 준다 — 구독한 기기 없음 · 본인이 푸시를 끔 ·
/// 서버에 VAPID 설정 없음. 저쪽이 일부러 그렇게 갈라 놓았는데
/// (<c>NotificationEndpoints</c> 의 「보낸 것이 하나도 없으면 성공으로 말하지
/// 않는다」), 부르는 쪽이 <c>IsSuccessStatusCode</c> 만 보면 그 구분이 여기서
/// 통째로 사라진다.
/// </para>
/// <para>
/// 실제로 그렇게 사라졌다. 시스템 관리자(<c>kdh</c>)의 구독 기기가 없어져
/// 2026-09-24 부터 그 계정으로 간 푸시가 <b>한 건도 배달되지 않았는데</b>,
/// 보낸 쪽(<c>ai_task.notify_error</c>)은 내내 비어 있어 작업 화면은
/// 「알림 정상」으로 보였다. 받는 사람만 아는 고장은 아무도 안 고친다.
/// </para>
/// <para>
/// <b>부르는 자리가 둘이라 여기 하나만 둔다</b> — 결과 알림
/// (<see cref="AiTaskNotifier"/>)과 요청·남긴말 알림
/// (<see cref="AiRequestAlerter"/>). 한쪽에만 적으면 반드시 갈라진다.
/// </para>
/// </remarks>
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
