using AIAgentServer.Data;
using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Services;

/// <summary>
/// AI 호출 한 건을 <c>scom.ai_usage_logs</c> 에 적는다. <b>사람별 사용량의 정본이다.</b>
/// </summary>
/// <remarks>
/// <para>
/// [왜 여기여야 하나 — LLM 서버는 사람을 모른다]
/// </para>
///
/// <para>
/// llama.cpp 는 토큰 수를 응답에 실어 주지만(<c>usage</c>) 그것은 <b>요청 한
/// 건의 값</b>이고, 포털의 모든 요청은 API 키 하나로 들어가므로 그쪽에서는
/// 전부 같은 손님이다. 사람을 아는 자리는 <b>이 서비스 하나뿐</b>이다 —
/// 게이트웨이가 붙여 주는 <c>X-User-Id</c> 가 여기까지만 온다.
/// </para>
///
/// <para>
/// [적다 실패해도 대화는 끝까지 간다]
/// </para>
///
/// <para>
/// 사용량은 곁다리고 대화가 본일이다. DB 가 죽었거나 연결 문자열이 없는 장비
/// (개발 PC)에서 AI 기능이 통째로 멎으면 본말이 뒤집힌다. 그래서 <b>모든
/// 예외를 삼키고</b> 경고만 남긴다 — <see cref="AiUsageTracker"/> 가 한도
/// 정보를 줍다 실패해도 조용한 것과 같은 선이다.
/// </para>
///
/// <para>
/// [기다리지 않는다]
/// </para>
///
/// <para>
/// 적는 자리가 <b>답을 다 흘려보낸 직후</b>다. 거기서 DB 왕복을 기다리면
/// 사람이 보는 「생각 중」이 그만큼 길어진다. 배경으로 떼어 보내고 바로
/// 돌아온다 — 포털 셸이 오류 기록을 큐에 넣고 바로 돌아오는 것과 같은 까닭이다
/// (<c>PortalErrorHandler</c>).
/// </para>
///
/// <para>
/// <b>그래서 몇 건은 잃을 수 있다.</b> 프로세스가 그 사이에 죽으면 그 줄은
/// 없다. 이 숫자는 회계가 아니라 <b>「누가 많이 쓰나」를 사람이 보는 것</b>이
/// 목적이라 그 정도로 충분하다(<see cref="AiUsageTracker"/> 머리말과 같은 판단).
/// </para>
/// </remarks>
public sealed class AiUsageLog(
    IServiceScopeFactory scopes,
    IHttpContextAccessor http,
    IConfiguration config,
    ILogger<AiUsageLog> logger)
{
    /// <summary>
    /// 연결 문자열이 아예 없으면 적지 않는다. <b>한 번만 따진다</b> —
    /// 호출마다 설정을 뒤질 값이 아니다.
    /// </summary>
    private readonly bool _enabled = !string.IsNullOrWhiteSpace(
        config.GetConnectionString("jsinicore")
        ?? config["jsinicore"]
        ?? Environment.GetEnvironmentVariable("jsinicore"));

    /// <summary>
    /// 한 건 적는다. <b>기다리지 않는다</b> — 부르는 쪽은 그대로 이어 간다.
    /// </summary>
    /// <param name="feature"><c>chat</c> · <c>chat-stream</c> · <c>suggest-code</c> ….</param>
    /// <param name="providerKey"><b>실제로 답한</b> 공급자. 고른 것이 아니다.</param>
    /// <param name="usage">공급자가 준 토큰 수. <c>null</c> 이면 모르는 채로 적는다.</param>
    /// <param name="failReason">실패했을 때의 까닭. 성공이면 <c>null</c>.</param>
    public void Record(
        string feature,
        string providerKey,
        string? model,
        AiTokenUsage? usage,
        int? latencyMs,
        bool ok,
        string? failReason = null)
    {
        if (!_enabled) return;

        var row = new AiUsageRow
        {
            // **여기서 찍는다.** 배경 작업 안에서 찍으면 큐에서 기다린 시간만큼
            // 뒤로 밀려, 「몇 시에 물었나」가 실제와 달라진다.
            OccurredAt = DateTime.UtcNow,
            UserId = CurrentUserId(),
            Feature = feature,
            ProviderKey = providerKey,
            Model = model,
            PromptTokens = usage?.PromptTokens,
            CompletionTokens = usage?.CompletionTokens,
            TotalTokens = usage?.TotalTokens,
            LatencyMs = latencyMs,
            Ok = ok,
            FailReason = Shorten(failReason),
        };

        _ = WriteAsync(row);
    }

    /// <summary>
    /// 지금 요청을 낸 사람. <b>게이트웨이가 붙여 준 헤더만 믿는다</b> —
    /// 그 앞에서 들어온 같은 이름의 헤더는 게이트웨이가 지우고 다시 만든다.
    /// </summary>
    /// <remarks>
    /// 요청 밖(배경 작업·헬스체크)에서 부르면 <c>null</c> 이다. 그것을 빈
    /// 글자로 적으면 조회에서 「이름 없는 사용자 한 명」으로 뭉쳐 사람 수가 틀린다.
    /// </remarks>
    private string? CurrentUserId()
    {
        var id = http.HttpContext?.Request.Headers["X-User-Id"].ToString();
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    private async Task WriteAsync(AiUsageRow row)
    {
        try
        {
            // 요청의 수명과 무관하게 돈다. 요청 범위의 DbContext 를 들고 가면
            // 스트리밍이 끝나 범위가 닫힌 뒤에 쓰다가 ObjectDisposedException 이 난다.
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();

            db.AiUsageLogs.Add(row);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // 삼킨다(머리말). 다만 흔적은 남긴다 — 화면이 비어 있을 때
            // 「아무도 안 썼다」와 「못 적고 있다」를 가를 곳이 여기뿐이다.
            logger.LogWarning(ex, "AI 사용량을 적지 못했습니다: {Feature}", row.Feature);
        }
    }

    /// <summary>
    /// 실패 까닭을 한 줄로 줄인다. 예외 메시지가 통째로 들어오면 표의 칸을
    /// 넘치게 채우고, 거기서 읽을 것은 앞부분뿐이다.
    /// </summary>
    private static string? Shorten(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;

        var line = reason.Trim().ReplaceLineEndings(" ");
        return line.Length <= 300 ? line : line[..300];
    }
}
