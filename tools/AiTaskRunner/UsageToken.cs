using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// CLI 가 제 자리에 넣어 둔 토큰을 꺼내 온다.
/// </summary>
/// <remarks>
/// <para>
/// <b>토큰을 실행기 설정에 베껴 적지 않는다.</b> 베껴 두면 그 CLI 가 다시
/// 로그인한 날부터 실행기만 조용히 401 을 받고, 화면에는 「읽지 못함」만
/// 뜬다 — 고장의 원인이 실행기 밖에 있는데 실행기를 들여다보게 된다.
/// 그래서 <b>자리(파일 경로와 JSON 경로)만 설정에 적고 값은 그때그때 읽는다.</b>
/// </para>
/// <para>
/// <b>읽은 값은 로그에도 보고에도 싣지 않는다.</b> 실패했을 때 남기는 것은
/// 「어느 파일의 어느 자리에서 못 찾았다」까지다.
/// </para>
/// </remarks>
public static class UsageToken
{
    /// <summary>
    /// JSON 파일에서 점으로 이은 자리의 글자를 꺼낸다.
    /// </summary>
    /// <param name="file">파일 경로. 맨 앞의 <c>~</c> 는 집 폴더로 편다.</param>
    /// <param name="path">
    /// <c>authTokens.*.token</c> 처럼 점으로 이은 자리.
    /// <c>*</c> 는 「이름은 모르겠고 첫 칸」이다 — 코파일럿의 설정은 열쇠가
    /// <c>https://github.com:계정</c> 이라 이름을 적어 둘 수가 없다.
    /// </param>
    /// <returns>못 찾으면 <c>null</c>. <b>던지지 않는다</b> — 곁들이는 일이다.</returns>
    public static string? Read(string file, string path)
    {
        if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var full = Expand(file);

        if (!File.Exists(full))
        {
            return null;
        }

        // **주석이 섞여 있다.** 코파일럿의 config.json 은 맨 위 두 줄이
        // `// ...` 로 시작한다 — 기본 설정으로 읽으면 첫 글자에서 터진다.
        using var doc = JsonDocument.Parse(File.ReadAllText(full), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var node = doc.RootElement;

        foreach (var step in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (step == "*")
            {
                var first = node.EnumerateObject().FirstOrDefault();

                if (first.Value.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }

                node = first.Value;
                continue;
            }

            if (!node.TryGetProperty(step, out node))
            {
                return null;
            }
        }

        return node.ValueKind == JsonValueKind.String ? node.GetString() : null;
    }

    private static string Expand(string path)
        => path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;
}
