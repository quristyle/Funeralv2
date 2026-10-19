using System.Net;
using System.Text;
using JSini.Web.Admin.Api;
using JSini.Web.Http;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>메일 직발송(notification/emails/send)의 봉투 모양을 못 박는다. 서버는 ApiResponse&lt;bool&gt; 로 답하는데 화면이 객체로 읽으려 해서 모든 발송이 응답 해석 실패로 떴다.
/// 메일은 그때마다 실제로 나갔으므로 같은 메일이 여러 통 갔다. 빌드와 화면으로는 안 보여 봉투 한 줄을 여기 적어 둔다.</summary>
public sealed class EmailSendEnvelopeTests
{
    /// <summary>운영 NotificationServer 가 실제로 돌려준 본문 그대로다.</summary>
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

    /// <summary>꺼 둔 사람뿐이면 거짓이다. 예외가 아니라 설정을 존중한 결과라 서버가 200 으로 답한다 — 화면은 이 값으로 「보냈습니다」를 가른다.</summary>
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
