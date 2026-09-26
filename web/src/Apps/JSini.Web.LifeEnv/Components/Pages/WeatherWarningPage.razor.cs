using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.LifeEnv.Api;
using System.Globalization;
using System.Text.RegularExpressions;
using WeatherWarningItem = JSini.Web.LifeEnv.Api.WeatherWarning;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherWarningPage
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_command);

    /// <summary>
    /// 한 특보번호의 발표들. 안에서도, 그룹끼리도 최신이 앞이다.
    /// </summary>
    private sealed record WarningGroup(string? WarningNum, WeatherWarningItem Latest, List<WeatherWarningItem> Items);

    /// <summary>관리지역 하나와 그 지역이 걸린 문장들.</summary>
    private sealed record LocationHit(WeatherLocation Location, List<WeatherWarningSentence> Sentences);

    private List<WeatherWarningItem> _warnings = [];
    private List<string> _commands = [];
    private string? _command;

    private Dictionary<string, WeatherWarningZone> _zones = new(StringComparer.Ordinal);

    private int? _selectedId;
    private WeatherWarningFullDetails? _detail;
    private bool _detailLoading;

    /// <summary>본문에서 칠할 구역명. 긴 것부터 — 짧은 것이 먼저 먹으면 겹친다.</summary>
    private List<string> _keywords = [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private List<WarningGroup> Groups
    {
        get
        {
            var filtered = string.IsNullOrEmpty(_command)
                ? _warnings
                : [.. _warnings.Where(w => string.Equals(w.Command, _command, StringComparison.Ordinal))];

            return [.. filtered
                .GroupBy(w => string.IsNullOrWhiteSpace(w.WarningNum) ? $"temp-{w.Id}" : w.WarningNum!)
                .Select(g =>
                {
                    var items = g.OrderByDescending(w => w.TmFc, StringComparer.Ordinal).ToList();
                    return new WarningGroup(items[0].WarningNum, items[0], items);
                })
                .OrderByDescending(g => g.Latest.TmFc, StringComparer.Ordinal)];
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _selectedId = null;
        _detail = null;

        {
            // 구역 마스터는 특보와 무관하게 늘 같다. 나란히 부른다.
            var warnings = Client.GetWarningsAsync(all: true);
            var zones = Client.GetWarningZonesAsync();

            await Task.WhenAll(warnings, zones);

            _zones = zones.Result
                .Where(z => !string.IsNullOrWhiteSpace(z.RegId))
                .GroupBy(z => z.RegId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // 서버는 7일치를 준다. 이틀 안의 것만 현황으로 본다.
            var cutoff = DateTime.Now.AddHours(-48);

            _warnings = [.. warnings.Result
                .Where(w => ParseMoment(w.TmFc) is { } at && at >= cutoff)
                .OrderByDescending(w => w.TmFc, StringComparer.Ordinal)];

            _commands = [.. _warnings
                .Select(w => w.Command)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];

            if (_warnings.Count > 0)
            {
                // 맨 위 그룹을 자동으로 편다. 오른쪽이 비어 있으면 이 화면이
                // 무엇을 보여 주는 곳인지 알기 어렵다.
                await SelectAsync(Groups[0].Latest.Id);
            }
        }

        return _warnings.Count;
    }, "최근 48시간 안에 발표된 특보가 없습니다.", "특보를 읽지 못했습니다");

    private async Task SelectAsync(int id)
    {
        _selectedId = id;
        _detailLoading = true;
        _detail = null;
        _keywords = [];

        try
        {
            _detail = await Client.GetWarningFullDetailsAsync(id);

            // 칠할 말은 서버가 준 구역명에서만 뽑는다. 구역명 전체는 공백으로
            // 나뉜 여러 낱말이라 낱말 단위로도 넣는다.
            var words = new HashSet<string>(StringComparer.Ordinal);

            foreach (var zone in _detail?.RelatedZones ?? [])
            {
                if (!string.IsNullOrWhiteSpace(zone.RegKo))
                {
                    words.Add(zone.RegKo.Trim());
                }

                foreach (var part in (zone.RegName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    words.Add(part.Trim());
                }
            }

            _keywords = [.. words.Where(w => w.Length > 1).OrderByDescending(w => w.Length)];
        }
        catch (ApiException ex)
        {
            Say($"통보문을 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _detailLoading = false;
        }
    }

    /// <summary>
    /// 이 특보에서 우리 관리지역이 걸린 문장.
    ///
    /// 특보 목록이 이미 <c>matchedLocations</c> 와 <c>sentences</c> 를 함께
    /// 준다(<c>all=true</c>). 그래서 카드 요약을 그리려고 상세를 다시 부를
    /// 필요가 없다 — 그러면 목록 한 판에 왕복이 수십 번 생긴다.
    /// </summary>
    private List<LocationHit> MatchedSentences(WeatherWarningItem warning)
    {
        var hits = new List<LocationHit>();

        if (warning.MatchedLocations.Count == 0 || warning.Sentences.Count == 0)
        {
            return hits;
        }

        foreach (var location in warning.MatchedLocations)
        {
            var code = location.WarningAreaCode;

            if (string.IsNullOrWhiteSpace(code) || !_zones.TryGetValue(code, out var zone))
            {
                continue;
            }

            var keywords = new HashSet<string>(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(zone.RegKo))
            {
                keywords.Add(zone.RegKo.Trim());
            }

            foreach (var part in (zone.RegName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                keywords.Add(part.Trim());
            }

            var matched = warning.Sentences
                .Where(s => keywords.Any(k => k.Length > 1 && $"{s.Title} {s.Content}".Contains(k, StringComparison.Ordinal)))
                .OrderBy(s => s.Sequence)
                .DistinctBy(s => s.Content)
                .Where(s => !string.IsNullOrWhiteSpace(s.Title))
                .ToList();

            if (matched.Count > 0)
            {
                hits.Add(new LocationHit(location, matched));
            }
        }

        return hits;
    }

    private string ZoneName(string? code) =>
        code is not null && _zones.TryGetValue(code, out var zone) ? zone.RegKo ?? code : string.Empty;

    /// <summary>문장 제목 앞의 글머리(`o `)를 뗀다. 통보문 서식이라 화면에서는 군더더기다.</summary>
    private static string TrimBullet(string? title) =>
        string.IsNullOrWhiteSpace(title) ? string.Empty : Regex.Replace(title, @"^o\s*", string.Empty);

    private static string CommandTone(string? command) =>
        command is not null && command.Contains("해제", StringComparison.Ordinal)
            ? "jsini-badge--ok"
            : "jsini-badge--warn";

    /// <summary>
    /// 발표 시각(<c>yyyyMMddHHmm</c>)을 읽는다. 서버가 문자열로 주므로
    /// 정렬은 문자열 그대로 해도 되지만, 자르고 보여 주려면 날짜여야 한다.
    /// </summary>
    private static DateTime? ParseMoment(string? tmFc) =>
        DateTime.TryParseExact(tmFc, "yyyyMMddHHmm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var at) ? at : null;

    private static string Moment(string? tmFc, string format = "MM-dd HH:mm") =>
        ParseMoment(tmFc)?.ToString(format, CultureInfo.InvariantCulture) ?? tmFc ?? string.Empty;

    private static string Elapsed(string? tmFc)
    {
        if (ParseMoment(tmFc) is not { } at)
        {
            return string.Empty;
        }

        var gap = DateTime.Now - at;

        return gap switch
        {
            { TotalMinutes: < 1 } => "방금",
            { TotalHours: < 1 } => $"{(int)gap.TotalMinutes}분 전",
            { TotalDays: < 1 } => $"{(int)gap.TotalHours}시간 전",
            _ => $"{(int)gap.TotalDays}일 전",
        };
    }
}
