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
}
