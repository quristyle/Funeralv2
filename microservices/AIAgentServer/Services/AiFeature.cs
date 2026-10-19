namespace AIAgentServer.Services;

/// <summary>사용량 줄의 <c>feature</c> 칸에 적는 값. <b>글자가 한 곳에만 있어야 한다.</b> 적는 쪽()과 거르는 쪽(사용량 조회 · 화면의 고르개)이 같은 글자를 써야 하는데, 양쪽에 손으로 적으면 오타가 오류가 아니라 조용한 빈 목록으로 나온다</summary>
public static class AiFeature
{
    /// <summary>한 번에 받는 대화. 상태 화면의 정밀 확인도 이 길로 온다.</summary>
    public const string Chat = "chat";

    /// <summary>흘려 받는 대화. <b>사람이 실제로 쓰는 AI쳇이 이것이다</b> — 화면(<c>/site/ai/chat</c>)과 헤더 서랍이 둘 다 이 길로 간다.</summary>
    public const string ChatStream = "chat-stream";

    /// <summary>공통코드 영문 이름 추천.</summary>
    public const string SuggestCode = "suggest-code";

    /// <summary>다국어 번역 추천.</summary>
    public const string SuggestI18n = "suggest-i18n";

    /// <summary>메뉴 자동 채우기. 설명을 보고 이름·제목·화면·아이콘을 고른다.</summary>
    public const string SuggestMenu = "suggest-menu";

    /// <summary>글 요약. 포털 「메일 보내기」가 본문을 추려 머리말로 쓴다. 대화()와 갈라 센다.</summary>
    public const string Summarize = "summarize";

    /// <summary>줄 단위 번역. 「AI 작업」 상세의 로그 탭이 원문과 번역을 좌우로 견주려고 쓴다. 요약()과 갈라 센다.</summary>
    public const string Translate = "translate";
}
