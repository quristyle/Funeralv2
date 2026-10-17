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

/// <summary>
/// 추정 줄을 <b>실제 파일에 대 보았나, 대 보았으면 무엇이었나.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 필요한가 (2026-10-17).</b> 결과문에서 뽑은 경로는 모델이 글을 읽고
/// 적은 것이라 <b>파일이 아닌 것이 섞인다.</b> 운영에 쌓인 추정 줄을 훑어
/// 보니 <c>AiChangedFiles.Merge</c>(메서드 이름) ·
/// <c>projmng.ai_task_run.files_merged_at</c>(DB 칸 이름) 같은 것이 그대로
/// 「처리 파일」로 서 있었고, 반대로 <c>web/AiAskPanel.razor</c> 처럼
/// <b>중간 마디가 빠진 경로</b>도 흔했다(참값은
/// <c>web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor</c>).
/// </para>
/// <para>
/// <b>대 보는 것은 실행기다.</b> 서버는 컨테이너 안이라 대상 경로를 볼 수
/// 없다(<c>AiRunnerController</c> 의 「대상의 git 상태」와 같은 까닭).
/// 실행기가 저장소 목록에 대 보고 <see cref="Found"/> · <see cref="Missing"/> ·
/// <see cref="Ambiguous"/> 중 하나로 답한다.
/// </para>
/// </remarks>
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
/// <param name="Derived">
/// <b>git 이 본 것이 아니라 결과문에서 뽑아낸 것인가.</b> 참이면 상태 글자 뒤에
/// 확인 표가 하나 붙어 저장된다(<c>M?</c> · <c>M*</c> · <c>M!</c> · <c>M~</c>) —
/// 화면과 메일이 그것을 「추정」으로 적는다.
/// </param>
/// <param name="Check">
/// 그 추정 줄을 실제 파일에 대 본 결과. <see cref="Derived"/> 가 거짓이면
/// 언제나 <see cref="AiChangedFileCheck.None"/> 이다.
/// </param>
public sealed record AiChangedFile(
    AiChangedFileKind Kind,
    string Path,
    bool Derived = false,
    AiChangedFileCheck Check = AiChangedFileCheck.None);

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
            var check = CheckOf(mark);

            rows.Add(new AiChangedFile(
                KindOf(mark), path, check != AiChangedFileCheck.None, check));
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
    /// <param name="targetPath">
    /// 이 작업의 대상 위치. 모델이 적어 준 <b>온 경로에서 이것을 떼</b>
    /// 「대상 위치 다음부터」로 맞춘다(<see cref="Normalize"/>).
    /// </param>
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
    /// 모델이 준 경로를 쓸 수 있는 꼴로. <b>경로가 아니면 널이다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 모델은 <c>./web/x.razor</c> · <c>/home/lee/…/web/x.razor</c> ·
    /// <c>`web/x.razor`</c> 처럼 제각각으로 적는다. 앞머리와 따옴표를 떼고
    /// 역슬래시를 세워 <b>git 이 적은 것과 같은 모양</b>으로 맞춰야 중복이
    /// 걸러진다 — 안 맞추면 같은 파일이 두 줄로 선다.
    /// </para>
    /// <para>
    /// <b>대상 경로를 알면 그것부터 뗀다</b>(2026-10-17). 모델은 결과문에
    /// <c>/home/lee/Funeralv2/web/x.razor</c> 처럼 <b>장비의 온 경로</b>를
    /// 그대로 옮겨 적는 일이 잦은데, 그것을 모르면 앞의 <c>/</c> 만 떨어져
    /// <c>home/lee/Funeralv2/web/x.razor</c> 라는 <b>없는 경로</b>가 선다.
    /// 「대상 위치 다음부터」가 화면이 적어야 할 경로다.
    /// </para>
    /// </remarks>
    /// <param name="raw">모델이 적어 준 날것.</param>
    /// <param name="targetPath">
    /// 이 작업의 대상 위치(<c>ai_target.target_path</c>). 없으면 안 떼고,
    /// 작업공간 뿌리를 떼는 옛 규칙만 걸린다.
    /// </param>
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
    /// 상태 칸 하나에서 <b>확인 표</b>만 읽는다.
    /// </summary>
    /// <remarks>
    /// 마지막 한 글자다 — <c>?</c> 아직 · <c>*</c> 있음 · <c>!</c> 없음 ·
    /// <c>~</c> 여럿. 표가 없으면 git 이 센 줄이라
    /// <see cref="AiChangedFileCheck.None"/> 이다.
    /// </remarks>
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
    /// 갈래를 상태 칸 글자로. <b>추정이면 확인 표를 하나 붙인다</b> — 옛 읽개도
    /// 앞 한 글자만 보므로 그대로 읽히고(뜻만 모른다), 새 읽개는 그 표를 본다.
    /// </summary>
    /// <remarks>
    /// 표는 넷이다. <c>?</c> 아직 안 대 봤다 · <c>*</c> 대 보니 있다 ·
    /// <c>!</c> 대 보니 없다 · <c>~</c> 이름만 맞는 것이 여럿이라 못 골랐다.
    /// <b>확인 표를 안 쓰던 때의 글(<c>M?</c>)은 그대로 「아직」으로 읽힌다.</b>
    /// </remarks>
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
    /// <b>대 본 결과를 목록에 새긴다.</b> 실행기가 돌려준 답
    /// (<c>AiRunFileCheckService.ApplyAsync</c>)이 여기로 온다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 하는 일이 둘이다. ① 추정 줄의 <b>확인 표</b>를 바꾸고, ② 반 마디였던
    /// 경로를 <b>실행기가 찾아 준 온 경로로 고친다</b>.
    /// </para>
    /// <para>
    /// <b>고치고 나면 겹칠 수 있다.</b> <c>web/AiAskPanel.razor</c> 가
    /// <c>web/src/…/AiAskPanel.razor</c> 로 펴지면 git 이 이미 센 줄과 같아진다 —
    /// 그때는 <b>git 줄만 남긴다</b>(합칠 때와 같은 규칙). 추정끼리 겹쳐도
    /// 앞의 것만 남는다.
    /// </para>
    /// <para>
    /// <b>답이 안 온 줄은 그대로 둔다</b> — 「아직」인 채로 남아 다음 바퀴에
    /// 다시 물린다. 아무것도 안 바뀌었으면 <c>null</c> 이라, 부르는 쪽이
    /// 헛 UPDATE 를 치지 않는다.
    /// </para>
    /// </remarks>
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
