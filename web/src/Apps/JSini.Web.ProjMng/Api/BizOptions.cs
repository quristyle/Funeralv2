using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using JSini.Web.Components.Data;
using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.ProjMng.Api;

/// <summary>포털 범용 셀렉트의 메타데이터 한 행 (<c>scom.biz_select_configs</c>).</summary>
public sealed record BizSelectConfig
{
    public string BizType { get; init; } = string.Empty;

    /// <summary>호출 대상 MSA (auth · funeral · helpdesk · projmng · file · ai). 게이트웨이 프리픽스다.</summary>
    public string? ServiceCode { get; init; }

    /// <summary>MSA 프리픽스를 뺀 서비스 내부 경로.</summary>
    public string ApiUrl { get; init; } = string.Empty;

    public string? HttpMethod { get; init; }

    public string? LabelField { get; init; }

    public string? ValueField { get; init; }

    /// <summary>응답에서 목록을 찾는 경로 (점 표기).</summary>
    public string? ResultPath { get; init; }

    /// <summary>후처리. <c>FLATTEN</c> 이면 트리 응답을 한 줄짜리 목록으로 편다.</summary>
    public string? ProcessorType { get; init; }

    /// <summary>호출 시 항상 함께 보내는 고정 파라미터 (JSON 객체 문자열).</summary>
    public string? StaticParams { get; init; }

    /// <summary>런타임 파라미터를 넣을 본문 내 경로 (점 표기). 비면 최상위.</summary>
    public string? ParamPath { get; init; }
}

/// <summary>셀렉트 항목 하나.</summary>
/// <param name="Value">고른 값. 원본이 숫자여도 문자열로 통일한다 — 프로시저 파라미터 규약과 같다.</param>
/// <param name="Label">사람이 읽는 이름.</param>
/// <param name="Item">원본 행 전체. 라벨·값 말고 다른 컬럼이 필요한 화면이 쓴다.</param>
public sealed record BizOption(
    string Value,
    string Label,
    IReadOnlyDictionary<string, string> Item);

/// <summary>포털 범용 셀렉트(BizSelect)의 목록을 읽는다. 어느 API 를 어떤 모양으로 부를지는 메타데이터(scom.biz_select_configs)가 정한다.
/// 메타데이터만 캐시한다(목록은 매번 읽는다). 봉투가 셋이라 resultPath 를 뿌리·data·data.result 순으로 대 본다. 묶음 이름은 헬프데스크와 같아야 한다.</summary>
public sealed class BizOptions(
    GatewayClient gateway,
    IHttpClientFactory httpFactory,
    ReferenceData data,
    ILogger<BizOptions> logger)
{
    /// <summary>메타데이터 조회 경로. AuthServer 의 시스템 영역이다.</summary>
    private const string ConfigUrl = "auth/system/biz-select/configs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary><paramref name="bizType"/> 의 목록을 읽는다. 설정이 없거나 실패하면 빈 목록 — 셀렉트 하나가 안 찬다고 화면이 죽으면 안 된다.</summary>
    public async Task<IReadOnlyList<BizOption>> GetAsync(
        string bizType,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bizType))
        {
            return [];
        }

        try
        {
            var config = await FindConfigAsync(bizType, cancellationToken);
            if (config is null)
            {
                logger.LogWarning("BizSelect 메타데이터에 없는 타입: {BizType}", bizType);
                return [];
            }

            var root = await CallAsync(config, BuildParams(config, parameters), cancellationToken);
            if (root is null)
            {
                return [];
            }

            var rows = Unwrap(root.Value, config.ResultPath);

            if (string.Equals(config.ProcessorType, "FLATTEN", StringComparison.OrdinalIgnoreCase))
            {
                rows = Flatten(rows);
            }

            var labelField = string.IsNullOrWhiteSpace(config.LabelField) ? "name" : config.LabelField;
            var valueField = string.IsNullOrWhiteSpace(config.ValueField) ? "id" : config.ValueField;

            var options = new List<BizOption>(rows.Count);
            foreach (var row in rows)
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var item = ToItem(row);
                options.Add(new BizOption(
                    item.GetValueOrDefault(valueField, string.Empty),
                    item.GetValueOrDefault(labelField, string.Empty),
                    item));
            }

            return options;
        }
        catch (ApiException ex)
        {
            logger.LogWarning(ex, "BizSelect [{BizType}] 조회 실패", bizType);
            return [];
        }
    }

    /// <summary>설정 캐시를 비운다. 통이 하나라 부르면 모두에게 반영된다.</summary>
    public void ClearCache() => data.Invalidate(ReferenceData.Groups.BizSelectConfigs);

    private async Task<BizSelectConfig?> FindConfigAsync(
        string bizType,
        CancellationToken cancellationToken)
    {
        // 실패는 예외로 알린다 — 통은 예외가 지나가면 담지 않으므로 다음에 다시 시도한다.
        var configs = await data.SharedAsync<IReadOnlyList<BizSelectConfig>>(
            ReferenceData.Groups.BizSelectConfigs, "all",
            async () => await gateway.GetListAsync<BizSelectConfig>(ConfigUrl, cancellationToken));

        return configs?.FirstOrDefault(c =>
            string.Equals(c.BizType, bizType, StringComparison.Ordinal));
    }

    /// <summary>고정 파라미터와 런타임 파라미터를 합친다. paramPath 가 있으면 런타임 값을 그 자리에 넣는다 (프로시저 이름은 최상위, 조회 조건은 MainParam 안).</summary>
    private static Dictionary<string, object?> BuildParams(
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
            if (cursor.TryGetValue(part, out var existing) && existing is Dictionary<string, object?> nested)
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
        var leaf = cursor.TryGetValue(leafKey, out var current) && current is Dictionary<string, object?> dict
            ? dict
            : new Dictionary<string, object?>();
        foreach (var (key, value) in extra)
        {
            leaf[key] = value;
        }

        cursor[leafKey] = leaf;
        return body;
    }

    private static Dictionary<string, object?> ParseStaticParams(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var result = new Dictionary<string, object?>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.Clone();
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>설정이 가리키는 MSA 를 부른다. 같은 이름의 HttpClient 를 꺼내 쓴다 — 경로·본문 모양이 메타데이터에서 와서 타입 메서드로는 태울 수 없다.</summary>
    private async Task<JsonElement?> CallAsync(
        BizSelectConfig config,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var http = httpFactory.CreateClient(nameof(GatewayClient));

        var service = config.ServiceCode?.Trim();
        var apiUrl = config.ApiUrl.StartsWith('/') ? config.ApiUrl : $"/{config.ApiUrl}";

        // 기본주소가 …/api/ 로 끝나므로 앞의 / 를 뗀다. serviceCode 가 비면 apiUrl 이 프리픽스를 품은 것으로 본다.
        var url = string.IsNullOrEmpty(service)
            ? apiUrl.TrimStart('/')
            : $"{service}{apiUrl}";

        var method = (config.HttpMethod ?? "GET").Trim().ToUpperInvariant();

        HttpResponseMessage response;
        if (method == "GET")
        {
            response = await http.GetAsync(
                AppendQuery(url, parameters), cancellationToken);
        }
        else
        {
            // MainParam 은 문자열 사전이다 — 메타데이터의 숫자 값을 여기서 맞춘다.
            if (parameters.TryGetValue("MainParam", out var main)
                && main is Dictionary<string, object?> mainDict)
            {
                parameters["MainParam"] = mainDict.ToDictionary(
                    pair => pair.Key,
                    pair => ToStringValue(pair.Value));
            }

            response = await http.PostAsJsonAsync(url, parameters, JsonOptions, cancellationToken);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "BizSelect 호출 실패: {Url} → {Status}", url, (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return doc.RootElement.Clone();
        }
    }

    private static string AppendQuery(string url, Dictionary<string, object?> parameters)
    {
        if (parameters.Count == 0)
        {
            return url;
        }

        var pairs = parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(ToStringValue(pair.Value))}");
        return $"{url}?{string.Join('&', pairs)}";
    }

    /// <summary>파라미터 값을 문자열로. <see cref="ProjMngClient"/> 의 규약과 같은 표기를 쓴다.</summary>
    private static string ToStringValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss"),
        DateOnly date => date.ToString("yyyy-MM-dd"),
        bool flag => flag ? "true" : "false",
        JsonElement element => element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>응답에서 목록을 찾는다. resultPath 를 뿌리·data·data.result 순으로 대 보고, 없으면 관행 경로를 차례로 본다. 처음 나오는 배열이 답이다.</summary>
    private static IReadOnlyList<JsonElement> Unwrap(JsonElement root, string? resultPath)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            candidates.Add(resultPath);
            candidates.Add($"data.{resultPath}");
            candidates.Add($"data.result.{resultPath}");
        }

        // 표준 봉투의 프로시저 결과는 rows 에 실린다.
        candidates.Add("data.result.rows");
        candidates.Add("data.result");
        candidates.Add("result");
        candidates.Add("data");
        candidates.Add(string.Empty);

        foreach (var path in candidates)
        {
            var found = Walk(root, path);
            if (found is { ValueKind: JsonValueKind.Array } array)
            {
                return [.. array.EnumerateArray()];
            }
        }

        return [];
    }

    private static JsonElement? Walk(JsonElement root, string path)
    {
        var current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(part, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>트리 응답을 한 줄짜리 목록으로 편다 (부서처럼 계층이 있는 데이터).</summary>
    private static IReadOnlyList<JsonElement> Flatten(IReadOnlyList<JsonElement> rows)
    {
        var result = new List<JsonElement>();

        void Recurse(IEnumerable<JsonElement> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                result.Add(node);

                if (node.TryGetProperty("children", out var children)
                    && children.ValueKind == JsonValueKind.Array)
                {
                    Recurse(children.EnumerateArray());
                }
            }
        }

        Recurse(rows);
        return result;
    }

    /// <summary>행 하나를 문자열 사전으로. 중첩 객체·배열은 JSON 그대로 담는다.</summary>
    private static Dictionary<string, string> ToItem(JsonElement row)
    {
        var item = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in row.EnumerateObject())
        {
            item[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => property.Value.ToString(),
            };
        }

        return item;
    }
}
