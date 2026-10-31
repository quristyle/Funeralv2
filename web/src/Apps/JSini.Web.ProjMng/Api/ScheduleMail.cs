using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 일정표의 「메일발송」 통로. 포털관리 「메일 보내기」와 같은 <c>notification/mails</c> 로 보내므로
/// 보낸 글이 내 보낸 메일함과 발송 기록에 그대로 남는다.
/// </summary>
public sealed class ScheduleMailClient(GatewayClient gateway)
{
    public Task<ScheduleMailResult?> SendAsync(ScheduleMailRequest request, CancellationToken ct = default)
        => gateway.PostAsync<ScheduleMailResult>("notification/mails", request, ct);

    /// <summary>사람 찾기 창의 명단. 포털관리 「메일 보내기」의 「사용자 고르기」와 같은 계정 목록이다.</summary>
    public Task<IReadOnlyList<ScheduleMailPerson>> GetPeopleAsync(CancellationToken ct = default)
        => gateway.GetListAsync<ScheduleMailPerson>("auth/system/account/list", ct);
}

/// <summary>계정 목록 한 줄에서 메일 받는 사람을 고르는 데 쓰는 칸만.</summary>
public sealed class ScheduleMailPerson
{
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DeptName { get; set; }
    public string? CompanyName { get; set; }
}

/// <summary><c>MailComposeDto</c> 와 짝을 맞춘 보내기 요청. 첨부는 없다.</summary>
public sealed class ScheduleMailRequest
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<object> Attachments { get; set; } = [];
    public bool HasAiSummary { get; set; }
}

public sealed class ScheduleMailResult
{
    public string Id { get; set; } = string.Empty;
    public bool Sent { get; set; }
    public int ToCount { get; set; }
    public int CcCount { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// 메일에 실을 일정 한 건. 날짜는 일정표가 그리는 그 벽시계 값이다.
/// 사람은 보이는 이름으로 받는다. 추가대상은 공통코드 값(ADD · CHANGE · MOVE)이다.
/// </summary>
public sealed record ScheduleMailItem(
    DateTime Start, DateTime End, string Title,
    string? Requester = null, string? Assignee = null, string? AddTarget = null);

/// <summary>본문을 채울 재료. 기간은 사람이 고른 것이고, 「지난·앞으로」를 가르는 기준은 늘 오늘이다.</summary>
public sealed record ScheduleMailInput(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    IReadOnlyList<ScheduleMailItem> Items,
    string? ProjectName,
    string? SenderName,
    string? Message);

/// <summary>
/// 일정을 세로 타임라인으로 그린 메일 본문. 기본 틀도 템플릿과 같은 치환자(<c>{{timeline}}</c> …)로 채운다 —
/// 기본 모양과 템플릿이 다른 길로 만들어지면 한쪽만 고쳐진다.
/// 받는 이가 간부라 메일 프로그램마다 모양이 같아야 해서 표와 인라인 스타일만 쓴다(Outlook 이 CSS 클래스·flex 를 버린다).
/// </summary>
public static class ScheduleMailBody
{
    public const int DaysAround = 7;

    private static readonly CultureInfo Ko = CultureInfo.GetCultureInfo("ko-KR");

    private const string Font = "'Malgun Gothic','맑은 고딕','Apple SD Gothic Neo',sans-serif";
    private const string Navy = "#1f3a5f";
    private const string Accent = "#2f6fdb";
    private const string Muted = "#6b7785";
    private const string Line = "#d9e0e8";

    /// <summary>템플릿에 쓸 수 있는 치환자. 관리 화면이 이 표를 그대로 보여 준다.</summary>
    public static readonly IReadOnlyList<(string Key, string Text)> Placeholders =
    [
        ("project", "프로젝트 이름 (모든 프로젝트면 「프로젝트」)"),
        ("period", "기간 — 2026.10.24(토) ~ 2026.11.07(토)"),
        ("from", "시작일 — 2026.10.24(토)"),
        ("to", "종료일 — 2026.11.07(토)"),
        ("today", "기준일(오늘) — 2026.10.31(토)"),
        ("sender", "보내는 사람 이름"),
        ("signature", "「○○○ 드림」 한 줄 (보내는 사람이 없으면 빈칸)"),
        ("message", "인사말 (비우면 기본 인사말)"),
        ("count", "기간 안의 일정 건수"),
        ("pastCount", "오늘 전 일정 건수"),
        ("todayCount", "오늘 일정 건수"),
        ("aheadCount", "오늘 뒤 일정 건수"),
        ("summary", "건수 요약 세 칸 (HTML)"),
        ("timeline", "날짜별 일정 타임라인 (HTML)"),
    ];

    /// <summary>제목을 비운 템플릿과 기본 틀이 쓰는 제목.</summary>
    public const string DefaultSubject = "[{{project}} 일정 보고] {{period}}";

    /// <summary>기본 틀. 관리 화면의 「기본 틀 불러오기」가 이것을 편집기에 깐다.</summary>
    public static readonly string DefaultTemplate = BuildDefaultTemplate();

    public static (DateOnly From, DateOnly To) Window(DateOnly today) =>
        (today.AddDays(-DaysAround), today.AddDays(DaysAround));

    public static string Dow(DateOnly day) => Ko.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek);

    /// <summary>타임라인에 올라갈 날짜. 기간보다 먼저 시작해 걸쳐 들어온 일정은 기간 첫날에 둔다.</summary>
    public static DateOnly PlacedOn(ScheduleMailItem item, DateOnly from) =>
        DateOnly.FromDateTime(item.Start) < from ? from : DateOnly.FromDateTime(item.Start);

    /// <summary>기간에 걸친 일정만.</summary>
    public static IEnumerable<ScheduleMailItem> InRange(IEnumerable<ScheduleMailItem> items, DateOnly from, DateOnly to) =>
        items.Where(i => DateOnly.FromDateTime(i.End < i.Start ? i.Start : i.End) >= from
                         && DateOnly.FromDateTime(i.Start) <= to);

    /// <summary>제목. 템플릿 제목이 비면 기본 제목을 쓴다. 제목은 글자라 치환값을 HTML 로 바꾸지 않는다.</summary>
    public static string Subject(ScheduleMailInput input, string? subjectTemplate = null) =>
        Fill(string.IsNullOrWhiteSpace(subjectTemplate) ? DefaultSubject : subjectTemplate, Values(input, html: false)).Trim();

    /// <summary>본문. 템플릿이 비면 기본 틀로 만든다.</summary>
    public static string Build(ScheduleMailInput input, string? template = null) =>
        Fill(string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template, Values(input, html: true));

    /// <summary><c>{{key}}</c> 를 갈아 끼운다. 모르는 이름은 그대로 둔다 — 지우면 오타가 빈칸으로 숨는다.</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        Regex.Replace(template, @"\{\{\s*([A-Za-z]+)\s*\}\}",
            m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>치환값 한 벌. <paramref name="html"/> 이면 글자 값을 HTML 로 옮긴다.</summary>
    public static IReadOnlyDictionary<string, string> Values(ScheduleMailInput input, bool html)
    {
        var (from, to, today) = (input.From, input.To, input.Today);
        var days = Days(input);

        var past = days.Where(d => d.Key < today).Sum(d => d.Value.Count);
        var todayCount = days.GetValueOrDefault(today)?.Count ?? 0;
        var ahead = days.Where(d => d.Key > today).Sum(d => d.Value.Count);

        string T(string text) => html ? Enc(text) : text;

        var project = string.IsNullOrWhiteSpace(input.ProjectName) ? "프로젝트" : input.ProjectName.Trim();
        var sender = input.SenderName?.Trim() ?? string.Empty;
        var period = $"{DayText(from)} ~ {DayText(to)}";

        var message = !string.IsNullOrWhiteSpace(input.Message)
            ? (html ? Enc(input.Message.Trim()).Replace("\r\n", "\n").Replace("\n", "<br>") : input.Message.Trim())
            : (html
                ? $"안녕하십니까.<br>{Enc(period)} 기간의 일정을 정리하여 보고드립니다."
                : $"안녕하십니까. {period} 기간의 일정을 정리하여 보고드립니다.");

        return new Dictionary<string, string>
        {
            ["project"] = T(project),
            ["period"] = T(period),
            ["from"] = T(DayText(from)),
            ["to"] = T(DayText(to)),
            ["today"] = T(DayText(today)),
            ["sender"] = T(sender),
            ["signature"] = sender.Length == 0 ? string.Empty
                : html ? $"<div style=\"margin-top:14px;font-weight:700;color:{Navy};\">{Enc(sender)} 드림</div>" : $"{sender} 드림",
            ["message"] = message,
            ["count"] = (past + todayCount + ahead).ToString(Ko),
            ["pastCount"] = past.ToString(Ko),
            ["todayCount"] = todayCount.ToString(Ko),
            ["aheadCount"] = ahead.ToString(Ko),
            ["summary"] = html ? Summary(past, todayCount, ahead) : string.Empty,
            ["timeline"] = html ? TimelineBlock(days, from, to, today) : string.Empty,
        };
    }

    private static string DayText(DateOnly d) => $"{d:yyyy.MM.dd}({Dow(d)})";

    private static Dictionary<DateOnly, List<ScheduleMailItem>> Days(ScheduleMailInput input) =>
        InRange(input.Items, input.From, input.To)
            .GroupBy(i => PlacedOn(i, input.From))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Start).ThenBy(i => i.Title, StringComparer.Ordinal).ToList());

    private static string BuildDefaultTemplate()
    {
        var sb = new StringBuilder();

        sb.Append($"<div style=\"margin:0;padding:24px 0;background:#f3f5f8;font-family:{Font};\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        sb.Append("<tr><td align=\"center\">");
        sb.Append($"<table role=\"presentation\" width=\"640\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;max-width:640px;background:#ffffff;border:1px solid {Line};border-radius:10px;overflow:hidden;\">");

        // 머리띠
        sb.Append($"<tr><td style=\"background:{Navy};padding:28px 32px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;color:#a9bcd6;\">SCHEDULE REPORT</div>");
        sb.Append("<div style=\"margin-top:6px;font-size:22px;font-weight:700;color:#ffffff;\">{{project}} 일정 보고</div>");
        sb.Append("<div style=\"margin-top:8px;font-size:13px;color:#d5e0ee;\">{{period}} · 기준일 {{today}}</div>");
        sb.Append("</td></tr>");

        sb.Append("<tr><td style=\"padding:28px 32px 8px 32px;font-size:14px;line-height:1.7;color:#333333;\">{{message}}</td></tr>");
        sb.Append("<tr><td style=\"padding:16px 32px 8px 32px;\">{{summary}}</td></tr>");
        sb.Append("<tr><td style=\"padding:16px 32px 8px 32px;\">{{timeline}}</td></tr>");

        // 맺음
        sb.Append("<tr><td style=\"padding:20px 32px 28px 32px;font-size:14px;line-height:1.7;color:#333333;\">");
        sb.Append("일정에 변동이 생기면 다시 말씀드리겠습니다.<br>감사합니다.{{signature}}");
        sb.Append("</td></tr>");

        sb.Append($"<tr><td style=\"padding:14px 32px;background:#f6f8fa;border-top:1px solid {Line};font-size:11px;color:#8a96a3;\">");
        sb.Append("본 메일은 JSini 포털 프로젝트 일정표에서 발송되었습니다.");
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table></div>");

        return sb.ToString();
    }

    private static string Summary(int past, int today, int ahead)
    {
        var sb = new StringBuilder();

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        sb.Append(Stat("지난 일정", past, Muted, "#f6f8fa"));
        sb.Append("<td width=\"12\"></td>");
        sb.Append(Stat("오늘", today, Accent, "#eef4ff"));
        sb.Append("<td width=\"12\"></td>");
        sb.Append(Stat("앞으로", ahead, Navy, "#f6f8fa"));
        sb.Append("</tr></table>");

        return sb.ToString();
    }

    private static string TimelineBlock(Dictionary<DateOnly, List<ScheduleMailItem>> days, DateOnly from, DateOnly to, DateOnly today)
    {
        var todayInRange = today >= from && today <= to;

        if (days.Count == 0)
        {
            return $"<div style=\"padding:28px 0;text-align:center;font-size:14px;color:{Muted};border:1px dashed {Line};border-radius:8px;\">이 기간에 기록된 일정이 없습니다.</div>";
        }

        var sb = new StringBuilder();
        var withYear = from.Year != to.Year;
        var pastDays = days.Where(d => d.Key < today).ToList();
        var nextDays = days.Where(d => d.Key >= today).ToList();

        if (pastDays.Count > 0)
        {
            sb.Append(Section("지난 일정", "진행된 일정"));
            sb.Append(Timeline(pastDays, today, withYear));
        }

        // 오늘이 기간 안이면 일정이 없어도 「오늘」 칸을 세운다 — 어디가 지금인지 보이게.
        if (todayInRange && !days.ContainsKey(today))
        {
            nextDays.Insert(0, new KeyValuePair<DateOnly, List<ScheduleMailItem>>(today, []));
        }

        if (nextDays.Count > 0)
        {
            sb.Append(Section(todayInRange ? "오늘 · 앞으로" : "앞으로", "예정된 일정"));
            sb.Append(Timeline(nextDays, today, withYear));
        }

        return sb.ToString();
    }

    private static string Stat(string label, int count, string color, string bg) =>
        $"<td width=\"33%\" style=\"background:{bg};border-radius:8px;padding:14px 8px;text-align:center;\">"
        + $"<div style=\"font-size:12px;color:{Muted};\">{label}</div>"
        + $"<div style=\"margin-top:4px;font-size:22px;font-weight:700;color:{color};\">{count}<span style=\"font-size:13px;font-weight:400;\"> 건</span></div>"
        + "</td>";

    private static string Section(string title, string sub) =>
        $"<div style=\"margin:18px 0 10px 0;padding-bottom:8px;border-bottom:2px solid {Navy};\">"
        + $"<span style=\"font-size:15px;font-weight:700;color:{Navy};\">{title}</span>"
        + $"<span style=\"margin-left:8px;font-size:12px;color:{Muted};\">{sub}</span></div>";

    private static string Timeline(IEnumerable<KeyValuePair<DateOnly, List<ScheduleMailItem>>> days, DateOnly today, bool withYear)
    {
        var sb = new StringBuilder();
        (int Year, int Month)? month = null;

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");

        foreach (var (day, list) in days)
        {
            // 날짜 상자에 월을 안 적으므로, 달이 바뀌는 자리(와 구역의 첫 줄)에 그 달을 적은 선을 긋는다.
            if (month != (day.Year, day.Month))
            {
                month = (day.Year, day.Month);
                sb.Append(MonthDivider(day, withYear));
            }

            var isToday = day == today;
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var dowColor = day.DayOfWeek == DayOfWeek.Sunday ? "#d64545"
                : day.DayOfWeek == DayOfWeek.Saturday ? "#2f6fdb" : Muted;
            var boxBg = isToday ? Accent : weekend ? "#f6f8fa" : "#ffffff";
            var boxFg = isToday ? "#ffffff" : Navy;
            var dot = isToday ? Accent : day < today ? "#b7c2ce" : Navy;

            sb.Append("<tr>");

            // 날짜 상자: 일 (요일)
            sb.Append("<td width=\"74\" valign=\"top\" style=\"padding:0 0 14px 0;\">");
            sb.Append($"<table role=\"presentation\" width=\"64\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:{boxBg};border:1px solid {(isToday ? Accent : Line)};border-radius:8px;\"><tr><td align=\"center\" style=\"padding:7px 0;white-space:nowrap;\">");
            sb.Append($"<span style=\"font-size:20px;line-height:24px;font-weight:700;color:{boxFg};\">{day.Day}</span>");
            sb.Append($"<span style=\"margin-left:2px;font-size:11px;font-weight:700;color:{(isToday ? "#ffffff" : dowColor)};\">({Dow(day)})</span>");
            sb.Append("</td></tr></table>");
            sb.Append("</td>");

            // 세로 줄과 점
            sb.Append($"<td width=\"22\" valign=\"top\" style=\"padding:0;border-left:2px solid {Line};\">");
            sb.Append($"<div style=\"width:12px;height:12px;margin:14px 0 0 -9px;border-radius:50%;background:{dot};border:2px solid #ffffff;\"></div>");
            sb.Append("</td>");

            // 그날의 일정
            sb.Append("<td valign=\"top\" style=\"padding:0 0 14px 0;\">");

            if (isToday)
            {
                sb.Append($"<div style=\"margin:2px 0 6px 0;font-size:11px;font-weight:700;color:{Accent};\">TODAY</div>");
            }

            if (list.Count == 0)
            {
                sb.Append($"<div style=\"padding:10px 14px;font-size:13px;color:{Muted};background:#f6f8fa;border-radius:6px;\">예정된 일정이 없습니다.</div>");
            }

            foreach (var item in list)
            {
                var sub = SubText(item);
                var badge = TargetBadge(item.AddTarget);

                sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 6px 0;background:{(isToday ? "#f4f8ff" : "#fbfcfd")};border:1px solid {Line};border-left:3px solid {(day < today ? "#b7c2ce" : Accent)};border-radius:6px;\"><tr>");
                sb.Append("<td valign=\"middle\" style=\"padding:10px 14px;\">");
                sb.Append($"<div style=\"font-size:14px;font-weight:700;color:#222222;\">{Enc(item.Title)}</div>");
                if (sub is not null)
                {
                    sb.Append($"<div style=\"margin-top:3px;font-size:12px;color:{Muted};\">{sub}</div>");
                }
                sb.Append("</td>");
                if (badge is not null)
                {
                    sb.Append($"<td width=\"1\" align=\"right\" valign=\"middle\" style=\"padding:10px 12px 10px 0;white-space:nowrap;\">{badge}</td>");
                }
                sb.Append("</tr></table>");
            }

            sb.Append("</td></tr>");
        }

        sb.Append("</table>");

        return sb.ToString();
    }

    /// <summary>달이 바뀌는 자리의 구분선. 기간이 해를 넘으면 해도 적는다.</summary>
    private static string MonthDivider(DateOnly day, bool withYear)
    {
        var label = withYear ? $"{day.Year}년 {day.Month}월" : $"{day.Month}월";

        return "<tr><td colspan=\"3\" style=\"padding:4px 0 12px 0;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>"
            + $"<td width=\"1\" style=\"padding:0 10px 0 0;white-space:nowrap;font-size:13px;font-weight:700;color:{Navy};\">{label}</td>"
            + $"<td style=\"border-bottom:1px solid {Line};font-size:1px;line-height:1px;\">&nbsp;</td>"
            + "</tr></table></td></tr>";
    }

    /// <summary>제목 아래 한 줄 — 요청자 · 담당자. 여러 날에 걸친 일정은 날짜 구간을 덧붙인다(시각은 적지 않는다).</summary>
    private static string? SubText(ScheduleMailItem item)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Requester)) parts.Add($"요청자 {Enc(item.Requester.Trim())}");
        if (!string.IsNullOrWhiteSpace(item.Assignee)) parts.Add($"담당자 {Enc(item.Assignee.Trim())}");

        var startDay = DateOnly.FromDateTime(item.Start);
        var endDay = DateOnly.FromDateTime(item.End < item.Start ? item.Start : item.End);

        if (endDay > startDay)
        {
            parts.Add($"{Md(startDay)}({Dow(startDay)}) ~ {Md(endDay)}({Dow(endDay)}) · {endDay.DayNumber - startDay.DayNumber + 1}일간");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>추가대상 딱지. 값이 없으면 그리지 않는다.</summary>
    private static string? TargetBadge(string? code)
    {
        if (AddTargets.Name(code) is not { } name)
        {
            return null;
        }

        var (fg, bg) = code!.Trim().ToUpperInvariant() switch
        {
            "ADD" => ("#1a7f37", "#e6f4ea"),
            "CHANGE" => ("#b35900", "#fff1e0"),
            "MOVE" => ("#2f6fdb", "#e8f0fe"),
            _ => (Muted, "#f0f2f5"),
        };

        return $"<span style=\"display:inline-block;padding:3px 10px;border-radius:10px;font-size:11px;font-weight:700;line-height:14px;color:{fg};background:{bg};border:1px solid {fg};\">{Enc(name)}</span>";
    }

    private static string Md(DateOnly d) => $"{d.Month}/{d.Day}";

    private static string Enc(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
