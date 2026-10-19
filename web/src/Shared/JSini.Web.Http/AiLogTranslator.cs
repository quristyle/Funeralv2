namespace JSini.Web.Http;

/// <summary>옮겨 온 줄 묶음과 그것을 옮긴 AI. <see cref="Lines"/> 의 개수와 차례는 보낸 것과 같다. 못 옮긴 줄에는 원문이 남는다(서버가 지킨다).</summary>
public sealed class AiTranslatedLinesDto
{
    /// <summary>옮긴 줄들. 보낸 개수 그대로다.</summary>
    public List<string> Lines { get; set; } = [];

    /// <summary>실제로 답한 공급자의 키. 로컬이 꺼져 있으면 다른 값일 수 있다.</summary>
    public string? Provider { get; set; }

    /// <summary>그 공급자의 보여 줄 이름(<c>JSINI (로컬 LLM)</c>).</summary>
    public string? ProviderName { get; set; }

    /// <summary>그 공급자가 쓴 모델.</summary>
    public string? Model { get; set; }

    /// <summary>실제로 옮겨 꽂힌 줄 수. 나머지는 원문 그대로다.</summary>
    public int Translated { get; set; }
}

/// <summary>
/// 로그 줄을 로컬 LLM 으로 옮긴다. 「AI 작업」 상세의 로그 탭이 쓴다. 가는 곳이 AIAgentServer 라 업무 모듈이 아닌 여기 둔다(AiChatClient 와 같은 까닭).
/// 한 번에 수백 줄을 올리면 모델의 답 상한에 걸려 뒤가 잘린 채 돌아오고, 오류로도 드러나지 않는다. 그래서 끊어 보내고, 온 것부터 화면에 꽂는다.
/// 로컬 LLM 을 적어 보내는 것은 로그 전문이 바깥 공급자로 먼저 나가면 안 되기 때문이다. 로컬이 꺼져 있으면 서버가 넘기고, 누가 답했는지를 받아 화면이 그대로 보여 준다.
/// </summary>
public sealed class AiLogTranslator(GatewayClient gateway)
{
    /// <summary>한 번에 보내는 줄 수. 작을수록 빨리 보이고, 많을수록 왕복이 준다. 스무 줄이면 로컬 LLM 의 답 상한 안에 넉넉히 든다.</summary>
    public const int ChunkLines = 20;

    /// <summary>로컬 LLM 공급자의 키. 서버의 <c>AiProviderRegistry.JsiniKey</c> 와 같다.</summary>
    public const string LocalProvider = "jsini";

    /// <summary>줄 묶음 하나를 옮긴다. 못 옮기면 <see cref="ApiException"/> 이다. 부르는 쪽이 사정을 말해야 한다.</summary>
    public async Task<AiTranslatedLinesDto?> TranslateAsync(
        IReadOnlyList<string> lines, string targetLang, CancellationToken ct = default)
        => await gateway.PostAsync<AiTranslatedLinesDto>(
            "ai/translate",
            new { lines, targetLang, provider = LocalProvider },
            ct);
}
