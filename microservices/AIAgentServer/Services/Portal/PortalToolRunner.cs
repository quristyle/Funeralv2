using AIAgentServer.Services.Rag;

namespace AIAgentServer.Services.Portal;

/// <summary><see cref="PortalToolSet"/> 이 펼쳐 놓은 도구를 실제로 실행한다.</summary>
public sealed class PortalToolRunner(
    UserDirectory users,
    HelpDeskGateway helpDesk,
    ProjMngGateway projMng,
    MailGateway mail,
    RagStore ragStore,
    EmbeddingClient embed,
    ILogger<PortalToolRunner> logger)
{
    private const int DefaultTake = 30;
    private const int MaxTake = 100;
    private const int DocumentTopK = 6;

    /// <summary>
    /// 도구 하나를 부른다. <b>절대 예외를 던지지 않는다</b> — <see cref="PortalDay"/> 가 날짜 형식이
    /// 틀렸을 때 터뜨리는 것을 비롯해 모든 실패를 글로 돌려줘야, 모델이 그 글을 읽고 형식을 고쳐
    /// 다시 부를 수 있다. 예외가 이 호출 경로를 타고 올라가면 McpToolGateway.CallAsync 머리말대로
    /// 사람에게는 대화가 통째로 죽은 것으로 보인다.
    /// </summary>
    public async Task<string> CallAsync(
        string name, IReadOnlyDictionary<string, object?> args, RagScope? scope, CancellationToken ct = default)
    {
        try
        {
            return name switch
            {
                "find_user" => await FindUserAsync(args, ct),
                "search_requests" => await SearchRequestsAsync(args, ct),
                "get_request" => await GetRequestAsync(args, ct),
                "search_schedules" => await SearchSchedulesAsync(args, ct),
                "search_todos" => await SearchTodosAsync(args, ct),
                "search_mails" => await SearchMailsAsync(args, ct),
                "search_documents" => await SearchDocumentsAsync(args, scope, ct),
                _ => "그런 도구가 없습니다.",
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "포털 도구 호출 실패: {Tool}", name);
            return $"({name} 도구를 쓰지 못했습니다: {ex.Message})";
        }
    }

    private async Task<string> FindUserAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var name = Str(args, "name");
        if (name is null) return "이름을 알려주셔야 찾을 수 있습니다.";

        var people = await users.FindAsync(name, ct);
        return UserDirectory.Describe(people);
    }

    private async Task<string> SearchRequestsAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var take = Take(args);

        // HelpDeskServer 의 /internal/requests 는 DateTime? from/to 를 받아 RequestedAt 과
        // >=/<= 로 그대로 견준다(자기 쪽에서 날짜를 다시 안 바꾼다) — 그래서 한국 날짜를
        // PortalDay 로 미리 UTC 경계로 바꿔서 보낸다. 직접 산수(±9시간)를 하지 않는다.
        var fromUtc = ParseStartUtc(args, "from");
        var toUtc = ParseEndExclusiveUtc(args, "to");

        var (total, items) = await helpDesk.SearchRequestsAsync(
            Str(args, "userId"), fromUtc, toUtc, Str(args, "status"), Str(args, "keyword"), take, ct);

        var lines = items.Select(RenderRequest).ToList();

        // "요청 글" 이 문자 그대로 web/Assistant.razor.cs 의 RequestRefPattern 이 찾는 낱말이다 —
        // 화면이 답 글 속 "요청(글)? #id" 모양을 그 낱말로 가려 근거 링크를 건다. 여기 말을 바꾸면
        // (예: "헬프데스크 글"로) 그 정규식도 함께 고쳐야 한다 — 안 고치면 링크가 오류 없이 조용히 사라진다.
        return PortalToolResult.Render("요청 글", lines, total, take);
    }

    private async Task<string> GetRequestAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var id = Int(args, "id");
        if (id is null) return "요청 글 번호(id)를 알려주셔야 합니다.";

        var detail = await helpDesk.GetRequestAsync(id.Value, ct);
        if (detail is null) return $"요청 글 #{id}을(를) 찾지 못했습니다.";

        // 이 머리글은 "요청 글" 없이 바로 #id 로 시작한다 — 화면의 근거 링크 정규식(위 참고)은
        // 그 낱말이 있어야 걸리므로, 모델이 이 글을 요약하며 "요청글"을 다시 안 붙이면 여기서
        // 나온 #id 인용은 링크가 안 될 수 있다. 알려진 틈이다(web/.../Assistant.razor.cs 머리말).
        var header = $"#{detail.Id} {detail.Title} [{detail.Status}]\n"
            + $"접수 {PortalDay.ToKorean(detail.RequestedAt)}"
            + (detail.AcceptedAt is { } a ? $", 처리 {PortalDay.ToKorean(a)}" : "")
            + (detail.CompletedAt is { } c ? $", 완료 {PortalDay.ToKorean(c)}" : "")
            + (detail.AuthorName is null ? "" : $", 글쓴이 {detail.AuthorName}")
            + (detail.AssigneeName is null ? "" : $", 담당 {detail.AssigneeName}")
            + (detail.CompanyName is null ? "" : $", {detail.CompanyName}")
            + "\n\n" + detail.Description;

        if (detail.Comments.Count == 0) return header + "\n\n(댓글 없음)";

        var comments = detail.Comments.Select(c =>
            $"- {(c.IsDeleted ? "(지운 댓글)" : c.AuthorName ?? c.AuthorType)} {PortalDay.ToKorean(c.CreatedAt)}: {c.CommentText}");

        return header + "\n\n댓글:\n" + string.Join("\n", comments);
    }

    /// <summary>헬프데스크와 프로젝트관리 일정을 나란히 불러 한 목록으로 합친다. 한쪽이 비어 있어도
    /// (헬프데스크의 일정 표는 운영에서 늘 비어 있다) 다른 쪽 결과는 그대로 나와야 한다 —
    /// <see cref="Task.WhenAll(Task[])"/> 로 같이 기다릴 뿐, 서로의 결과에 기대지 않는다.
    /// <b>모델이 userId 만 주고 name 을 안 줄 수 있다</b> — 그때 이름 없이 그대로 넘기면 프로젝트관리의
    /// 손으로 적은 담당자 칸(로그인 아이디가 아니라 이름이 섞여 있다)에서 거의 안 걸린다. 그래서
    /// 이름이 없으면 <see cref="UserDirectory.FindByIdAsync"/> 로 직접 찾아 채운다(<see cref="ResolveScheduleIdentity"/>).</summary>
    private async Task<string> SearchSchedulesAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var userId = Str(args, "userId");
        var name = Str(args, "name");
        var keyword = Str(args, "keyword");
        var take = Take(args);

        // 아이디만 왔고 이름이 없을 때만 되찾는다 — 아이디는 유일해서 동명이인 걱정이 없다.
        // 반대로 이름만 왔을 때는(userId 가 null) 아이디를 거꾸로 추측하지 않는다 — 같은 이름의
        // 계정이 여럿이면(예: 김지수 셋) 엉뚱한 사람의 아이디를 붙이게 된다.
        string? lookedUpName = null;
        if (userId is not null && name is null)
        {
            var person = await users.FindByIdAsync(userId, ct);
            lookedUpName = person?.Name; // 못 찾아도 그냥 null — 아이디만으로 계속 진행한다.
        }

        var identity = ResolveScheduleIdentity(userId, name, lookedUpName);

        // 프로젝트관리는 DateOnly 를 그대로 받으므로 한국 날짜 글자를 그대로 넘긴다 — 여기서
        // UTC 로 바꾸면 그 서버가 다시 날짜로 해석할 때 하루가 밀릴 수 있다.
        var fromDay = Str(args, "from");
        var toDay = Str(args, "to");

        // 헬프데스크는 timestamptz(StartDate/EndDate)를 직접 견주는 서버라, 한국 날짜를
        // PortalDay.StartUtc/EndExclusiveUtc 로 미리 UTC 경계로 바꿔서 넘긴다.
        var fromUtc = fromDay is null ? (DateTime?)null : PortalDay.StartUtc(fromDay);
        var toUtc = toDay is null ? (DateTime?)null : PortalDay.EndExclusiveUtc(toDay);

        var helpDeskTask = helpDesk.SearchSchedulesAsync(identity.UserId, fromUtc, toUtc, keyword, take, ct);
        var projMngTask = projMng.SearchSchedulesAsync(identity.UserId, identity.Name, fromDay, toDay, keyword, take, ct);
        await Task.WhenAll(helpDeskTask, projMngTask);

        var (hTotal, hItems) = helpDeskTask.Result;
        var (pTotal, pItems) = projMngTask.Result;

        var lines = new List<string>(hItems.Count + pItems.Count);
        lines.AddRange(hItems.Select(RenderHelpDeskSchedule));
        lines.AddRange(pItems.Select(RenderProjMngSchedule));

        var total = hTotal + pTotal;
        var shown = lines.Take(Math.Min(lines.Count, take)).ToList();
        return PortalToolResult.Render("일정", shown, total, take);
    }

    private async Task<string> SearchTodosAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var userId = Str(args, "userId");

        // ProjMngServer 의 /internal/todos 는 DateOnly? 로 그대로 받는다 — 한국 날짜 글자를
        // 시각 없이 그대로 보낸다(ValidDay 는 형식만 확인하고 바꾸지 않는다).
        var fromDay = ValidDay(args, "from");
        var toDay = ValidDay(args, "to");
        var done = Bool(args, "done");
        var take = Take(args);

        var (total, items) = await projMng.SearchTodosAsync(userId, fromDay, toDay, done, take, ct);
        var lines = items.Select(RenderTodo).ToList();
        return PortalToolResult.Render("할 일", lines, total, take);
    }

    private async Task<string> SearchMailsAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var userId = Str(args, "userId");

        // NotificationServer 의 /internal/mails 는 DateTime? 을 받지만, 그 안에서 다시
        // DateOnly.FromDateTime(f) 로 한국 날짜를 뽑아 자기가 UTC 경계로 바꾼다. 그래서 여기서
        // 시각 없는 "yyyy-MM-dd" 글자를 그대로 보내야 한다 — 미리 UTC 로 바꿔 보내면 그 인스턴트의
        // 날짜가 다시 DateOnly.FromDateTime 에 걸려 하루가 밀린다.
        var fromDay = ValidDay(args, "from");
        var toDay = ValidDay(args, "to");
        var keyword = Str(args, "keyword");
        var take = Take(args);

        var (total, items) = await mail.SearchMailsAsync(userId, fromDay, toDay, keyword, take, ct);
        var lines = items.Select(RenderMail).ToList();
        return PortalToolResult.Render("보낸 메일", lines, total, take);
    }

    private async Task<string> SearchDocumentsAsync(
        IReadOnlyDictionary<string, object?> args, RagScope? scope, CancellationToken ct)
    {
        var query = Str(args, "query");
        if (query is null) return "무엇을 찾을지 질문(query)을 알려주셔야 합니다.";

        float[]? vector = null;
        try
        {
            vector = await embed.EmbedOneAsync(query, ct);
        }
        catch (Exception ex)
        {
            // 임베딩 서버가 죽어도 낱말 검색으로는 찾는다 — RagAugmenter 와 같은 요령이다.
            logger.LogWarning(ex, "문서 검색: 임베딩에 실패해 낱말 검색만 합니다.");
        }

        var hits = ragStore.Search(scope?.Category, query, vector, DocumentTopK, scope?.DocIds);
        var lines = hits.Select(h => $"- [{h.Chunk.Source} {h.Chunk.Heading}] {Shorten(h.Chunk.Text)}").ToList();

        return PortalToolResult.Render("문서 검색", lines, lines.Count, DocumentTopK);
    }

    /// <summary>search_schedules 에 프로젝트관리로 넘길 (아이디, 이름)을 정한다. 세 갈래뿐이다 —
    /// (1) 이름이 이미 왔으면 그대로 쓴다(아이디도 그대로, 둘 다 손대지 않는다).
    /// (2) 아이디만 왔으면 <paramref name="lookedUpName"/>(되찾은 이름, 없으면 null)으로 채운다.
    /// (3) 아이디도 이름도 없으면 그대로 둘 다 null.
    /// <b>이름만으로 아이디를 지어내는 길은 없다</b> — 동명이인이면 엉뚱한 사람의 아이디가 된다.</summary>
    internal static (string? UserId, string? Name) ResolveScheduleIdentity(
        string? userId, string? name, string? lookedUpName)
    {
        if (name is not null) return (userId, name);
        return userId is null ? (null, null) : (userId, lookedUpName);
    }

    // ── 줄 꾸미기 — 순수 함수라 따로 시험한다 ──────────────────────

    internal static string RenderRequest(HelpDeskGateway.RequestRow r)
    {
        var period = $"접수 {PortalDay.ToKorean(r.RequestedAt)}";
        var done = r.CompletedAt is { } c ? $", 완료 {PortalDay.ToKorean(c)}" : "";
        var author = r.AuthorName is null ? "" : $", 글쓴이 {r.AuthorName}";
        var assignee = r.AssigneeName is null ? "" : $", 담당 {r.AssigneeName}";
        var company = r.CompanyName is null ? "" : $", {r.CompanyName}";
        var match = string.IsNullOrEmpty(r.Match) ? "" : $", 관련 {r.Match}";
        return $"- #{r.Id} {r.Title} [{r.Status}] ({period}{done}{author}{assignee}{company}{match})";
    }

    /// <summary>헬프데스크 일정 한 줄. <c>Owner</c> 가 이미 사람 이름으로 풀려 있다고 밝혀 적는다 —
    /// 프로젝트관리 쪽(원문 아이디/이름 뒤섞임)과 같은 말로 보이지 않게 하려는 것이다.</summary>
    internal static string RenderHelpDeskSchedule(HelpDeskGateway.ScheduleRow r)
    {
        var period = $"{PortalDay.ToKorean(r.StartDate)} ~ {PortalDay.ToKorean(r.EndDate)}";
        var state = r.Done ? "완료" : "진행";
        var owner = r.Owner is null ? "담당 미정" : $"담당 {r.Owner}";
        return $"- [헬프데스크] {r.Title} ({period}, {state}, {owner})";
    }

    /// <summary>프로젝트관리 일정 한 줄. <c>담당(원문)</c> 이라고 적어 — 로그인 아이디일 수도 손으로
    /// 적은 이름일 수도 있는 값임을 모델이 알게 한다(헬프데스크의 "담당"과 섞이지 않도록).</summary>
    internal static string RenderProjMngSchedule(ProjMngGateway.ScheduleRow r)
    {
        var period = $"{FormatDay(r.StartDate)} ~ {FormatDay(r.EndDate)}";
        var owner = r.Owner is null ? "담당(원문) 미정" : $"담당(원문) {r.Owner}";
        var role = string.IsNullOrEmpty(r.Role) ? "" : $", 역할 {r.Role}";
        var note = string.IsNullOrWhiteSpace(r.Note) ? "" : $", 메모 {r.Note}";
        return $"- [프로젝트관리] {r.Title ?? "(제목 없음)"} ({period}, {r.State}, {owner}{role}{note})";
    }

    internal static string RenderTodo(ProjMngGateway.TodoRow r)
    {
        var day = FormatDay(r.TargetDay);
        var state = r.Done ? "완료" : "미완료";
        var owner = r.Owner is null ? "" : $", 담당 {r.Owner}";
        var note = string.IsNullOrWhiteSpace(r.Comments) ? "" : $" — {r.Comments}";
        return $"- {r.Title} ({day}, {state}{owner}){note}";
    }

    internal static string RenderMail(MailGateway.MailRow r)
        => $"- [{r.Kind}] {r.Subject ?? "(제목 없음)"} → {r.To} ({PortalDay.ToKorean(r.SentAt)})";

    internal static string FormatDay(DateOnly? d) => d?.ToString("yyyy-MM-dd") ?? "미정";

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";

    // ── 도구 인자 읽기 ──────────────────────────────────────────

    private static int Take(IReadOnlyDictionary<string, object?> args)
        => Math.Clamp(Int(args, "take") ?? DefaultTake, 1, MaxTake);

    /// <summary>날짜 글자를 그대로 돌려주되, 형식만 미리 확인한다(틀리면 여기서 던져 바깥 try/catch 가
    /// 사람이 읽을 글로 바꾼다) — 호출한 뒤 서버에 보내고 나서야 형식 오류를 알면 왕복이 헛되다.</summary>
    private static string? ValidDay(IReadOnlyDictionary<string, object?> args, string key)
    {
        var s = Str(args, key);
        if (s is null) return null;
        _ = PortalDay.StartUtc(s);
        return s;
    }

    private static DateTime? ParseStartUtc(IReadOnlyDictionary<string, object?> args, string key)
    {
        var s = Str(args, key);
        return s is null ? null : PortalDay.StartUtc(s);
    }

    private static DateTime? ParseEndExclusiveUtc(IReadOnlyDictionary<string, object?> args, string key)
    {
        var s = Str(args, key);
        return s is null ? null : PortalDay.EndExclusiveUtc(s);
    }

    private static string? Str(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var v) || v is null) return null;

        var s = v switch
        {
            string str => str,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } je => je.GetString(),
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Null } => null,
            System.Text.Json.JsonElement je => je.ToString(),
            _ => v.ToString(),
        };

        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static int? Int(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var v) || v is null) return null;

        return v switch
        {
            int i => i,
            double d => (int)d,
            long l => (int)l,
            System.Text.Json.JsonElement je when je.TryGetInt32(out var n) => n,
            string s when int.TryParse(s, out var n) => n,
            _ => null,
        };
    }

    private static bool? Bool(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var v) || v is null) return null;

        return v switch
        {
            bool b => b,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True } => true,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False } => false,
            string s when bool.TryParse(s, out var b) => b,
            _ => null,
        };
    }
}
