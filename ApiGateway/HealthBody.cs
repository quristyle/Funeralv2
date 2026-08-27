using System.Text.Json;

/// <summary>
/// 각 서비스가 <c>/health</c> 본문에 담아 보낸 점검 결과를 읽는다.
/// </summary>
/// <remarks>
/// <para>
/// 형식은 <c>JSini.Shared.Infrastructure/HealthChecks/HealthCheckJson.cs</c> 가 만든다.
/// 게이트웨이는 <b>판정하지 않고 읽어 올리기만</b> 한다 — LLM 주소·모델명·접속 문자열은
/// 그 서비스의 설정이고, 게이트웨이가 알아야 할 이유가 없다.
/// </para>
///
/// <para>
/// <b>옛 형식도 받아 준다.</b> 아직 새 형식으로 바꾸지 않은 서비스는 본문이
/// <c>Healthy</c> 라는 글자 하나다. 그때는 딸린 것이 없는 것으로 보고 넘어간다 —
/// 서비스를 하나씩 바꿔 나갈 수 있어야 한다.
/// </para>
/// </remarks>
public sealed class HealthBody
{
    /// <summary>딸린 것 목록. 없으면 빈 목록이다.</summary>
    public List<object> Dependencies { get; init; } = new();

    /// <summary>서비스가 '제 일을 못 한다' 고 보고했는지.</summary>
    public bool IsDegraded { get; init; }

    /// <summary>
    /// 왜 그 상태인지 한 줄. 정상이 아닌 점검의 설명을 모아 만든다.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 본문을 읽는다. 형식을 모르면 null 을 준다(호출한 쪽은 그냥 넘어간다).
    /// </summary>
    public static HealthBody? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        // 옛 형식: 본문이 "Healthy" 같은 글자 하나. JSON 이 아니다.
        var trimmed = body.TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '[')) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var overall = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var isDegraded = string.Equals(overall, "Degraded", StringComparison.OrdinalIgnoreCase);

            var deps = new List<object>();
            var reasons = new List<string>();

            if (root.TryGetProperty("checks", out var checks)
                && checks.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in checks.EnumerateArray())
                {
                    var name = c.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var checkStatus = c.TryGetProperty("status", out var cs) ? cs.GetString() : null;
                    var description = c.TryGetProperty("description", out var d) ? d.GetString() : null;
                    var durationMs = c.TryGetProperty("durationMs", out var dur) && dur.TryGetInt32(out var dv) ? dv : 0;

                    // 'dependency' 태그가 붙은 것만 딸린 것으로 올린다.
                    // 서비스 내부 점검까지 화면에 늘어놓으면 정작 봐야 할 것이 묻힌다.
                    var isDependency = false;
                    if (c.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in tags.EnumerateArray())
                        {
                            if (string.Equals(t.GetString(), "dependency", StringComparison.OrdinalIgnoreCase))
                            {
                                isDependency = true;
                                break;
                            }
                        }
                    }

                    if (isDependency)
                    {
                        deps.Add(new
                        {
                            name,
                            status = checkStatus,
                            description,
                            durationMs,
                            // 주소·모델명처럼 화면이 함께 보여 주는 값. 비밀은 서비스가 담지 않는다.
                            data = c.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                                ? JsonSerializer.Deserialize<Dictionary<string, object>>(data.GetRawText())
                                : null,
                        });
                    }

                    // 정상이 아닌 것만 이유로 모은다.
                    if (!string.Equals(checkStatus, "Healthy", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(description))
                    {
                        reasons.Add(description!);
                    }
                }
            }

            return new HealthBody
            {
                Dependencies = deps,
                IsDegraded = isDegraded,
                Reason = reasons.Count > 0 ? string.Join(" / ", reasons) : null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
