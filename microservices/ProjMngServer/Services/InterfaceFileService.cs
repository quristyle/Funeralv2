using System.Text;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 인터페이스 첨부. <b>표를 만들지 않고 파일 이름에 메타를 담는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <c>data/if_files/{프로젝트}/{인터페이스}/{파일번호}__{원래 이름}</c> 한 줄이
/// 저장소의 전부다. 목록은 디렉터리를 읽어 만든다. 올린 사람·설명까지 남기려면
/// 표가 필요하지만, 지금 화면이 보여 주는 것은 이름·크기·시각 셋뿐이다.
/// </para>
///
/// <para>
/// [내려받기를 정적 서비스에 맡기지 않는다]
/// </para>
///
/// <para>
/// 무엇이 올라오든 <b>늘 첨부로</b> 내려 준다. 정적 경로로 열어 두면 올라온
/// HTML 이 우리 출처에서 실행된다.
/// </para>
///
/// <para>
/// [개발 장비에 올린 것은 운영에 없다]
/// </para>
///
/// <para>
/// 바이트가 그 장비의 파일 시스템에 있고 DB 에는 흔적이 없다. 저장소 전체가
/// 그런 상태이므로(루트 CLAUDE.md) 새로 생기는 함정은 아니지만, 첨부가 보이지
/// 않는다는 신고가 오면 <b>여기부터 본다.</b>
/// </para>
/// </remarks>
public sealed class InterfaceFileService(IWebHostEnvironment env)
{
    /// <summary>파일 하나의 최대 크기.</summary>
    private const long MaxBytes = 25L * 1024 * 1024;

    /// <summary>한 번에 올릴 수 있는 개수.</summary>
    public const int MaxCount = 20;

    /// <summary>파일번호와 원래 이름을 가르는 글자.</summary>
    private const string Sep = "__";

    private string Root => Path.Combine(env.ContentRootPath, "data", "if_files");

    private string DirOf(int prjRid, int ifId) =>
        Path.Combine(Root, prjRid.ToString(), ifId.ToString());

    /// <summary>
    /// 경로 조작과 제어문자를 막고 길이를 줄인다. <b>확장자는 그대로 둔다</b> —
    /// 내려받은 사람이 무엇인지 알아야 한다.
    /// </summary>
    private static string SafeName(string? name)
    {
        var n = Path.GetFileName(name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(n)) n = "file";

        var bad = Path.GetInvalidFileNameChars()
            .Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|'])
            .ToHashSet();

        var sb = new StringBuilder(n.Length);
        foreach (var c in n) sb.Append(bad.Contains(c) || char.IsControl(c) ? '_' : c);

        // 앞의 점을 떼면 숨김파일과 `..` 흉내가 함께 막힌다.
        n = sb.ToString().TrimStart('.');

        if (n.Contains(Sep)) n = n.Replace(Sep, "_");
        if (n.Length > 120) n = n[..60] + "~" + n[^40..];

        return n.Length == 0 ? "file" : n;
    }

    /// <summary>우리가 만든 번호만 받는다 — 경로 조작을 여기서 끊는다.</summary>
    private static bool ValidId(string id) =>
        id.Length is > 8 and < 40 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private FileInfo? Find(int prjRid, int ifId, string fileId)
    {
        if (!ValidId(fileId)) return null;

        var dir = new DirectoryInfo(DirOf(prjRid, ifId));
        return dir.Exists ? dir.GetFiles($"{fileId}{Sep}*").FirstOrDefault() : null;
    }

    private static IfFileRow Shape(FileInfo f)
    {
        var i = f.Name.IndexOf(Sep, StringComparison.Ordinal);

        return new IfFileRow
        {
            FileId = i > 0 ? f.Name[..i] : f.Name,
            Name = i > 0 ? f.Name[(i + Sep.Length)..] : f.Name,
            Size = f.Length,
            Modified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
        };
    }

    /// <summary>인터페이스별 개수. 목록의 배지가 읽는다.</summary>
    public List<IfFileCount> Counts(int prjRid)
    {
        var root = new DirectoryInfo(Path.Combine(Root, prjRid.ToString()));
        if (!root.Exists) return [];

        return [.. root.GetDirectories()
            .Where(d => int.TryParse(d.Name, out _))
            .Select(d => new IfFileCount { IfId = int.Parse(d.Name), Cnt = d.GetFiles().Length })
            .Where(x => x.Cnt > 0)];
    }

    public List<IfFileRow> List(int prjRid, int ifId)
    {
        var dir = new DirectoryInfo(DirOf(prjRid, ifId));
        if (!dir.Exists) return [];

        return [.. dir.GetFiles().OrderByDescending(f => f.LastWriteTime).Select(Shape)];
    }

    /// <returns>담은 것들. 크기를 넘는 것이 있으면 <paramref name="tooBig"/> 에 그 이름.</returns>
    public async Task<List<IfFileRow>> SaveAsync(
        int prjRid, int ifId, IFormFileCollection files, Action<string> tooBig)
    {
        var over = files.FirstOrDefault(f => f.Length > MaxBytes);
        if (over is not null)
        {
            tooBig(over.FileName);
            return [];
        }

        var dir = DirOf(prjRid, ifId);
        Directory.CreateDirectory(dir);

        var saved = new List<IfFileRow>();

        foreach (var f in files)
        {
            if (f.Length == 0) continue;

            var id = $"{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..22];
            var path = Path.Combine(dir, $"{id}{Sep}{SafeName(f.FileName)}");

            await using (var fs = File.Create(path)) await f.CopyToAsync(fs);

            saved.Add(Shape(new FileInfo(path)));
        }

        return saved;
    }

    /// <returns>(전체 경로, 내려보낼 이름). 없으면 <c>null</c>.</returns>
    public (string Path, string Name)? Open(int prjRid, int ifId, string fileId)
    {
        var f = Find(prjRid, ifId, fileId);
        if (f is null) return null;

        var i = f.Name.IndexOf(Sep, StringComparison.Ordinal);
        return (f.FullName, i > 0 ? f.Name[(i + Sep.Length)..] : f.Name);
    }

    public bool Delete(int prjRid, int ifId, string fileId)
    {
        var f = Find(prjRid, ifId, fileId);
        if (f is null) return false;

        f.Delete();
        return true;
    }
}
