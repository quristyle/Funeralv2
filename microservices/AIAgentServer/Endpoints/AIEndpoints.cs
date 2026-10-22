using System.Text.Json;
using AIAgentServer.Services;
using AIAgentServer.DTOs;
using AIAgentServer.Services.Mcp;
using AIAgentServer.Services.Rag;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentServer.Endpoints;

public static class AIEndpoints
{
    /// <summary>헤더로도 공급자를 받는다. 본문·쿼리에 넣을 자리가 없는 호출을 위한 뒷문이다.</summary>
    private const string ProviderHeader = "X-AI-Provider";

    /// <summary>이번에 물어본 말(맨 마지막 user 줄). 지난 대화는 이전 턴에 담겼으므로 새로 더해진 한 줄만 고른다.</summary>
    private static string LastUserMessage(List<AIAgentServer.DTOs.Message>? messages) =>
        messages?.LastOrDefault(m =>
            string.Equals(m.role, "user", StringComparison.OrdinalIgnoreCase))?.content
        ?? string.Empty;

    /// <summary>JSON 글자 칸 하나를 꺼낸다. 없거나 글자가 아니면 null. 칸 이름의 대소문자는 직렬화 설정이 정하므로 둘 다 본다.</summary>
    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>채팅창에서 고른 사내 자료 범위를 푼다. 안 골랐으면 (null, null), 못 쓰는 범위면 (null, 까닭).</summary>
    private static async Task<(RagScope? Scope, string? Error)> ResolveRagScopeAsync(
        AIAgentServer.DTOs.ChatRequestDto request, UserContext? user, RagLibrary lib, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RagCategory)) return (null, null);

        try
        {
            return (await lib.ResolveChatScopeAsync(request.RagCategory.Trim(), request.RagFolderId, user, ct), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return (null, ex.Message);
        }
    }

    public static void MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        // 게이트웨이가 /api/ai 를 떼고 넘기므로 여기 경로에는 붙지 않는다.
        var group = app.MapGroup("/").WithTags("AI Agent Services");

        // 고를 수 있는 AI 목록. 프론트에 이름을 박으면 설정과 어긋난다. 키는 내보내지 않고 configured 만 알려 준다.
        // 웹을 찾아 가며 답하기(MCP 도구). 기존 /chat 과 길을 나눴다 — 자동 전환·사용량 기록이 얽힌 길에 도구 루프를 끼우면 위험하다.

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

        // RAG 문서 검색. 아직 대화에 끼워 넣지 않는다 — 나쁜 자료가 들어가면 모델이 그걸 근거로 틀린 말을 지어내므로, 검색 품질을 먼저 확인한다.

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

        // LLM 장비 하드웨어 상태. 추론과 같은 길(nginx + Bearer)을 타며, 장비가 꺼져 있어도 200 으로 reachable=false 와 까닭을 보낸다.
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

        // 사내 문서(RAG)·웹 검색(MCP) 상태. 설정·색인·확인(Probe 가 2분마다 재는 실제 호출)을 섞어 담는다.
        // 설정만으로는 고장이 안 보인다 — 색인은 디스크에 남고 대화는 낱말 검색으로 돈다. 장비가 꺼져도 200 이다.
        group.MapGet("/features/status", async (
            [FromQuery] bool? recheck,
            [FromServices] RagStore store,
            [FromServices] RagLibrary library,
            [FromServices] RagIndexer indexer,
            [FromServices] McpToolGateway mcp,
            [FromServices] AiFeatureProbe probe,
            [FromServices] IConfiguration config,
            CancellationToken ct) =>
        {
            var force = recheck == true;

            // 눌러 보는 것들은 병렬로 돈다 — 임베딩이 20초를 끌어도 MCP 쪽 답은 그대로 나온다.
            var embedTask = probe.EmbeddingAsync(force, ct);
            var mcpTask = probe.McpServerAsync(force, ct);
            var sttTask = probe.SpeechAsync(force, ct);
            var toolsTask = mcp.ListAsync(ct);

            // 문서 수(DB)와 토막 수(메모리)가 어긋나면 「올려만 놓고 색인을 안 돌린」 상태다.
            var cats = await library.CategoriesAsync(ct);
            var docStats = (await library.DocumentStatsAsync(ct))
                .ToDictionary(x => x.CategoryId, StringComparer.Ordinal);

            await Task.WhenAll(embedTask, mcpTask, sttTask, toolsTask);
            var embed = await embedTask;
            var mcpServer = await mcpTask;
            var stt = await sttTask;
            var tools = await toolsTask;

            // 저장소 문서(docs/*.md)는 DB 에 없는 카테고리라, 색인에만 있는 것까지 합쳐야 전부를 보여 준다.
            var known = cats.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            var rows = cats
                .Select(c => (Id: c.Id, Name: c.Name, FromDb: true))
                .Concat(store.Categories
                    .Where(k => !known.Contains(k))
                    .Select(k => (Id: k, Name: k == indexer.Category ? "저장소 문서" : k, FromDb: false)))
                .OrderBy(x => x.Name, StringComparer.CurrentCulture)
                .Select(x =>
                {
                    var stat = store.StatOf(x.Id);
                    var docs = docStats.GetValueOrDefault(x.Id);

                    return new
                    {
                        id = x.Id,
                        name = x.Name,
                        fromLibrary = x.FromDb,
                        indexed = stat is not null,
                        chunks = stat?.Chunks ?? 0,
                        // 토막은 있는데 벡터가 없으면 그 카테고리는 낱말 검색만 된다.
                        vectorChunks = stat?.VectorChunks ?? 0,
                        sources = stat?.Sources ?? 0,
                        dimensions = stat?.Dimensions ?? 0,
                        model = stat?.Model,
                        builtAt = stat?.BuiltAt,
                        indexBytes = stat?.FileBytes,
                        documents = docs?.Total ?? 0,
                        documentsIndexed = docs?.Indexed ?? 0,
                        documentsPending = docs?.Pending ?? 0,
                        documentsFailed = docs?.Failed ?? 0,
                        documentsSkipped = docs?.Skipped ?? 0,
                        documentBytes = docs?.Bytes ?? 0,
                    };
                })
                .ToList();

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                rag = new
                {
                    // 끄면 검색 엔드포인트만 남고 대화에는 자료가 안 붙는다.
                    augmentChat = config.GetValue("Rag:AugmentChat", true),
                    topK = config.GetValue("Rag:TopK", 4),
                    minVectorScore = config.GetValue("Rag:MinVectorScore", 0.40),
                    // 비어 있으면 모든 카테고리를 함께 본다.
                    chatCategory = config["Rag:ChatCategory"],
                    repoCategory = indexer.Category,
                    indexPath = store.IndexPath,
                    uploadRoot = library.Root,
                    chunks = store.Count,
                    builtAt = store.BuiltAt,
                    categories = rows,
                    embedding = new
                    {
                        reachable = embed.Reachable,
                        model = embed.Model,
                        dimensions = embed.Dimensions,
                        latencyMs = embed.LatencyMs,
                        endpoint = embed.Endpoint,
                        message = embed.Message,
                        checkedAt = embed.CheckedAt,
                    },
                },
                web = new
                {
                    // 도구 자체를 끈 것인지, 대화에서만 안 쓰는 것인지는 다르다.
                    enabled = config.GetValue("Mcp:Enabled", true),
                    augmentChat = config.GetValue("Mcp:AugmentChat", false),
                    maxToolRounds = config.GetValue("Mcp:MaxToolRounds", 4),
                    skipWhenRagScore = config.GetValue("Mcp:SkipWhenRagScore", 0.55),
                    endpoint = config["Mcp:Endpoint"],
                    connected = tools.Count > 0,
                    tools = tools.Select(t => new { name = t.Name, description = t.Description }),
                    server = new
                    {
                        reachable = mcpServer.Reachable,
                        searchProvider = mcpServer.SearchProvider,
                        endpoint = mcpServer.Endpoint,
                        latencyMs = mcpServer.LatencyMs,
                        message = mcpServer.Message,
                        checkedAt = mcpServer.CheckedAt,
                    },
                },

                // 말로 묻는 길. 이것이 끊겨도 대화는 멀쩡하다(서비스 상태로는 안 갈린다). RAG·웹 검색과 같은 갈래라 여기 둔다.
                speech = new
                {
                    reachable = stt.Reachable,
                    model = stt.Model,
                    language = stt.Language,
                    latencyMs = stt.LatencyMs,
                    endpoint = stt.Endpoint,
                    message = stt.Message,
                    checkedAt = stt.CheckedAt,
                },
            }));
        })
        .WithName("GetAiFeatureStatus")
        .WithSummary("사내 문서 검색(RAG)·웹 검색(MCP)·받아쓰기(STT)의 상태");

        // 웹 검색을 실제로 한 번 돌려 본다. 기본 DuckDuckGo 는 HTML 을 긁어 구조가 바뀌면 오류 없이 0건이 되므로 상태로는 안 갈린다.
        // 바깥으로 나가는 요청이라 사람이 누를 때만 쓴다.
        group.MapPost("/features/web-check", async (
            [FromQuery] string? q,
            [FromServices] McpToolGateway mcp,
            CancellationToken ct) =>
        {
            if (!mcp.Enabled)
            {
                return Results.Ok(ApiResponse<object>.Fail("웹 도구가 꺼져 있습니다. (Mcp:Enabled)", "C400"));
            }

            var query = string.IsNullOrWhiteSpace(q) ? "장례식장 관리 시스템" : q.Trim();
            var started = System.Diagnostics.Stopwatch.StartNew();

            var text = await mcp.CallAsync(
                "web_search",
                new Dictionary<string, object?> { ["query"] = query, ["count"] = 3 },
                ct);
            started.Stop();

            // 도구는 실패해도 던지지 않고 괄호로 시작하는 글로 돌려준다(성공한 결과는 JSON).
            var failed = text.StartsWith('(');
            var hits = new List<object>();
            string? note = failed ? text : null;

            if (!failed)
            {
                // 글자 그대로 뿌리지 않는다 — 한글이 유니코드 이스케이프로 박힌 JSON 이라 고장난 것처럼 보인다.
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("results", out var arr)
                        && arr.ValueKind == JsonValueKind.Array)
                    {
                        hits.AddRange(arr.EnumerateArray().Select(x => (object)new
                        {
                            title = Str(x, "Title") ?? Str(x, "title"),
                            url = Str(x, "Url") ?? Str(x, "url"),
                        }));
                    }
                }
                catch (JsonException)
                {
                    // 모양이 바뀌었어도 받은 것은 보여 준다 — 그것이 곧 단서다.
                    note = text.Length > 500 ? text[..500] + "…" : text;
                }
            }

            return Results.Ok(ApiResponse<object>.Ok(new
            {
                query,
                // 0건도 실패로 센다 — DuckDuckGo 는 구조가 바뀌면 오류 없이 빈 목록이 와서 가장 흔한 고장이다.
                ok = !failed && hits.Count > 0,
                count = hits.Count,
                latencyMs = (int)started.ElapsedMilliseconds,
                results = hits,
                note,
            }));
        })
        .WithName("CheckWebSearch")
        .WithSummary("웹 검색을 한 번 실제로 돌려 본다");

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

                    // 한도 대체 모델들. 비어 있으면 바꿔치기 없음 — 화면이 자동 전환 켜짐/꺼짐을 구분해 보여야 한다.
                    fallbackModels = p.FallbackModels,
                    maxModelAttempts = p.MaxModelAttempts,

                    // 마지막 응답 헤더 사용량. 한 번도 안 불렀으면 null — 따로 찔러 보면 그 호출이 한도를 깎는다.
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
                // 무료가 아니어서 기본 모델로 바꿔 부른 마지막 건. 뜨면 환경설정의 무료 목록을 손볼 신호다.
                lastModelSubstitution = substitution is null ? null : new
                {
                    provider = substitution.ProviderKey,
                    from = substitution.From,
                    to = substitution.To,
                    reason = substitution.Reason,
                    at = substitution.At,
                    count = substitution.Count,
                },
                // 한도에 걸려 다른 무료 모델로 바꿔 부른 마지막 건. 무료 아님(lastModelSubstitution)과 달리 시간이 지나면 풀리는 혼잡이라 칸을 나눴다.
                lastModelRotation = rotation is null ? null : new
                {
                    provider = rotation.ProviderKey,
                    from = rotation.From,
                    to = rotation.To,
                    reason = rotation.Reason,
                    at = rotation.At,
                    count = rotation.Count,
                },

                // 지금 쉬는 모델. 고른 모델이 여기 있으면 고른 것과 다른 모델이 답하므로 반드시 보여야 한다.
                restingModels = resting.Select(r => new
                {
                    provider = r.ProviderKey,
                    model = r.Model,
                    until = r.Until,
                    reason = r.Reason,
                }),

                // 자동 전환이 켜져 있는지. 화면이 "전환 안 됨" 과 "전환 꺼짐" 을 구분한다.
                failoverEnabled = registry.FailoverOnConnectFailure,
                // 마지막 자동 전환. 조용히 일어나면 안 된다 — 관리자는 로컬 장비가 꺼졌다는 사실을 알아야 한다.
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

        // 확인된 무료 모델 목록. OpenRouter 는 무료 모델을 수시로 바꾸므로 어긋난 것을 사람이 알아야 한다. 카탈로그만 부르고 AI 사용 한도는 쓰지 않는다.
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

        // 정밀 확인(생성까지). /health 자동 점검은 접속·모델 목록까지만 보고, 생성은 GPU 를 쓰고 모델 로딩에 수십 초가 걸려 사람이 누를 때만 돈다.
        // ?provider=groq 로 공급자를 고른다. 확인 뒤에는 자동 점검 캐시를 버린다.
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
                // 가장 짧은 질문을 던져 응답이 왔는지만 본다. 자동 전환은 끈다(allowFailover: false) — 다른 공급자가 대신 답하면 꺼진 장비를 정상으로 보고한다.
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

                // 실패도 200 으로 준다 — 화면이 결과를 읽어 보여 주어야 한다.
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

        // 말소리를 글로(STT). 글을 어떻게 쓸지는 화면이 정한다(받아쓰기 · 음성 모드). 소리는 base64 로 받는다 — Blazor Server 라 브라우저가 게이트웨이를 직접 못 부른다(CORS).
        group.MapPost("/stt", async (
            [FromBody] SttRequest body,
            [FromServices] SpeechClient speech,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.AudioBase64))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("소리가 비어 있습니다.", "C400"));
            }

            byte[] audio;
            try
            {
                audio = Convert.FromBase64String(body.AudioBase64);
            }
            catch (FormatException)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("소리를 읽지 못했습니다.", "C400"));
            }

            var result = await speech.TranscribeAsync(
                audio,
                body.ContentType ?? "audio/webm",
                body.FileName ?? "utterance.webm",
                ct);

            // 실패도 200 으로 준다. 400 이면 공용 오류 처리가 먼저 삼켜 화면이 까닭을 보여 줄 수 없다.
            return Results.Ok(ApiResponse<object>.Ok(new
            {
                ok = result.Ok,
                text = result.Text,
                latencyMs = result.LatencyMs,
                message = result.Message,
            }));
        })
        .WithName("Transcribe")
        .WithSummary("녹음한 말소리를 글로 바꾼다");

        group.MapPost("/chat", async (
            HttpContext context,
            [FromBody] AIAgentServer.DTOs.ChatRequestDto request,
            UserContext? user,
            [FromServices] ILLMService llmService,
            [FromServices] RagLibrary ragLibrary,
            CancellationToken ct) =>
        {
            if (request.Messages == null || request.Messages.Count == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("메시지 내역이 없습니다.", "C400"));
            }

            var (ragScope, scopeError) = await ResolveRagScopeAsync(request, user, ragLibrary, ct);
            if (scopeError is not null)
            {
                return Results.Ok(ApiResponse<object>.Fail(scopeError, "C400"));
            }

            var provider = request.Provider ?? ReadProviderHeader(context);

            try
            {
                var reply = await llmService.ChatAsync(
                    request.Messages, provider, allowFailover: true, model: request.Model,
                    ragMode: request.RagMode, ragScope: ragScope);
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
            UserContext? user,
            [FromServices] ILLMService llmService,
            [FromServices] AiChatStore chatStore,
            [FromServices] RagLibrary ragLibrary) =>
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

            // 스트리밍은 헤더를 보냈으므로 상태 코드로 실패를 못 알린다 — 오류도 본문에 흘려 보내 사용자가 답 자리에서 이유를 읽게 한다.
            // 오간 말은 서버가 담는다. 답을 다 받은 뒤 화면이 따로 올리면 창을 먼저 닫은 대화가 사라진다. 안내(notice)는 답이 아니라 담지 않는다.
            var answer = new System.Text.StringBuilder();

            // 못 쓰는 범위를 조용히 「전체」로 넓히면 고른 사람은 그 카테고리에서 찾은 줄 안다.
            var (ragScope, scopeError) = await ResolveRagScopeAsync(
                request, user, ragLibrary, context.RequestAborted);
            if (scopeError is not null)
            {
                await context.Response.WriteAsync($"data: {JsonSerializer.Serialize("⚠️ " + scopeError)}\n\n");
                await context.Response.Body.FlushAsync();
                return;
            }

            try
            {
                await foreach (var part in llmService.StreamChatAsync(
                    request.Messages, provider, request.Model, request.RagMode, ragScope))
                {
                    if (part.Notice is null && part.Text is { Length: > 0 } saved)
                    {
                        answer.Append(saved);
                    }

                    // 답 글자는 JSON 문자열로, 안내는 객체로 보낸다 — 화면이 모양으로 갈라 안내를 대화 기록에 넣지 않게 한다.
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

            // 한 번만 담는다(조각마다 담으면 DB 왕복이 수십 번). 한 글자도 못 받은 실패는 담지 않는다(AiChatStore.RecordTurn).
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

        // 메뉴 자동 채우기. 목록이 본문이라 POST 다.
        group.MapPost("/suggest-menu", async (
            HttpContext context,
            [FromBody] MenuSuggestRequest request,
            [FromServices] ILLMService llmService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Brief) && string.IsNullOrWhiteSpace(request.Parent))
            {
                return Results.BadRequest(ApiResponse<object>.Fail("설명이 없습니다.", "C400"));
            }

            try
            {
                var suggestion = await llmService.SuggestMenuAsync(
                    request, request.Provider ?? ReadProviderHeader(context), request.Model);
                return Results.Ok(ApiResponse<MenuSuggestion>.Ok(suggestion));
            }
            catch (AiProviderException ex)
            {
                return AiFailure(ex);
            }
        })
        .WithName("SuggestMenu");

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

    /// <summary>줄 번역 요청. <b>글 한 덩이가 아니라 줄 목록으로 받는다.</b> 글로 받아 서버가 가르면 줄바꿈 표기(\n · \r\n)에 따라 줄 수가 달라진다.</summary>
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

    /// <summary>요약 요청. <b>본문으로 받는다</b> — 글이 길어 쿼리에 실을 수 없다.</summary>
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

    /// <summary>AI 호출 실패를 화면이 읽을 수 있는 모양으로 바꾼다. 한도 초과는 429 로 그대로 올린다.</summary>
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

/// <summary>받아쓰기 요청. 소리를 base64 로 싣는다.</summary>
/// <param name="ContentType">
/// 녹음의 종류. <b>브라우저마다 다르다</b> — 크롬은 <c>audio/webm;codecs=opus</c>,
/// 사파리는 <c>audio/mp4</c> 를 준다. 가리지 않고 그대로 넘긴다.
/// </param>
/// <param name="FileName">받아쓰기 서버 로그에 남을 이름. 확장자만 쓸모가 있다.</param>
public sealed record SttRequest(string AudioBase64, string? ContentType, string? FileName);
