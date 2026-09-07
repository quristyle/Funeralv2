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
/// <param name="Item">
/// 서버가 준 원본 행 전체. 라벨·값 말고 다른 컬럼이 필요한 화면이 쓴다
/// (예: 사용자 화면이 <c>loginId</c> 로 참여자를 거른다).
/// </param>
public sealed record BizOption(
    string Value,
    string Label,
    IReadOnlyDictionary<string, string> Item);

/// <summary>
/// 포털 범용 셀렉트(BizSelect)의 목록을 읽는다 — Vue 의 <c>api/biz-select.ts</c> 를 잇는다.
///
/// [설정은 DB 에 있다]
///
/// 화면은 <c>bizType</c> 만 알면 된다. "어느 MSA 의 어느 API 를 어떤 모양으로
/// 부르는가" 는 전부 메타데이터(<c>scom.biz_select_configs</c>)가 정하고,
/// 그 메타데이터는 AuthServer 가 내려준다. 그래서 이 클라이언트가 <c>auth</c>
/// 경로를 부른다 — Vue 도 어느 화면에서든 같은 경로를 불렀다.
///
/// [봉투 해석이 관대한 이유]
///
/// Vue 는 서비스마다 요청 클라이언트가 따로 있어 봉투를 각자 벗겼다
/// (포털 <c>{code,data}</c> · 헬프데스크 <c>{success,data}</c> · 프로젝트관리
/// <c>{code,cols,data}</c>). 여기는 클라이언트가 하나라 그 층이 없으므로,
/// <c>resultPath</c> 를 뿌리·<c>data</c>·<c>data.result</c> 순서로 대 보고
/// 처음 배열이 나오는 곳을 쓴다. 메타데이터의 <c>resultPath</c> 값을 바꾸지
/// 않고 세 봉투를 모두 받기 위한 선택이다.
///
/// [설정만 캐시한다 — 그리고 회로 바깥에서 한다]
///
/// 담기는 것은 <b>메타데이터뿐</b>이다. 실제 목록은 매번 새로 읽는다 —
/// 파라미터가 화면마다 다르고, 그 결과는 자료라 낡으면 안 된다.
///
/// <para>
/// 한동안 그 메타데이터를 이 클래스가 필드로 들고 있었다. 이 서비스가 scoped 라
/// <b>업무를 넘나들면 통째로 사라졌고</b>(Piral 모듈 컨테이너가 갈린다), 접속자
/// 수만큼 같은 표를 읽었다. 지금은 <see cref="ReferenceData"/> 에 맡긴다.
/// </para>
///
/// <para>
/// <b>헬프데스크와 묶음을 나눠 쓴다</b>(<see cref="ReferenceData.Groups.BizSelectConfigs"/>).
/// 그쪽도 같은 엔드포인트를 읽으므로 이름이 갈리면 아무 오류 없이 통이 둘로
/// 나뉜다. 그리고 <c>SharedAsync</c> 인 근거는 백엔드다 —
/// <c>GET /auth/system/biz-select/configs</c> 는 <c>IBizSelectConfigService</c> 를
/// 그대로 부르고 그 서비스는 신원을 보지 않는다.
/// </para>
/// </summary>
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

    /// <summary>
    /// <paramref name="bizType"/> 의 목록을 읽는다.
    ///
    /// 설정이 없거나 호출이 실패하면 빈 목록을 준다 — 셀렉트 하나가 안 찬다고
    /// 화면이 통째로 죽으면 사용자가 무엇을 했는지 알 수 없게 된다(Vue 와 같다).
    /// </summary>
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

    /// <summary>
    /// 설정 캐시를 비운다. 관리 화면에서 메타데이터를 고친 뒤에 부른다.
    /// </summary>
    /// <remarks>
    /// 이제 통이 하나라 <b>부르면 모두에게 반영된다.</b> 전에는 부른 사람의
    /// 회로에서만 비어서, 그 화면이 Admin 앱에 있는 동안에는 사실상 회로를
    /// 새로 여는 것 말고는 방법이 없었다.
    /// </remarks>
    public void ClearCache() => data.Invalidate(ReferenceData.Groups.BizSelectConfigs);

    private async Task<BizSelectConfig?> FindConfigAsync(
        string bizType,
        CancellationToken cancellationToken)
    {
        // 실패는 **예외로** 알린다. 통은 예외가 지나가면 담지 않으므로
        // (`cache.Set` 에 닿기 전에 밖으로 나간다) 다음에 다시 시도한다.
        // 그 예외는 위쪽 GetAsync 의 try 가 받아 빈 목록으로 바꾼다.
        var configs = await data.SharedAsync<IReadOnlyList<BizSelectConfig>>(
            ReferenceData.Groups.BizSelectConfigs, "all",
            async () => await gateway.GetListAsync<BizSelectConfig>(ConfigUrl, cancellationToken));

        return configs?.FirstOrDefault(c =>
            string.Equals(c.BizType, bizType, StringComparison.Ordinal));
    }

    /// <summary>
    /// 고정 파라미터(<c>staticParams</c>)와 화면이 넘긴 런타임 파라미터를 합친다.
    /// <c>paramPath</c> 가 있으면 런타임 파라미터를 그 자리에 겹쳐 넣는다 —
    /// 프로시저 이름은 본문 최상위에, 조회 조건은 <c>MainParam</c> 안에 넣어야
    /// 하는 규약(프로젝트관리)을 메타데이터로 표현하기 위한 것이다.
    /// </summary>
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

    /// <summary>
    /// 설정이 가리키는 MSA 를 부른다.
    ///
    /// <see cref="GatewayClient"/> 를 쓰지 않고 같은 이름의 HttpClient
    /// (기본주소·인증 핸들러가 이미 붙어 있다)를 꺼내 쓴다 — 경로·메서드·본문
    /// 모양이 전부 메타데이터에서 오므로 특정 봉투를 강제하는 타입 메서드로는
    /// 태울 수 없다.
    /// </summary>
    private async Task<JsonElement?> CallAsync(
        BizSelectConfig config,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var http = httpFactory.CreateClient(nameof(GatewayClient));

        var service = config.ServiceCode?.Trim();
        var apiUrl = config.ApiUrl.StartsWith('/') ? config.ApiUrl : $"/{config.ApiUrl}";

        // 기본주소가 …/api/ 로 끝나므로 앞의 / 를 떼야 상대 경로로 붙는다.
        // serviceCode 가 비어 있으면 apiUrl 이 프리픽스를 이미 품고 있는 것으로
        // 본다 (Vue 와 같은 폴백).
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
            // 프로젝트관리 규약: MainParam 은 문자열 사전이다. 메타데이터를 타고 온
            // 값은 숫자일 수 있어 여기서 맞춰 준다 (Vue projmngRequest 와 같다).
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

    /// <summary>
    /// 응답에서 목록을 찾는다. <c>resultPath</c> 를 뿌리 → <c>data</c> →
    /// <c>data.result</c> 순서로 대 보고, 그래도 없으면 관행 경로
    /// (<c>data.result.rows</c> · <c>data.result</c> · <c>result</c> · <c>data</c> · 뿌리)를
    /// 차례로 본다. 처음 나오는 <b>배열</b>이 답이다.
    /// </summary>
    private static IReadOnlyList<JsonElement> Unwrap(JsonElement root, string? resultPath)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            candidates.Add(resultPath);
            candidates.Add($"data.{resultPath}");
            candidates.Add($"data.result.{resultPath}");
        }

        // 표준 봉투의 프로시저 결과는 rows 에 실린다 — Vue 의 projmng 클라이언트가
        // resultPath='data' 를 rows 로 되짚던 것과 같은 폴백이다.
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
