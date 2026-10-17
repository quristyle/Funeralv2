using System.Globalization;
using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// 클로드 코드(<c>claude</c>)의 한도를 읽는다.
/// </summary>
/// <remarks>
/// <para>
/// <b>화면에 찍히는 글이 아니라 세션 기록을 읽는다.</b> <c>claude -p /usage</c> 가
/// 표준출력에 내놓는 것은 사람이 보라고 만든 문단이고, 거기에는
/// 「<c>Last 24h · 2703 requests · 29 sessions</c>」 처럼 <i>한도가 아닌 퍼센트</i>가
/// 함께 적혀 있다. 같은 출력을 <see cref="UsageText"/> 의 정규식으로 훑으면
/// 그 숫자들이 세션 한도 칸으로 걸어 들어온다. 같은 호출이 세션 기록
/// (<c>~/.claude/projects/&lt;폴더&gt;/&lt;세션id&gt;.jsonl</c>)에 <c>usageReport.rate_limits</c>
/// 를 구조로 남기므로, <b>글자가 아니라 그것을 읽는다.</b>
/// </para>
/// <para>
/// 실제 기록(2026-10-11, 줄인 것):
/// </para>
/// <code>
/// "rate_limits": { "limits": [
///   { "kind": "session",       "group": "session", "percent": 0,  "resets_at": null },
///   { "kind": "weekly_all",    "group": "weekly",  "percent": 39, "resets_at": "2026-10-15T13:59:59.803224+00:00" },
///   { "kind": "weekly_scoped", "group": "weekly",  "percent": 0,  "resets_at": "2026-10-15T14:00:00+00:00",
///     "scope": { "model": { "display_name": "Fable" } } }
/// ]}
/// </code>
/// <para>
/// <b>모델로 끊은 주간은 칸을 따로 낸다.</b> 계정 전체의 주간과 특정 모델의
/// 주간은 서로 다른 주머니라, 한 줄로 뭉개면 둘 중 하나가 찬 것이 보이지
/// 않는다 — <c>agy</c> 의 모델군과 같은 이유로
/// <see cref="AiUsageItem.BucketNm"/> 을 쓴다. 모델 이름은 CLI 가 적어 준
/// 것을 그대로 쓴다. 바뀌면 서버가 옛 칸을 치운다.
/// </para>
/// <para>
/// <b>시각은 UTC 로 온다.</b> 글자를 읽던 시절의 「오늘인가 내일인가」 추측이
/// 여기서는 통째로 사라진다 — 기록이 절대 시각을 들고 있다.
/// </para>
/// <para>
/// <b>구독 한도가 없으면 CLI 가 비용 요약으로 내려앉는다.</b> 로그인이 끊긴
/// 장비에서 <c>/usage</c> 는 오류가 아니라 「<c>Total cost: $0.0000 …</c>」 를
/// 내놓고 기록에는 <c>"limits": null</c> 이 남는다. 그것을 「한도를 못 읽었다」로만
/// 적으면 사람이 파서를 들여다보게 되므로, <b>로그인을 짚어 준다.</b>
/// </para>
/// </remarks>
public static class UsageClaude
{
    /// <summary>세션 기록을 찾을 자리의 기본값. 설정이 비어 있을 때 쓴다.</summary>
    private const string DefaultRoot = "~/.claude/projects";

    /// <summary>
    /// 「다시 로그인해야 한다」를 화면이 알아보는 표. 까닭 글의 맨 앞에 붙는다.
    /// </summary>
    /// <remarks>
    /// <b>글자를 눈으로 맞춰 보라고 두는 것이 아니다.</b> 화면은 이 표를 보고
    /// 배지를 「OAuth 재로그인 필요」로 바꾸고, 사람에게 보일 때는 떼어 낸다
    /// (<c>AiUsageText.NeedsAuth</c>). 여느 실패와 같은 「읽지 못함」으로
    /// 묶이면 <i>고칠 곳이 실행기 장비의 로그인인데 파서를 들여다보게 된다.</i>
    /// 표를 바꾸면 화면 쪽도 함께 고쳐야 한다 — DB 칸을 늘리지 않으려고
    /// 까닭 글 하나에 실어 보내는 값이다.
    /// </remarks>
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

        // 봉투를 못 열었으면 받은 것을 그대로 보여 준다 — 사람이 보면 안다.
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

        // 기록은 찾았는데 한도가 비어 있다. **파서 탓이 아니다.**
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

    /// <summary>
    /// 요금제와 <b>로그인이 끊기는 날</b>을 한 칸에만 얹는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>한도와 다른 종류의 시각이다.</b> 주간 한도는 저절로 다시 차지만
    /// 이것은 사람이 그 장비에 붙어 다시 로그인해야 풀린다. 2026-10-17 에
    /// 실제로 끊겼고, 그 날짜는 그 전부터 이 파일에 적혀 있었다.
    /// </para>
    /// <para>
    /// <b>토큰은 읽지 않는다.</b> 꺼내는 것은 요금제 이름과 만료 시각뿐이다.
    /// </para>
    /// <para>
    /// 칸이 여럿일 때 <b>계정 칸에만</b> 얹는다 — 모델로 끊은 주간마다
    /// 같은 배지를 달면 같은 사실이 화면에 두 번 앉는다.
    /// </para>
    /// </remarks>
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
    /// <returns>못 읽으면 둘 다 <c>null</c>. <b>던지지 않는다</b> — 곁들이는 일이다.</returns>
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

            // 1970 부터의 **밀리초**다. 초로 읽으면 1970 년이 나온다.
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

    /// <summary>
    /// <c>--output-format json</c> 의 한 줄을 열어 세션 번호와 사람이 읽을 글을 꺼낸다.
    /// </summary>
    /// <remarks>
    /// 표준오류가 앞뒤로 섞여 들어오므로 <b>줄마다 열어 본다</b>. 못 열면
    /// 둘 다 비워 돌려주고, 부르는 쪽이 글자 읽기로 내려간다.
    /// </remarks>
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

    /// <summary>
    /// 세션 번호가 경로에 넣어도 되는 모양인가.
    /// </summary>
    /// <remarks>
    /// CLI 출력에서 온 글자를 그대로 파일 이름에 붙이지 않는다. 지금은 UUID 지만
    /// 그 약속이 깨졌을 때 <c>../</c> 가 섞여 들어오는 쪽이 훨씬 나쁘다.
    /// </remarks>
    private static string? Safe(string? id)
        => id is { Length: >= 8 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            ? id
            : null;

    // ── 세션 기록 ───────────────────────────────────────────

    /// <summary>
    /// 그 세션의 기록에서 <c>usageReport.rate_limits.limits</c> 를 꺼낸다.
    /// </summary>
    /// <returns>
    /// 배열을 찾으면 그것. <b>기록을 못 찾았거나 읽다 실패하면 <c>null</c></b> —
    /// 「기록이 없다」와 「한도가 비어 있다」를 가르려고 빈 배열과 구별한다.
    /// </returns>
    private static JsonElement? Limits(string root, string sessionId)
    {
        try
        {
            var dir = Expand(string.IsNullOrWhiteSpace(root) ? DefaultRoot : root);

            if (!Directory.Exists(dir))
            {
                return null;
            }

            // 프로젝트 폴더가 수백 개라 폴더 이름을 짐작하지 않고 훑는다.
            // 이름이 정확히 박혀 있어 비용은 폴더 수만큼이다.
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

            // 창의 뜻은 <c>group</c> 으로 정한다 — <c>kind</c> 는 모델로 끊은
            // 것까지 이름에 담고 있어(weekly_scoped) 새 이름이 생길 때마다
            // 여기가 어긋난다. 묶음 이름은 그보다 느리게 바뀐다.
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

    /// <summary>
    /// 이 한도가 무엇으로 끊겨 있나 — 모델 이름. 계정 전체면 빈 글자다.
    /// </summary>
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

    /// <summary>
    /// 기록을 못 읽었을 때 사람이 보는 글이라도 읽어 본다.
    /// </summary>
    /// <remarks>
    /// <b>한도 문단까지만 넘긴다.</b> 그 아래의 「무엇이 한도를 쓰고 있나」
    /// 단락에는 <c>70% of your usage was at &gt;150k context</c> 처럼 한도가 아닌
    /// 퍼센트가 줄줄이 있고, 바로 위에 <c>29 sessions</c> 가 있어 그 숫자들이
    /// <i>세션 한도</i>로 걸어 들어온다.
    /// </remarks>
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
