namespace ProjMngServer.Services;

/// <summary>
/// 소스 파일 읽기(md_source_context)가 받은 경로를 등록된 소스 뿌리 아래로 묶는다.
/// </summary>
/// 경로는 화면이 훑기 결과를 그대로 돌려보내는 값이지만 본문이라 바꿔 보낼 수 있다.
public static class SourcePathGuard {

  private static readonly StringComparison PathComparison =
    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

  private static readonly string[] DeniedPrefixes = ["/proc", "/etc", "/root", "/sys", "/dev"];

  private static readonly string[] DeniedKeyPrefixes = ["id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"];

  private static readonly string[] DeniedExtensions = [".env", ".pem", ".key", ".pfx", ".p12"];

  /// <summary>읽어도 되면 정규화한 경로를 <paramref name="fullPath"/> 에 담고 참.</summary>
  public static bool TryResolve(string? requested, IEnumerable<string> roots, out string fullPath, out string reason) {
    fullPath = string.Empty;

    if (string.IsNullOrWhiteSpace(requested) || !Path.IsPathRooted(requested)) {
      reason = "파일 경로가 올바르지 않습니다.";
      return false;
    }

    try {
      fullPath = Path.GetFullPath(requested);
    }
    catch (Exception) {
      reason = "파일 경로가 올바르지 않습니다.";
      return false;
    }

    if (IsDenied(fullPath)) {
      reason = "읽을 수 없는 파일입니다.";
      return false;
    }

    var target = fullPath;
    var root = UsableRoots(roots).FirstOrDefault(r => IsUnder(target, r));
    if (root is null) {
      reason = "등록된 소스 경로 밖의 파일은 읽을 수 없습니다.";
      return false;
    }

    // 뿌리 안의 심볼릭 링크는 밖을 가리킬 수 있어 따라가지 않는다.
    if (HasLinkBelow(root, fullPath)) {
      reason = "링크된 경로는 읽을 수 없습니다.";
      return false;
    }

    if (!File.Exists(fullPath)) {
      reason = "파일이 서버에 없습니다.";
      return false;
    }

    reason = string.Empty;
    return true;
  }

  private static IEnumerable<string> UsableRoots(IEnumerable<string> roots) {
    foreach (var raw in roots) {
      if (string.IsNullOrWhiteSpace(raw) || !Path.IsPathRooted(raw)) continue;

      string full;
      try {
        full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw.Trim()));
      }
      catch (Exception) {
        continue;
      }

      // 디스크 뿌리를 등록해 두면 모든 파일이 「뿌리 아래」가 된다.
      if (string.Equals(full, Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty), PathComparison)
          || full.Length <= 1
          || IsDenied(full)) {
        continue;
      }

      yield return full;
    }
  }

  private static bool IsUnder(string fullPath, string root) =>
    fullPath.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);

  private static bool IsDenied(string fullPath) {
    var normalized = fullPath.Replace('\\', '/');

    if (DeniedPrefixes.Any(p => normalized.Equals(p, StringComparison.OrdinalIgnoreCase)
                             || normalized.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))) {
      return true;
    }

    if (normalized.Split('/').Any(s => s.Equals(".ssh", StringComparison.OrdinalIgnoreCase))) {
      return true;
    }

    var name = Path.GetFileName(normalized).ToLowerInvariant();

    return (name.StartsWith("appsettings") && name.EndsWith(".json"))
        || name == ".env" || name.StartsWith(".env.")
        || DeniedExtensions.Any(name.EndsWith)
        || DeniedKeyPrefixes.Any(name.StartsWith);
  }

  private static bool HasLinkBelow(string root, string fullPath) {
    var current = fullPath;

    while (current.Length > root.Length) {
      FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
      if (info.Exists && info.LinkTarget is not null) return true;

      current = Path.GetDirectoryName(current) ?? root;
    }

    return false;
  }
}
