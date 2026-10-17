using System.Text;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「처리 파일」 한 줄이 무엇을 당했나.
/// </summary>
/// <remarks>
/// git 의 <c>--name-status</c> 글자를 그대로 옮긴 것이다. <see cref="Unknown"/>
/// 은 <b>옛 건</b>(<c>diff_stat</c> 만 있는 건)에서만 나온다 — <c>--stat</c> 은
/// 「몇 줄이 바뀌었나」만 알려 주지 더해진 파일인지 고쳐진 파일인지는 말하지 않는다.
/// </remarks>
public enum AiRunFileKind
{
    /// <summary>무엇을 당했는지 모른다. 배지 없이 경로만 적는다.</summary>
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
/// <param name="Path">저장소 뿌리에서 본 경로. <see cref="AiRunFileKind.More"/> 면 「외 N개」 같은 글이다.</param>
/// <param name="Derived">
/// <b>git 이 본 것이 아니라 결과문에서 뽑아낸 줄인가.</b> 담긴 글에서 상태 칸이
/// <c>M?</c> 처럼 <c>?</c> 로 끝나는 줄이다 — 서버가 둘을 합치면서 그 표를 단다
/// (<c>ProjMngServer.Services.AiChangedFiles</c>). 화면은 그 줄에 「추정」을 적어
/// <b>확인된 것과 섞지 않는다</b>.
/// </param>
public sealed record AiRunFile(AiRunFileKind Kind, string Path, bool Derived = false)
{
    /// <summary>경로의 마지막 마디 — 화면이 굵게 쓰는 이름.</summary>
    public string Name
    {
        get
        {
            var cut = Path.LastIndexOf('/');

            return cut < 0 || cut == Path.Length - 1 ? Path : Path[(cut + 1)..];
        }
    }

    /// <summary>
    /// 이름 앞의 자리. <b>없으면 빈 글자다</b> — 뿌리에 놓인 파일이 「/」 하나를
    /// 달고 서지 않게.
    /// </summary>
    public string Folder
    {
        get
        {
            var cut = Path.LastIndexOf('/');

            return cut <= 0 ? string.Empty : Path[..cut];
        }
    }

    /// <summary>배지에 적을 한 마디. <see cref="AiRunFileKind.Unknown"/> 이면 빈 글자다.</summary>
    public string KindText => Kind switch
    {
        AiRunFileKind.Added => "추가",
        AiRunFileKind.Modified => "수정",
        AiRunFileKind.Deleted => "삭제",
        AiRunFileKind.Renamed => "이름변경",
        AiRunFileKind.Copied => "복사",
        AiRunFileKind.TypeChanged => "형식변경",
        _ => string.Empty,
    };

    /// <summary>배지에 걸 꾸밈 이름. 색은 CSS 가 정한다.</summary>
    /// <remarks>
    /// <b>추정 줄은 색을 따로 쓴다.</b> 「추가」 배지가 확인된 것과 같은 초록으로
    /// 서면, 옆에 작게 붙은 「추정」을 못 보고 지나간 사람에게 그 줄은 <b>git 이
    /// 본 것과 구별되지 않는다</b>.
    /// </remarks>
    public string KindClass => Derived
        ? "pm-peek__chg-mark--guess"
        : Kind switch
        {
            AiRunFileKind.Added => "pm-peek__chg-mark--add",
            AiRunFileKind.Deleted => "pm-peek__chg-mark--del",
            AiRunFileKind.Modified or AiRunFileKind.TypeChanged => "pm-peek__chg-mark--mod",
            _ => "pm-peek__chg-mark--move",
        };

    /// <summary>
    /// 줄에 마우스를 올렸을 때의 설명. <b>추정 줄에만 있다</b> — 왜 이 줄이
    /// 다른 색인지는 어딘가에 적혀 있어야 한다.
    /// </summary>
    public string Hint => Derived
        ? $"{Path} (AI 의 결과문에서 뽑은 경로입니다. git 으로 확인되지 않았습니다.)"
        : Path;
}

/// <summary>
/// 실행 한 번이 <b>건드린 파일 목록</b>을 읽는다. 화면의 「처리 파일」이 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// [읽는 것이 둘이다]
/// </para>
/// <para>
/// 정본은 <c>ai_task_run.changed_files</c> 다. 실행기가
/// <c>git diff --name-status</c> 로 따로 물어 적어 둔 것이고, 한 줄에
/// <c>상태\t경로</c> 다(<c>AiTaskRunner.Workspace.ChangedFilesAsync</c>).
/// <b>커밋 전의 새 파일도 들어 있다</b> — 그것까지 적어야 「생성된 파일」이
/// 목록에서 빠지지 않는다.
/// </para>
/// <para>
/// 그 칸이 생기기 전(2026-10-12)의 건에는 <c>diff_stat</c> 뿐이다. 그래서
/// <b>그때만 그것을 되읽는다</b> — 아예 안 그리면 지난 건을 열었을 때 「처리
/// 파일」 칸이 통째로 사라져 기능이 고장난 것처럼 보인다. 대신 그쪽에서 나온
/// 줄은 <see cref="AiRunFileKind.Unknown"/> 이다. <c>--stat</c> 은 더해진
/// 파일인지 고쳐진 파일인지를 말하지 않으므로 <b>모르는 것을 지어내지 않는다.</b>
/// </para>
/// <para>
/// [왜 화면이 다시 세지 않나]
/// </para>
/// <para>
/// 세어야 할 자리는 작업공간이고 그것은 운영 장비에 있다. 화면은 적혀 있는
/// 것을 읽어 그릴 뿐이다 — 요약이 그런 것과 같은 까닭이다(<see cref="AiRunSummary"/>).
/// </para>
/// </remarks>
public static class AiRunFiles
{
    /// <summary>
    /// 적혀 있는 것을 목록으로. <b>읽을 것이 없으면 빈 목록이다</b> — 그때
    /// 화면은 「처리 파일」 칸을 그리지 않는다.
    /// </summary>
    /// <param name="changedFiles"><c>changed_files</c> — 한 줄에 <c>상태\t경로</c>.</param>
    /// <param name="diffStat"><c>diff_stat</c> — 옛 건에만 쓰는 뒷길.</param>
    public static IReadOnlyList<AiRunFile> Parse(string? changedFiles, string? diffStat)
    {
        var rows = FromChanged(changedFiles);

        return rows.Count > 0 ? rows : FromStat(diffStat);
    }

    /// <summary>정본 — <c>상태\t경로</c> 줄들.</summary>
    private static List<AiRunFile> FromChanged(string? text)
    {
        var rows = new List<AiRunFile>();

        foreach (var line in Lines(text))
        {
            // **마지막 칸이 지금 경로다.** 적는 쪽은 칸 둘로 적지만
            // (<c>Workspace.ChangedFilesAsync</c> 가 상태를 한 글자로 줄인다)
            // git 의 날것은 이름이 바뀐 줄에서 <c>R100\t옛것\t지금것</c> 으로
            // 셋이다 — 앞 탭만 보면 그 줄의 경로가 「옛것 탭 지금것」이 된다.
            var cols = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);

            if (cols.Length < 2)
            {
                continue;
            }

            var path = Unquote(cols[^1].Trim());

            if (path.Length == 0)
            {
                continue;
            }

            // 상태 칸이 `?` 로 끝나면 **git 이 본 것이 아니라 결과문에서 뽑은
            // 줄**이다(`M?`). 서버가 둘을 합치면서 그 표를 단다.
            rows.Add(new AiRunFile(KindOf(cols[0]), path, cols[0].EndsWith('?')));
        }

        return rows;
    }

    /// <summary>
    /// 옛 건 — <c>경로 | 12 ++--</c> 줄들.
    /// </summary>
    /// <remarks>
    /// 마지막 합계 줄(<c>3 files changed, …</c>)은 파일이 아니라 세어 둔 말이라
    /// 버린다. <b>칸막이(<c>|</c>)가 없는 줄은 집지 않는다</b> — 480자에서
    /// 잘리면서 생긴 토막이 거기로 들어온다.
    /// </remarks>
    private static List<AiRunFile> FromStat(string? text)
    {
        var rows = new List<AiRunFile>();

        foreach (var line in Lines(text))
        {
            var cut = line.LastIndexOf('|');

            if (cut <= 0)
            {
                continue;
            }

            var path = Unquote(line[..cut].Trim());

            if (path.Length > 0)
            {
                rows.Add(new AiRunFile(AiRunFileKind.Unknown, path));
            }
        }

        return rows;
    }

    /// <summary>
    /// 상태 칸 하나를 갈래로. <b>앞 한 글자만 본다</b> — 이름이 바뀐 줄의
    /// <c>R100</c> 처럼 닮은 정도가 붙어 오는 날것도, <c>M?</c> 처럼 추정 표가
    /// 붙은 것도 같은 갈래로 읽힌다(<c>-</c> 는 갈래를 모르는 줄이다).
    /// </summary>
    private static AiRunFileKind KindOf(string mark) => (mark.Length > 0 ? mark[..1] : mark) switch
    {
        "A" => AiRunFileKind.Added,
        "M" => AiRunFileKind.Modified,
        "D" => AiRunFileKind.Deleted,
        "R" => AiRunFileKind.Renamed,
        "C" => AiRunFileKind.Copied,
        "T" => AiRunFileKind.TypeChanged,
        "…" => AiRunFileKind.More,
        _ => AiRunFileKind.Unknown,
    };

    private static IEnumerable<string> Lines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r", string.Empty)
                  .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// git 이 따옴표로 싸서 내놓은 경로를 <b>사람이 읽는 글자로 되돌린다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// git 은 <c>core.quotePath</c> 가 켜져 있으면 ASCII 밖의 글자를 바이트
    /// 단위 8진 탈출로 적는다 — <c>"\355\225\234\352\270\200.md"</c>. 지금의
    /// 실행기는 그 설정을 꺼서 묻지만(<c>core.quotePath=false</c>),
    /// <b>이미 쌓인 <c>diff_stat</c> 에는 그렇게 적혀 있다.</b> 되돌리지 않으면
    /// 한국어 이름의 파일이 화면에서 역슬래시 숫자 더미로 보인다.
    /// </para>
    /// <para>
    /// <b>바이트로 모아 한 번에 푼다.</b> 한 글자가 바이트 셋인 UTF-8 이라
    /// 탈출 하나씩 풀면 글자가 되지 않는다.
    /// </para>
    /// </remarks>
    private static string Unquote(string path)
    {
        if (path.Length < 2 || path[0] != '"' || path[^1] != '"')
        {
            return path;
        }

        var body = path[1..^1];
        var bytes = new List<byte>(body.Length);

        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != '\\')
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(body[i].ToString()));
                continue;
            }

            if (i + 3 < body.Length
                && body[i + 1] is >= '0' and <= '7'
                && body[i + 2] is >= '0' and <= '7'
                && body[i + 3] is >= '0' and <= '7')
            {
                bytes.Add((byte)Convert.ToInt32(body.Substring(i + 1, 3), 8));
                i += 3;
                continue;
            }

            // `\"` · `\\` · `\t` 따위. 모르는 것은 뒤 글자를 그대로 쓴다.
            if (i + 1 < body.Length)
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(Escape(body[i + 1]).ToString()));
                i++;
            }
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }

    private static char Escape(char c) => c switch
    {
        'n' => '\n',
        't' => '\t',
        _ => c,
    };
}
