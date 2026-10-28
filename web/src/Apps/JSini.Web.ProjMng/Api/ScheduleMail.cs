using System.Globalization;
using System.Net;
using System.Text;
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

/// <summary>메일에 실을 일정 한 건. 날짜는 일정표가 그리는 그 벽시계 값이다.</summary>
public sealed record ScheduleMailItem(DateTime Start, DateTime End, string Title);

/// <summary>
/// 오늘을 가운데 두고 앞뒤 일주일의 일정을 세로 타임라인으로 그린 메일 본문.
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

    public static (DateOnly From, DateOnly To) Window(DateOnly today) =>
        (today.AddDays(-DaysAround), today.AddDays(DaysAround));

    public static string Subject(DateOnly today, string? projectName)
    {
        var (from, to) = Window(today);
        var head = string.IsNullOrWhiteSpace(projectName) ? "일정 보고" : $"{projectName.Trim()} 일정 보고";

        return $"[{head}] {from:yyyy.MM.dd}({Dow(from)}) ~ {to:yyyy.MM.dd}({Dow(to)})";
    }

    public static string Dow(DateOnly day) => Ko.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek);

    /// <summary>타임라인에 올라갈 날짜. 구간보다 먼저 시작해 걸쳐 들어온 일정은 구간 첫날에 둔다.</summary>
    public static DateOnly PlacedOn(ScheduleMailItem item, DateOnly from) =>
        DateOnly.FromDateTime(item.Start) < from ? from : DateOnly.FromDateTime(item.Start);

    public static string Build(
        DateOnly today,
        IEnumerable<ScheduleMailItem> items,
        string? projectName,
        string? senderName,
        string? message)
    {
        var (from, to) = Window(today);

        var days = items
            .Where(i => DateOnly.FromDateTime(i.End < i.Start ? i.Start : i.End) >= from
                        && DateOnly.FromDateTime(i.Start) <= to)
            .GroupBy(i => PlacedOn(i, from))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Start).ThenBy(i => i.Title, StringComparer.Ordinal).ToList());

        var past = days.Where(d => d.Key < today).Sum(d => d.Value.Count);
        var todayCount = days.GetValueOrDefault(today)?.Count ?? 0;
        var ahead = days.Where(d => d.Key > today).Sum(d => d.Value.Count);

        var sb = new StringBuilder();

        sb.Append($"<div style=\"margin:0;padding:24px 0;background:#f3f5f8;font-family:{Font};\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");
        sb.Append("<tr><td align=\"center\">");
        sb.Append($"<table role=\"presentation\" width=\"640\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;max-width:640px;background:#ffffff;border:1px solid {Line};border-radius:10px;overflow:hidden;\">");

        // 머리띠
        sb.Append($"<tr><td style=\"background:{Navy};padding:28px 32px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:2px;color:#a9bcd6;\">SCHEDULE REPORT</div>");
        sb.Append($"<div style=\"margin-top:6px;font-size:22px;font-weight:700;color:#ffffff;\">{Enc(string.IsNullOrWhiteSpace(projectName) ? "프로젝트" : projectName.Trim())} 일정 보고</div>");
        sb.Append($"<div style=\"margin-top:8px;font-size:13px;color:#d5e0ee;\">{from:yyyy.MM.dd}({Dow(from)}) ~ {to:yyyy.MM.dd}({Dow(to)}) · 기준일 {today:yyyy.MM.dd}({Dow(today)})</div>");
        sb.Append("</td></tr>");

        // 인사
        sb.Append("<tr><td style=\"padding:28px 32px 8px 32px;font-size:14px;line-height:1.7;color:#333333;\">");
        if (!string.IsNullOrWhiteSpace(message))
        {
            sb.Append(Enc(message.Trim()).Replace("\r\n", "\n").Replace("\n", "<br>"));
        }
        else
        {
            sb.Append("안녕하십니까.<br>");
            sb.Append($"오늘({today.Month}월 {today.Day}일)을 기준으로 지난 일주일간 진행된 일정과 앞으로 일주일간 예정된 일정을 정리하여 보고드립니다.");
        }
        sb.Append("</td></tr>");

        // 요약 세 칸
        sb.Append("<tr><td style=\"padding:16px 32px 8px 32px;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        sb.Append(Stat("지난 일주일", past, Muted, "#f6f8fa"));
        sb.Append("<td width=\"12\"></td>");
        sb.Append(Stat("오늘", todayCount, Accent, "#eef4ff"));
        sb.Append("<td width=\"12\"></td>");
        sb.Append(Stat("앞으로 일주일", ahead, Navy, "#f6f8fa"));
        sb.Append("</tr></table>");
        sb.Append("</td></tr>");

        // 타임라인
        sb.Append("<tr><td style=\"padding:16px 32px 8px 32px;\">");

        if (days.Count == 0)
        {
            sb.Append($"<div style=\"padding:28px 0;text-align:center;font-size:14px;color:{Muted};border:1px dashed {Line};border-radius:8px;\">이 기간에 기록된 일정이 없습니다.</div>");
        }
        else
        {
            var pastDays = days.Where(d => d.Key < today).ToList();
            var nextDays = days.Where(d => d.Key >= today).ToList();

            if (pastDays.Count > 0)
            {
                sb.Append(Section("지난 일주일", "진행된 일정"));
                sb.Append(Timeline(pastDays, today));
            }

            if (!days.ContainsKey(today))
            {
                nextDays.Insert(0, new KeyValuePair<DateOnly, List<ScheduleMailItem>>(today, []));
            }

            sb.Append(Section("오늘 · 앞으로 일주일", "예정된 일정"));
            sb.Append(Timeline(nextDays, today));
        }

        sb.Append("</td></tr>");

        // 맺음
        sb.Append("<tr><td style=\"padding:20px 32px 28px 32px;font-size:14px;line-height:1.7;color:#333333;\">");
        sb.Append("일정에 변동이 생기면 다시 말씀드리겠습니다.<br>감사합니다.");
        if (!string.IsNullOrWhiteSpace(senderName))
        {
            sb.Append($"<div style=\"margin-top:14px;font-weight:700;color:{Navy};\">{Enc(senderName.Trim())} 드림</div>");
        }
        sb.Append("</td></tr>");

        sb.Append($"<tr><td style=\"padding:14px 32px;background:#f6f8fa;border-top:1px solid {Line};font-size:11px;color:#8a96a3;\">");
        sb.Append("본 메일은 JSini 포털 프로젝트 일정표에서 발송되었습니다.");
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table></div>");

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

    private static string Timeline(IEnumerable<KeyValuePair<DateOnly, List<ScheduleMailItem>>> days, DateOnly today)
    {
        var sb = new StringBuilder();

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">");

        foreach (var (day, list) in days)
        {
            var isToday = day == today;
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var dowColor = day.DayOfWeek == DayOfWeek.Sunday ? "#d64545"
                : day.DayOfWeek == DayOfWeek.Saturday ? "#2f6fdb" : Muted;
            var boxBg = isToday ? Accent : weekend ? "#f6f8fa" : "#ffffff";
            var boxFg = isToday ? "#ffffff" : Navy;
            var dot = isToday ? Accent : day < today ? "#b7c2ce" : Navy;

            sb.Append("<tr>");

            // 날짜 상자: 월 · 일 · 요일
            sb.Append("<td width=\"74\" valign=\"top\" style=\"padding:0 0 14px 0;\">");
            sb.Append($"<table role=\"presentation\" width=\"64\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:{boxBg};border:1px solid {(isToday ? Accent : Line)};border-radius:8px;\"><tr><td align=\"center\" style=\"padding:6px 0;\">");
            sb.Append($"<div style=\"font-size:11px;color:{(isToday ? "#dbe7ff" : Muted)};\">{day.Month}월</div>");
            sb.Append($"<div style=\"font-size:22px;line-height:26px;font-weight:700;color:{boxFg};\">{day.Day}</div>");
            sb.Append($"<div style=\"font-size:11px;font-weight:700;color:{(isToday ? "#ffffff" : dowColor)};\">{Ko.DateTimeFormat.GetDayName(day.DayOfWeek)}</div>");
            sb.Append("</td></tr></table>");
            sb.Append("</td>");

            // 세로 줄과 점
            sb.Append($"<td width=\"22\" valign=\"top\" style=\"padding:0;border-left:2px solid {Line};\">");
            sb.Append($"<div style=\"width:12px;height:12px;margin:22px 0 0 -9px;border-radius:50%;background:{dot};border:2px solid #ffffff;\"></div>");
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
                var period = PeriodText(item);

                sb.Append($"<div style=\"margin:0 0 6px 0;padding:10px 14px;background:{(isToday ? "#f4f8ff" : "#fbfcfd")};border:1px solid {Line};border-left:3px solid {(day < today ? "#b7c2ce" : Accent)};border-radius:6px;\">");
                sb.Append($"<div style=\"font-size:14px;font-weight:700;color:#222222;\">{Enc(item.Title)}</div>");
                if (period is not null)
                {
                    sb.Append($"<div style=\"margin-top:3px;font-size:12px;color:{Muted};\">{period}</div>");
                }
                sb.Append("</div>");
            }

            sb.Append("</td></tr>");
        }

        sb.Append("</table>");

        return sb.ToString();
    }

    /// <summary>제목 아래 한 줄. 하루짜리 종일 일정은 날짜 상자가 이미 말하므로 적지 않는다.</summary>
    private static string? PeriodText(ScheduleMailItem item)
    {
        var start = item.Start;
        var end = item.End < item.Start ? item.Start : item.End;
        var hasTime = start.TimeOfDay != TimeSpan.Zero || end.TimeOfDay != TimeSpan.Zero;
        var startDay = DateOnly.FromDateTime(start);
        var endDay = DateOnly.FromDateTime(end);

        if (startDay == endDay)
        {
            return hasTime ? $"{start:HH:mm} ~ {end:HH:mm}" : null;
        }

        var range = hasTime
            ? $"{Md(startDay)}({Dow(startDay)}) {start:HH:mm} ~ {Md(endDay)}({Dow(endDay)}) {end:HH:mm}"
            : $"{Md(startDay)}({Dow(startDay)}) ~ {Md(endDay)}({Dow(endDay)})";

        var days = endDay.DayNumber - startDay.DayNumber + 1;

        return $"{range} · {days}일간";
    }

    private static string Md(DateOnly d) => $"{d.Month}/{d.Day}";

    private static string Enc(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
