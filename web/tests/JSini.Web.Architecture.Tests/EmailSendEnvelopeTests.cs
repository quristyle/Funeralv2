using System.Net;
using System.Text;
using JSini.Web.Admin.Api;
using JSini.Web.Http;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 메일 직발송(<c>notification/emails/send</c>)의 <b>봉투 모양</b>을 못 박는다.
///
/// <para>
/// [무슨 일이 있었나]
/// </para>
///
/// <para>
/// 서버는 <c>ApiResponse&lt;bool&gt;</c> 로 답한다 — 봉투 안쪽이
/// <c>data.result: [true]</c> 다. 그런데 화면 클라이언트가 그것을 객체
/// (<c>EmailSendResultDto</c>)로 읽으려 했다. <see cref="GatewayClient"/> 는
/// 봉투를 벗기다 <c>JsonException</c> 을 만나면 <b>「응답을 해석하지
/// 못했습니다」</b> 로 바꿔 던지므로, 화면에는 <b>언제나</b>
/// 「메일을 보내지 못했습니다 — 응답을 해석하지 못했습니다」가 떴다.
/// </para>
///
/// <para>
/// <b>메일은 그때마다 실제로 나갔다.</b> 200 은 SMTP 가 받아 준 다음에만
/// 오기 때문이다. 그래서 보낸 사람은 실패한 줄 알고 다시 눌렀고, 받는 사람은
/// 같은 메일을 세 통 받았다(2026-09-28, <c>scom.push_send_logs</c>).
/// </para>
///
/// <para>
/// [왜 테스트로 막는가]
/// </para>
///
/// <para>
/// 이 어긋남은 <b>빌드로도 화면으로도 안 보인다.</b> 타입이 맞는지는
/// 런타임에 오는 JSON 만 알고, 그 JSON 은 다른 서비스가 만든다. 봉투 한 줄을
/// 여기 적어 두면 어느 쪽이 모양을 바꿔도 이 자리에서 걸린다.
/// </para>
/// </summary>
public sealed class EmailSendEnvelopeTests
{
    /// <summary>
    /// 운영 NotificationServer 가 실제로 돌려준 본문 그대로다
    /// (<c>POST /emails/send</c> 를 시험 기동한 인스턴스에 불러 받은 것).
    /// </summary>
    private const string SentEnvelope =
        """
        {"success":true,"code":"S000","message":"메일을 보냈습니다.",
         "data":{"result":[true],"page":{"total":1}},
         "timestamp":"2026-09-28T08:11:16.5406225Z","traceId":null,"path":null,"realmessage":null}
        """;

    /// <summary>받는 사람이 모두 메일을 꺼 둔 경우. <b>200 인데 거짓</b>이다.</summary>
    private const string NotSentEnvelope =
        """
        {"success":true,"code":"S000","message":"받는 사람이 알림을 끄고 있어 보내지 않았습니다: kdh",
         "data":{"result":[false],"page":{"total":1}},
         "timestamp":"2026-09-28T08:11:16.5406225Z","traceId":null,"path":null,"realmessage":null}
        """;

    [Fact]
    public async Task 보냈으면_참을_돌려준다()
    {
        var sent = await SendAsync(SentEnvelope);

        Assert.True(sent);
    }

    /// <summary>
    /// 꺼 둔 사람뿐이면 거짓이다. <b>예외가 아니다</b> — 설정을 존중한 결과라
    /// 서버가 200 으로 답한다. 화면은 이 값으로 「보냈습니다」를 가른다.
    /// </summary>
    [Fact]
    public async Task 안_보냈으면_거짓을_돌려준다()
    {
        var sent = await SendAsync(NotSentEnvelope);

        Assert.False(sent);
    }

    private static async Task<bool> SendAsync(string envelope)
    {
        using var http = new HttpClient(new StubHandler(envelope))
        {
            BaseAddress = new Uri("http://localhost/api/"),
        };

        var api = new AdminClient(new GatewayClient(http));

        return await api.SendEmailAsync(new EmailSendRequest
        {
            To = "someone@example.com",
            Subject = "제목",
            Body = "<p>본문</p>",
            Html = true,
        });
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
