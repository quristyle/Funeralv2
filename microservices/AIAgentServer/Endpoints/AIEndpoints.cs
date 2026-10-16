using System.Text.Json;
using AIAgentServer.Services;
using AIAgentServer.Services.Mcp;
using AIAgentServer.Services.Rag;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentServer.Endpoints;

public static class AIEndpoints
{
    /// <summary>
    /// 공급자를 헤더로도 받는다. 본문·쿼리에 넣을 자리가 없는 호출을 위한 뒷문이다.
    /// </summary>
    private const string ProviderHeader = "X-AI-Provider";

    /// <summary>
    /// 이번에 사람이 물어본 말. <b>맨 마지막 <c>user</c> 줄</b>이다.
    /// </summary>
    /// <remarks>
    /// 화면은 지난 대화를 전부 실어 보낸다(문맥). 그 전체를 담으면 열 때마다
    /// 같은 말이 겹겹이 쌓이므로 <b>새로 더해진 한 줄만</b> 골라낸다 —
    /// 그 앞의 것들은 이미 지난 턴에 담겼다.
    /// </remarks>
    private static string LastUserMessage(List<AIAgentServer.DTOs.Message>? messages) =>
        messages?.LastOrDefault(m =>
            string.Equals(m.role, "user", StringComparison.OrdinalIgnoreCase))?.content
        ?? string.Empty;

    public static void MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        // Gateway 설정에서 PathRemovePrefix: "/api/ai" 가 설정되어 있으므로,
        // 클라이언트가 /api/ai/suggest-code 호출 시, 이 백엔드로는 /suggest-code 가 전달됩니다.
        var group = app.MapGroup("/").WithTags("AI Agent Services");

        // ── 고를 수 있는 AI 목록 ─────────────────────────────────
        //
        // 화면의 선택 목록을 여기서 받아 간다. 프론트에 이름을 박아 두면 설정에서
        // 공급자를 더하거나 뺄 때 두 곳을 고쳐야 하고, 실제로는 설정돼 있지도 않은
        // 공급자가 목록에 남는다.
        //
        // **키는 절대 내보내지 않는다.** 설정이 됐는지 여부(`configured`)만 알려 준다.
        // ── 웹을 찾아 가며 답하기 (MCP 도구) ──────────────────────
        //
        // 모델이 **스스로 판단해** 웹 검색을 부르고, 결과를 읽고, 모자라면 그
        // 쪽을 열어 본 뒤 답한다. 답에는 참고한 주소가 붙는다.
        //
        // **기존 /chat 과 길을 나눠 두었다.** 거기에는 공급자 자동전환·사용량
        // 기록이 얽혀 있어 도구 루프를 끼우면 잘 도는 길을 위험하게 한다.
        // 도구가 필요한 질문만 이리로 보낸다.

        group.MapGet("/mcp/tools", async (
            [FromServices] McpToolGateway mcp,
            CancellationToken ct) =>
        {
            var tools = await mcp.ListAsync(ct);
            return Results.Ok(ApiResponse<object>.Ok(new
            {
                connected = tools.Count > 0,
                tools = tools.Select(t => new { t.Name, t.Description }),
            }));
        })
        .WithName("ListMcpTools")
        .WithSummary("MCP 도구 서버가 주는 도구 목록");

        group.MapPost("/chat/web", async (
            [FromBody] WebAskRequest body,
            [FromServices] WebChatAgent agent,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Question))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("질문이 없습니다.", "C400"));
            }

            try
            {
                var r = await agent.AskAsync(body.Question, body.Provider, ct);
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    answer = r.Answer,
                    sources = r.Sources.Select(s => new { s.Title, s.Url }),
                    toolCalls = r.ToolCalls,
                    rounds = r.Rounds,
                }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail($"답하지 못했습니다 — {ex.Message}", "C500"));
            }
        })
        .WithName("WebChat")
        .WithSummary("웹을 찾아 가며 답한다 (출처 포함)");

        // ── RAG — 문서 검색 ──────────────────────────────────────
        //
        // **아직 대화에 끼워 넣지 않는다.** 검색 품질을 먼저 눈으로 보고 고치는
        // 단계다. 좋지 않은 자료를 프롬프트에 넣으면 모델이 그걸 근거로 틀린
        // 말을 지어내므로, 붙이기 전에 "제대로 찾는가" 부터 확인해야 한다.

        group.MapGet("/rag/status", ([FromServices] RagStore store) =>
            Results.Ok(ApiResponse<object>.Ok(new
            {
                chunks = store.Count,
                model = store.Model,
                builtAt = store.BuiltAt,
                indexPath = store.IndexPath,
            })))
        .WithName("GetRagStatus")
        .WithSummary("RAG 색인 상태");

        // 색인 다시 만들기. 문서를 전부 읽어 임베딩하므로 몇 분 걸릴 수 있다.
        group.MapPost("/rag/reindex", async (
            [FromServices] RagIndexer indexer,
            CancellationToken ct) =>
        {
            try
            {
                var index = await indexer.BuildAsync(ct);
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    chunks = index.Chunks.Count,
                    dimensions = index.Dimensions,
                    model = index.Model,
                    builtAt = index.BuiltAt,
                }));
            }
            catch (Exception ex)
            {
                return Results.Ok(ApiResponse<object>.Fail($"색인을 만들지 못했습니다 — {ex.Message}", "C500"));
            }
        })
        .WithName("RebuildRagIndex")
        .WithSummary("RAG 색인 다시 만들기");

        group.MapGet("/rag/search", async (
            [FromQuery] string q,
            [FromQuery] int? topK,
            [FromQuery] string? category,
            [FromServices] RagStore store,
            [FromServices] EmbeddingClient embed,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("찾을 말을 주세요.", "C400"));
            }

            // 임베딩 서버가 죽어도 낱말 검색으로는 답한다.
            float[]? vector = null;
            string? note = null;
            try
            {
                vector = await embed.EmbedOneAsync(q, ct);
            }
            catch (Exception ex)
            {
                note = $"임베딩 서버에 닿지 못해 낱말 검색만 했습니다 — {ex.Message}";
            }

            var hits = store.Search(
                string.IsNullOrWhiteSpace(category) ? null : category,
                q, vector, topK is > 0 and <= 20 ? topK.Value : 5);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                query = q,
                category,
                note,
                vectorUsed = vector is not null,
                hits = hits.Select(h => new
                {
                    source = h.Chunk.Source,
                    heading = h.Chunk.Heading,
                    score = Math.Round(h.Score, 5),
                    vectorScore = Math.Round(h.VectorScore, 4),
                    keywordScore = Math.Round(h.KeywordScore, 3),
                    text = h.Chunk.Text.Length > 400 ? h.Chunk.Text[..400] + "…" : h.Chunk.Text,
                }),
            }));
        })
        .WithName("SearchRag")
        .WithSummary("RAG 문서 검색 (벡터 + 낱말 혼합)");

        // ── LLM 장비 하드웨어 상태 ───────────────────────────────
        //
        // 포털의 「LLM 장비 상태」 화면이 이것을 읽는다. 장비가 내부망에 있어
        // 브라우저가 직접 못 묻고, 컨테이너에 SSH 키를 넣는 것도 피했다 —
        // **추론에 쓰는 바로 그 길(nginx + Bearer)을 그대로 탄다.**
        //
        // 장비가 꺼져 있어도 200 으로 답한다. `reachable=false` 와 까닭을 담아
        // 보내야 화면이 「왜 안 보이는지」를 말할 수 있다. 500 으로 끊으면
        // 화면에는 그냥 빈 자리만 남는다.
        group.MapGet("/llm/hardware", async (
            [FromServices] LlmHostMetricsClient client,
            CancellationToken ct) =>
        {
            var r = await client.ReadAsync(ct);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                reachable = r.Reachable,
                message = r.Message,
                endpoint = r.Endpoint,
                metrics = r.Metrics,
            }));
        })
        .WithName("GetLlmHardware")
        .WithSummary("LLM 장비의 GPU·CPU·메모리·디스크·서비스 상태");

        group.MapGet("/providers", ([FromServices] AiProviderRegistry registry) =>
        {
            var list = registry.All.Select(p =>
            {
                var usage = AiUsageTracker.Get(p.Key);

                return new
                {
                    key = p.Key,
                    displayName = p.DisplayName,
                    model = p.Model,
                    configured = p.IsConfigured,
                    isDefault = p.Key == registry.DefaultKey,
                    maxTokens = p.MaxTokens,
                    // 사용자가 이 공급자의 모델을 고를 수 있는지 (OpenRouter 만).
                    allowModelChoice = p.AllowModelChoice,
                    // 무료 모델만 쓰도록 강제하는 공급자인지 (OpenRouter 만).
                    requireFreeModel = p.RequireFreeModel,
                    timeoutSeconds = p.TimeoutSeconds,
                    connectTimeoutSeconds = registry.ConnectTimeout == Timeout.InfiniteTimeSpan
                        ? (int?)null
                        : (int)registry.ConnectTimeout.TotalSeconds,
                    // 우리 쪽 하루 상한을 켜 둔 경우에만 뜻이 있다.
                    maxRequestsPerDay = p.MaxRequestsPerDay,
                    usedToday = AiProviderRegistry.UsedToday(p.Key),

                    // [한도에 걸렸을 때 대신 쓸 모델들]
                    // 비어 있으면 바꿔치기를 하지 않는다는 뜻이다 — 화면이
                    // "자동 전환 켜짐/꺼짐" 을 그대로 보여 줄 수 있어야 한다.
                    fallbackModels = p.FallbackModels,
                    maxModelAttempts = p.MaxModelAttempts,

                    // [사용량]
                    // 공급자가 응답 헤더로 알려 준 마지막 값이다. 한 번도 부르지 않았으면
                    // null 이다 — 사용량을 알려고 따로 찔러 보지 않는다(그 호출이 한도를 깎는다).
                    usage = usage is null ? null : new
                    {
                        callsOk = usage.CallsOk,
                        callsFailed = usage.CallsFailed,
                        lastCallAt = usage.LastCallAt,
                        lastLatencyMs = usage.LastLatencyMs,
                        limitRequests = usage.LimitRequests,
                        remainingRequests = usage.RemainingRequests,
                        limitTokens = usage.LimitTokens,
                        remainingTokens = usage.RemainingTokens,
                        resetRequests = usage.ResetRequests,
                        resetTokens = usage.ResetTokens,
                        // 이 숫자가 '언제 기준' 인지. 없으면 한도 헤더를 준 적이 없는 공급자다.
                        observedAt = usage.ObservedAt,
                    },
                };
            });

            var failover = AiUsageTracker.LastFailover;
            var substitution = AiUsageTracker.LastModelSubstitution;
            var rotation = AiUsageTracker.LastModelRotation;
            var resting = AiModelCooldown.Snapshot();

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                defaultProvider = registry.DefaultKey,
                // 고른 모델이 무료가 아니어서 기본 모델로 바꿔 부른 마지막 건.
                // OpenRouter 가 무료 목록을 바꾸면 여기 뜬다 — 환경설정의 목록을
                // 손봐야 한다는 신호다.
                lastModelSubstitution = substitution is null ? null : new
                {
                    provider = substitution.ProviderKey,
                    from = substitution.From,
                    to = substitution.To,
                    reason = substitution.Reason,
                    at = substitution.At,
                    count = substitution.Count,
                },
                // 한도에 걸려 다른 무료 모델로 바꿔 부른 마지막 건.
                //
                // 위 `lastModelSubstitution`(무료가 아니어서 바꿈)과 **다른 칸이다.**
                // 저쪽은 설정 목록을 손봐야 한다는 신호이고, 이쪽은 시간이 지나면
                // 풀리는 혼잡이다. 한 칸에 뭉개면 무엇을 해야 할지 알 수 없다.
                lastModelRotation = rotation is null ? null : new
                {
                    provider = rotation.ProviderKey,
                    from = rotation.From,
                    to = rotation.To,
                    reason = rotation.Reason,
                    at = rotation.At,
                    count = rotation.Count,
                },

                // [지금 쉬는 모델]
                //
                // 한도에 걸린 모델은 잠시 건너뛴다. 사용자가 고른 모델이 여기 있으면
                // **고른 것과 다른 모델이 답하고 있다는 뜻**이라 반드시 보여야 한다.
                restingModels = resting.Select(r => new
                {
                    provider = r.ProviderKey,
                    model = r.Model,
                    until = r.Until,
                    reason = r.Reason,
                }),

                // 자동 전환이 켜져 있는지. 화면이 "전환 안 됨" 과 "전환 꺼짐" 을 구분한다.
                failoverEnabled = registry.FailoverOnConnectFailure,
                // 마지막 자동 전환. 전환이 조용히 일어나면 안 된다 —
                // 관리자는 로컬 장비가 꺼져 있다는 사실을 알아야 한다.
                lastFailover = failover is null ? null : new
                {
                    from = failover.From,
                    to = failover.To,
                    at = failover.At,
                    count = failover.Count,
                },
                providers = list,
            }));
        })
        .WithName("ListAiProviders");

        // ── 확인된 무료 모델 목록 ────────────────────────────────
        //
        // OpenRouter 는 무료 모델을 수시로 바꾼다. 환경설정의 선택 목록은 고정이라
        // 시간이 지나면 어긋나는데, **어긋난 것을 사람이 알아야** 고칠 수 있다.
        // 이 경로가 지금 실제로 무료인 목록을 알려 준다.
        //
        // 여기서 부르는 것은 카탈로그(가격표)뿐이라 **AI 사용 한도를 쓰지 않는다.**
        group.MapGet("/models", async (
            [FromQuery] string? provider,
            [FromServices] AiProviderRegistry registry,
            [FromServices] FreeModelGuard guard) =>
        {
            var target = registry.Resolve(provider);

            if (!target.RequireFreeModel || string.IsNullOrWhiteSpace(target.ModelCatalogUrl))
            {
                // 모델을 고를 수 없는 공급자다. 설정된 것 하나만 알려 준다.
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    provider = target.Key,
                    allowModelChoice = target.AllowModelChoice,
                    freeOnly = false,
                    models = string.IsNullOrWhiteSpace(target.Model)
                        ? Array.Empty<string>()
                        : new[] { target.Model },
                }));
            }

            var free = await guard.GetFreeModelsAsync(target.ModelCatalogUrl);

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                provider = target.Key,
                allowModelChoice = target.AllowModelChoice,
                freeOnly = true,
                // 목록을 못 받았으면 null 이다. 빈 목록('무료 모델이 없다')과 구분해야 한다.
                available = free is not null,
                currentModel = target.Model,
                // 지금 설정된 기본 모델이 여전히 무료 목록에 있는지.
                currentModelIsFree = free?.Contains(target.Model) ?? false,
                models = free?.OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToArray()
                    ?? Array.Empty<string>(),
            }));
        })
        .WithName("ListFreeModels");

        // ── 정밀 확인 (생성까지 되는지) ──────────────────────────
        //
        // `/health` 의 자동 점검은 **접속과 모델 목록까지만** 본다. 그것으로 '장비 꺼짐' 과
        // '모델 미로딩' 은 잡히지만, 실제로 토큰을 만들어 내는지는 알 수 없다.
        //
        // 생성까지 확인하려면 GPU 를 쓰고, 모델이 아직 메모리에 없으면 수십 초가 걸린다.
        // 그래서 자동 점검에 넣지 않고 **사람이 누를 때만** 실행한다
        // (상태 화면의 '정밀 확인' 버튼).
        //
        // 확인이 끝나면 자동 점검의 캐시를 버린다 — 방금 알아낸 사실이 있는데
        // 화면이 30초 전 값을 계속 보여 주면 이상하다.
        //
        // `?provider=groq` 로 **어느 공급자를 확인할지 고를 수 있다.** 로컬 LLM 이 꺼져
        // 있을 때 Groq 는 멀쩡한지 따로 확인해야 하기 때문이다.
        group.MapPost("/health/deep", async (
            HttpContext context,
            [FromQuery] string? provider,
            [FromQuery] string? model,
            [FromServices] ILLMService llmService,
            [FromServices] AiProviderRegistry registry) =>
        {
            var requested = provider ?? ReadProviderHeader(context);
            var target = registry.Resolve(requested);
            var started = DateTime.UtcNow;

            try
            {
                // 가장 짧은 질문을 던진다. 답의 내용은 보지 않는다 —
                // "응답이 돌아왔다" 는 사실만 확인하는 것이 목적이다.
                //
                // **자동 전환을 끈다(allowFailover: false).** 이 버튼은 "이 공급자가
                // 되는가" 를 보는 진단이다. 다른 공급자가 대신 답해 버리면
                // 꺼져 있는 장비를 '정상' 이라고 보고하게 된다.
                var answer = await llmService.ChatAsync(
                    new List<AIAgentServer.DTOs.Message>
                    {
                        new() { role = "user", content = "ping" },
                    },
                    target.Key,
                    allowFailover: false,
                    model: model);

                var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                LlmHealthCheck.InvalidateCache();

                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    ok = true,
                    provider = target.Key,
                    providerName = target.DisplayName,
                    model = target.Model,
                    latencyMs = elapsed,
                    // 답이 비어 있으면 연결은 됐지만 생성이 안 된 것이다. 구분해서 알려 준다.
                    generated = !string.IsNullOrWhiteSpace(answer),
                    rateLimited = false,
                    message = string.IsNullOrWhiteSpace(answer)
                        ? $"{target.DisplayName} 이 응답했지만 생성된 내용이 없습니다."
                        : $"{target.DisplayName} 이 정상 응답했습니다. ({elapsed}ms)",
                }));
            }
            catch (Exception ex)
            {
                var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                LlmHealthCheck.InvalidateCache();

                // 한도 초과는 고장이 아니다. 화면이 다른 색으로 보여 줄 수 있게 구분해 준다.
                var rateLimited = ex is AiProviderException { IsRateLimited: true };

                // 실패도 200 으로 돌려준다 — 화면이 결과를 읽어 보여 주어야 하고,
                // '점검이 실패했다' 는 것 자체가 정상적인 응답이다.
                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    ok = false,
                    provider = target.Key,
                    providerName = target.DisplayName,
                    model = target.Model,
                    latencyMs = elapsed,
                    generated = false,
                    rateLimited,
                    message = ex is AiProviderException
                        ? ex.Message
                        : $"{target.DisplayName} 호출이 실패했습니다. ({ex.GetBaseException().Message})",
                }));
            }
        })
        .WithName("DeepCheckLlm");

        group.MapPost("/chat", async (
            HttpContext context,
            [FromBody] AIAgentServer.DTOs.ChatRequestDto request,
            [FromServices] ILLMService llmService) =>
        {
            if (request.Messages == null || request.Messages.Count == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("메시지 내역이 없습니다.", "C400"));
            }

            var provider = request.Provider ?? ReadProviderHeader(context);

            try
            {
                var reply = await llmService.ChatAsync(
                    request.Messages, provider, allowFailover: true, model: request.Model);
                return Results.Ok(ApiResponse<string>.Ok(reply));
            }
            catch (AiProviderException ex)
            {
                return AiFailure(ex);
            }
        })
        .WithName("GeneralChat");

        group.MapPost("/chat/stream", async (
            HttpContext context,
            [FromBody] AIAgentServer.DTOs.ChatRequestDto request,
            [FromServices] ILLMService llmService,
            [FromServices] AiChatStore chatStore) =>
        {
            if (request.Messages == null || request.Messages.Count == 0)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("메시지 내역이 없습니다.", "C400"));
                return;
            }

            var provider = request.Provider ?? ReadProviderHeader(context);

            context.Response.Headers.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no"; // Nginx 등의 리버스 프록시 버퍼링 방지

            // Kestrel 및 YARP ApiGateway의 응답 버퍼링 완전 비활성화
            var responseBodyFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            responseBodyFeature?.DisableBuffering();

            // 응답 스트림이 즉시 시작됨을 브라우저에 알림
            await context.Response.Body.FlushAsync();

            // 스트리밍은 헤더를 이미 보냈으므로 상태 코드로 실패를 알릴 수 없다.
            // **오류도 본문에 흘려 보낸다** — 화면은 받은 조각을 그대로 이어 붙이므로
            // 사용자는 답 자리에서 이유를 읽게 된다. 예전에는 "⚠️ 오류가 발생했습니다."
            // 한 줄만 나와서 무엇을 고쳐야 하는지 알 수 없었다.
            // [오간 말을 서버가 담는다]
            //
            // 화면이 답을 다 받은 뒤 따로 올리는 길도 있었지만, 그러면
            // **창을 먼저 닫은 대화가 통째로 사라진다** — 답은 끝까지
            // 흘러갔는데 올리는 쪽이 없어서다. 여기서는 흘려보낸 글자를
            // 이미 손에 들고 있다.
            //
            // 안내(notice)는 **담지 않는다.** 답이 아니고, 담아 두면 다음에
            // 그 대화를 열었을 때 안내가 AI 가 한 말로 되살아난다.
            var answer = new System.Text.StringBuilder();

            try
            {
                await foreach (var part in llmService.StreamChatAsync(
                    request.Messages, provider, request.Model))
                {
                    if (part.Notice is null && part.Text is { Length: > 0 } saved)
                    {
                        answer.Append(saved);
                    }

                    // [답과 안내를 다른 모양으로 보낸다]
                    //
                    // 답 글자는 예전처럼 **JSON 문자열**로 보낸다(`data: "글자"`).
                    // 안내는 **객체**로 보낸다(`data: {"notice":"…","kind":"…"}`).
                    //
                    // 화면이 모양으로 갈라서, 안내는 말풍선 밖에 보여 주고
                    // **대화 기록에는 넣지 않는다.** 예전에는 안내도 답 글자와 똑같이
                    // 나가서 기록에 쌓이고 다음 턴 문맥으로 다시 올라갔다.
                    var payload = part.Notice is null
                        ? JsonSerializer.Serialize(part.Text ?? string.Empty)
                        : JsonSerializer.Serialize(new { notice = part.Notice, kind = part.Kind });

                    await context.Response.WriteAsync($"data: {payload}\n\n");
                    await context.Response.Body.FlushAsync();
                }
            }
            catch (AiProviderException ex)
            {
                var prefix = ex.IsRateLimited ? "⏳ " : "⚠️ ";
                await context.Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(prefix + ex.Message)}\n\n");
                await context.Response.Body.FlushAsync();
            }

            // **끝에서 한 번만 담는다.** 조각마다 담으면 한 턴에 DB 왕복이
            // 수십 번이고, 어차피 사람이 보는 것은 다 이어 붙인 한 덩어리다.
            //
            // 실패해서 한 글자도 못 받았으면 아무것도 안 담긴다 — 답 없는
            // 질문을 쌓아 두지 않는다(AiChatStore.RecordTurn). 그 실패는
            // 사용량 기록이 따로 남긴다.
            if (!string.IsNullOrWhiteSpace(request.SessionId)
                && context.Request.Headers["X-User-Id"].ToString() is { Length: > 0 } owner)
            {
                chatStore.RecordTurn(
                    owner, request.SessionId, LastUserMessage(request.Messages), answer.ToString());
            }
        })
        .WithName("StreamChat");

        group.MapGet("/suggest-code", async (
            HttpContext context,
            [FromQuery] string word,
            [FromQuery] bool? natural,
            [FromQuery] string? provider,
            [FromQuery] string? model,
            [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("입력값이 없습니다.", "C400"));
            }

            try
            {
                var suggestedCode = await llmService.SuggestCommonCodeAsync(
                    word, natural ?? false, provider ?? ReadProviderHeader(context), model);
                return Results.Ok(ApiResponse<string>.Ok(suggestedCode));
            }
            catch (AiProviderException ex)
            {
                return AiFailure(ex);
            }
        })
        .WithName("SuggestCommonCode");

        group.MapGet("/suggest-i18n", async (
            HttpContext context,
            [FromQuery] string key,
            [FromQuery] string targetLang,
            [FromQuery] string? provider,
            [FromQuery] string? model,
            [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(targetLang))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("필수 파라미터가 누락되었습니다.", "C400"));
            }

            try
            {
                var suggested = await llmService.SuggestI18nTranslationAsync(
                    key, targetLang, provider ?? ReadProviderHeader(context), model);
                return Results.Ok(ApiResponse<string>.Ok(suggested));
            }
            catch (AiProviderException ex)
            {
                return AiFailure(ex);
            }
        })
        .WithName("SuggestI18nTranslation");

        // ── 글 요약 ──────────────────────────────────────────────
        //
        // 포털 「메일 보내기」가 쓴 본문을 추려 머리말로 넣는다.
        //
        // **GET 이 아니라 POST 다.** 위의 두 추천은 낱말 하나를 받아 쿼리로
        // 보내지만, 요약이 받는 것은 메일 본문 전체(수 KB~수십 KB)라 URL 에
        // 실을 수 없다 — 실으면 게이트웨이와 서버의 URL 길이 상한에 걸려
        // 414 가 나고, 그 오류는 「AI 가 안 된다」로 읽힌다.
        group.MapPost("/summarize", async (
            HttpContext context,
            [FromBody] SummarizeRequest request,
            [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("요약할 내용이 없습니다.", "C400"));
            }

            try
            {
                var summary = await llmService.SummarizeAsync(
                    request.Text, request.MaxLines ?? 3,
                    request.Provider ?? ReadProviderHeader(context), request.Model);

                return Results.Ok(ApiResponse<string>.Ok(summary));
            }
            catch (AiProviderException ex)
            {
                return AiFailure(ex);
            }
        })
        .WithName("SummarizeText");

        // ── 줄 번역 ──────────────────────────────────────────────
        //
        // 「AI 작업」 상세(`/projmng/ai/task/{번호}`)의 로그 탭이 원문과 번역을
        // 좌우로 견주는 데 쓴다.
        //
        // **요약과 모양이 다르다.** 요약은 글 한 덩이를 받아 글 한 덩이를
        // 돌려주지만, 여기는 **줄 목록을 받아 같은 개수의 줄 목록**을 돌려준다 —
        // 보는 쪽이 Monaco 의 좌우 비교 편집기라 왼쪽 n번째와 오른쪽 n번째가
        // 짝이어야 읽히기 때문이다.
        //
        // **끊어 보내는 것은 부르는 쪽 몫이다.** 한 번에 수백 줄을 올리면
        // 모델의 답 상한(`MaxTokens`)에 걸려 **뒤가 잘린 채로 돌아오고**,
        // 그러면 사람은 한참 기다린 끝에 절반만 받는다. 화면은 스무 줄씩
        // 끊어 보내 오는 대로 오른쪽을 채운다.
        group.MapPost("/translate", async (
            HttpContext context,
            [FromBody] TranslateRequest request,
            [FromServices] ILLMService llmService) =>
        {
            var lines = request.Lines;

            if (lines is null || lines.Count == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("번역할 줄이 없습니다.", "C400"));
            }

            if (lines.Count > LLMService.MaxTranslateLines)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(
                    $"한 번에 {LLMService.MaxTranslateLines}줄까지 보낼 수 있습니다. 끊어서 보내십시오.",
                    "C400"));
            }

            if (lines.Sum(line => line?.Length ?? 0) > LLMService.MaxTranslateInput)
            {
                return Results.BadRequest(ApiResponse<object>.Fail(
                    $"한 번에 {LLMService.MaxTranslateInput}자까지 보낼 수 있습니다. 끊어서 보내십시오.",
                    "C400"));
            }

            try
            {
                var answer = await llmService.TranslateAsync(
                    [.. lines.Select(line => line ?? string.Empty)],
                    request.TargetLang ?? "ko",
                    request.Provider ?? ReadProviderHeader(context),
                    request.Model);

                return Results.Ok(ApiResponse<object>.Ok(new
                {
                    lines = answer.Lines,
                    provider = answer.ProviderKey,
                    providerName = answer.ProviderName,
                    model = answer.Model,
                    translated = answer.Translated,
                }));
            }
            catch (AiProviderException ex)
            {
                return AiFailure(ex);
            }
        })
        .WithName("TranslateLines");
    }

    /// <summary>
    /// 줄 번역 요청. <b>글 한 덩이가 아니라 줄 목록으로 받는다.</b>
    /// </summary>
    /// <remarks>
    /// 글로 받아 서버가 가르면 줄바꿈 표기(<c>\n</c> · <c>\r\n</c>)에 따라
    /// 줄 수가 달라진다. 그 수가 곧 화면의 좌우 짝이라, <b>한 줄이 어긋나면
    /// 그 아래가 통째로 밀린다.</b> 가르는 일을 보는 쪽에 맡긴다.
    /// </remarks>
    public sealed class TranslateRequest
    {
        /// <summary>옮길 줄들. 차례와 개수가 그대로 돌아온다.</summary>
        public List<string?>? Lines { get; set; }

        /// <summary>옮겨 갈 말(<c>ko</c> · <c>en</c> · <c>ja</c> · <c>zh</c>). 비우면 한국어.</summary>
        public string? TargetLang { get; set; }

        /// <summary>
        /// 쓸 공급자. 화면은 <c>jsini</c>(로컬 LLM)를 적어 보낸다 —
        /// <b>로그 전부를 바깥으로 내보내지 않으려는 것</b>이고, 그 장비가
        /// 꺼져 있으면 서버가 스스로 다음 공급자로 넘긴다.
        /// </summary>
        public string? Provider { get; set; }

        public string? Model { get; set; }
    }

    /// <summary>
    /// 요약 요청. <b>본문으로 받는다</b> — 글이 길어 쿼리에 실을 수 없다.
    /// </summary>
    public sealed class SummarizeRequest
    {
        /// <summary>요약할 원문. HTML 이면 부르는 쪽이 태그를 걷어 보낸다.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>몇 줄까지 받을지. 비우면 세 줄.</summary>
        public int? MaxLines { get; set; }

        public string? Provider { get; set; }

        public string? Model { get; set; }
    }

    private static string? ReadProviderHeader(HttpContext context)
    {
        var value = context.Request.Headers[ProviderHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// AI 호출 실패를 화면이 읽을 수 있는 모양으로 바꾼다.
    /// </summary>
    /// <remarks>
    /// <b>한도 초과는 429 로 그대로 올린다.</b> 500 으로 뭉개면 화면이 '서버 오류' 로 보여
    /// 사람이 관리자에게 연락하게 된다. 실제로 필요한 것은 "잠시 뒤에 다시 하거나
    /// 다른 모델을 고르라" 는 안내다.
    /// </remarks>
    private static IResult AiFailure(AiProviderException ex)
    {
        if (ex.IsRateLimited)
        {
            return Results.Json(
                ApiResponse<object>.Fail(ex.Message, "C429"),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return Results.Json(
            ApiResponse<object>.Fail(ex.Message, "C502"),
            statusCode: StatusCodes.Status502BadGateway);
    }
}

/// <summary>웹 검색 대화 요청.</summary>
public sealed record WebAskRequest(string Question, string? Provider);
