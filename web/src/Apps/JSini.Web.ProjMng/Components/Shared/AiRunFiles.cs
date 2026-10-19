using System.Text;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「처리 파일」 한 줄이 무엇을 당했나. git 의 --name-status 글자를 옮긴 것이고, Unknown 은 옛 건(diff_stat 만 있는 건)에서만 나온다.</summary>
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

/// <summary>추정 줄을 실제 파일에 대 보았나. 상태 칸 끝 글자로 온다(M? 아직 · M* 있음 · M! 없음 · M~ 여럿). 대 보는 것은 실행기다.</summary>
public enum AiRunFileCheck
{
    /// <summary>댈 것이 없다 — git 이 센 줄이다.</summary>
    None,

    /// <summary>추정 줄인데 <b>아직 안 대 봤다</b>.</summary>
    Pending,

    /// <summary>대 보니 <b>실제로 있는 파일</b>이다. 경로도 온 경로로 펴져 있다.</summary>
    Found,

    /// <summary>대 보니 <b>대상에 그런 파일이 없다</b>.</summary>
    Missing,

    /// <summary>이름만 맞는 것이 <b>여럿이라 고르지 못했다</b>.</summary>
    Ambiguous,
}

/// <summary>처리 파일 한 줄.</summary>
public sealed record AiRunFile(
    AiRunFileKind Kind,
    string Path,
    bool Derived = false,
    AiRunFileCheck Check = AiRunFileCheck.None)
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

    /// <summary>배지에 적을 한 마디. Unknown 이면 빈 글자다.</summary>
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

    /// <summary>배지에 걸 꾸밈 이름. 색은 CSS 가 정한다. 추정 줄은 확인된 것과 같은 색이면 구별되지 않아 따로 쓰고, 없는 경로는 틀린 것이라 한 칸 더 간다.</summary>
    public string KindClass => Check == AiRunFileCheck.Missing
        ? "pm-peek__chg-mark--gone"
        : Derived
            ? "pm-peek__chg-mark--guess"
            : Kind switch
            {
                AiRunFileKind.Added => "pm-peek__chg-mark--add",
                AiRunFileKind.Deleted => "pm-peek__chg-mark--del",
                AiRunFileKind.Modified or AiRunFileKind.TypeChanged => "pm-peek__chg-mark--mod",
                _ => "pm-peek__chg-mark--move",
            };

    /// <summary>배지 뒤에 붙는 확인 표 한 글자. 색만으로는 색을 못 가리는 사람에게 말을 건다.</summary>
    public string CheckMark => Check switch
    {
        AiRunFileCheck.Found => "✓",
        AiRunFileCheck.Missing => "✕",
        AiRunFileCheck.Ambiguous => "?",
        AiRunFileCheck.Pending => "?",
        _ => string.Empty,
    };

    /// <summary>줄에 마우스를 올렸을 때의 설명. 추정 줄에만 있다 — 왜 다른 색인지 어딘가에 적혀 있어야 한다.</summary>
    public string Hint => Check switch
    {
        AiRunFileCheck.Found =>
            $"{Path} (AI 의 결과문에서 뽑은 경로입니다. 대상에 실제로 있는 파일임을 확인했습니다.)",
        AiRunFileCheck.Missing =>
            $"{Path} (AI 의 결과문에서 뽑은 경로인데, 대상에 그런 파일이 없습니다.)",
        AiRunFileCheck.Ambiguous =>
            $"{Path} (AI 의 결과문에서 뽑은 경로입니다. 같은 이름의 파일이 여럿이라 어느 것인지 가리지 못했습니다.)",
        AiRunFileCheck.Pending =>
            $"{Path} (AI 의 결과문에서 뽑은 경로입니다. 아직 실제 파일에 대 보지 않았습니다.)",
        _ => Path,
    };
}

/// <summary>실행 한 번이 건드린 파일 목록을 읽는다. 정본은 ai_task_run.changed_files(커밋 전의 새 파일도 들어 있다), 칸이 생기기 전 건은 diff_stat 을 되읽는다. 화면은 다시 세지 않고 적혀 있는 것을 그릴 뿐이다.</summary>
public static class AiRunFiles
{
    /// <summary>적혀 있는 것을 목록으로. 읽을 것이 없으면 빈 목록이고, 그때 화면은 「처리 파일」 칸을 그리지 않는다.</summary>
    public static IReadOnlyList<AiRunFile> Parse(string? changedFiles, string? diffStat)
    {
        var rows = FromChanged(changedFiles);

        return rows.Count > 0 ? rows : FromStat(diffStat);
    }

    /// <summary>정본 — 상태\t경로 줄들.</summary>
    private static List<AiRunFile> FromChanged(string? text)
    {
        var rows = new List<AiRunFile>();

        foreach (var line in Lines(text))
        {
            // 마지막 칸이 지금 경로다. 이름이 바뀐 줄은 R100\t옛것\t지금것 으로 셋이라, 앞 탭만 보면 경로가 틀린다.
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

            // 상태 칸의 마지막 한 글자가 확인 표다 — 붙어 있으면 git 이 본 것이 아니라 결과문에서 뽑은 줄이다. 서버가 합치면서 달고, 실행기가 대 본 뒤 바꾼다.
            var check = CheckOf(cols[0]);

            rows.Add(new AiRunFile(KindOf(cols[0]), path, check != AiRunFileCheck.None, check));
        }

        return rows;
    }

    /// <summary>옛 건 — 경로 | 12 ++-- 줄들. 합계 줄은 파일이 아니라 버리고, 칸막이가 없는 토막은 집지 않는다.</summary>
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

    /// <summary>상태 칸 하나를 갈래로. 앞 한 글자만 본다 — 닮은 정도가 붙은 R100 이나 추정 표가 붙은 M? 도 같은 갈래로 읽힌다.</summary>
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

    /// <summary>상태 칸 하나에서 확인 표만 읽는다 — 마지막 한 글자다. 표가 없으면 git 이 센 줄이고, 표를 쓰기 전에 쌓인 M? 는 Pending 으로 읽힌다.</summary>
    private static AiRunFileCheck CheckOf(string mark) =>
        (mark.Length > 1 ? mark[^1] : ' ') switch
        {
            '?' => AiRunFileCheck.Pending,
            '*' => AiRunFileCheck.Found,
            '!' => AiRunFileCheck.Missing,
            '~' => AiRunFileCheck.Ambiguous,
            _ => AiRunFileCheck.None,
        };

    private static IEnumerable<string> Lines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r", string.Empty)
                  .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>git 이 따옴표로 싸서 낸 경로를 사람이 읽는 글자로 되돌린다. 바이트로 모아 한 번에 푼다(UTF-8 글자가 여러 탈출에 걸친다). 이미 쌓인 diff_stat 의 8진 탈출을 되돌리지 않으면 한국어 이름이 역슬래시 더미로 보인다.</summary>
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

            // \" · \\ · \t 따위. 모르는 것은 뒤 글자를 그대로 쓴다.
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
