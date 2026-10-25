# 업무 비서 화면 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 슈퍼관리자·임원이 RAG 문서와 포털 업무 데이터(헬프데스크·프로젝트관리·메일)를 가로질러 물어볼 수 있는 대화 화면을 만든다.

**Architecture:** 업무 데이터를 색인하지 않고 **모델에게 도구를 쥐어 준다.** `Services/Mcp/WebChatAgent.cs` 의 `tool_calls` 루프를 본떠 AIAgentServer 안에 포털 도구를 두고, 각 도구는 사내 서비스의 **조회 전용 내부 엔드포인트**를 공유 비밀로 부른다. 마지막 답만 SSE 로 흘린다. RAG 도 이 화면에서는 도구다.

**Tech Stack:** .NET 10 · Minimal API · EF Core/PostgreSQL · xunit · Blazor + DevExpress · Qwen3-Next-80B(로컬, OpenAI 규격 tool_calls)

**Spec:** [docs/superpowers/specs/2026-10-24-portal-ai-assistant-design.md](../specs/2026-10-24-portal-ai-assistant-design.md)

## Global Constraints

- **읽기만 한다.** 도구도 내부 엔드포인트도 쓰기를 하지 않는다. `POST`/`PUT`/`DELETE` 를 새로 열지 않는다(대화 엔드포인트 하나만 예외).
- **시각은 UTC.** `DateTime.Now`·`DateTime.Today` 금지, `AppTime` 을 쓴다. 도구 경계에서만 한국 날짜(`YYYY-MM-DD`)로 주고받는다.
- 목록 도구 기본 **30건**, 최대 **100건**. 잘렸으면 결과에 `"총 N건 중 M건"` 을 적는다.
- 커밋 메시지·주석·문서는 한국어. 주석은 코드로 알 수 없는 것만.
- 비밀값은 커밋하지 않는다. `scripts/secrets.env.example` 만 갱신한다.
- **기존 AI쳇을 건드리지 않는다** — `AiChatPanel`, `RagAugmenter`, `LLMService` 의 기존 길은 그대로 둔다.
- 화면은 `/admin/ai/assistant`, 슈퍼관리자·임원만.
- 회사 경계를 넘는다(모든 회사 자료를 본다).

## Review Focus

- **내부 토큰이 비었을 때** — 도구가 조용히 꺼지고 문서 질문만 받아야 한다. 예외로 터지면 화면이 통째로 죽는다. (Task 6)
- **동명이인** — `find_user("홍길동")` 이 둘을 돌려줬을 때 한 명을 임의로 고르면 남의 자료가 나간다. 후보를 그대로 넘기고 모델이 되묻게 해야 한다. (Task 4)
- **한국 날짜 경계** — "10-17일 이후" 는 KST 10-17 00:00 = UTC 10-16 15:00 이다. 날짜를 그대로 UTC 로 쓰면 하루가 통째로 어긋난다. (Task 5)
- **빈 결과** — 도구가 0건을 주면 "없습니다" 여야 한다. 결과 글이 비어 있으면 모델이 다른 도구 결과로 메운다. 빈 결과도 `"0건"` 이라고 적어 보낸다. (Task 5)
- **라운드 소진** — 세 시스템을 도느라 도구 라운드를 다 써도 답은 나와야 한다. 도구 없이 한 번 더 불러 정리시킨다. (Task 8)

---

## 파일 구성

| 파일 | 맡은 일 |
|---|---|
| `microservices/Common/Internal/InternalCallGuard.cs` | 공유 비밀 검사. 세 서비스가 함께 쓴다 |
| `microservices/HelpDeskServer/Endpoints/InternalQueryEndpoints.cs` | 요청글·댓글 조회(권한 무시) |
| `microservices/ProjMngServer/Controllers/InternalQueryController.cs` | 일정·할 일 조회 |
| `microservices/NotificationServer/Endpoints/InternalQueryEndpoints.cs` | 보낸 메일·발송 기록 조회 |
| `AIAgentServer/Services/Portal/PortalDay.cs` | 한국 날짜 ↔ UTC 구간 |
| `AIAgentServer/Services/Portal/PortalToolResult.cs` | 도구 결과를 글로. 상한·잘림 표시 |
| `AIAgentServer/Services/Portal/PortalToolSet.cs` | 도구 목록과 JSON 스키마 |
| `AIAgentServer/Services/Portal/PortalToolRunner.cs` | 이름 → 실행. 게이트웨이들을 든다 |
| `AIAgentServer/Services/Portal/HelpDeskGateway.cs` 외 3 | 내부 엔드포인트 호출 |
| `AIAgentServer/Services/Portal/AssistantAgent.cs` | 도구 루프 + 마지막 답 스트리밍 |
| `AIAgentServer/Services/Portal/AssistantAudit.cs` | 기록 한 줄 만들어 담기 |
| `AIAgentServer/Endpoints/AssistantEndpoints.cs` | `/assistant/*` |
| `web/.../Admin/Components/Pages/Assistant.razor(.cs)` | 화면 |
| `web/.../Admin/Api/AssistantClient.cs` | 게이트웨이 호출 |
| `microservices/AIAgentServer.Tests/` | **새 시험 프로젝트** |
| `deploy/sql/ai-assistant-audit-2026-10-25.sql` | 기록 표 |

---

### Task 1: 시험 프로젝트와 내부 호출 자격

**Files:**
- Create: `microservices/AIAgentServer.Tests/AIAgentServer.Tests.csproj`
- Create: `microservices/Common/Internal/InternalCallGuard.cs`
- Test: `microservices/AIAgentServer.Tests/InternalCallGuardTests.cs`

**Interfaces:**
- Produces: `JSini.Shared.Internal.InternalCallGuard.IsAllowed(string? header, string? configured) -> bool`

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class InternalCallGuardTests
{
    [Fact]
    public void 설정값이_비면_언제나_막는다()
    {
        Assert.False(InternalCallGuard.IsAllowed("아무거나", null));
        Assert.False(InternalCallGuard.IsAllowed("아무거나", ""));
        Assert.False(InternalCallGuard.IsAllowed("아무거나", "   "));
    }

    [Fact]
    public void 머리글이_없거나_다르면_막는다()
    {
        Assert.False(InternalCallGuard.IsAllowed(null, "비밀"));
        Assert.False(InternalCallGuard.IsAllowed("비밀 ", "비밀"));
        Assert.False(InternalCallGuard.IsAllowed("Bearer 비밀", "비밀"));
    }

    [Fact]
    public void 똑같을_때만_통과한다()
        => Assert.True(InternalCallGuard.IsAllowed("비밀", "비밀"));
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests`
Expected: 컴파일 실패 — `InternalCallGuard` 없음

- [ ] **Step 3: `InternalCallGuard.IsAllowed(string? header, string? configured) -> bool` 를 `microservices/Common/Internal/InternalCallGuard.cs` 에 구현한다**

네임스페이스는 `JSini.Shared.Internal`. 비교는 길이 누출을 줄이려 `CryptographicOperations.FixedTimeEquals` 로 한다.

- [ ] **Step 4: 돌려서 통과를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests`
Expected: PASS 3건

- [ ] **Step 5: 커밋**

```bash
git add microservices/Common/Internal/InternalCallGuard.cs microservices/AIAgentServer.Tests
git commit -m "업무 비서: 내부 호출 자격 검사와 AIAgentServer 시험 프로젝트"
```

---

### Task 2: 헬프데스크 내부 조회

**Files:**
- Create: `microservices/HelpDeskServer/Endpoints/InternalQueryEndpoints.cs`
- Modify: `microservices/HelpDeskServer/Program.cs` (엔드포인트 등록)
- Test: `microservices/AIAgentServer.Tests/PersonMatchTests.cs`
- Create: `microservices/Common/Internal/PersonMatch.cs`

**Interfaces:**
- Consumes: `InternalCallGuard.IsAllowed`
- Produces:
  - `GET /internal/requests?userId&from&to&status&keyword&take` → `{ total, items[] }`, `items[i] = { id, title, status, requestedAt, acceptedAt, completedAt, authorName, assigneeName, companyName, match }`
  - `GET /internal/requests/{id}` → 본문 + 댓글 전부
  - `GET /internal/schedules?userId&from&to&keyword&take` → 헬프데스크 `Schedules` 표.
    `items[i] = { source:"helpdesk", title, startDate, endDate, done, owner }` —
    **Task 3 의 프로젝트관리 일정과 칸 이름을 똑같이 맞춘다.** 도구 하나가 둘을 합친다
  - `match` 는 `PersonMatch` 를 쉼표로 이은 글자열(`"쓴 글,댓글"`)

**왜 `match` 가 있나:** 사양이 "쓴 것 · 맡은 것 · 댓글 단 것 전부" 를 모아 **무엇으로 걸렸는지 표시해서** 돌려주기로 했다.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class PersonMatchTests
{
    [Fact]
    public void 세_갈래로_걸리면_셋_다_적는다()
    {
        var m = PersonMatch.Of(wrote: true, assigned: true, commented: true);
        Assert.Equal("쓴 글,맡은 글,댓글", m);
    }

    [Fact]
    public void 하나도_안_걸리면_빈_글자다()
        => Assert.Equal("", PersonMatch.Of(false, false, false));

    [Fact]
    public void 걸린_것만_차례대로_적는다()
        => Assert.Equal("쓴 글,댓글", PersonMatch.Of(wrote: true, assigned: false, commented: true));
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter PersonMatchTests`
Expected: 컴파일 실패

- [ ] **Step 3: `PersonMatch.Of(bool wrote, bool assigned, bool commented) -> string` 을 구현한다**

- [ ] **Step 4: 내부 엔드포인트를 구현한다**

`InternalQueryEndpoints.MapInternalQueryEndpoints(this IEndpointRouteBuilder)` — 그룹 `/internal`, 모든 길의 첫 줄에서 `InternalCallGuard.IsAllowed(http.Request.Headers["X-Internal-Token"], config["Assistant:InternalToken"])` 를 보고 아니면 `Results.NotFound()`.

**`NotFound` 로 돌려준다** — `Unauthorized` 는 이 길이 있다는 것을 알려 준다.

사람 조건(`userId`)은 세 갈래를 OR 로 묶는다: 글쓴이(`Customer` → `AuthUserLink`), 담당자(`Admin` → `AuthUserLink`), 댓글 쓴 사람(`ImprovementComment`). 회사로 거르지 않는다. 날짜는 `RequestedAt` 기준.

- [ ] **Step 5: `Program.cs` 에 `app.MapInternalQueryEndpoints();` 를 더하고 빌드한다**

Run: `dotnet build microservices/HelpDeskServer`
Expected: 경고 없이 성공

- [ ] **Step 6: 시험을 돌린다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter PersonMatchTests`
Expected: PASS 3건

- [ ] **Step 7: 커밋**

```bash
git add microservices/Common/Internal/PersonMatch.cs microservices/HelpDeskServer microservices/AIAgentServer.Tests
git commit -m "업무 비서: 헬프데스크 내부 조회 엔드포인트"
```

---

### Task 3: 프로젝트관리·알림 내부 조회

**Files:**
- Create: `microservices/ProjMngServer/Controllers/InternalQueryController.cs`
- Create: `microservices/NotificationServer/Endpoints/InternalQueryEndpoints.cs`
- Modify: `microservices/NotificationServer/Program.cs`

**Interfaces:**
- Produces:
  - `GET /internal/schedules?userId&from&to&keyword&take` → 프로젝트관리 WBS 일정.
    `items[i] = { source:"projmng", title, startDate, endDate, done, owner }` —
    Task 2 의 헬프데스크 일정과 **칸 이름이 같아야 한다**
  - `GET /internal/todos?userId&from&to&done&take` → `projmng.home_todo`. `items[i] = { title, targetDay, done, owner, comments }`
  - `GET /internal/mails?userId&from&to&keyword&take` → `{ total, items[] }`, `items[i] = { id, subject, to, sentAt, kind }`. `kind` 는 `"보낸 메일"` · `"발송 기록"`

**받은 메일은 없다.** 포털에 수신함이 없으므로 이 길은 보낸 사람 기준만 낸다.

- [ ] **Step 1: 세 길을 구현한다**

ProjMng 는 컨트롤러 방식이다(`ProjMngServer/Controllers/` 의 기존 모양을 따른다). 자격 검사는 Task 1 의 `InternalCallGuard` 를 `[ApiController]` 의 첫 줄에서 똑같이 쓴다.

- [ ] **Step 2: 빌드한다**

Run: `dotnet build microservices/ProjMngServer && dotnet build microservices/NotificationServer`
Expected: 성공

- [ ] **Step 3: 토큰을 설정 보기에 더한다**

`scripts/secrets.env.example` 에 `Assistant__InternalToken=` 한 줄. 값은 넣지 않는다.

- [ ] **Step 4: 커밋**

```bash
git add microservices/ProjMngServer microservices/NotificationServer scripts/secrets.env.example
git commit -m "업무 비서: 프로젝트관리·알림 내부 조회 엔드포인트"
```

---

### Task 4: 사람 찾기

**Files:**
- Create: `microservices/AIAgentServer/Services/Portal/UserDirectory.cs`
- Test: `microservices/AIAgentServer.Tests/UserDirectoryTests.cs`

**Interfaces:**
- Produces:
  - `sealed record PortalPerson(string UserId, string Name, string? CompanyName, string? Dept)`
  - `UserDirectory.FindAsync(string name, CancellationToken) -> Task<IReadOnlyList<PortalPerson>>`
  - `UserDirectory.Describe(IReadOnlyList<PortalPerson>) -> string`

`scom` 의 계정 표를 `AiUsageDbContext` 로 읽는다(이미 `Accounts` 가 있다). 이름이 **들어맞는 것과 품은 것**을 다 준다.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class UserDirectoryTests
{
    [Fact]
    public void 아무도_없으면_없다고_적는다()
        => Assert.Equal("찾는 이름의 사용자가 없습니다.", UserDirectory.Describe([]));

    [Fact]
    public void 한_명이면_그_사람만_적는다()
    {
        var s = UserDirectory.Describe([new PortalPerson("u1", "홍길동", "진네트웍스", "개발팀")]);
        Assert.Contains("u1", s, StringComparison.Ordinal);
        Assert.Contains("홍길동", s, StringComparison.Ordinal);
        Assert.DoesNotContain("여러", s, StringComparison.Ordinal);
    }

    [Fact]
    public void 동명이인이면_고르지_말고_되물으라고_적는다()
    {
        var s = UserDirectory.Describe([
            new PortalPerson("u1", "홍길동", "진네트웍스", "개발팀"),
            new PortalPerson("u2", "홍길동", "한주", "영업팀"),
        ]);

        Assert.Contains("u1", s, StringComparison.Ordinal);
        Assert.Contains("u2", s, StringComparison.Ordinal);
        Assert.Contains("되물어", s, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter UserDirectoryTests`
Expected: 컴파일 실패

- [ ] **Step 3: `UserDirectory` 를 구현한다**

`Describe` 는 둘 이상일 때 **"어느 쪽인지 사용자에게 되물어라"** 를 결과 글에 넣는다 — 모델이 임의로 한 명을 고르면 남의 자료가 나간다. `FindAsync` 는 이름 완전일치 먼저, 그다음 품은 것, 최대 10명.

- [ ] **Step 4: 돌려서 통과를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter UserDirectoryTests`
Expected: PASS 3건

- [ ] **Step 5: 커밋**

```bash
git add microservices/AIAgentServer/Services/Portal/UserDirectory.cs microservices/AIAgentServer.Tests
git commit -m "업무 비서: 사람 찾기 — 동명이인은 고르지 않고 되묻는다"
```

---

### Task 5: 날짜 경계와 도구 결과 다듬기

**Files:**
- Create: `microservices/AIAgentServer/Services/Portal/PortalDay.cs`
- Create: `microservices/AIAgentServer/Services/Portal/PortalToolResult.cs`
- Test: `microservices/AIAgentServer.Tests/PortalDayTests.cs`
- Test: `microservices/AIAgentServer.Tests/PortalToolResultTests.cs`

**Interfaces:**
- Produces:
  - `PortalDay.StartUtc(string koreanDate) -> DateTime` · `PortalDay.EndUtc(string koreanDate) -> DateTime`
  - `PortalDay.ToKorean(DateTime utc) -> string` (`"2026-10-17 14:30"`)
  - `PortalToolResult.Render(string title, IReadOnlyList<string> lines, int total, int cap) -> string`

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class PortalDayTests
{
    [Fact]
    public void 한국_날짜의_시작은_전날_UTC_15시다()
        => Assert.Equal(new DateTime(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc), PortalDay.StartUtc("2026-10-17"));

    [Fact]
    public void 한국_날짜의_끝은_그날_UTC_15시_직전이다()
        => Assert.Equal(new DateTime(2026, 10, 17, 14, 59, 59, DateTimeKind.Utc), PortalDay.EndUtc("2026-10-17"));

    [Fact]
    public void UTC_를_한국_시각_글로_바꾼다()
        => Assert.Equal("2026-10-17 00:30", PortalDay.ToKorean(new DateTime(2026, 10, 16, 15, 30, 0, DateTimeKind.Utc)));
}

public class PortalToolResultTests
{
    [Fact]
    public void 빈_결과도_영건이라고_적는다()
    {
        var s = PortalToolResult.Render("요청글", [], total: 0, cap: 30);
        Assert.Contains("0건", s, StringComparison.Ordinal);
    }

    [Fact]
    public void 잘렸으면_총_몇건_중_몇건인지_적는다()
    {
        var s = PortalToolResult.Render("요청글", Enumerable.Repeat("줄", 30).ToList(), total: 312, cap: 30);
        Assert.Contains("총 312건 중 30건", s, StringComparison.Ordinal);
    }

    [Fact]
    public void 안_잘렸으면_총_몇건만_적는다()
    {
        var s = PortalToolResult.Render("요청글", Enumerable.Repeat("줄", 4).ToList(), total: 4, cap: 30);
        Assert.Contains("4건", s, StringComparison.Ordinal);
        Assert.DoesNotContain("중", s, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter "PortalDayTests|PortalToolResultTests"`
Expected: 컴파일 실패

- [ ] **Step 3: 둘을 구현한다**

`PortalDay` 는 고정 +9 시간이 아니라 `TimeZoneInfo` 로 바꾼다(한국은 서머타임이 없지만 변환 규칙을 한곳에 둔다). `docs/utc-time.md` 와 `AppTime` 을 따른다.

- [ ] **Step 4: 돌려서 통과를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter "PortalDayTests|PortalToolResultTests"`
Expected: PASS 6건

- [ ] **Step 5: 커밋**

```bash
git add microservices/AIAgentServer/Services/Portal microservices/AIAgentServer.Tests
git commit -m "업무 비서: 한국 날짜 경계와 도구 결과 다듬기(상한·잘림 표시)"
```

---

### Task 6: 도구 목록과 실행기

**Files:**
- Create: `microservices/AIAgentServer/Services/Portal/PortalToolSet.cs`
- Create: `microservices/AIAgentServer/Services/Portal/PortalToolRunner.cs`
- Create: `microservices/AIAgentServer/Services/Portal/HelpDeskGateway.cs`
- Create: `microservices/AIAgentServer/Services/Portal/ProjMngGateway.cs`
- Create: `microservices/AIAgentServer/Services/Portal/MailGateway.cs`
- Modify: `microservices/AIAgentServer/Program.cs`
- Test: `microservices/AIAgentServer.Tests/PortalToolSetTests.cs`

**Interfaces:**
- Consumes: `UserDirectory`, `PortalDay`, `PortalToolResult`, `RagStore.Search`
- Produces:
  - `PortalToolSet.Tools(bool internalEnabled) -> IReadOnlyList<McpToolInfo>` — `internalEnabled` 가 false 면 `search_documents` 하나만
  - `PortalToolRunner.CallAsync(string name, IReadOnlyDictionary<string, object?> args, RagScope? scope, CancellationToken) -> Task<string>`

도구 이름은 사양 그대로: `find_user` · `search_requests` · `get_request` · `search_schedules` · `search_todos` · `search_mails` · `search_documents`.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class PortalToolSetTests
{
    [Fact]
    public void 내부_토큰이_없으면_문서_도구_하나만_남는다()
    {
        var tools = PortalToolSet.Tools(internalEnabled: false);
        Assert.Single(tools);
        Assert.Equal("search_documents", tools[0].Name);
    }

    [Fact]
    public void 내부_토큰이_있으면_일곱_가지를_연다()
    {
        var names = PortalToolSet.Tools(internalEnabled: true).Select(t => t.Name).ToArray();
        Assert.Equal(7, names.Length);
        Assert.Contains("find_user", names);
        Assert.Contains("search_requests", names);
        Assert.Contains("search_mails", names);
    }

    [Fact]
    public void 모든_도구가_설명과_스키마를_갖는다()
    {
        foreach (var t in PortalToolSet.Tools(true))
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Description), t.Name);
            Assert.Equal(JsonValueKind.Object, t.Schema.ValueKind);
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter PortalToolSetTests`
Expected: 컴파일 실패

- [ ] **Step 3: 게이트웨이 셋을 구현한다**

각각 `HttpClient` 하나로 Task 2·3 의 `/internal/*` 를 부르고 `X-Internal-Token` 머리글을 붙인다. 주소는 `HelpDesk:Endpoint` · `ProjMng:Endpoint` · `Notification:Endpoint` 설정에서 읽고, 없으면 `http://localhost:5400` · `:5450` · `:5460`.

- [ ] **Step 4: `PortalToolSet` 과 `PortalToolRunner` 를 구현한다**

`search_schedules` 는 **헬프데스크와 프로젝트관리를 나란히 불러 합친다** — 묻는
사람은 일정이 어디 있는지 모른다. 칸 이름이 같으므로 이어 붙이고 `source` 로만 가른다.
`search_documents` 는 `EmbeddingClient.EmbedOneAsync` + `RagStore.Search(scope?.Category, query, vector, 6, scope?.DocIds)`. 날짜 인자는 `PortalDay` 로 바꾸고, 목록 결과는 `PortalToolResult.Render` 로 낸다. 모르는 이름이면 `"그런 도구가 없습니다."` 를 글로 돌려준다 — **예외를 던지지 않는다**(루프가 멈춘다).

- [ ] **Step 5: `Program.cs` 에 DI 를 더한다**

`AddHttpClient<HelpDeskGateway>` 외 둘, `AddScoped<PortalToolRunner>`, `AddScoped<UserDirectory>`.

- [ ] **Step 6: 돌려서 통과를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter PortalToolSetTests`
Expected: PASS 3건

- [ ] **Step 7: 커밋**

```bash
git add microservices/AIAgentServer
git commit -m "업무 비서: 포털 도구 목록과 실행기 — 내부 토큰이 없으면 문서 도구만 남는다"
```

---

### Task 7: 기록 표

**Files:**
- Create: `deploy/sql/ai-assistant-audit-2026-10-25.sql`
- Create: `microservices/AIAgentServer/Services/Portal/AssistantAudit.cs`
- Modify: `microservices/AIAgentServer/Data/AiUsageDbContext.cs`
- Test: `microservices/AIAgentServer.Tests/AssistantAuditTests.cs`

**Interfaces:**
- Produces:
  - 표 `scom.ai_assistant_audit` — 사양의 칸 그대로
  - `AssistantAuditRow` (EF 엔티티), `AiUsageDbContext.AssistantAudits`
  - `AssistantAudit.Build(string askedBy, string question, string? scopeCategory, IReadOnlyList<string> toolCalls, IReadOnlyList<string> sources, string answer, long elapsedMs, string? error) -> AssistantAuditRow`

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class AssistantAuditTests
{
    [Fact]
    public void 답을_통째로_담는다()
    {
        var row = AssistantAudit.Build("u1", "질문", null, [], [], new string('가', 50_000), 10, null);
        Assert.Equal(50_000, row.Answer.Length);
    }

    [Fact]
    public void 도구_호출과_근거는_JSON_배열로_담는다()
    {
        var row = AssistantAudit.Build("u1", "질문", "hanju",
            ["find_user({\"name\":\"홍길동\"})"], ["요청글 #1042"], "답", 10, null);

        Assert.Equal("hanju", row.ScopeCategory);
        Assert.StartsWith("[", row.ToolCalls, StringComparison.Ordinal);
        Assert.Contains("1042", row.Sources, StringComparison.Ordinal);
    }

    [Fact]
    public void 시각은_UTC_다()
        => Assert.Equal(DateTimeKind.Utc, AssistantAudit.Build("u1", "q", null, [], [], "a", 1, null).AskedAt.Kind);
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter AssistantAuditTests`
Expected: 컴파일 실패

- [ ] **Step 3: SQL 과 엔티티를 쓴다**

`rag-documents-2026-10-16.sql` 과 같은 모양(멱등, `IF NOT EXISTS`, `COMMENT ON`). 칸: `id` · `asked_by` · `asked_at timestamptz` · `question` · `scope_category` · `tool_calls jsonb` · `sources jsonb` · `answer` · `elapsed_ms` · `error`. 찾는 색인은 `(asked_by, asked_at desc)`.

- [ ] **Step 4: `AssistantAudit.Build` 를 구현한다**

`AskedAt` 은 `AppTime` 으로. **답을 자르지 않는다** — 무엇이 나갔는지가 기록의 전부다.

- [ ] **Step 5: 돌려서 통과를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter AssistantAuditTests`
Expected: PASS 3건

- [ ] **Step 6: 커밋**

```bash
git add deploy/sql/ai-assistant-audit-2026-10-25.sql microservices/AIAgentServer
git commit -m "업무 비서: 질문·도구·근거·답 전문을 남기는 기록 표"
```

---

### Task 8: 도구 루프와 답 스트리밍

**Files:**
- Create: `microservices/AIAgentServer/Services/Portal/AssistantAgent.cs`
- Test: `microservices/AIAgentServer.Tests/AssistantAgentTests.cs` (`AssistantPromptTests` · `AssistantLoopTests`)

**Interfaces:**
- Consumes: `PortalToolSet`, `PortalToolRunner`, `AiProviderRegistry`, `AssistantAudit`
- Produces:
  - `AssistantAgent.SystemPrompt(DateTime nowUtc, RagScope? scope) -> string`
  - `AssistantAgent.RunAsync(ModelCall call, ToolCall tool, JsonArray messages, JsonArray? toolSpec, int maxRounds, CancellationToken ct) -> Task<(string Answer, IReadOnlyList<string> Called)>`
    - `delegate Task<JsonObject?> ModelCall(JsonArray messages, JsonArray? tools, CancellationToken ct)`
    - `delegate Task<string> ToolCall(string name, IReadOnlyDictionary<string, object?> args, CancellationToken ct)`
  - `AssistantAgent.StreamAsync(IReadOnlyList<Message> messages, RagScope? scope, string askedBy, CancellationToken) -> IAsyncEnumerable<ChatStreamPart>`

**모델 호출을 대리자로 뺀다.** 루프 자체(라운드 소진 · 도구가 터졌을 때)를 가짜
모델로 시험하려는 것이다 — 그러지 않으면 Review Focus 의 「라운드 소진」을
운영에서나 확인하게 된다.

`WebChatAgent.AskAsync` 의 얼개를 그대로 쓴다 — 도구 라운드는 스트리밍이 아니고, 라운드를 다 쓰면 **도구 없이 한 번 더 불러** 정리시킨다. 다른 점 하나: 마지막 답은 `ChatStreamPart.Content` 로 흘리고, 도구를 부를 때마다 `ChatStreamPart.Info("요청글 뒤지는 중…", "tool")` 을 먼저 흘린다.

- [ ] **Step 1: 실패하는 시험을 쓴다**

```csharp
public class AssistantPromptTests
{
    [Fact]
    public void 오늘_한국_날짜를_프롬프트에_박는다()
    {
        var p = AssistantAgent.SystemPrompt(new DateTime(2026, 10, 24, 16, 0, 0, DateTimeKind.Utc), null);
        Assert.Contains("2026-10-25", p, StringComparison.Ordinal); // UTC 16시는 한국의 다음 날
    }

    [Fact]
    public void 자료가_없으면_모른다고_하라고_이른다()
    {
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, null);
        Assert.Contains("없습니다", p, StringComparison.Ordinal);
        Assert.Contains("지어내지", p, StringComparison.Ordinal);
    }

    [Fact]
    public void 고른_카테고리를_프롬프트에_적는다()
    {
        var p = AssistantAgent.SystemPrompt(DateTime.UtcNow, new RagScope("hanju", null, "한주 › 규정"));
        Assert.Contains("한주 › 규정", p, StringComparison.Ordinal);
    }
}

public class AssistantLoopTests
{
    // 늘 도구를 부르겠다고만 하는 모델. 라운드를 다 쓰게 만든다.
    private static JsonObject WantsTool(int i) => JsonNode.Parse(
        """{"tool_calls":[{"id":"c@I","function":{"name":"search_todos","arguments":"{}"}}]}"""
            .Replace("@I", i.ToString()))!.AsObject();

    private static JsonObject Says(string text) => JsonNode.Parse(
        """{"content":"@T"}""".Replace("@T", text))!.AsObject();

    [Fact]
    public async Task 라운드를_다_써도_답이_나온다()
    {
        var calls = 0;

        var (answer, called) = await AssistantAgent.RunAsync(
            // 도구를 안 줬다는 것은 마지막 정리 호출이라는 뜻이다.
            call: (msgs, tools, ct) => Task.FromResult<JsonObject?>(
                tools is null ? Says("찾은 것만 정리했습니다.") : WantsTool(++calls)),
            tool: (name, args, ct) => Task.FromResult("0건"),
            messages: [], toolSpec: [], maxRounds: 3, ct: default);

        Assert.Equal("찾은 것만 정리했습니다.", answer);
        Assert.Equal(3, called.Count);
    }

    [Fact]
    public async Task 도구를_안_부르면_그_답으로_끝낸다()
    {
        var (answer, called) = await AssistantAgent.RunAsync(
            call: (m, t, ct) => Task.FromResult<JsonObject?>(Says("없습니다.")),
            tool: (n, a, ct) => Task.FromResult(""),
            messages: [], toolSpec: [], maxRounds: 6, ct: default);

        Assert.Equal("없습니다.", answer);
        Assert.Empty(called);
    }

    [Fact]
    public async Task 도구가_터져도_루프는_멈추지_않는다()
    {
        // 세 시스템 가운데 하나가 죽어도 나머지로 답해야 한다.
        var (answer, _) = await AssistantAgent.RunAsync(
            call: (m, t, ct) => Task.FromResult<JsonObject?>(
                t is null ? Says("정리") : WantsTool(1)),
            tool: (n, a, ct) => throw new HttpRequestException("헬프데스크가 죽었다"),
            messages: [], toolSpec: [], maxRounds: 2, ct: default);

        Assert.Equal("정리", answer);
    }
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter AssistantPromptTests`
Expected: 컴파일 실패

- [ ] **Step 3: `SystemPrompt` 를 구현한다**

사양의 「프롬프트에서 반드시 말할 것」 다섯 줄을 모두 담는다: 오늘 날짜(한국) · 빈 결과는 "없습니다" · 잘린 것은 몇 건 중 몇 건 · 숫자는 그대로 옮긴다 · 근거를 밝힌다. 거기에 **"자료에 없으면 지어내지 말고 모른다고 한다"** 를 더한다.

- [ ] **Step 4: `RunAsync` 를 구현한다**

`WebChatAgent.AskAsync:81~125` 의 루프 그대로다. 다른 점 둘: 모델 호출이 대리자고,
**도구가 예외를 던지면 그 까닭을 도구 결과 글로 바꿔 넣는다**(루프를 멈추지 않는다).
도구 결과는 `Cap(result, 8000)`.

- [ ] **Step 5: `StreamAsync` 를 구현한다**

`RunAsync` 를 실제 모델 호출로 감싼다. 라운드 상한은 `Assistant:MaxToolRounds`
(기본 6, 1~10 으로 가둔다). 도구를 부를 때마다 `ChatStreamPart.Info(…, "tool")` 을
먼저 흘린다. 끝나면 `AssistantAudit.Build` 로 **한 번만** 담는다.

- [ ] **Step 6: 돌려서 통과를 확인한다**

Run: `dotnet test microservices/AIAgentServer.Tests --filter "AssistantPromptTests|AssistantLoopTests"`
Expected: PASS 6건

- [ ] **Step 7: 커밋**

```bash
git add microservices/AIAgentServer
git commit -m "업무 비서: 도구 루프와 답 스트리밍"
```

---

### Task 9: 엔드포인트

**Files:**
- Create: `microservices/AIAgentServer/Endpoints/AssistantEndpoints.cs`
- Modify: `microservices/AIAgentServer/Program.cs`

**Interfaces:**
- Produces:
  - `POST /assistant/chat` — 본문 `{ messages[], ragCategory?, ragFolderId?, sessionId? }`. SSE. `AIEndpoints.cs:680~760` 의 `StreamChat` 과 **같은 모양**(`text/event-stream`, `X-Accel-Buffering: no`, 답은 JSON 문자열 · 안내는 `{notice,kind}` 객체)
  - `GET /assistant/scopes` — 카테고리 전부(`RagLibrary.CategoriesAsync`). 이 화면은 `CanReadInChat` 으로 거르지 않는다
  - `GET /assistant/audit?from&to&askedBy&take` — 기록 조회

- [ ] **Step 1: 셋을 구현한다**

`/assistant/chat` 은 `X-User-Id` 가 없으면 401. 범위는 `RagLibrary.ResolveChatScopeAsync` 를 쓰되 권한 검사를 건너뛰는 갈래를 더한다(`EnsureCategoryAccessAsync` 를 부르지 않는다).

- [ ] **Step 2: `Program.cs` 에 `app.MapAssistantEndpoints();` 를 더한다**

- [ ] **Step 3: 빌드하고 실제로 눌러 본다**

```bash
dotnet build microservices/AIAgentServer
```

`dev.bat ai` 로 띄운 뒤 `curl -N -H 'X-User-Id: <본인>' -d '{"messages":[{"role":"user","content":"오늘 이후 할일목록 보여줘"}]}' -H 'Content-Type: application/json' http://localhost:5029/assistant/chat`
Expected: `data:` 줄이 흘러나오고 마지막에 답이 선다

- [ ] **Step 4: 커밋**

```bash
git add microservices/AIAgentServer
git commit -m "업무 비서: /assistant 엔드포인트 셋"
```

---

### Task 10: 화면

**Files:**
- Create: `web/src/Apps/JSini.Web.Admin/Components/Pages/Assistant.razor`
- Create: `web/src/Apps/JSini.Web.Admin/Components/Pages/Assistant.razor.cs`
- Create: `web/src/Apps/JSini.Web.Admin/Api/AssistantClient.cs`
- Modify: `web/src/Apps/JSini.Web.Admin/AdminModule.cs`
- Test: `web/tests/JSini.Web.Architecture.Tests/AssistantPageTests.cs`

**Interfaces:**
- Consumes: `/api/ai/assistant/*` (게이트웨이가 `/api/ai` 를 떼고 :5029 로 보낸다)
- Produces: `@page "/admin/ai/assistant"` · `[RouteKey("admin.ai.assistant")]`

- [ ] **Step 1: 실패하는 시험을 쓴다**

`AiAskLayoutTests.cs` 의 본문 대조 방식을 따른다.

```csharp
public sealed class AssistantPageTests
{
    [Fact]
    public void 화면_경로와_라우트키가_짝이다()
    {
        var razor = PageRazor();
        Assert.Contains("@page \"/admin/ai/assistant\"", razor, StringComparison.Ordinal);
        Assert.Contains("[RouteKey(\"admin.ai.assistant\")]", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void 도구_진행_안내를_그리는_자리가_있다()
        => Assert.Contains("\"tool\"", PageCode(), StringComparison.Ordinal);

    [Fact]
    public void 자료_범위_고르개가_있다()
        => Assert.Contains("assistant__scope", PageRazor(), StringComparison.Ordinal);
}
```

- [ ] **Step 2: 돌려서 실패를 확인한다**

Run: `dotnet test web/tests/JSini.Web.Architecture.Tests --filter AssistantPageTests`
Expected: FAIL — 파일 없음

- [ ] **Step 3: `AssistantClient` 를 구현한다**

`AiChatClient.StreamAsync` 의 SSE 읽는 길을 그대로 본뜬다. `AdminModule.cs:42` 의 `RagLibraryClient` 등록 옆에 `AddHttpClient<AssistantClient>`.

- [ ] **Step 4: 화면을 구현한다**

전체 폭 대화. 머리에 자료 범위 고르개(`assistant__scope`), 가운데 대화, 아래 입력. 안내 조각의 `kind` 가 `"tool"` 이면 진행 띠로, 답은 본문으로 그린다. 근거의 요청글 번호는 `/helpdesk/requests/{id}` 로 링크한다. 고정 라벨·제목·안내문은 넣지 않는다(밀도 우선).

- [ ] **Step 5: 돌려서 통과를 확인한다**

Run: `dotnet test web/tests/JSini.Web.Architecture.Tests --filter AssistantPageTests`
Expected: PASS 3건

- [ ] **Step 6: 전체 아키텍처 시험을 돌린다**

Run: `cd web && dotnet test`
Expected: 기존 533건 + 새 3건 모두 PASS

- [ ] **Step 7: 커밋**

```bash
git add web/src/Apps/JSini.Web.Admin web/tests
git commit -m "업무 비서: /admin/ai/assistant 화면"
```

---

### Task 11: 메뉴와 권한

**Files:**
- Create: `deploy/sql/ai-assistant-menu-2026-10-25.sql`

- [ ] **Step 1: 메뉴 한 줄을 넣는 SQL 을 쓴다**

`SYS_AI_ASSISTANT`, 경로 `/admin/ai/assistant`, 부모는 RAG 문서함(`SYS_RAG_DOCS`)과 같은 자리. 멱등으로 쓴다.

**역할 매기기는 SQL 로 하지 않는다** — 「임원」 역할이 권한관리에 있는지 확인한 뒤 화면에서 맨다. 없으면 만들어야 한다.

- [ ] **Step 2: 확인한다**

`/admin/ai/assistant` 를 슈퍼관리자로 열어 보고, 권한 없는 계정으로 주소를 쳐서 막히는지 본다(`docs/screen-access.md` 의 기본 차단).
Expected: 슈퍼관리자는 열리고, 다른 계정은 차단 화면 + 「차단 접근기록」에 한 줄

- [ ] **Step 3: 커밋**

```bash
git add deploy/sql/ai-assistant-menu-2026-10-25.sql
git commit -m "업무 비서: 메뉴 등록"
```

---

### Task 12: 실측

**Files:**
- Create: `docs/portal-ai-assistant.md`

- [ ] **Step 1: 질문 25개로 잰다**

일정 5 · 과거 5 · 문서 5 · 섞인 것 5 · **자료에 없는 것 5**. 사양의 「재는 것」 네 가지를 센다: 맞음 · 빠뜨림 · 지어냄 · 시간.

- [ ] **Step 2: 숫자를 문서에 적는다**

`docs/portal-ai-assistant.md` — 무엇을 왜 이렇게 지었는지, 재어 나온 숫자, 라운드 수를 어떻게 정했는지. 지어냄이 0 이 아니면 프롬프트를 고치고 다시 잰다.

- [ ] **Step 3: `CLAUDE.md` 의 AIAgentServer 자리에 한 단락을 더하고 커밋**

```bash
git add docs/portal-ai-assistant.md CLAUDE.md
git commit -m "업무 비서: 실측 결과와 문서"
```
