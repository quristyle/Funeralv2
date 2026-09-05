using System.Text.Json;
using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.HelpDesk.Api;

/// <summary>셀렉트 옵션 한 칸. 값은 원본 타입을 잃지 않도록 JsonElement 로 둔다.</summary>
public sealed record BizOption(string Label, string? Value);

/// <summary>옵션과 원본 행을 함께 돌려준다 — 라벨·값 말고 다른 컬럼이 필요한 화면이 있다.</summary>
public sealed record BizOptionsResult(IReadOnlyList<JsonElement> Items, IReadOnlyList<BizOption> Options)
{
    public static readonly BizOptionsResult Empty = new([], []);
}

/// <summary>
/// 메타데이터로 셀렉트 목록을 읽어 오는 단일 통로 — Vue 의 <c>api/biz-select.ts</c>.
///
/// 설정은 전부 DB(<c>scom.biz_select_configs</c>)에 있고
/// <c>/auth/system/biz-select/configs</c> 로 받는다. 화면은 <c>bizType</c> 만 알면
/// 되고, "어느 MSA 의 어느 API 를 어떤 모양으로 부르는가" 는 여기서 정한다.
///
/// <c>serviceCode</c> 는 <b>봉투를 벗길 클라이언트를 고르는 키</b>다:
///   auth·funeral·file·ai → <see cref="GatewayClient"/> (funeralv2 봉투),
///   helpdesk → <see cref="HelpDeskApi"/> (헬프데스크 봉투).
/// projmng 은 이 앱의 화면이 쓰지 않아 지원하지 않는다(경고만 남긴다).
/// </summary>
public sealed class BizOptionService(
    GatewayClient gateway,
    HelpDeskApi helpdesk,
    ILogger<BizOptionService> logger)
{
    /// <summary>biz-select 설정 한 줄. scom.biz_select_configs 와 대응한다.</summary>
    public sealed class BizSelectConfig
    {
        public string Id { get; set; } = string.Empty;
        public string BizType { get; set; } = string.Empty;
        public string? ServiceCode { get; set; }
        public string ApiUrl { get; set; } = string.Empty;
        public string? HttpMethod { get; set; }
        public string? LabelField { get; set; }
        public string? ValueField { get; set; }
        public string? ResultPath { get; set; }
        public string? ProcessorType { get; set; }
        public string? StaticParams { get; set; }
        public string? ParamPath { get; set; }
    }

    private IReadOnlyList<BizSelectConfig>? _configs;

    /// <summary>
    /// bizType 의 목록을 읽어 원본 행과 셀렉트 옵션을 함께 돌려준다.
    /// 설정이 없으면 경고만 남기고 빈 결과를 준다 — 메타데이터를 아직 안 넣은
    /// 화면이 통째로 죽지 않게 한다.
    /// </summary>
    public async Task<BizOptionsResult> FetchOptionsAsync(
        string bizType,
        IReadOnlyDictionary<string, object?>? runtimeParams = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bizType))
        {
            return BizOptionsResult.Empty;
        }

        var config = await GetConfigAsync(bizType, ct);
        if (config is null)
        {
            logger.LogWarning("[BizSelect] 메타데이터에 없는 타입입니다: {BizType}", bizType);
            return BizOptionsResult.Empty;
        }

        var body = BuildParams(config, runtimeParams);
        List<JsonElement> items;
        try
        {
            items = await CallAsync(config, body, ct);
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "[BizSelect] {BizType} 조회 실패", bizType);
            return BizOptionsResult.Empty;
        }

        if (string.Equals(config.ProcessorType, "FLATTEN", StringComparison.OrdinalIgnoreCase))
        {
            items = FlattenTree(items);
        }

        var labelField = string.IsNullOrWhiteSpace(config.LabelField) ? "name" : config.LabelField;
        var valueField = string.IsNullOrWhiteSpace(config.ValueField) ? "id" : config.ValueField;

        var options = items
            .Select(item => new BizOption(
                GetText(item, labelField) ?? string.Empty,
                GetText(item, valueField)))
            .ToList();

        return new BizOptionsResult(items, options);
    }

    private async Task<BizSelectConfig?> GetConfigAsync(string bizType, CancellationToken ct)
    {
        // 설정은 자주 바뀌지 않아 회로(사용자) 수명 동안 캐싱한다 — Vue 의
        // useBizSelectStore 와 같은 폭이다.
        _configs ??= await gateway.GetListAsync<BizSelectConfig>("auth/system/biz-select/configs", ct);
        return _configs.FirstOrDefault(c =>
            string.Equals(c.BizType, bizType, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<JsonElement>> CallAsync(
        BizSelectConfig config,
        Dictionary<string, object?> body,
        CancellationToken ct)
    {
        var method = (config.HttpMethod ?? "GET").ToUpperInvariant();
        var service = config.ServiceCode?.Trim();

        if (string.Equals(service, "helpdesk", StringComparison.OrdinalIgnoreCase))
        {
            // HelpDeskApi 의 BaseAddress 에 /helpdesk 가 이미 붙어 있다.
            var path = config.ApiUrl.TrimStart('/');
            var data = method == "GET"
                ? await helpdesk.GetAsync<JsonElement>(path, body, ct)
                : await helpdesk.PostAsync<JsonElement>(path, body, ct);
            return Extract(data, config.ResultPath);
        }

        if (string.Equals(service, "projmng", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[BizSelect] projmng 서비스는 헬프데스크 앱에서 지원하지 않는다: {BizType}", config.BizType);
            return [];
        }

        // 포털 계열(auth·funeral·file·ai). serviceCode 가 비어 있으면 예전처럼
        // apiUrl 이 프리픽스를 이미 품고 있는 것으로 본다.
        var url = string.IsNullOrEmpty(service)
            ? config.ApiUrl.TrimStart('/')
            : $"{service}/{config.ApiUrl.TrimStart('/')}";

        if (method == "GET")
        {
            var rows = await gateway.GetListAsync<JsonElement>(HelpDeskApi.WithQuery(url, body), ct);
            return ExtractRows(rows, config.ResultPath);
        }

        var posted = await gateway.PostAsync<JsonElement>(url, body, ct);
        return Extract(posted, config.ResultPath);
    }

    /// <summary>
    /// 고정 파라미터(staticParams)와 런타임 파라미터를 합친다.
    /// paramPath 가 있으면 런타임 파라미터를 그 자리(점 표기)에 넣는다.
    /// </summary>
    private Dictionary<string, object?> BuildParams(
        BizSelectConfig config,
        IReadOnlyDictionary<string, object?>? runtime)
    {
        var body = ParseStaticParams(config.StaticParams);
        var extra = runtime ?? new Dictionary<string, object?>();

        if (string.IsNullOrWhiteSpace(config.ParamPath))
        {
            foreach (var (key, value) in extra)
            {
                body[key] = value;
            }

            return body;
        }

        var parts = config.ParamPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var cursor = body;
        foreach (var part in parts[..^1])
        {
            if (cursor.TryGetValue(part, out var next) && next is Dictionary<string, object?> nested)
            {
                cursor = nested;
            }
            else
            {
                var created = new Dictionary<string, object?>();
                cursor[part] = created;
                cursor = created;
            }
        }

        var leafKey = parts[^1];
        if (cursor.TryGetValue(leafKey, out var existing) && existing is Dictionary<string, object?> leaf)
        {
            foreach (var (key, value) in extra)
            {
                leaf[key] = value;
            }
        }
        else
        {
            cursor[leafKey] = new Dictionary<string, object?>(extra);
        }

        return body;
    }

    private Dictionary<string, object?> ParseStaticParams(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw);
            return parsed ?? [];
        }
        catch (JsonException)
        {
            logger.LogWarning("[BizSelect] static_params 가 올바른 JSON 이 아닙니다: {Raw}", raw);
            return [];
        }
    }

    // ── JSON 유틸 ────────────────────────────────────────────────

    private static List<JsonElement> Extract(JsonElement data, string? resultPath)
    {
        var target = data;
        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            foreach (var part in resultPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (target.ValueKind != JsonValueKind.Object
                    || !target.TryGetProperty(part, out target))
                {
                    return [];
                }
            }
        }

        return target.ValueKind == JsonValueKind.Array
            ? [.. target.EnumerateArray()]
            : [];
    }

    private static List<JsonElement> ExtractRows(IReadOnlyList<JsonElement> rows, string? resultPath)
    {
        // GatewayClient 가 이미 봉투의 result 배열을 벗겼다. resultPath 가
        // 'result' 면 할 일이 없고, 다른 경로면 각 행이 아니라 응답 전체 경로라
        // 여기서는 그대로 둔다 (auth·funeral 설정은 전부 'result' 다).
        return [.. rows];
    }

    private static List<JsonElement> FlattenTree(IReadOnlyList<JsonElement> list)
    {
        var result = new List<JsonElement>();
        void Recurse(IEnumerable<JsonElement> nodes)
        {
            foreach (var node in nodes)
            {
                result.Add(node);
                if (node.ValueKind == JsonValueKind.Object
                    && node.TryGetProperty("children", out var children)
                    && children.ValueKind == JsonValueKind.Array)
                {
                    Recurse(children.EnumerateArray());
                }
            }
        }

        Recurse(list);
        return result;
    }

    /// <summary>대소문자를 가리지 않고 속성 값을 글자로 꺼낸다.</summary>
    public static string? GetText(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var member in element.EnumerateObject())
        {
            if (string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase))
            {
                return member.Value.ValueKind switch
                {
                    JsonValueKind.String => member.Value.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => member.Value.ToString(),
                };
            }
        }

        return null;
    }
}
