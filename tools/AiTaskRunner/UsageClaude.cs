using System.Globalization;
using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// 클로드 코드(claude)의 한도를 읽는다. 화면 글이 아니라 세션 기록의 usageReport.rate_limits 를 읽는다 — 글자로 읽으면 「Last 24h · 2703 requests」의 숫자가 세션 한도로 잘못 걸린다.
/// 모델로 끊은 주간은 칸을 따로 내고, 시각은 UTC 로 온다. 구독 한도가 없으면(로그인 끊김) 비용 요약으로 내려앉으므로 로그인을 짚어 준다.
/// </summary>
public static class UsageClaude
{
    /// <summary>세션 기록을 찾을 자리의 기본값. 설정이 비어 있을 때 쓴다.</summary>
    private const string DefaultRoot = "~/.claude/projects";

/// <summary>화면이 「다시 로그인해야 한다」를 알아보는 표. 화면 쪽 NeedsAuth 와 같아야 한다.</summary>
    public const string AuthMark = "[OAUTH]";

    /// <summary>
    /// <c>/usage</c> 한 번의 결과를 한도 칸들로 옮긴다.
    /// </summary>
    /// <param name="kind">CLI 종류. 그대로 실어 보낸다.</param>
    /// <param name="raw">
    /// CLI 가 뱉은 것 전부. <c>--output-format json</c> 의 한 줄짜리 봉투이고,
    /// 그 안에 세션 번호(<c>session_id</c>)와 사람이 읽을 글(<c>result</c>)이 들어 있다.
    /// </param>
    /// <param name="transcriptRoot">세션 기록이 쌓이는 폴더. 비어 있으면 기본값.</param>
    /// <param name="authFile">로그인 정보가 든 JSON. 비어 있으면 안 읽는다.</param>
    public static List<AiUsageItem> Parse(
        string kind, string raw, string transcriptRoot, string authFile = "")
    {
        var (sessionId, text) = Envelope(raw);

        // 봉투를 못 열었으면 받은 것을 그대로 보여 준다.
        var shown = string.IsNullOrWhiteSpace(text) ? raw : text;

        var items = Read(kind, sessionId, transcriptRoot, shown);

        Stamp(items, authFile);

        return items;
    }

    /// <summary>한도 칸을 만든다. 로그인 정보는 아직 얹지 않는다.</summary>
    private static List<AiUsageItem> Read(
        string kind, string? sessionId, string transcriptRoot, string shown)
    {
        if (sessionId is null)
        {
            return [.. Fallback(kind, shown)];
        }

        var limits = Limits(transcriptRoot, sessionId);

        if (limits is null)
        {
            // 기록을 못 찾았다. 자리가 바뀐 것일 수 있으니 글자라도 읽어 본다.
            return [.. Fallback(kind, shown)];
        }

        var items = Map(kind, limits.Value, shown);

        if (items.Count > 0)
        {
            return items;
        }

        // 기록은 찾았는데 한도가 비어 있다 — 파서 탓이 아니라 실행기 장비의 로그인 문제다.
        return
        [
            new AiUsageItem
            {
                RunnerKind = kind,
                Ok = false,
                RawText = shown,
                ErrorText = $"{AuthMark} 구독 한도를 돌려주지 않았습니다. "
                    + "실행기 장비의 claude 로그인이 끊겼습니다 — 그 장비에서 다시 로그인하십시오.",
            },
        ];
    }

    // ── 로그인 정보 ─────────────────────────────────────────

    /// <summary>요금제와 로그인이 끊기는 날을 계정 칸에만 얹는다. 한도와 달리 사람이 다시 로그인해야 풀린다. 토큰은 읽지 않는다.</summary>
    private static void Stamp(List<AiUsageItem> items, string authFile)
    {
        if (items.Count == 0 || string.IsNullOrWhiteSpace(authFile))
        {
            return;
        }

        var (plan, expiresAt) = Auth(authFile);

        if (plan is null && expiresAt is null)
        {
            return;
        }

        var head = items.FirstOrDefault(i => string.IsNullOrEmpty(i.BucketNm)) ?? items[0];

        head.PlanNm ??= plan;
        head.AuthExpiresAt ??= expiresAt;
    }

    /// <summary>로그인 정보 파일에서 요금제와 만료 시각을 꺼낸다.</summary>
    /// <returns>못 읽으면 둘 다 <c>null</c>. 던지지 않는다 — 곁들이는 일이다.</returns>
    private static (string? Plan, DateTime? ExpiresAt) Auth(string file)
    {
        try
        {
            var full = Expand(file);

            if (!File.Exists(full))
            {
                return (null, null);
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(full));

            if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                || oauth.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            var plan = Text(oauth, "subscriptionType");

            // 1970 부터의 밀리초다(초로 읽으면 1970 년이 나온다).
            var expiresAt = Number(oauth, "refreshTokenExpiresAt") is { } ms and > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms).UtcDateTime
                : (DateTime?)null;

            return (plan, expiresAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, null);
        }
    }

    // ── 봉투 ────────────────────────────────────────────────

    /// <summary>--output-format json 의 한 줄을 열어 세션 번호와 글을 꺼낸다. 표준오류가 섞이므로 줄마다 열어 보고, 못 열면 둘 다 비워 준다.</summary>
    private static (string? SessionId, string? Text) Envelope(string raw)
    {
        foreach (var line in (raw ?? string.Empty).Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] != '{')
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("session_id", out var id)
                    || id.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var text = root.TryGetProperty("result", out var result)
                           && result.ValueKind == JsonValueKind.String
                    ? result.GetString()
                    : null;

                return (Safe(id.GetString()), text);
            }
            catch (JsonException)
            {
                // 한 줄이 JSON 이 아니었을 뿐이다. 다음 줄을 본다.
            }
        }

        return (null, null);
    }

    /// <summary>세션 번호가 경로에 넣어도 되는 모양인가. CLI 출력의 글자를 그대로 파일 이름에 붙이지 않는다.</summary>
    private static string? Safe(string? id)
        => id is { Length: >= 8 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            ? id
            : null;

    // ── 세션 기록 ───────────────────────────────────────────

    /// <summary>
    /// 그 세션의 기록에서 <c>usageReport.rate_limits.limits</c> 를 꺼낸다.
    /// </summary>
    /// <returns>배열을 찾으면 그것. 기록을 못 찾았거나 읽다 실패하면 null — 「기록 없음」과 「한도 빔」을 가르려고 빈 배열과 구별한다.</returns>
    private static JsonElement? Limits(string root, string sessionId)
    {
        try
        {
            var dir = Expand(string.IsNullOrWhiteSpace(root) ? DefaultRoot : root);

            if (!Directory.Exists(dir))
            {
                return null;
            }

            // 프로젝트 폴더가 수백 개라 이름을 짐작하지 않고 훑는다.
            var file = Directory.EnumerateFiles(dir, $"{sessionId}.jsonl", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            }).FirstOrDefault();

            if (file is null)
            {
                return null;
            }

            JsonElement? found = null;

            foreach (var line in File.ReadLines(file))
            {
                // 한 줄이 수십 KB 인 기록이라 열기 전에 걸러 낸다.
                if (!line.Contains("usageReport", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);

                    if (doc.RootElement.TryGetProperty("usageReport", out var report)
                        && report.TryGetProperty("rate_limits", out var rate)
                        && rate.TryGetProperty("limits", out var limits)
                        && limits.ValueKind == JsonValueKind.Array)
                    {
                        // 마지막 것이 이긴다 — 한 세션에 여러 번 물었을 수 있다.
                        found = limits.Clone();
                    }
                }
                catch (JsonException)
                {
                    // 적다 만 줄. 다음 줄을 본다.
                }
            }

            return found;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ── 옮기기 ──────────────────────────────────────────────

    /// <summary>한도 배열을 칸들로 옮긴다.</summary>
    private static List<AiUsageItem> Map(string kind, JsonElement limits, string? shown)
    {
        var items = new List<AiUsageItem>();

        foreach (var limit in limits.EnumerateArray())
        {
            if (limit.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (Number(limit, "percent") is not { } percent)
            {
                continue;
            }

            // 창의 뜻은 group 으로 정한다 — kind 는 모델 이름까지 담고 있어 새 이름이 생길 때마다 어긋난다.
            var group = (Text(limit, "group") ?? Text(limit, "kind") ?? string.Empty)
                .ToLowerInvariant();

            var bucket = Scope(limit);
            var item = items.FirstOrDefault(i => i.BucketNm == bucket);

            if (item is null)
            {
                item = new AiUsageItem
                {
                    RunnerKind = kind,
                    BucketNm = bucket,
                    Ok = true,
                    RawText = shown,
                };

                items.Add(item);
            }

            var resetAt = Utc(Text(limit, "resets_at"));
            var used = Math.Clamp(percent, 0m, 100m);

            if (group.Contains("session"))
            {
                item.SessionPct ??= used;
                item.SessionResetAt ??= resetAt;
            }
            else if (group.Contains("week"))
            {
                item.WeekPct ??= used;
                item.WeekResetAt ??= resetAt;
            }
            else if (group.Contains("month"))
            {
                item.MonthPct ??= used;
                item.MonthResetAt ??= resetAt;
            }
        }

        return items;
    }

    /// <summary>이 한도가 무엇으로 끊겼나 — 모델 이름. 계정 전체면 빈 글자.</summary>
    private static string Scope(JsonElement limit)
    {
        if (!limit.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (scope.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object
            && Text(model, "display_name") is { Length: > 0 } name)
        {
            return name;
        }

        if (scope.TryGetProperty("surface", out var surface) && surface.ValueKind == JsonValueKind.Object
            && Text(surface, "display_name") is { Length: > 0 } where)
        {
            return where;
        }

        return string.Empty;
    }

    // ── 내려앉는 길 ─────────────────────────────────────────

    /// <summary>기록을 못 읽었을 때 사람이 보는 글의 한도 문단까지만 넘긴다. 그 아래 단락의 퍼센트는 한도가 아니라 세션 한도로 잘못 걸린다.</summary>
    private static AiUsageItem[] Fallback(string kind, string shown)
    {
        var head = Head(shown);

        if (!head.Contains('%'))
        {
            // 퍼센트가 한 글자도 없다. 비용 요약으로 내려앉은 모양이다.
            return
            [
                new AiUsageItem
                {
                    RunnerKind = kind,
                    Ok = false,
                    RawText = shown,
                    ErrorText = $"{AuthMark} 한도가 아니라 비용 요약이 돌아왔습니다. "
                        + "실행기 장비의 claude 로그인이 끊겼습니다 — 그 장비에서 다시 로그인하십시오.",
                },
            ];
        }

        var item = UsageText.Parse(kind, head);
        item.RawText = shown;

        return [item];
    }

    /// <summary>「무엇이 한도를 쓰고 있나」 단락 앞까지 자른다.</summary>
    private static string Head(string text)
    {
        var cut = (text ?? string.Empty)
            .IndexOf("contributing", StringComparison.OrdinalIgnoreCase);

        return cut < 0 ? text ?? string.Empty : text![..cut];
    }

    // ── 자잘한 것 ───────────────────────────────────────────

    private static string? Text(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? Number(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
           && value.TryGetDecimal(out var number)
            ? number
            : null;

    private static DateTime? Utc(string? text)
        => text is { Length: > 0 }
           && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
            ? when.UtcDateTime
            : null;

    private static string Expand(string path)
        => path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;
}
