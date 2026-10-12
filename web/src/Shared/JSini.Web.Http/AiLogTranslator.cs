namespace JSini.Web.Http;

/// <summary>
/// 옮겨 온 줄 묶음과, <b>그것을 옮긴 AI</b>.
/// </summary>
/// <remarks>
/// <see cref="Lines"/> 의 개수와 차례는 <b>보낸 것과 같다.</b> 서버가 그것을
/// 지켜 준다(<c>LLMService.MergeTranslation</c>) — 못 옮긴 줄에는 원문이 남는다.
/// </remarks>
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
/// 로그 줄을 <b>로컬 LLM 으로</b> 옮긴다. 「AI 작업」 상세의 로그 탭이 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 업무 모듈이 아니라 여기 있나]
/// </para>
/// <para>
/// 부르는 화면은 프로젝트관리 하나뿐이지만, <b>가는 곳이 그 모듈의 서비스가
/// 아니다</b> — <c>/api/ai/…</c> 는 AIAgentServer 의 경계다. 모듈은 자기
/// 경로만 부른다(<see cref="GatewayClient"/> 머리말). AI 쳇
/// (<see cref="AiChatClient"/>)이 여기 있는 것과 같은 자리·같은 까닭이다.
/// </para>
/// <para>
/// [왜 끊어 보내나]
/// </para>
/// <para>
/// 한 번에 수백 줄을 올리면 모델의 답 상한(<c>MaxTokens</c>)에 걸려 <b>뒤가
/// 잘린 채로</b> 돌아온다. 그러면 사람은 한참 기다린 끝에 절반만 받고, 잘린
/// 것은 오류로 드러나지도 않는다 — 그냥 아래쪽이 원문인 채로 남는다.
/// </para>
/// <para>
/// 끊어 보내면 얻는 것이 하나 더 있다. <b>온 것부터 화면에 꽂을 수 있다</b> —
/// 로컬 LLM 은 한 덩이에 십 초가 넘는 일이 흔해서, 다 모아서 한 번에 그리면
/// 그동안 오른쪽 칸이 통째로 비어 있다.
/// </para>
/// <para>
/// [공급자를 적어 보낸다 — 다른 호출과 다른 자리]
/// </para>
/// <para>
/// <see cref="AiChatClient"/> 를 타는 다른 AI 호출은 공급자를 비워 보내
/// 서버의 기본값에 맡긴다. 여기는 <b>로컬 LLM 을 적어 보낸다</b> — 올라가는
/// 것이 실행기가 찍은 로그 전문이라 바깥 공급자로 먼저 나갈 까닭이 없다.
/// </para>
/// <para>
/// <b>그래도 못 부르면 서버가 넘긴다.</b> 로컬 장비가 꺼져 있을 때 기능이
/// 통째로 죽는 것을 막는 장치가 이미 서버에 있고(<c>AI:AutoFailover</c>),
/// 그 길을 여기서만 따로 막지 않는다. 대신 <b>누가 답했는지</b>를 받아
/// 화면이 그대로 내건다(<see cref="AiTranslatedLinesDto.ProviderName"/>) —
/// 로컬인 줄 알았는데 아니었던 경우가 눈에 보여야 한다.
/// </para>
/// </remarks>
public sealed class AiLogTranslator(GatewayClient gateway)
{
    /// <summary>
    /// 한 번에 보내는 줄 수.
    /// </summary>
    /// <remarks>
    /// <b>작을수록 빨리 보이고 많을수록 왕복이 준다.</b> 스무 줄이면 로컬
    /// LLM 의 답 상한(2000토큰) 안에 넉넉히 들어가고, 한 덩이가 오는 데
    /// 몇 초라 오른쪽 칸이 눈에 띄게 채워진다.
    /// </remarks>
    public const int ChunkLines = 20;

    /// <summary>로컬 LLM 공급자의 키. 서버의 <c>AiProviderRegistry.JsiniKey</c> 와 같다.</summary>
    public const string LocalProvider = "jsini";

    /// <summary>
    /// 줄 묶음 하나를 옮긴다. <b>못 옮기면 <see cref="ApiException"/> 이다</b> —
    /// 부르는 쪽이 사정을 말해야 한다.
    /// </summary>
    public async Task<AiTranslatedLinesDto?> TranslateAsync(
        IReadOnlyList<string> lines, string targetLang, CancellationToken ct = default)
        => await gateway.PostAsync<AiTranslatedLinesDto>(
            "ai/translate",
            new { lines, targetLang, provider = LocalProvider },
            ct);
}
