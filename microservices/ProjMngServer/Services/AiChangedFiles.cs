using System.Text;

namespace ProjMngServer.Services;

/// <summary>
/// 「처리 파일」 한 줄이 무엇을 당했나.
/// </summary>
/// <remarks>
/// git 의 <c>--name-status</c> 글자를 그대로 옮긴 것이고, 화면 쪽
/// <c>JSini.Web.ProjMng.Components.Shared.AiRunFileKind</c> 와 <b>같은 갈래</b>다.
/// 두 벌인 것은 프론트가 백엔드 프로젝트를 참조하지 않기 때문이다 — 글자
/// (<c>A</c> · <c>M</c> · <c>D</c> …)가 둘 사이의 약속이다.
/// </remarks>
public enum AiChangedFileKind
{
    /// <summary>무엇을 당했는지 모른다.</summary>
    Unknown,

    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,

    /// <summary>파일 종류가 바뀐 것(심볼릭 링크 ↔ 보통 파일 따위).</summary>
    TypeChanged,

    /// <summary>「외 N개」 — 상한을 넘어 접힌 나머지를 세어 둔 줄.</summary>
    More,
}

/// <summary>처리 파일 한 줄.</summary>
/// <param name="Kind">무엇을 당했나.</param>
/// <param name="Path">저장소 뿌리에서 본 경로.</param>
/// <param name="Derived">
/// <b>git 이 본 것이 아니라 결과문에서 뽑아낸 것인가.</b> 참이면 상태 글자 뒤에
/// <c>?</c> 가 붙어 저장된다(<c>M?</c>) — 화면과 메일이 그것을 「추정」으로 적는다.
/// </param>
public sealed record AiChangedFile(AiChangedFileKind Kind, string Path, bool Derived = false);

/// <summary>
/// 「처리 파일」 목록을 <b>읽고 · 합치고 · 적는</b> 규칙 한 벌.
/// </summary>
/// <remarks>
/// <para>
/// [왜 합치나]
/// </para>
/// <para>
/// 정본은 실행기가 <c>git diff --name-status</c> 로 물어 온 목록이다
/// (<c>AiTaskRunner.Workspace.ChangedFilesAsync</c>). 그것이 가장 정확하지만
/// <b>늘 손에 들어오지는 않는다</b> — git 이 아닌 대상(폴더 복사본)에는 물을
/// 데가 없고, 실행기가 옛 바이너리면 그 칸을 아예 안 보내고(2026-10-12 이전),
/// 작업공간이 치워진 뒤에는 다시 셀 수 없다. 그때 「처리 파일」 탭이 통째로
/// 비어 <b>기능이 고장난 것처럼</b> 보인다.
/// </para>
/// <para>
/// 그래서 <b>결과문에서 한 벌 더 뽑는다</b>(<c>AiResultSummarizer.ExtractFilesAsync</c>).
/// AI 는 제가 무엇을 고쳤는지 결과문에 적어 두므로 거기서 경로를 건질 수 있다.
/// 다만 그것은 <b>모델이 읽어 낸 것</b>이라 git 만큼 미덥지 않다 — 그래서
/// 합칠 때 <b>git 이 본 것이 언제나 이긴다</b>. git 이 못 본 줄만
/// <see cref="AiChangedFile.Derived"/> 로 더해지고, 화면은 그 줄에 「추정」을
/// 달아 <b>둘을 섞지 않는다</b>.
/// </para>
/// </remarks>
public static class AiChangedFiles
{
    /// <summary>
    /// 담아 둘 줄 수의 상한. <b>실행기의 같은 값과 맞춘다</b>
    /// (<c>Workspace.ChangedFileLines</c>) — 대량 치환 한 번이 수천 줄을 DB 와
    /// 회로로 밀어 넣지 않게.
    /// </summary>
    public const int MaxLines = 200;

    /// <summary>모델에게서 받을 경로의 길이 상한. 이보다 길면 경로가 아니다.</summary>
    private const int MaxPathLength = 300;

    /// <summary>
    /// 담긴 글(<c>상태\t경로</c> 줄들)을 목록으로. <b>읽을 것이 없으면 빈 목록이다.</b>
    /// </summary>
    public static List<AiChangedFile> Parse(string? text)
    {
        var rows = new List<AiChangedFile>();

        foreach (var line in Lines(text))
        {
            // **마지막 칸이 지금 경로다.** 이름이 바뀐 줄은 git 날것에서
            // `R100\t옛것\t지금것` 으로 칸이 셋이다.
            var cols = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);

            if (cols.Length < 2)
            {
                continue;
            }

            var path = cols[^1].Trim();

            if (path.Length == 0)
            {
                continue;
            }

            var mark = cols[0];

            rows.Add(new AiChangedFile(KindOf(mark), path, mark.EndsWith('?')));
        }

        return rows;
    }

    /// <summary>
    /// <b>git 이 본 것과 결과문에서 뽑은 것을 합친다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 규칙은 셋이다.
    /// </para>
    /// <list type="number">
    ///   <item><description><b>git 이 먼저고, git 이 이긴다.</b> 같은 경로가
    ///   양쪽에 있으면 git 이 말한 갈래를 쓴다 — 모델이 「새로 만들었다」고 적은
    ///   파일이 실은 이미 있던 것을 고친 경우가 흔하다.</description></item>
    ///   <item><description><b>git 이 못 본 줄만 뒤에 붙는다</b>(<c>Derived</c>).
    ///   앞뒤를 섞지 않으므로 목록을 위에서부터 읽으면 확실한 것이 먼저다.</description></item>
    ///   <item><description><b>「외 N개」 줄은 늘 맨 끝</b>이다. 그 줄은 파일이
    ///   아니라 세어 둔 말이라 가운데 끼면 목록이 거기서 끝난 것처럼 읽힌다.</description></item>
    /// </list>
    /// <para>
    /// <b>합쳐도 빈손이면 널이다</b> — 화면이 그 탭을 「없음」으로 그린다.
    /// </para>
    /// </remarks>
    /// <param name="gitText">실행기가 보내 온 정본 글. 없을 수 있다.</param>
    /// <param name="derived">결과문에서 뽑은 것. 없을 수 있다.</param>
    public static string? Merge(string? gitText, IReadOnlyList<AiChangedFile>? derived)
    {
        var merged = new List<AiChangedFile>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AiChangedFile? more = null;

        foreach (var row in Parse(gitText))
        {
            if (row.Kind == AiChangedFileKind.More)
            {
                more ??= row;
                continue;
            }

            if (seen.Add(row.Path))
            {
                merged.Add(row);
            }
        }

        foreach (var row in derived ?? [])
        {
            if (Normalize(row.Path) is not { } path || !seen.Add(path))
            {
                continue;
            }

            merged.Add(row with { Path = path, Derived = true });
        }

        if (merged.Count == 0)
        {
            return null;
        }

        if (merged.Count > MaxLines)
        {
            var over = merged.Count - MaxLines;

            merged = [.. merged.Take(MaxLines)];
            more = new AiChangedFile(AiChangedFileKind.More, $"외 {over}개");
        }

        if (more is not null)
        {
            merged.Add(more);
        }

        return Format(merged);
    }

    /// <summary>목록을 <c>상태\t경로</c> 줄들로 되돌린다.</summary>
    public static string Format(IReadOnlyList<AiChangedFile> rows)
    {
        var sb = new StringBuilder();

        foreach (var row in rows)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(MarkOf(row.Kind, row.Derived)).Append('\t').Append(row.Path);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 모델이 준 경로를 쓸 수 있는 꼴로. <b>경로가 아니면 널이다.</b>
    /// </summary>
    /// <remarks>
    /// 모델은 <c>./web/x.razor</c> · <c>/home/lee/…/web/x.razor</c> ·
    /// <c>`web/x.razor`</c> 처럼 제각각으로 적는다. 앞머리와 따옴표를 떼고
    /// 역슬래시를 세워 <b>git 이 적은 것과 같은 모양</b>으로 맞춰야 중복이
    /// 걸러진다 — 안 맞추면 같은 파일이 두 줄로 선다.
    /// </remarks>
    public static string? Normalize(string? raw)
    {
        var path = (raw ?? string.Empty).Trim().Trim('`', '"', '\'', ' ');

        if (path.Length == 0 || path.Length > MaxPathLength || path.Any(char.IsControl))
        {
            return null;
        }

        path = path.Replace('\\', '/');

        // 절대 경로는 작업공간 뿌리를 떼 낸다 — 뿌리 이름은 실행마다 다르므로
        // 알아낼 수 없고, 저장소 안의 자리만 남기면 git 쪽과 맞는다.
        if (path.StartsWith('/'))
        {
            var cut = path.IndexOf("/ai-workspaces/", StringComparison.Ordinal);

            if (cut >= 0 && path.IndexOf('/', cut + 15) is var next and > 0)
            {
                path = path[(next + 1)..];
            }
            else
            {
                path = path.TrimStart('/');
            }
        }

        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        path = path.TrimStart('/').TrimEnd();

        // 폴더로 끝나는 것 · 위로 올라가는 것은 파일 목록이 아니다.
        if (path.Length == 0 || path.EndsWith('/') || path.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        // 파일 이름이 한 마디도 없으면 경로로 치지 않는다.
        return path.Split('/')[^1].Length == 0 ? null : path;
    }

    /// <summary>
    /// 모델이 적은 갈래 한 마디를 갈래로. <b>모르는 말은 「고침」으로 떨어뜨리지
    /// 않는다</b> — 지어내는 것보다 모른다고 두는 편이 낫다.
    /// </summary>
    public static AiChangedFileKind KindFromWord(string? word) =>
        (word ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "added" or "add" or "created" or "create" or "new" or "추가" or "신규" or "생성"
                => AiChangedFileKind.Added,
            "modified" or "modify" or "changed" or "change" or "updated" or "update" or "수정" or "변경"
                => AiChangedFileKind.Modified,
            "deleted" or "delete" or "removed" or "remove" or "삭제" or "제거"
                => AiChangedFileKind.Deleted,
            "renamed" or "rename" or "moved" or "move" or "이름변경" or "이동"
                => AiChangedFileKind.Renamed,
            "copied" or "copy" or "복사" => AiChangedFileKind.Copied,
            _ => AiChangedFileKind.Unknown,
        };

    /// <summary>
    /// 상태 칸 하나를 갈래로. <b>앞 한 글자만 본다</b> — <c>R100</c> 처럼 닮은
    /// 정도가 붙어 오는 날것도, <c>M?</c> 처럼 추정 표가 붙은 것도 같이 읽힌다.
    /// </summary>
    public static AiChangedFileKind KindOf(string? mark) =>
        (mark is { Length: > 0 } m ? m[..1] : string.Empty) switch
        {
            "A" => AiChangedFileKind.Added,
            "M" => AiChangedFileKind.Modified,
            "D" => AiChangedFileKind.Deleted,
            "R" => AiChangedFileKind.Renamed,
            "C" => AiChangedFileKind.Copied,
            "T" => AiChangedFileKind.TypeChanged,
            "…" => AiChangedFileKind.More,
            _ => AiChangedFileKind.Unknown,
        };

    /// <summary>
    /// 갈래를 상태 칸 글자로. <b>추정이면 <c>?</c> 를 붙인다</b> — 옛 읽개도
    /// 앞 한 글자만 보므로 그대로 읽히고(뜻만 모른다), 새 읽개는 그 표를 본다.
    /// </summary>
    public static string MarkOf(AiChangedFileKind kind, bool derived)
    {
        var mark = kind switch
        {
            AiChangedFileKind.Added => "A",
            AiChangedFileKind.Modified => "M",
            AiChangedFileKind.Deleted => "D",
            AiChangedFileKind.Renamed => "R",
            AiChangedFileKind.Copied => "C",
            AiChangedFileKind.TypeChanged => "T",
            AiChangedFileKind.More => "…",

            // 갈래를 모르는 줄에도 칸은 있어야 한다 — 없으면 탭이 하나뿐이라
            // 읽개가 그 줄을 통째로 버린다. **`?` 를 쓰지 않는다** — 그 글자는
            // 추정 표라, 모르는 줄과 추정 줄이 같은 모양이 된다.
            _ => "-",
        };

        // 「외 N개」 줄에는 추정 표를 붙이지 않는다. 그 줄은 파일이 아니다.
        return derived && kind != AiChangedFileKind.More ? mark + "?" : mark;
    }

    private static IEnumerable<string> Lines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r", string.Empty)
                  .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
