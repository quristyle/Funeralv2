namespace AIAgentServer.DTOs;

/// <summary>대화 한 줄기(주제 하나). 고르개의 한 줄이다.</summary>
public class AiChatSessionDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 주제. <b><c>null</c> 은 아직 아무 말도 안 한 대화</b>다 —
    /// 화면이 「새 대화」로 그린다. 첫 질문이 들어오면 거기서 따온다.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>언제 열었나. <b>UTC 다.</b></summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>마지막으로 말이 오간 때. <b>목록 차례의 기준</b>이다.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>대화 속 한 마디.</summary>
public class AiChatMessageDto
{
    /// <summary><c>user</c> · <c>assistant</c>.</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>이름 바꾸기 요청.</summary>
public class RenameAiChatSessionDto
{
    /// <summary>
    /// 새 제목. <b>비우면 지운다</b> — 그때 화면은 「새 대화」로 그린다.
    /// </summary>
    public string? Title { get; set; }
}
