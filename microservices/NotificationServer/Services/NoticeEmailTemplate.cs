using System.Text;
using System.Text.RegularExpressions;

namespace NotificationServer.Services;

/// <summary>평문으로 들어온 알림 한 통을 회사 메일 꼴로 입힌다. HTML 로 온 본문은 이미 완성된 문서라 건드리지 않는다.
/// 메일은 웹이 아니라 인라인 스타일과 table 뼈대만 쓴다. 색값은 AuthServer 의 AccountEmailTemplates 와 같게 둔다.</summary>
public static class NoticeEmailTemplate
{
    // 브랜드 색 (비밀번호 찾기 메일 · 소개 사이트 문의 메일과 같은 값)
    private const string Ink = "#1a1a1a";
    private const string Steel = "#6b7280";
    private const string Mist = "#e5e7eb";
    private const string Paper = "#fafafa";

    /// <summary>미리보기 글에 담을 길이. 메일함 목록이 잘라 보여 주는 만큼만 넣는다.</summary>
    private const int PreheaderLength = 90;

    /// <summary>평문 알림 한 통을 HTML 메일로 만든다. actionUrl 이 없으면 본문 끝의 링크 줄을 찾아 단추로 바꾼다.</summary>
    public static string Render(
        string subject,
        string plainBody,
        string? senderName = null,
        string? actionUrl = null,
        string? actionLabel = null)
    {
        var title = (subject ?? string.Empty).Trim();
        var (cleanText, btnUrl, btnLabel) = ResolveAction(plainBody, actionUrl, actionLabel);

        // 제목과 본문이 같으면 본문을 뺀다 — 같은 문장이 큰 글씨와 작은 글씨로 두 번 적히는 것을 막는다.
        var duplicated = string.Equals(cleanText, title, StringComparison.Ordinal);

        var inner = new StringBuilder();

        inner.Append($"""
            <p style="margin:0 0 6px; font-size:12px; letter-spacing:2px; text-transform:uppercase; color:{Steel};">알림</p>
            <h2 style="margin:0; font-size:20px; line-height:1.5; font-weight:bold; color:{Ink};">{Escape(title)}</h2>
            """);

        var hasBody = !duplicated && cleanText.Length > 0;
        var hasButton = !string.IsNullOrWhiteSpace(btnUrl);

        if (hasBody || hasButton)
        {
            inner.Append($"""

                <div style="margin-top:20px; padding-top:20px; border-top:1px solid {Mist};">
                """);

            if (hasBody)
            {
                inner.Append($"""
                  {Paragraphs(cleanText)}
                """);
            }

            if (hasButton)
            {
                inner.Append($"""

                  {Button(btnLabel ?? "포털에서 보기", btnUrl!)}
                """);
            }

            inner.Append("""

                </div>
                """);
        }

        if (!string.IsNullOrWhiteSpace(senderName))
        {
            inner.Append($"""

                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="width:100%; margin-top:28px;">
                  <tr>
                    <td style="padding:14px 16px; background:{Paper}; border:1px solid {Mist}; font-size:13px; line-height:1.7; color:{Steel};">
                      보낸 사람 <strong style="color:{Ink};">{Escape(senderName.Trim())}</strong>
                    </td>
                  </tr>
                </table>
                """);
        }

        return Frame(title, Preheader(duplicated ? title : cleanText), inner.ToString());
    }

    /// <summary>같은 내용의 평문 갈래. HTML 과 함께 실어 보낸다 — HTML 만 실으면 평문 클라이언트에서 빈 칸이 되고 스팸 점수도 오른다.</summary>
    public static string PlainAlternative(
        string subject,
        string plainBody,
        string? senderName = null,
        string? actionUrl = null,
        string? actionLabel = null)
    {
        var title = (subject ?? string.Empty).Trim();
        var (cleanText, btnUrl, btnLabel) = ResolveAction(plainBody, actionUrl, actionLabel);
        var sb = new StringBuilder();

        sb.Append(title);

        if (!string.Equals(cleanText, title, StringComparison.Ordinal) && cleanText.Length > 0)
        {
            sb.Append("\n\n").Append(cleanText);
        }

        if (!string.IsNullOrWhiteSpace(btnUrl))
        {
            sb.Append("\n\n").Append(btnLabel ?? "포털에서 보기").Append(": ").Append(btnUrl);
        }

        if (!string.IsNullOrWhiteSpace(senderName))
        {
            sb.Append("\n\n보낸 사람 ").Append(senderName.Trim());
        }

        sb.Append("\n\n— JSini 업무 포털 (발신 전용)");

        return sb.ToString();
    }

    private static readonly Regex TrailingActionRegex = new(
        @"(?:^|\n)\s*(?:(?<label>[^\r\n:]+?)\s*:\s*)?(?<url>https?://\S+)\s*$",
        RegexOptions.Compiled);

    /// <summary>본문과 파라미터에서 액션 버튼(URL · 라벨)을 뽑고, 본문 끝의 링크 줄을 정리해 URL 이 평문으로 노출되지 않게 한다.</summary>
    private static (string CleanBody, string? ActionUrl, string? ActionLabel) ResolveAction(
        string? plainBody, string? actionUrl, string? actionLabel)
    {
        var text = Normalize(plainBody);
        var targetUrl = actionUrl?.Trim();
        var targetLabel = actionLabel?.Trim();

        // 1) 명시적으로 actionUrl 이 넘어온 경우
        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            // 본문 끝에 해당 URL 링크 줄이 있다면 본문에서 제거 (URL 중복 및 평문 노출 방지)
            var match = TrailingActionRegex.Match(text);
            if (match.Success)
            {
                var matchedRaw = match.Groups["url"].Value.TrimEnd(".,;:!?)]}'\"".ToCharArray());
                if (string.Equals(matchedRaw, targetUrl.TrimEnd(".,;:!?)]}'\"".ToCharArray()), StringComparison.OrdinalIgnoreCase))
                {
                    text = text[..match.Index].TrimEnd();
                }
            }

            if (string.IsNullOrWhiteSpace(targetLabel))
            {
                targetLabel = DetectLabel(targetUrl);
            }

            return (text, targetUrl, targetLabel);
        }

        // 2) actionUrl 이 넘어오지 않은 경우, 본문 끝의 링크 줄 자동 탐지
        var trailingMatch = TrailingActionRegex.Match(text);
        if (trailingMatch.Success)
        {
            var matchedUrl = trailingMatch.Groups["url"].Value;
            var matchedLabel = trailingMatch.Groups["label"].Value?.Trim();

            while (matchedUrl.Length > 0 && ".,;:!?)]}'\"".Contains(matchedUrl[^1]))
            {
                matchedUrl = matchedUrl[..^1];
            }

            if (matchedUrl.Length > 0)
            {
                var isWeather = matchedUrl.Contains("/weather", StringComparison.OrdinalIgnoreCase);
                var isNote = matchedUrl.Contains("/note", StringComparison.OrdinalIgnoreCase);
                var isKnownAction = isWeather || isNote
                    || (!string.IsNullOrEmpty(matchedLabel) && (
                        matchedLabel.Contains("보기", StringComparison.Ordinal)
                        || matchedLabel.Contains("확인", StringComparison.Ordinal)
                        || matchedLabel.Contains("바로가기", StringComparison.Ordinal)
                        || matchedLabel.Contains("링크", StringComparison.Ordinal)));

                if (isKnownAction)
                {
                    text = text[..trailingMatch.Index].TrimEnd();
                    targetUrl = matchedUrl;
                    targetLabel = string.IsNullOrWhiteSpace(targetLabel)
                        ? (!string.IsNullOrWhiteSpace(matchedLabel) ? matchedLabel : DetectLabel(targetUrl))
                        : targetLabel;

                    if (isWeather && (string.IsNullOrWhiteSpace(targetLabel) || targetLabel == "포털에서 보기"))
                    {
                        targetLabel = "날씨 확인하기";
                    }

                    return (text, targetUrl, targetLabel);
                }
            }
        }

        return (text, null, null);
    }

    private static string DetectLabel(string url)
    {
        if (url.Contains("/weather", StringComparison.OrdinalIgnoreCase)) return "날씨 확인하기";
        if (url.Contains("/note", StringComparison.OrdinalIgnoreCase)) return "쪽지함에서 보기";
        return "포털에서 보기";
    }

    /// <summary>표로 짠 단추. 바탕은 칸이 깔고 여백은 링크가 준다 — Outlook 에서도 칸 전체가 눌리게 하려고.</summary>
    private static string Button(string label, string href) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin-top:24px;">
          <tr>
            <td align="center" style="background:{Ink}; border-radius:4px;">
              <a href="{Escape(href)}"
                 target="_blank"
                 style="display:inline-block; padding:12px 28px; font-size:14px; font-weight:bold; letter-spacing:-0.01em; color:#ffffff; text-decoration:none; border-radius:4px;">{Escape(label)}</a>
            </td>
          </tr>
        </table>
        """;

    /// <summary>줄바꿈을 한 가지로 맞추고 앞뒤 빈 줄을 턴다.</summary>
    private static string Normalize(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    /// <summary>메일함 목록에서 제목 옆에 붙는 미리보기 글. 안 넣으면 클라이언트가 본문 첫 글자를 아무렇게나 끌어다 쓴다.</summary>
    private static string Preheader(string text)
    {
        var flat = Regex.Replace(text, @"\s+", " ").Trim();

        return flat.Length <= PreheaderLength
            ? flat
            : flat[..PreheaderLength] + "…";
    }

    /// <summary>평문을 문단으로 바꾼다. 이스케이프가 먼저고 주소 잇기가 나중이다 — 순서를 바꾸면 넣은 a 태그가 글자로 보인다.</summary>
    private static string Paragraphs(string text)
    {
        var sb = new StringBuilder();

        foreach (var block in Regex.Split(text, @"\n{2,}"))
        {
            var lines = block
                .Split('\n')
                .Select(line => Linkify(Escape(line.Trim())));

            sb.Append($"""
                <p style="margin:0 0 14px; font-size:15px; line-height:1.85; color:{Ink}; white-space:normal; word-break:break-word;">{string.Join("<br />", lines)}</p>
                """);
        }

        return sb.ToString();
    }

    /// <summary>본문의 http(s) 주소를 누를 수 있게 만든다. 문장 끝 마침표와 괄호는 주소에서 뗀다.</summary>
    private static string Linkify(string escaped) => Regex.Replace(
        escaped,
        @"https?://[^\s<>""]+",
        match =>
        {
            var url = match.Value;
            var tail = string.Empty;

            while (url.Length > 0 && ".,;:!?)]}'\"".Contains(url[^1]))
            {
                tail = url[^1] + tail;
                url = url[..^1];
            }

            if (url.Length == 0)
            {
                return match.Value;
            }

            return $"""<a href="{url}" style="color:{Ink}; text-decoration:underline;">{url}</a>{tail}""";
        });

    /// <summary>공통 틀 — 상단 브랜드 줄 + 본문 카드 + 하단 서명.</summary>
    private static string Frame(string title, string preheader, string inner) => $"""
        <!doctype html>
        <html lang="ko">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <meta name="color-scheme" content="light only" />
          <title>{Escape(title)}</title>
        </head>
        <body style="margin:0; padding:0; background:{Paper};">
          <div style="display:none; max-height:0; overflow:hidden; opacity:0; color:transparent;">{Escape(preheader)}</div>

          <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="width:100%; background:{Paper};">
            <tr>
              <td align="center" style="padding:32px 16px;">

                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600"
                       style="width:100%; max-width:600px; background:#ffffff; border:1px solid {Mist}; font-family:'Apple SD Gothic Neo','Malgun Gothic',AppleGothic,-apple-system,BlinkMacSystemFont,sans-serif;">

                  <!-- 브랜드 줄. 로고는 이미지가 아니라 글자다(머리말 참고). -->
                  <tr>
                    <td style="padding:24px 32px; border-bottom:2px solid {Ink};">
                      <span style="font-size:16px; font-weight:bold; letter-spacing:4px; color:{Ink};">JSINI</span>
                    </td>
                  </tr>

                  <tr>
                    <td style="padding:36px 32px;">
                      {inner}
                    </td>
                  </tr>

                  <tr>
                    <td style="padding:20px 32px; background:{Paper}; border-top:1px solid {Mist}; font-size:12px; line-height:1.7; color:{Steel};">
                      이 메일은 발신 전용입니다 — 회신하셔도 읽는 사람이 없습니다.<br />
                      JSini · 만들고, 계속 함께 간다
                    </td>
                  </tr>

                </table>

              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    private static string Escape(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
}
