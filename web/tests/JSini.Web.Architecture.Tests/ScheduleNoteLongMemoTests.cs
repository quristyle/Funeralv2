using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 긴 일정 메모가 통째로 기본값 한 건으로 떨어지던 길(<see cref="ScheduleNoteAssist.DraftManyAsync"/>).
/// 가짜 모델은 진짜처럼 답의 길이에 상한이 있어, 일정이 열몇 건이면 JSON 이 중간에 끊긴다.
/// </summary>
public sealed class ScheduleNoteLongMemoTests
{
    private static readonly Regex Dated = new(@"^\s*(?<t>\[.+?\].+?)\s+(?<d>\d{4}-\d{2}-\d{2})\s*$", RegexOptions.Multiline);

    /// <summary>메모 칸의 날짜 줄마다 한 건을 지어 주되 답을 <paramref name="maxChars"/> 에서 자른다. <paramref name="failCall"/> 번째 부름은 502 다.</summary>
    private static readonly JsonSerializerOptions Korean = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private sealed class FakeModel(int maxChars, int failCall = 0) : HttpMessageHandler
    {
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == failCall)
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var prompt = doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
            var memo = prompt[(prompt.LastIndexOf("메모:", StringComparison.Ordinal) + 3)..];

            var items = Dated.Matches(memo).Select(m => JsonSerializer.Serialize(new
            {
                start = m.Groups["d"].Value,
                start_time = (string?)null,
                end = m.Groups["d"].Value,
                end_time = (string?)null,
                title = m.Groups["t"].Value + " 운영반영",
                summary = m.Groups["t"].Value + " 을 운영에 반영한다.",
                requester = (string?)null,
                assignee = "이순열",
            }, Korean));
            var answer = "{\"schedules\":[" + string.Join(",", items) + "]}";
            if (answer.Length > maxChars) answer = answer[..maxChars];

            var sse = $"data: {JsonSerializer.Serialize(answer)}\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") };
        }
    }

    private static string Memo(int lines) =>
        "아래 일정을 제목에 운영반영 을 덧붙여서 표현 해줘.\n"
        + string.Join("\n", Enumerable.Range(0, lines).Select(i => $"[{5100 + i}]화면 {i}\t{(i < lines / 2 ? "2026-12-28" : "2027-01-04")}"));

    private static ScheduleNoteAssist Assist(FakeModel model) =>
        new(new AiChatClient(new HttpClient(model) { BaseAddress = new Uri("http://ai.test/api/") }), NullLogger<ScheduleNoteAssist>.Instance);

    [Fact]
    public async Task 일정_스물다섯_줄이_다_올라간다()
    {
        // 6건이면 1100자 남짓 — 상한 1500자는 6건을 넉넉히 담지만 25건은 못 담는다.
        var model = new FakeModel(maxChars: 1500);

        var drafts = await Assist(model).DraftManyAsync(Memo(25), "이순열");

        Assert.Equal(5, model.Calls);
        Assert.Equal(25, drafts.Count);
        Assert.Equal(25, drafts.Select(d => d.Title).Distinct().Count());
        Assert.All(drafts, d => Assert.EndsWith(" 운영반영", d.Title));
        Assert.Equal(new DateOnly(2027, 1, 4), drafts[^1].Start);
    }

    [Fact]
    public async Task 한_묶음이_실패해도_나머지는_모델_답으로_그_묶음은_줄_그대로_올라간다()
    {
        var model = new FakeModel(maxChars: 1500, failCall: 2);

        var drafts = await Assist(model).DraftManyAsync(Memo(13), "이순열");

        Assert.Equal(13, drafts.Count);
        Assert.Equal(7, drafts.Count(d => d.Title!.EndsWith(" 운영반영")));
        Assert.Contains(drafts, d => d.Title == "[5106]화면 6" && d.Start == new DateOnly(2027, 1, 4));
    }

    [Fact]
    public async Task 묶음_하나의_답도_잘리면_닫힌_것까지는_건진다()
    {
        var model = new FakeModel(maxChars: 500);   // 두세 건에서 끊긴다

        var drafts = await Assist(model).DraftManyAsync(Memo(6), "이순열");

        Assert.Equal(1, model.Calls);
        Assert.InRange(drafts.Count, 2, 5);
        Assert.All(drafts, d => Assert.EndsWith(" 운영반영", d.Title));
    }
}
