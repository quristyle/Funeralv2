using System.Text;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>인터페이스 첨부. 표를 만들지 않고 파일 이름에 메타를 담는다 — 저장소는 <c>data/if_files/{프로젝트}/{인터페이스}/{파일번호}__{원래 이름}</c> 한 줄이고, 목록은 디렉터리를 읽어 만든다.</summary>
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

    /// <summary>경로 조작과 제어문자를 막고 길이를 줄인다. 확장자는 그대로 둔다 — 내려받은 사람이 무엇인지 알아야 한다.</summary>
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

            var id = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..22];
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
