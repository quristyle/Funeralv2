namespace AIAgentServer.Services;

/// <summary>
/// 사용량 줄의 <c>feature</c> 칸에 적는 값. <b>글자가 한 곳에만 있어야 한다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 적는 쪽(<see cref="LLMService"/>)과 거르는 쪽(사용량 조회 · 화면의 고르개)이
/// 같은 글자를 써야 하는데, 양쪽에 손으로 적으면 오타가 오류가 아니라
/// <b>조용한 빈 목록</b>으로 나온다 — 「그 기능은 아무도 안 썼구나」로 읽힌다.
/// </para>
/// <para>
/// <b>값을 바꾸지 않는다.</b> 이미 쌓인 줄의 글자가 그대로 남아 있어서,
/// 이름을 고치면 옛 줄과 새 줄이 다른 기능으로 갈라진다. 보여 줄 이름은
/// 화면이 따로 붙인다.
/// </para>
/// </remarks>
public static class AiFeature
{
    /// <summary>한 번에 받는 대화. 상태 화면의 정밀 확인도 이 길로 온다.</summary>
    public const string Chat = "chat";

    /// <summary>
    /// 흘려 받는 대화. <b>사람이 실제로 쓰는 AI쳇이 이것이다</b> —
    /// 화면(<c>/site/ai/chat</c>)과 헤더 서랍이 둘 다 이 길로 간다.
    /// </summary>
    public const string ChatStream = "chat-stream";

    /// <summary>공통코드 영문 이름 추천.</summary>
    public const string SuggestCode = "suggest-code";

    /// <summary>다국어 번역 추천.</summary>
    public const string SuggestI18n = "suggest-i18n";

    /// <summary>
    /// 글 요약. 포털 「메일 보내기」가 본문을 추려 머리말로 쓴다.
    /// </summary>
    /// <remarks>
    /// <b>대화(<see cref="Chat"/>)와 갈라 센다.</b> 한 번이 무겁고(본문 전체를
    /// 올린다) 쓰는 사람이 뚜렷해서, 섞어 두면 「AI쳇을 쓴 적도 없는데 토큰이
    /// 왜 이리 나가나」의 까닭이 화면 어디에도 안 보인다.
    /// </remarks>
    public const string Summarize = "summarize";

    /// <summary>
    /// 줄 단위 번역. 「AI 작업」 상세의 로그 탭이 원문과 번역을 좌우로
    /// 견주려고 쓴다.
    /// </summary>
    /// <remarks>
    /// <b>요약(<see cref="Summarize"/>)과 갈라 센다.</b> 한 번 보는 데 호출이
    /// 여러 번 나간다 — 로그를 스무 줄씩 끊어 보내기 때문이다. 요약과 섞어
    /// 두면 「메일 한 통 요약했을 뿐인데 호출이 왜 이리 많나」가 된다.
    /// </remarks>
    public const string Translate = "translate";
}
