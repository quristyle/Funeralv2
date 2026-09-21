using System.Text;
using System.Text.RegularExpressions;

namespace NotificationServer.Services;

/// <summary>
/// <b>평문으로 들어온 알림 한 통을 회사 메일 꼴로 입힌다.</b>
/// </summary>
/// <remarks>
/// <para>
/// [왜 서버가 입히나]
/// </para>
///
/// <para>
/// 메일을 보내는 자리마다 자기 틀을 들고 있었다 — AuthServer 는
/// <c>AccountEmailTemplates</c>, SiteServer 는 <c>InquiryEmailTemplates</c>,
/// ProjMngServer 는 <c>AiTaskNotifier.Body</c>. 셋 다 <c>html = true</c> 로
/// 보내므로 제 모양이 나왔다. <b>틀이 없는 길이 하나 남아 있었다</b> —
/// 업무 화면의 「알림 보내기」(생일 축하 등)는 사람이 친 글을 그대로
/// <c>html = false</c> 로 넘겼고, 받는 쪽 메일함에는 <b>글자만 덩그러니</b>
/// 도착했다. 회사가 보낸 메일로 보이지 않는다.
/// </para>
///
/// <para>
/// 그 틀을 화면(Blazor)에 두지 않은 까닭은, <b>평문으로 보내는 부르는 쪽이
/// 앞으로도 늘기 때문</b>이다. HTML 을 만들어 실어 보내게 하면 그 수만큼
/// 틀이 복제되고, 어느 하나가 어긋나도 메일함에서만 보인다. 여기서 한 번
/// 입히면 <c>/emails/send</c> 로 오는 <b>모든</b> 평문이 같은 얼굴이 된다.
/// </para>
///
/// <para>
/// [이미 HTML 로 오는 것은 손대지 않는다]
/// </para>
///
/// <para>
/// <c>html = true</c> 로 온 본문은 부르는 쪽이 이미 완성된 문서(<c>&lt;html&gt;</c>
/// 통째)를 보낸 것이다. 거기에 또 틀을 씌우면 문서가 두 겹이 되어 클라이언트마다
/// 다르게 무너진다. <b>평문일 때만</b> 이 틀을 쓴다.
/// </para>
///
/// <para>
/// [메일은 웹이 아니다]
/// </para>
///
/// <list type="bullet">
///   <item><description><b>인라인 스타일만.</b> <c>&lt;style&gt;</c> 블록은
///   지워지는 클라이언트가 있다.</description></item>
///   <item><description><b>뼈대는 <c>&lt;table&gt;</c>.</b> Outlook 이
///   <c>flex</c>·<c>grid</c> 를 못 읽는다.</description></item>
///   <item><description><b>이미지를 쓰지 않는다.</b> 기본 차단이라 깨진 상자만
///   남는다 — 로고도 글자로 짠 워드마크다.</description></item>
/// </list>
///
/// <para>
/// 색값과 뼈대는 <c>AuthServer.Services.AccountEmailTemplates</c> 에서 그대로
/// 가져왔다. <b>같은 회사가 보내는 메일이 서로 다른 톤으로 말하지 않게</b>
/// 하는 것이 목적이라, 저쪽을 고치면 여기도 같이 고친다.
/// </para>
/// </remarks>
public static class NoticeEmailTemplate
{
    // 브랜드 색 (비밀번호 찾기 메일 · 소개 사이트 문의 메일과 같은 값)
    private const string Ink = "#1a1a1a";
    private const string Steel = "#6b7280";
    private const string Mist = "#e5e7eb";
    private const string Paper = "#fafafa";

    /// <summary>
    /// 미리보기 글에 담을 길이. 메일함 목록이 잘라 보여 주는 만큼만 넣는다 —
    /// 더 넣어 봐야 감춰진 글자로 남는다.
    /// </summary>
    private const int PreheaderLength = 90;

    /// <summary>
    /// 평문 알림 한 통을 HTML 메일로 만든다.
    /// </summary>
    /// <param name="subject">제목. <b>카드의 큰 글씨가 된다.</b></param>
    /// <param name="plainBody">
    /// 사람이 친 본문(평문). 빈 줄로 나뉜 덩이가 문단이 되고 그 안의 줄바꿈은
    /// 그대로 살린다 — <b>친 대로 보이는 것</b>이 이 틀의 약속이다.
    /// </param>
    /// <param name="senderName">
    /// 보낸 사람 이름. 있으면 본문 아래 서명 줄에 적는다. <b>받은 쪽이 먼저
    /// 묻는 것이 「누가 보냈나」</b>라서다. 없으면 그 줄을 통째로 뺀다.
    /// </param>
    public static string Render(string subject, string plainBody, string? senderName = null)
    {
        var title = (subject ?? string.Empty).Trim();
        var text = Normalize(plainBody);

        // **제목과 본문이 같으면 본문을 뺀다.** 부르는 쪽은 내용 칸이 비었을 때
        // 제목을 본문으로 삼아 보낸다(빈 메일보다 낫다). 그대로 두면 같은
        // 문장이 큰 글씨와 작은 글씨로 두 번 적힌 메일이 된다.
        var duplicated = string.Equals(text, title, StringComparison.Ordinal);

        var inner = new StringBuilder();

        inner.Append($"""
            <p style="margin:0 0 6px; font-size:12px; letter-spacing:2px; text-transform:uppercase; color:{Steel};">알림</p>
            <h2 style="margin:0; font-size:20px; line-height:1.5; font-weight:bold; color:{Ink};">{Escape(title)}</h2>
            """);

        if (!duplicated && text.Length > 0)
        {
            inner.Append($"""

                <div style="margin-top:20px; padding-top:20px; border-top:1px solid {Mist};">
                  {Paragraphs(text)}
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

        return Frame(title, Preheader(duplicated ? title : text), inner.ToString());
    }

    /// <summary>
    /// 같은 내용의 <b>평문 갈래</b>. HTML 과 함께 실어 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HTML 만 실은 메일은 <b>평문으로만 읽는 클라이언트에서 빈 칸</b>이 되고,
    /// 스팸 점수도 올라간다. 원래 들어온 글이 이미 평문이므로 갈래를 만드는
    /// 값이 거의 들지 않는다 — 안 실을 이유가 없다.
    /// </para>
    /// </remarks>
    public static string PlainAlternative(string subject, string plainBody, string? senderName = null)
    {
        var title = (subject ?? string.Empty).Trim();
        var text = Normalize(plainBody);
        var sb = new StringBuilder();

        sb.Append(title);

        if (!string.Equals(text, title, StringComparison.Ordinal) && text.Length > 0)
        {
            sb.Append("\n\n").Append(text);
        }

        if (!string.IsNullOrWhiteSpace(senderName))
        {
            sb.Append("\n\n보낸 사람 ").Append(senderName.Trim());
        }

        sb.Append("\n\n— JSini 업무 포털 (발신 전용)");

        return sb.ToString();
    }

    /// <summary>줄바꿈을 한 가지로 맞추고 앞뒤 빈 줄을 턴다.</summary>
    private static string Normalize(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    /// <summary>
    /// 메일함 목록에서 제목 옆에 따라붙는 미리보기 글. <b>안 넣으면</b>
    /// 클라이언트가 본문 첫 글자를 아무렇게나 끌어다 쓴다.
    /// </summary>
    private static string Preheader(string text)
    {
        var flat = Regex.Replace(text, @"\s+", " ").Trim();

        return flat.Length <= PreheaderLength
            ? flat
            : flat[..PreheaderLength] + "…";
    }

    /// <summary>
    /// 평문을 문단으로 바꾼다 — 빈 줄이 문단을 가르고 한 줄 줄바꿈은
    /// <c>&lt;br&gt;</c> 로 살린다.
    /// </summary>
    /// <remarks>
    /// <b>이스케이프가 먼저고 주소 잇기가 나중이다.</b> 순서를 바꾸면 우리가
    /// 넣은 <c>&lt;a&gt;</c> 가 다시 이스케이프되어 태그가 글자로 보인다.
    /// </remarks>
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

    /// <summary>
    /// 본문에 적힌 <c>http(s)</c> 주소를 누를 수 있게 만든다.
    /// </summary>
    /// <remarks>
    /// <b>이미 이스케이프된 글자를 받는다</b>(위 주석). 그래서 <c>&amp;amp;</c>
    /// 같은 꼴이 주소 안에 들어 있어도 그대로 두면 된다 — href 에서도 같은
    /// 뜻이다. 문장 끝의 마침표·괄호는 주소에서 뗀다: 「…를 여십시오.」처럼
    /// 붙여 쓴 글에서 마침표까지 링크에 들어가면 그 주소는 열리지 않는다.
    /// </remarks>
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

    /// <summary>
    /// 공통 틀 — 상단 브랜드 줄 + 본문 카드 + 하단 서명.
    /// </summary>
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
