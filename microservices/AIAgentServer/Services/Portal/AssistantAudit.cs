using System.Text.Json;

using AIAgentServer.Data;

using JSini.Shared.Infrastructure.Time;

namespace AIAgentServer.Services.Portal;

/// <summary>업무 비서는 묻는 사람의 평소 권한 밖 자료(다른 회사·다른 사람의 요청글·일정·할 일)를 읽고 답한다.
/// 그 선을 넘은 흔적이 바로 이 줄이다 — 그래서 질문이나 도구 인자가 아니라 <b>답 전문</b>을 남긴다. 누가
/// 무엇을 물었느냐가 아니라 <b>무엇이 실제로 나갔느냐</b>가 감사의 대상이기 때문이다. DB 는 건드리지 않는
/// 순수 함수라 저장은 호출한 쪽(Task 8)의 몫이다.</summary>
public static class AssistantAudit
{
    public static AssistantAuditRow Build(
        string askedBy,
        string question,
        string? scopeCategory,
        IReadOnlyList<string> toolCalls,
        IReadOnlyList<string> sources,
        string answer,
        long elapsedMs,
        string? error) => new()
    {
        AskedBy = askedBy,
        AskedAt = AppTime.UtcNow,
        Question = question,
        ScopeCategory = scopeCategory,
        ToolCalls = JsonSerializer.Serialize(toolCalls),
        Sources = JsonSerializer.Serialize(sources),
        Answer = answer,
        ElapsedMs = elapsedMs,
        Error = error,
    };
}
