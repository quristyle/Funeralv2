using System.Text;

namespace ProjMngServer.Services;

/// <summary>
/// 「처리 파일」 한 줄이 무엇을 당했나.
/// </summary>
/// 화면의 AiRunFileKind 와 같은 갈래다. 프론트가 백엔드를 참조하지 않아 두 벌이고, 글자(A · M · D …)가 둘 사이의 약속이다.
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

/// <summary>
/// 추정 줄을 실제 파일에 대 보았나, 대 보았으면 무엇이었나. 모델이 적은 경로는 파일이 아닌 것이 섞이고 반 마디만 남기도 하므로 실행기가 대 본다(서버는 대상 경로를 못 본다).
/// </summary>
public enum AiChangedFileCheck
{
    /// <summary>댈 것이 없다 — git 이 센 줄이다.</summary>
    None,

    /// <summary>추정 줄인데 <b>아직 안 대 봤다</b>(<c>M?</c>).</summary>
    Pending,

    /// <summary>
    /// 대 보니 <b>실제로 있는 파일</b>이다(<c>M*</c>). 경로가 반 마디였으면
    /// 이때 <b>온 경로로 고쳐져</b> 있다.
    /// </summary>
    Found,

    /// <summary>대 보니 <b>대상에 그런 파일이 없다</b>(<c>M!</c>).</summary>
    Missing,

    /// <summary>
    /// 이름만 맞는 것이 <b>여럿이라 고르지 못했다</b>(<c>M~</c>).
    /// <c>appsettings.json</c> 처럼 저장소에 열넷씩 있는 이름이 여기로 온다.
    /// </summary>
    Ambiguous,
}

/// <summary>처리 파일 한 줄.</summary>
/// <param name="Kind">무엇을 당했나.</param>
/// <param name="Path">저장소 뿌리에서 본 경로.</param>
/// <param name="Derived">결과문에서 뽑아낸 것이면 참. 상태 글자 뒤에 확인 표가 붙어 「추정」으로 적힌다.</param>
/// <param name="Check">추정 줄을 대 본 결과. Derived 가 거짓이면 언제나 None 이다.</param>
public sealed record AiChangedFile(
    AiChangedFileKind Kind,
    string Path,
    bool Derived = false,
    AiChangedFileCheck Check = AiChangedFileCheck.None);

/// <summary>
/// 「처리 파일」 목록을 읽고 합치고 적는 규칙 한 벌. 정본은 실행기의 git 목록이고, 모델 결과문에서 뽑은 줄은 git 이 못 본 것만 「추정」으로 더한다.
/// git 이 없는 대상이나 실행기가 옛 바이너리면 목록을 안 보내 탭이 비므로 결과문 쪽을 함께 둔다.
/// </summary>
public static class AiChangedFiles
{
    /// <summary>
    /// 담아 둘 줄 수의 상한. 실행기의 Workspace.ChangedFileLines 와 맞춘다 — 대량 치환 한 번이 수천 줄을 DB 와 회로로 밀어 넣지 않게.
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
            var check = CheckOf(mark);

            rows.Add(new AiChangedFile(
                KindOf(mark), path, check != AiChangedFileCheck.None, check));
        }

        return rows;
    }

    /// <summary>
    /// git 이 본 것과 결과문에서 뽑은 것을 합친다. git 이 이기고, git 이 못 본 줄만 뒤에 붙는다. 「외 N개」 줄은 늘 맨 끝이다.
    /// 합쳐도 빈손이면 null — 화면이 그 탭을 「없음」으로 그린다.
    /// </summary>
    /// <param name="gitText">실행기가 보내 온 정본 글. 없을 수 있다.</param>
    /// <param name="derived">결과문에서 뽑은 것. 없을 수 있다.</param>
    /// <param name="targetPath">이 작업의 대상 위치. 모델이 적은 온 경로에서 이것을 떼 「대상 위치 다음부터」로 맞춘다.</param>
    public static string? Merge(
        string? gitText, IReadOnlyList<AiChangedFile>? derived, string? targetPath = null)
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
            if (Normalize(row.Path, targetPath) is not { } path || !seen.Add(path))
            {
                continue;
            }

            // **합치는 자리에서는 아직 아무것도 대 보지 않았다.** 대 보는 것은
            // 실행기고(<c>AiRunFileCheckService</c>), 그 답이 오면 표가 바뀐다.
            merged.Add(row with { Path = path, Derived = true, Check = AiChangedFileCheck.Pending });
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

            sb.Append(MarkOf(row.Kind, row.Derived, row.Check)).Append('\t').Append(row.Path);
        }

        return sb.ToString();
    }

    /// <summary>
    /// 모델이 준 경로를 쓸 수 있는 꼴로. 앞머리와 따옴표를 떼고 git 과 같은 모양으로 맞춰야 중복이 걸러진다. 경로가 아니면 null.
    /// 대상 경로를 알면 그것부터 뗀다 — 모델이 장비의 온 경로를 그대로 옮겨 적어 앞 슬래시만 떨어진 없는 경로가 서기 때문이다.
    /// </summary>
    /// <param name="raw">모델이 적어 준 날것.</param>
    /// <param name="targetPath">이 작업의 대상 위치(ai_target.target_path). 없으면 작업공간 뿌리를 떼는 규칙만 건다.</param>
    public static string? Normalize(string? raw, string? targetPath = null)
    {
        var path = (raw ?? string.Empty).Trim().Trim('`', '"', '\'', ' ');

        if (path.Length == 0 || path.Length > MaxPathLength || path.Any(char.IsControl))
        {
            return null;
        }

        path = path.Replace('\\', '/');

        // **대상 위치가 먼저다.** 알고 있는 뿌리를 떼는 쪽이 짐작보다 정확하다.
        if (targetPath is { Length: > 0 } root)
        {
            var head = root.Replace('\\', '/').TrimEnd('/');

            if (head.Length > 0
                && path.Length > head.Length + 1
                && path[head.Length] == '/'
                && path.StartsWith(head, StringComparison.Ordinal))
            {
                path = path[(head.Length + 1)..];
            }
        }

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
    /// 상태 칸 하나에서 확인 표만 읽는다. 표가 없으면 git 이 센 줄이라 None 이다.
    /// </summary>
    public static AiChangedFileCheck CheckOf(string? mark) =>
        (mark is { Length: > 1 } m ? m[^1] : ' ') switch
        {
            '?' => AiChangedFileCheck.Pending,
            '*' => AiChangedFileCheck.Found,
            '!' => AiChangedFileCheck.Missing,
            '~' => AiChangedFileCheck.Ambiguous,
            _ => AiChangedFileCheck.None,
        };

    /// <summary>
    /// 갈래를 상태 칸 글자로. 추정이면 확인 표(? 아직 · * 있음 · ! 없음 · ~ 여럿)를 하나 붙인다. 확인 표가 없는 글(M?)은 「아직」으로 읽힌다.
    /// </summary>
    public static string MarkOf(
        AiChangedFileKind kind, bool derived, AiChangedFileCheck check = AiChangedFileCheck.Pending)
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
        if (!derived || kind == AiChangedFileKind.More)
        {
            return mark;
        }

        return mark + check switch
        {
            AiChangedFileCheck.Found => "*",
            AiChangedFileCheck.Missing => "!",
            AiChangedFileCheck.Ambiguous => "~",
            _ => "?",
        };
    }

    /// <summary>
    /// 대 본 결과를 목록에 새긴다. 추정 줄의 확인 표를 바꾸고, 반 마디였던 경로를 실행기가 찾은 온 경로로 고친다.
    /// 고치고 나면 git 줄과 겹칠 수 있어 git 줄만 남긴다. 답이 안 온 줄은 「아직」인 채로 다음 바퀴에 다시 물리고, 바뀐 것이 없으면 null.
    /// </summary>
    /// <param name="text">지금 적혀 있는 글.</param>
    /// <param name="results">날것 경로 → (찾은 경로, 결과).</param>
    public static string? ApplyChecks(
        string? text, IReadOnlyDictionary<string, (string Path, AiChangedFileCheck Check)> results)
    {
        var rows = Parse(text);

        if (rows.Count == 0 || results.Count == 0)
        {
            return null;
        }

        var kept = new List<AiChangedFile>(rows.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var changed = false;

        // git 줄이 먼저 자리를 잡아야 **펴진 추정 줄이 그 위로 겹치지 않는다.**
        foreach (var row in rows.Where(r => !r.Derived && r.Kind != AiChangedFileKind.More))
        {
            seen.Add(row.Path);
        }

        foreach (var row in rows)
        {
            if (row.Kind == AiChangedFileKind.More || !row.Derived)
            {
                kept.Add(row);
                continue;
            }

            if (!results.TryGetValue(row.Path, out var found))
            {
                kept.Add(row);
                continue;
            }

            var path = found.Check == AiChangedFileCheck.Found && found.Path.Length > 0
                ? found.Path
                : row.Path;

            if (!seen.Add(path))
            {
                // 펴 놓고 보니 이미 있는 줄이다. **지우는 것도 바뀐 것이다.**
                changed = true;
                continue;
            }

            if (path != row.Path || found.Check != row.Check)
            {
                changed = true;
            }

            kept.Add(row with { Path = path, Check = found.Check });
        }

        return changed ? Format(kept) : null;
    }

    private static IEnumerable<string> Lines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r", string.Empty)
                  .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
