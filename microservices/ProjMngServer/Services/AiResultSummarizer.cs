using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// AI 가 다시 써 준 결과 요약 한 벌. <b>메일의 「AI 의 답」 칸이 이것으로 그려진다.</b>
/// </summary>
/// <param name="Headline">한 문장. 무엇을 했나.</param>
/// <param name="Points">한 일·고친 것. 짧은 명사형 몇 줄.</param>
/// <param name="Checks">사람이 확인하거나 이어서 해야 할 것. 없을 수 있다.</param>
/// <param name="Title">목록에 걸 짧은 제목(20자 내외). 같은 호출에서 함께 받는다. 되읽은 요약에는 비어 있다.</param>
public sealed record AiResultSummary(
    string Headline, IReadOnlyList<string> Points, IReadOnlyList<string> Checks, string Title = "")
{
    /// <summary>「확인할 것」 묶음의 머리말. <see cref="ToText"/> 와 <see cref="Parse"/> 가 같이 쓴다.</summary>
    private const string ChecksLabel = "확인할 것";

    public bool IsEmpty => Headline.Length == 0 && Points.Count == 0;

    /// <summary>
    /// 작업 제목에 걸 한 줄. <b>제목 칸이 비면 헤드라인으로 돌아간다</b> —
    /// 「제목만 빼먹는」 모델이 있어도 목록이 지시문 앞부분에 머물지 않게 한다.
    /// </summary>
    public string TitleOrHeadline => Title.Length > 0 ? Title : Headline;

    /// <summary>
    /// <c>ai_task_run.summary_text</c> 에 남길 평문. 이어가기 지시문이 이 칸을 그대로 다음 실행의 문맥으로 올린다.
    /// </summary>
    public string ToText()
    {
        var sb = new StringBuilder();

        if (Headline.Length > 0)
        {
            sb.AppendLine(Headline);
        }

        if (Points.Count > 0)
        {
            sb.AppendLine();

            foreach (var p in Points)
            {
                sb.AppendLine($"- {p}");
            }
        }

        if (Checks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(ChecksLabel);

            foreach (var c in Checks)
            {
                sb.AppendLine($"- {c}");
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// <see cref="ToText"/> 로 남긴 것을 되읽는다. <b>같은 실행을 두 번 요약하지 않기
    /// 위해서다</b> — 모델을 한 번 더 부르는 일이고, 그 값은 이미 DB 에 있다.
    /// </summary>
    public static AiResultSummary? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var headline = string.Empty;
        var points = new List<string>();
        var checks = new List<string>();
        var inChecks = false;

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line == ChecksLabel)
            {
                inChecks = true;
                continue;
            }

            if (line.StartsWith('-'))
            {
                var item = line.TrimStart('-', ' ').Trim();

                if (item.Length == 0)
                {
                    continue;
                }

                (inChecks ? checks : points).Add(item);
                continue;
            }

            if (headline.Length == 0)
            {
                headline = line;
            }
            else if (!inChecks && points.Count == 0)
            {
                // 머리표 없는 줄이 또 나오면 결론이 이어진 것이다. 버리면 메일과 화면이 다른 말을 하게 되어 여기서 잇는다.
                headline = $"{headline} {line}";
            }
        }

        var summary = new AiResultSummary(headline, points, checks);

        return summary.IsEmpty ? null : summary;
    }
}

/// <summary>
/// 끝난 실행의 결과문을 AI 에게 한 번 더 정리시킨다. 메일 요약 칸은 결과문의 앞 몇 줄을 떠서 쓰므로 서론이 앉기 쉽다.
/// 실패하면 null 을 돌려 요약 없이 보내게 하고, 보내기 전에 SecretMask 로 가린다.
/// </summary>
public sealed class AiResultSummarizer(
    IConfiguration configuration, IHttpClientFactory http, ILogger<AiResultSummarizer> logger)
{
    /// <summary>AI 로 정리시킬 것인가. AI 가 안 떠 있는 장비에서 메일마다 헛호출을 막는 손잡이다.</summary>
    private readonly bool _enabled =
        configuration.GetValue("AiTasks:SummarizeResult", defaultValue: true);

    /// <summary>AI 로 제목을 다시 지을 것인가. 끄는 손잡이만 따로 둔다(AiTasks:MakeTitle).</summary>
    private readonly bool _makeTitleEnabled =
        configuration.GetValue("AiTasks:MakeTitle", defaultValue: true);

    /// <summary>AI 서비스 주소. 알림 서비스와 마찬가지로 같은 장비 안이라 루프백이다.</summary>
    private readonly string _aiUrl =
        configuration["AiTasks:AiUrl"] is { Length: > 0 } u ? u : "http://127.0.0.1:5029";

    /// <summary>비워 두면 AI 서비스의 기본 공급자가 받는다.</summary>
    private readonly string? _provider = configuration["AiTasks:SummaryProvider"];

    private readonly string? _model = configuration["AiTasks:SummaryModel"];

    /// <summary>
    /// 정리 호출의 상한(초). 모델 하나가 8초쯤 쓰므로 여기서 먼저 끊으면 곧 답할 모델을 버리는 셈이다.
    /// </summary>
    private readonly int _timeoutSeconds =
        configuration.GetValue("AiTasks:SummaryTimeoutSeconds", defaultValue: 45);

    /// <summary>
    /// 제목 한 줄만 받아 오는 호출의 상한(초). 요약과 같게 잡으면 안 된다 — 앱푸시가 이 호출 뒤에 서므로 길게 잡으면 알림 전체가 늦는다.
    /// </summary>
    private readonly int _titleTimeoutSeconds =
        Math.Clamp(configuration.GetValue("AiTasks:TitleTimeoutSeconds", defaultValue: 15), 1, 120);

    /// <summary>
    /// 정리시킬 글의 길이 상한. 넉넉히 잡으면 무료 공급자의 분당 토큰 한도를 긴 글 한 번이 다 태운다.
    /// </summary>
    private readonly int _maxChars =
        configuration.GetValue("AiTasks:SummaryMaxChars", defaultValue: 6000);

    /// <summary>
    /// 결과문에서 처리 파일 목록을 뽑아낼 것인가. 요약과 따로 끌 수 있어야 한다 — 메일이 기다리는 값이 아니라 화면의 탭이다.
    /// </summary>
    private readonly bool _extractFilesEnabled =
        configuration.GetValue("AiTasks:ExtractChangedFiles", defaultValue: true);

    /// <summary>
    /// 파일 목록을 뽑는 호출의 상한(초). 요약과 나란히 나가므로 더 길게 잡으면 완료 뒤처리 전체의 상한에 먼저 걸린다.
    /// </summary>
    private readonly int _extractTimeoutSeconds =
        Math.Clamp(configuration.GetValue("AiTasks:ExtractTimeoutSeconds", defaultValue: 45), 1, 300);

    /// <summary>
    /// 한 번에 받을 파일 수. 모델이 경로를 지어내기 시작하면 그 수만큼 거짓이 목록에 서므로 넉넉히 두지 않는다.
    /// git 이 본 것은 이 상한과 무관하게 다 들어간다(AiChangedFiles.MaxLines).
    /// </summary>
    private readonly int _extractMaxFiles =
        Math.Clamp(configuration.GetValue("AiTasks:ExtractMaxFiles", defaultValue: 50), 1, 200);

    /// <summary>
    /// 편집자의 성격. 사실을 더하지 말라가 이 프롬프트의 전부다 — 원문에 없는 말이 메일에 실리면 해롭다.
    /// </summary>
    private const string SystemPrompt =
        "당신은 기술 보고서 편집자입니다. 개발자가 아닌 사람이 휴대폰에서 읽을 "
        + "메일에 들어갈 요약을 씁니다. 주어진 글에 있는 사실만 쓰고, 없는 것은 "
        + "절대 지어내지 마십시오. 한국어로 쓰고, 설명 없이 JSON 하나만 답합니다.";

    private const string TitleSystemPrompt =
        "당신은 작업 목록의 제목을 짓는 편집자입니다. 지시문과 작업 결과를 보고 "
        + "작업 내용을 가장 잘 나타내는 아주 짧은 제목(20자 내외)을 한국어로 작성합니다. "
        + "설명이나 따옴표, 머리말 없이 제목 한 줄만 답하십시오.";

    /// <summary>
    /// 아직 돌지 않은 차수의 제목을 짓는 편집자. 손에 든 것이 지시문뿐이라 「무엇을 했나」를 쓰게 두면 모델이 결과를 지어내므로 「시킨 일」을 적게 한다.
    /// </summary>
    private const string TurnTitleSystemPrompt =
        "당신은 작업 목록의 제목을 짓는 편집자입니다. 아직 실행되지 않은 지시문만 보고 "
        + "무엇을 시킨 일인지 나타내는 아주 짧은 제목(20자 내외)을 한국어로 작성합니다. "
        + "결과나 완료 여부를 추측해 쓰지 마십시오. "
        + "설명이나 따옴표, 머리말 없이 제목 한 줄만 답하십시오.";

    /// <summary>
    /// 결과문에서 파일 경로만 베껴 내는 독자. 글을 쓰는 일이 아니므로 「지어내지 말라」 하나로 성격을 못 박는다.
    /// </summary>
    private const string FilesSystemPrompt =
        "당신은 작업 보고서에서 파일 경로를 뽑아내는 도구입니다. 주어진 글에 실제로 "
        + "적혀 있는 경로만 그대로 옮겨 적고, 없는 경로를 만들어 내지 마십시오. "
        + "설명 없이 JSON 하나만 답합니다.";

    /// <summary>
    /// AI 서비스 호출 공통부. <b>가리기·실패 시 처신이 요약과 제목 생성에 한 벌로 적용된다.</b>
    /// </summary>
    private async Task<string?> ChatAsync(
        string systemPrompt, string userPrompt, int timeoutSeconds, CancellationToken ct = default)
    {
        try
        {
            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_aiUrl.TrimEnd('/')}/chat")
            {
                Content = JsonContent.Create(new
                {
                    provider = _provider,
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt },
                    },
                }),
            };

            // 서비스 간 직접 호출이라 자기 이름을 적어 보낸다 — 알림 호출과 같다.
            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "AI 호출을 건너뜁니다 — AI 서비스가 HTTP {Status} 로 답했습니다.",
                    (int)res.StatusCode);
                return null;
            }

            return Unwrap(await res.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "AI 호출을 건너뜁니다 — AI 를 부르지 못했습니다.");
            return null;
        }
    }

    /// <summary>
    /// 결과문을 정리해 돌려준다. <b>어떤 이유로든 못 하면 null 이다 — 던지지 않는다.</b>
    /// </summary>
    public async Task<AiResultSummary?> SummarizeAsync(
        string? instruction, string? resultText, CancellationToken ct = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(resultText))
        {
            return null;
        }

        // **가리는 것이 먼저다.** 이 아래로는 우리 장비 밖으로 나갈 수 있다.
        var body = Clip(SecretMask.Apply(resultText), _maxChars);
        var asked = Clip(SecretMask.Apply(instruction), 1500);

        var reply = await ChatAsync(SystemPrompt, Ask(asked, body), _timeoutSeconds, ct);
        return reply is null ? null : FromJson(reply);
    }

    /// <summary>
    /// 끝난 작업의 지시문과 결과를 보고 20자 내외의 짧은 제목을 짓는다. 보통은 요약과 같은 호출로 와서, 여기는 빈손이었거나 제목 칸만 빠진 건의 되받기다.
    /// </summary>
    public async Task<string?> MakeTitleAsync(
        string? instruction, string? resultText, string? summaryHeadline = null, CancellationToken ct = default)
    {
        if (!_makeTitleEnabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(instruction) && string.IsNullOrWhiteSpace(resultText) && string.IsNullOrWhiteSpace(summaryHeadline))
        {
            return null;
        }

        var asked = Clip(SecretMask.Apply(instruction), 1000);
        var resultOrSummary = !string.IsNullOrWhiteSpace(summaryHeadline)
            ? summaryHeadline
            : Clip(SecretMask.Apply(resultText), 2000);

        var prompt = $$"""
            아래 지시문과 실행 결과를 보고, 이 작업의 핵심을 나타내는 20자 내외의 아주 짧은 제목을 하나 지어 주십시오.
            따옴표, 마침표, 부가 설명 없이 제목 텍스트만 한 줄로 출력하십시오.

            ## 지시문
            {{asked ?? "(없음)"}}

            ## 결과
            {{resultOrSummary ?? "(없음)"}}
            """;

        return OneLine(await ChatAsync(TitleSystemPrompt, prompt, _titleTimeoutSeconds, ct));
    }

    /// <summary>
    /// 아직 돌지 않은 차수의 제목을 짓는다 — 손에 든 것이 지시문뿐일 때다. 차수마다 제 제목이 있어야 접힌 머리줄로 펼칠 차수를 가린다(AiTurnTitler).
    /// 여기서 짓는 것은 「시킨 일」이고, 끝난 뒤에는 요약과 같은 호출이 그 값을 덮는다. 제목 생성 손잡이(AiTasks:MakeTitle)를 같이 본다 — 한쪽만 꺼지면 제목이 빠진 차수가 생긴다.
    /// </summary>
    public async Task<string?> MakeTurnTitleAsync(string? instruction, CancellationToken ct = default)
    {
        if (!_makeTitleEnabled || string.IsNullOrWhiteSpace(instruction))
        {
            return null;
        }

        // **가리는 것이 먼저다.** 지시문에도 접속 정보가 섞여 들어올 수 있고,
        // 이 호출은 우리 장비 밖으로 나갈 수 있는 경로다(공급자가 외부 API 인 경우).
        var asked = Clip(SecretMask.Apply(instruction), 1500);

        var prompt = $$"""
            아래는 AI 실행기에게 이제 보낼 지시문입니다. 이 지시가 무엇을 시키는 일인지
            나타내는 20자 내외의 아주 짧은 제목을 하나 지어 주십시오.
            따옴표, 마침표, 부가 설명 없이 제목 텍스트만 한 줄로 출력하십시오.

            ## 지시문
            {{asked}}
            """;

        return OneLine(await ChatAsync(TurnTitleSystemPrompt, prompt, _titleTimeoutSeconds, ct));
    }

    /// <summary>
    /// 결과문을 읽혀 이 실행이 건드린 파일 목록을 뽑아 온다. 못 하면 null — 던지지 않는다.
    /// 정본은 실행기의 git 목록이고 모델이 적은 줄은 추정이라 합칠 때 git 쪽이 이긴다. 고친 것이 없는 빈 목록과 실패(null)는 다르게 다룬다.
    /// </summary>
    public async Task<IReadOnlyList<AiChangedFile>?> ExtractFilesAsync(
        string? instruction, string? resultText, CancellationToken ct = default)
    {
        if (!_extractFilesEnabled || string.IsNullOrWhiteSpace(resultText))
        {
            return null;
        }

        // **가리는 것이 먼저다.** 이 아래로는 우리 장비 밖으로 나갈 수 있다.
        var body = Clip(SecretMask.Apply(resultText), _maxChars);
        var asked = Clip(SecretMask.Apply(instruction), 1000);

        var reply = await ChatAsync(
            FilesSystemPrompt, AskFiles(asked, body, _extractMaxFiles), _extractTimeoutSeconds, ct);

        return reply is null ? null : FilesFromJson(reply);
    }

    /// <summary>파일 목록을 받아 오는 한 통. <b>무엇을 넣지 말라를 못 박는다.</b></summary>
    private static string AskFiles(string? instruction, string? resultText, int max) => $$"""
        아래는 사람이 시킨 지시문과, AI 실행기가 그것을 처리하고 남긴 결과 원문입니다.
        이 작업에서 **실제로 새로 만들어지거나 고쳐지거나 지워진 파일**의 경로를 뽑아 주십시오.

        [규칙]
        - 글에 적혀 있는 경로만 쓰십시오. **없는 파일을 지어내지 마십시오.**
        - 저장소 뿌리에서 본 상대 경로로 적으십시오(앞의 `./` 나 절대 경로는 빼고).
        - **읽기만 한 파일**, 예시로 든 경로, 명령어·URL·패키지 이름은 넣지 마십시오.
        - kind 는 added(새로 만듦) · modified(고침) · deleted(지움) 중 하나입니다.
        - 최대 {{max}}개까지. 확실한 것부터 적으십시오.
        - 고친 파일이 없으면 빈 배열로 답하십시오.
        - 설명·머리말 없이 아래 모양의 JSON 하나만 답하십시오.

        {"files":[{"path":"web/src/Apps/Foo/Bar.razor","kind":"modified"}]}

        ## 시킨 것
        {{instruction ?? "(적힌 것이 없습니다)"}}

        ## 결과 원문
        {{resultText}}
        """;

    /// <summary>
    /// 모델이 준 글에서 파일 목록을 꺼낸다. 첫 <c>{</c> 와 마지막 <c>}</c> 사이만 읽고, 못 읽으면 빈 목록이 아니라 null 이다.
    /// </summary>
    private IReadOnlyList<AiChangedFile>? FilesFromJson(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            logger.LogInformation("처리 파일 뽑기를 건너뜁니다 — 모델이 JSON 을 주지 않았습니다.");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(reply[start..(end + 1)]);

            if (!doc.RootElement.TryGetProperty("files", out var files)
                || files.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var rows = new List<AiChangedFile>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in files.EnumerateArray())
            {
                if (rows.Count >= _extractMaxFiles)
                {
                    break;
                }

                // 경로만 줄로 적어 주는 모델도 있다 — `["a/b.cs", …]`.
                var raw = item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.ValueKind == JsonValueKind.Object && item.TryGetProperty("path", out var p)
                        ? p.GetString()
                        : null;

                if (AiChangedFiles.Normalize(raw) is not { } path || !seen.Add(path))
                {
                    continue;
                }

                var word = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("kind", out var k)
                    ? k.GetString()
                    : null;

                rows.Add(new AiChangedFile(AiChangedFiles.KindFromWord(word), path, Derived: true));
            }

            return rows;
        }
        catch (JsonException)
        {
            logger.LogInformation("처리 파일 뽑기를 건너뜁니다 — 모델의 JSON 을 읽지 못했습니다.");
            return null;
        }
    }

    /// <summary>
    /// 모델이 준 글에서 제목 한 줄만 꺼낸다. 따옴표로 감싸거나 설명을 덧붙이는 모델이 있어 앞뒤 따옴표를 떼고 첫 줄만 남긴다.
    /// </summary>
    private static string? OneLine(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return null;
        }

        var title = reply.Trim().Trim('"', '\'', '`', ' ');

        if (title.Contains('\n'))
        {
            title = title.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? title;
        }

        title = title.Trim().Trim('"', '\'', '`', ' ');

        return title.Length == 0 ? null
            : title.Length > 100 ? title[..100]
            : title;
    }

    /// <summary>
    /// 편집을 시키는 한 통. <b>칸과 길이를 못 박는다</b> — 자유롭게 쓰게 두면
    /// 메일에 다시 서론이 앉는다.
    /// </summary>
    private static string Ask(string? instruction, string? resultText) => $$"""
        아래는 사람이 시킨 지시문과, AI 실행기가 그것을 처리하고 남긴 결과 원문입니다.
        **둘을 함께 보고** 메일 요약 칸에 넣을 수 있게 정리해 주십시오.

        [규칙]
        - title: 목록에 걸 아주 짧은 제목(20자 내외). 따옴표·마침표 없이.
        - headline: 한 문장(60자 이내). 무엇을 했는지 결론부터.
        - points: 한 일·고친 것을 3~6개. 각 80자 이내, 명사형으로 짧게.
        - checks: 사람이 확인하거나 이어서 해야 할 것 0~3개. 없으면 빈 배열.
        - 원문에 없는 것을 지어내지 마십시오. 코드·로그·경로를 그대로 옮기지 마십시오.
        - 「~하겠습니다」 같은 서론은 버리고 실제로 한 것만 남기십시오.
        - 설명·머리말 없이 아래 모양의 JSON 하나만 답하십시오.

        {"title":"...","headline":"...","points":["...","..."],"checks":["..."]}

        ## 시킨 것
        {{instruction ?? "(적힌 것이 없습니다)"}}

        ## 결과 원문
        {{resultText}}
        """;

    /// <summary>
    /// 공용 응답 봉투에서 답 글자만 꺼낸다 — <c>{ data: { result: ["…"] } }</c>.
    /// </summary>
    private static string? Unwrap(string? envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(envelope);

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.Array
                && result.GetArrayLength() > 0)
            {
                return result[0].GetString();
            }
        }
        catch (JsonException)
        {
            // 봉투가 아니면 아래에서 null 이다.
        }

        return null;
    }

    /// <summary>
    /// 모델이 준 글에서 JSON 을 꺼내 요약으로 만든다. 인사말이나 울타리가 붙어도 첫 <c>{</c> 와 마지막 <c>}</c> 사이만 읽는다. 그래도 안 되면 null 이고 메일은 요약 없이 나간다.
    /// </summary>
    private AiResultSummary? FromJson(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            logger.LogInformation("결과 요약을 건너뜁니다 — 모델이 JSON 을 주지 않았습니다.");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(reply[start..(end + 1)]);
            var root = doc.RootElement;

            var headline = root.TryGetProperty("headline", out var h)
                ? Tidy(h.GetString(), 120)
                : string.Empty;

            // 제목은 작업 목록에 그대로 걸리므로 칸 길이(200)보다 훨씬 짧게 끊는다.
            var title = root.TryGetProperty("title", out var t)
                ? Tidy(t.GetString(), 60)
                : string.Empty;

            var summary = new AiResultSummary(
                headline,
                Items(root, "points", max: 6),
                Items(root, "checks", max: 3),
                title);

            return summary.IsEmpty ? null : summary;
        }
        catch (JsonException ex)
        {
            logger.LogInformation(ex, "결과 요약을 건너뜁니다 — JSON 을 읽지 못했습니다.");
            return null;
        }
    }

    /// <summary>배열 칸 하나를 읽는다. 없거나 배열이 아니면 빈 목록이다.</summary>
    private static IReadOnlyList<string> Items(JsonElement root, string name, int max)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => Tidy(e.GetString(), 140))
            .Where(s => s.Length > 0)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// 한 줄을 다듬는다. <b>머리표를 뗀다</b> — 메일은 이미 점을 찍어 주므로
    /// <c>- </c> 나 <c>* </c> 가 붙으면 점이 둘이 된다.
    /// </summary>
    private static string Tidy(string? text, int max)
    {
        var t = (text ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim()
            .TrimStart('-', '*', '·', '#', ' ')
            .Trim();

        return t.Length <= max ? t : t[..max] + "…";
    }

    /// <summary>
    /// 너무 긴 글을 줄인다. <b>앞뒤를 남긴다</b> — 결론은 대개 끝에 있어서
    /// 앞만 자르면 정리시킬 알맹이가 통째로 빠진다.
    /// </summary>
    private static string? Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= max)
        {
            return text?.Trim();
        }

        var head = max * 2 / 3;
        var tail = max - head;

        return $"{text[..head].Trim()}\n\n… (가운데 {text.Length - max}자 줄임) …\n\n{text[^tail..].Trim()}";
    }
}
