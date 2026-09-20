namespace AuthServer.Services;

/// <summary>
/// 계정 관련 메일 본문 템플릿 — <b>메일 HTML 은 전부 여기서만 만든다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 자리와 규칙은 SiteServer 의 <c>InquiryEmailTemplates</c> 와 같다. 색값도
/// 그쪽에서 그대로 가져왔다 — 같은 회사가 보내는 메일이 서로 다른 톤으로
/// 말하지 않게 한다. 셋째 서비스가 메일을 보내게 되면 그때 공용으로 올린다
/// (두 곳이면 복제, 세 번째부터 승격).
/// </para>
///
/// <para>
/// [메일은 웹이 아니다 — 여기서 쓸 수 없는 것들]
/// </para>
///
/// <list type="bullet">
///   <item><description><b>외부 CSS · 웹폰트 · 스크립트</b>. 클래스를 쓰지 않고
///   인라인 스타일만 쓴다.</description></item>
///   <item><description><b>외부 이미지</b>. 대부분의 클라이언트가 기본으로
///   차단해서 깨진 상자만 남는다. 그래서 로고도 SVG 가 아니라
///   <b>글자로 짠 워드마크</b>다.</description></item>
///   <item><description><b>flex · grid</b>. Outlook 이 못 읽는다. 뼈대는
///   <c>&lt;table&gt;</c> 로 짠다.</description></item>
/// </list>
///
/// <para>
/// [단추도 표로 짠다]
/// </para>
///
/// <para>
/// 여백 준 <c>&lt;a&gt;</c> 하나로 단추를 만들면 Outlook 에서 여백이 죽어
/// <b>밑줄 친 글자 한 줄</b>이 된다. 칸 하나짜리 표를 쓰고 바탕색은 그 칸에,
/// 여백은 링크에 준다 — 이러면 칸 전체가 눌린다.
/// </para>
///
/// <para>
/// [단추 밑에 주소를 그대로 한 번 더 적는다]
/// </para>
///
/// <para>
/// 회사 메일 게이트웨이가 링크를 자기 추적 주소로 바꿔 두는 곳이 있고,
/// 텍스트 전용으로 읽는 사람도 있다. 그럴 때 <b>손으로 복사할 주소</b>가
/// 없으면 메일이 도착해도 쓸 수 없다.
/// </para>
/// </remarks>
public static class AccountEmailTemplates
{
    // 브랜드 색 (소개 사이트 · 문의 메일과 같은 값)
    private const string Ink = "#1a1a1a";
    private const string Steel = "#6b7280";
    private const string Mist = "#e5e7eb";
    private const string Paper = "#fafafa";

    /// <summary>비밀번호 재설정 메일의 제목.</summary>
    public const string PasswordResetSubject = "[JSini 포털] 비밀번호 다시 정하기";

    /// <summary>
    /// 비밀번호 재설정 링크를 담은 메일 본문.
    /// </summary>
    /// <param name="who">받는 사람을 부르는 이름 (실명, 없으면 아이디).</param>
    /// <param name="link">재설정 주소. <b>이미 조립된 절대 주소</b>여야 한다.</param>
    /// <param name="lifetimeMinutes">링크 수명(분). 본문에 그대로 적는다.</param>
    public static string PasswordReset(string who, string link, int lifetimeMinutes)
    {
        // 주소는 두 곳에 들어간다 — href 와 눈에 보이는 글자. 둘 다 이스케이프한다.
        // (토큰은 base64url 이라 `&` 가 나올 일이 없지만, 주소 조립이 바뀌어도
        //  여기서 깨지지 않게 둔다.)
        var url = Escape(link);

        return Frame(
            preheader: $"{lifetimeMinutes}분 안에 새 비밀번호를 정해 주십시오.",
            inner: $"""
                <p style="margin:0 0 6px; font-size:12px; letter-spacing:2px; text-transform:uppercase; color:{Steel};">비밀번호 재설정</p>
                <h2 style="margin:0 0 16px; font-size:20px; line-height:1.4; font-weight:bold; color:{Ink};">{Escape(who)} 님, 안녕하세요.</h2>

                <p style="margin:0 0 28px; font-size:15px; line-height:1.8; color:{Ink};">
                  JSini 업무 포털의 비밀번호를 다시 정하시려면 아래 단추를 눌러 주십시오.
                </p>

                {Button("비밀번호 다시 정하기", link)}

                <p style="margin:24px 0 0; font-size:13px; line-height:1.7; color:{Steel};">
                  단추가 눌리지 않으면 아래 주소를 복사해 브라우저 주소창에 붙여 넣어 주십시오.
                </p>
                <p style="margin:8px 0 0; padding:12px 14px; background:{Paper}; border:1px solid {Mist}; font-size:12px; line-height:1.6; color:{Steel}; word-break:break-all;">
                  <a href="{url}" style="color:{Steel}; text-decoration:none;">{url}</a>
                </p>

                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="width:100%; margin-top:32px; border-top:1px solid {Mist};">
                  <tr>
                    <td style="padding-top:20px; font-size:13px; line-height:1.8; color:{Steel};">
                      <strong style="color:{Ink};">이 링크는 {lifetimeMinutes}분 동안만 쓸 수 있습니다.</strong>
                      한 번 쓰면 사라지고, 새로 요청하시면 앞서 받으신 링크는 바로 쓸 수 없게 됩니다.
                    </td>
                  </tr>
                  <tr>
                    <td style="padding-top:12px; font-size:13px; line-height:1.8; color:{Steel};">
                      요청하신 적이 없다면 이 메일을 버리셔도 됩니다.
                      <strong style="color:{Ink};">링크를 누르지 않는 한 비밀번호는 그대로입니다.</strong>
                    </td>
                  </tr>
                </table>
                """);
    }

    /// <summary>
    /// 표로 짠 단추. 바탕은 칸이 깔고 여백은 링크가 준다 —
    /// 그래야 Outlook 에서도 칸 전체가 눌린다(머리말 참고).
    /// </summary>
    private static string Button(string label, string href) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0">
          <tr>
            <td style="background:{Ink};">
              <a href="{Escape(href)}"
                 style="display:inline-block; padding:14px 32px; font-size:15px; font-weight:bold; letter-spacing:-0.01em; color:#ffffff; text-decoration:none;">{Escape(label)}</a>
            </td>
          </tr>
        </table>
        """;

    /// <summary>
    /// 공통 틀 — 상단 브랜드 줄 + 본문 + 하단 서명.
    /// </summary>
    /// <param name="preheader">
    /// 메일함 목록에서 제목 옆에 따라붙는 <b>미리보기 글</b>. 이것을 안 넣으면
    /// 클라이언트가 본문 첫 글자를 아무렇게나 끌어다 쓴다. 화면에는 안 보이게
    /// 감춘다.
    /// </param>
    /// <param name="inner">카드 안에 들어갈 본문 HTML.</param>
    private static string Frame(string preheader, string inner) => $"""
        <!doctype html>
        <html lang="ko">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <meta name="color-scheme" content="light only" />
          <title>{Escape(PasswordResetSubject)}</title>
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
