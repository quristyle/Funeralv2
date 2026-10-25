using System.Text.Json;

using AIAgentServer.Services.Mcp;

namespace AIAgentServer.Services.Portal;

/// <summary>업무 비서가 모델에게 보여 줄 포털 도구 일곱 가지의 생김새(이름·설명·매개변수 틀). 실제 호출은
/// <see cref="PortalToolRunner"/> 가 맡는다 — 여기는 모델이 「무엇을 어떻게 부를 수 있는지」만 적는다.</summary>
public static class PortalToolSet
{
    /// <summary>
    /// <paramref name="internalEnabled"/> 가 거짓이면(<c>Assistant:InternalToken</c> 이 비어 있다는 뜻)
    /// <c>search_documents</c> 하나만 돌려준다 — 나머지 여섯은 헬프데스크·프로젝트관리·알림 서버의
    /// 서버 간 호출에 그 토큰이 꼭 있어야 쓸 수 있고, 토큰 없이 불렀다가는 전부 404 로 돌아온다.
    /// 토큰 없는 개발 장비에서도 비서가 통째로 죽는 대신 문서 질문만은 답할 수 있게 하려는 것이다.
    /// </summary>
    public static IReadOnlyList<McpToolInfo> Tools(bool internalEnabled)
    {
        // search_gmail 은 내부 토큰이 없어도 선다 — 받아 온 메일은 다른 서비스에 묻지 않고
        // 이 서비스가 제 DB(scom.gmail_messages)에서 바로 읽기 때문이다.
        if (!internalEnabled) return [SearchGmail, SearchDocuments];

        return
        [
            FindUser,
            SearchRequests,
            GetRequest,
            SearchSchedules,
            SearchTodos,
            SearchMails,
            SearchGmail,
            SearchDocuments,
        ];
    }

    private static readonly McpToolInfo FindUser = new(
        "find_user",
        "포털 사용자 이름으로 로그인 아이디를 찾는다. 다른 포털 도구(search_requests · search_schedules 등)에 " +
        "넘길 userId 가 필요할 때 먼저 이 도구로 찾는다. 동명이인이면 하나를 고르지 않고 후보를 전부 돌려주니, " +
        "그럴 때는 사용자에게 어느 사람인지 되물어야 한다. " +
        "못 찾아도 그 사람이 없다는 뜻은 아니다 — 포털 계정이 없을 뿐이니 search_documents 로 더 찾아본다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "찾을 사람의 이름. 전체 일치가 아니어도 된다(부분 일치)." }
          },
          "required": ["name"]
        }
        """));

    private static readonly McpToolInfo SearchRequests = new(
        "search_requests",
        "헬프데스크 요청 글(건의·질문·버그 신고 등) 목록을 찾는다. userId 는 글쓴이·담당자·댓글 단 사람 " +
        "세 역할 중 아무거나로 걸린다. 비워 두면 조건에 맞는 모든 사람의 글을 찾는다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "userId": { "type": "string", "description": "포털 로그인 아이디(find_user 로 찾은 값)" },
            "from": { "type": "string", "description": "조회 시작 날짜(yyyy-MM-dd, 한국 날짜)" },
            "to": { "type": "string", "description": "조회 끝 날짜(yyyy-MM-dd, 한국 날짜, 이 날짜까지 포함)" },
            "status": { "type": "string", "description": "요청 상태. Pending(대기) · InProgress(진행) · Rejected(반려) · Completed(완료) · Consultation(협의) · Negotiation(논의) · UserCompleted(종료) 중 하나." },
            "keyword": { "type": "string", "description": "제목·본문에서 찾을 낱말" },
            "take": { "type": "integer", "description": "가져올 최대 건수(기본 30, 최대 100)" }
          }
        }
        """));

    private static readonly McpToolInfo GetRequest = new(
        "get_request",
        "요청 글 하나의 본문과 댓글을 전부 읽는다. search_requests 로 찾은 번호(id)를 넣는다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "id": { "type": "integer", "description": "요청 글 번호" }
          },
          "required": ["id"]
        }
        """));

    private static readonly McpToolInfo SearchSchedules = new(
        "search_schedules",
        "헬프데스크 일정과 프로젝트관리 일정(WBS)을 함께 찾는다 — 묻는 사람은 일정이 둘 중 어디 있는지 " +
        "모르므로 이 도구 하나가 양쪽을 합쳐 낸다. userId 만 알아도 된다(이름은 자동으로 찾아 채운다). " +
        "이름만 알고 아이디를 모르면 name 만 넘긴다 — 동명이인이 있을 수 있어 아이디는 추측하지 않는다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "userId": { "type": "string", "description": "포털 로그인 아이디(find_user 로 찾은 값). 이것만 있어도 이름은 자동으로 찾는다." },
            "name": { "type": "string", "description": "사람 이름. userId 를 모를 때만 넘긴다." },
            "from": { "type": "string", "description": "조회 시작 날짜(yyyy-MM-dd, 한국 날짜)" },
            "to": { "type": "string", "description": "조회 끝 날짜(yyyy-MM-dd, 한국 날짜, 이 날짜까지 포함)" },
            "keyword": { "type": "string", "description": "제목에서 찾을 낱말" },
            "take": { "type": "integer", "description": "가져올 최대 건수(기본 30, 최대 100, 두 출처 합산 기준)" }
          }
        }
        """));

    private static readonly McpToolInfo SearchTodos = new(
        "search_todos",
        "프로젝트관리의 할 일(홈 투두) 목록을 찾는다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "userId": { "type": "string", "description": "포털 로그인 아이디(find_user 로 찾은 값)" },
            "from": { "type": "string", "description": "조회 시작 날짜(yyyy-MM-dd, 한국 날짜)" },
            "to": { "type": "string", "description": "조회 끝 날짜(yyyy-MM-dd, 한국 날짜, 이 날짜까지 포함)" },
            "done": { "type": "boolean", "description": "완료 여부로 거르기. 생략하면 완료·미완료 모두." },
            "take": { "type": "integer", "description": "가져올 최대 건수(기본 30, 최대 100)" }
          }
        }
        """));

    private static readonly McpToolInfo SearchMails = new(
        "search_mails",
        "포털에서 보낸 메일(손으로 쓴 메일과 알림 직발송 메일 둘 다)을 찾는다. 받은 메일함은 없다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "userId": { "type": "string", "description": "보낸 사람의 포털 로그인 아이디(find_user 로 찾은 값)" },
            "from": { "type": "string", "description": "조회 시작 날짜(yyyy-MM-dd, 한국 날짜)" },
            "to": { "type": "string", "description": "조회 끝 날짜(yyyy-MM-dd, 한국 날짜, 이 날짜까지 포함)" },
            "keyword": { "type": "string", "description": "제목·받는 주소·본문 미리보기에서 찾을 낱말" },
            "take": { "type": "integer", "description": "가져올 최대 건수(기본 30, 최대 100)" }
          }
        }
        """));

    private static readonly McpToolInfo SearchGmail = new(
        "search_gmail",
        "받아 온 Gmail 을 찾는다. 포털이 보낸 메일만 보는 search_mails 와 달리 **받은 메일도** 있다. " +
        "묻는 사람 본인의 메일함만 보이므로 다른 사람의 메일은 이 도구로 찾을 수 없다 — " +
        "userId 같은 것을 넘길 자리가 없는 까닭이다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "from": { "type": "string", "description": "조회 시작 날짜(yyyy-MM-dd, 한국 날짜)" },
            "to": { "type": "string", "description": "조회 끝 날짜(yyyy-MM-dd, 한국 날짜, 이 날짜까지 포함)" },
            "fromAddr": { "type": "string", "description": "보낸 사람의 메일 주소나 이름(부분 일치)" },
            "keyword": { "type": "string", "description": "제목·본문에서 찾을 낱말" },
            "take": { "type": "integer", "description": "가져올 최대 건수(기본 30, 최대 100)" }
          }
        }
        """));

    private static readonly McpToolInfo SearchDocuments = new(
        "search_documents",
        "사내 문서함(RAG)에서 질문과 관련된 문서 토막을 찾는다. 제도·규정·사용법은 물론 " +
        "사람·조직·프로젝트 이름처럼 **포털 계정이나 업무 기록에는 없는 것**도 문서에는 적혀 있다. " +
        "다른 도구가 빈손으로 돌아왔으면 답을 맺기 전에 이 도구로 한 번 더 찾는다.",
        Schema("""
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "찾을 내용(질문 그대로 넣어도 된다)" }
          },
          "required": ["query"]
        }
        """));

    /// <summary>스키마 글을 파싱해 떼어 둔다(<c>Clone</c>) — 안 그러면 <see cref="JsonDocument"/> 가
    /// 버려질 때 뒤에 깔린 버퍼가 풀로 돌아가 <see cref="JsonElement"/> 가 깨진 메모리를 가리키게 된다.</summary>
    private static JsonElement Schema(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
