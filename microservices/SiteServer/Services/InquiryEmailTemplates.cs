using System.Text;
using SiteServer.DTOs;
using SiteServer.Entities;

namespace SiteServer.Services;

/// <summary>
/// 문의 관련 메일 본문 템플릿 — **메일 HTML 은 전부 여기서만 만든다.**
///
/// 메일 클라이언트는 외부 CSS · 웹폰트 · 스크립트를 지원하지 않으므로
/// 인라인 스타일 + 표 없는 단순 블록으로만 짠다. 외부 이미지도 넣지 않는다
/// (수신측이 차단해 깨진 상자만 남는다).
///
/// 문의 본문(Message)은 저장 전에 <see cref="InquiryHtmlSanitizer"/> 가 거른
/// HTML 이라는 전제로 그대로 끼워 넣는다.
/// </summary>
public static class InquiryEmailTemplates
{
    // 브랜드 잉크 색 (docs/brand — 소개 사이트와 같은 톤)
    private const string Ink = "#1a1a1a";
    private const string Steel = "#6b7280";
    private const string Mist = "#e5e7eb";
    private const string Paper = "#fafafa";

    /// <summary>접수 알림 — 담당자에게 가는 메일.</summary>
    public static (string Subject, string Body) Received(Guid inquiryId, InquiryRequestDto req)
    {
        var subject = $"[JSini 문의] {req.Name} — {req.Subject}";

        var rows = new StringBuilder();
        AppendRow(rows, "이름", Escape(req.Name));
        AppendRow(rows, "이메일", Escape(req.Email));
        if (!string.IsNullOrWhiteSpace(req.Company)) AppendRow(rows, "회사", Escape(req.Company));
        if (!string.IsNullOrWhiteSpace(req.Phone)) AppendRow(rows, "연락처", Escape(req.Phone));
        if (!string.IsNullOrWhiteSpace(req.Category)) AppendRow(rows, "분류", Escape(req.Category));
        AppendRow(rows, "접수번호", inquiryId.ToString());

        var body = Frame($"""
            <p style="margin:0 0 4px; font-size:12px; letter-spacing:2px; text-transform:uppercase; color:{Steel};">새 문의가 접수됐습니다</p>
            <h2 style="margin:0 0 20px; font-size:18px; color:{Ink};">{Escape(req.Subject)}</h2>
            <table cellpadding="0" cellspacing="0" style="width:100%; font-size:14px; color:{Ink}; border-top:1px solid {Mist};">
              {rows}
            </table>
            <div style="margin-top:24px; padding:16px 20px; background:{Paper}; border:1px solid {Mist}; font-size:14px; line-height:1.7; color:{Ink};">
              {req.Message}
            </div>
            <p style="margin:24px 0 0; font-size:12px; color:{Steel};">
              이 메일은 회사 소개 사이트의 문의 폼에서 자동 발송됐습니다.
              답장은 포털의 [사이트 문의내역] 화면에서 보낼 수 있습니다.
            </p>
            """);

        return (subject, body);
    }

    /// <summary>답장 — 문의한 사람에게 가는 메일.</summary>
    public static string Reply(string replyHtml, SiteInquiry original)
    {
        return Frame($"""
            <p style="margin:0 0 4px; font-size:12px; letter-spacing:2px; text-transform:uppercase; color:{Steel};">문의에 대한 답변입니다</p>
            <h2 style="margin:0 0 20px; font-size:18px; color:{Ink};">{Escape(original.Name)} 님, 안녕하세요.</h2>
            <div style="font-size:14px; line-height:1.8; color:{Ink};">
              {replyHtml}
            </div>
            <div style="margin-top:32px; padding-top:16px; border-top:1px solid {Mist};">
              <p style="margin:0 0 8px; font-size:12px; color:{Steel};">
                보내신 문의 · {original.CreatedAt:yyyy-MM-dd} · {Escape(original.Subject)}
              </p>
              <div style="padding:12px 16px; background:{Paper}; border-left:3px solid {Mist}; font-size:13px; line-height:1.7; color:{Steel};">
                {original.Message}
              </div>
            </div>
            <p style="margin:24px 0 0; font-size:12px; color:{Steel};">
              이 메일은 문의하신 내용에 대한 답변입니다. 추가 문의는 이 메일에 회신해 주세요.
            </p>
            """);
    }

    /// <summary>공통 틀 — 상단 브랜드 줄 + 본문 카드 + 하단 서명.</summary>
    private static string Frame(string inner) => $"""
        <!doctype html>
        <html lang="ko">
        <body style="margin:0; padding:0; background:#ffffff;">
          <div style="max-width:640px; margin:0 auto; padding:32px 24px; font-family:'Apple SD Gothic Neo','Malgun Gothic',AppleGothic,sans-serif;">
            <div style="padding-bottom:16px; border-bottom:2px solid {Ink};">
              <span style="font-size:16px; font-weight:bold; letter-spacing:4px; color:{Ink};">JSINI</span>
            </div>
            <div style="padding:28px 0;">
              {inner}
            </div>
            <div style="padding-top:16px; border-top:1px solid {Mist}; font-size:12px; color:{Steel};">
              JSini · 만들고, 계속 함께 간다
            </div>
          </div>
        </body>
        </html>
        """;

    private static void AppendRow(StringBuilder sb, string label, string value) =>
        sb.Append($"""
            <tr>
              <td style="width:88px; padding:8px 0; border-bottom:1px solid {Mist}; color:{Steel}; font-size:12px; vertical-align:top;">{label}</td>
              <td style="padding:8px 0; border-bottom:1px solid {Mist};">{value}</td>
            </tr>
            """);

    private static string Escape(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
}
