using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FileServer.Services;

/// <summary>
/// 파일 저장소에 **실제로 쓸 수 있는지** 점검한다.
/// </summary>
/// <remarks>
/// <para>
/// 이 서비스의 일은 파일을 보관하고 내주는 것이다. 프로세스가 멀쩡해도
/// 저장 경로가 없거나 디스크가 꽉 차면 업로드가 전부 실패한다.
/// 그런데 그 사실은 지금까지 <b>사용자가 업로드를 시도할 때에만</b> 드러났다.
/// </para>
///
/// <para>
/// <b>존재 확인만으로는 부족하다.</b> 디렉터리가 있어도 권한이 없거나 읽기 전용으로
/// 마운트되면 쓰기가 안 된다. 그래서 작은 파일을 실제로 만들고 지운다 —
/// "보관할 수 있는가" 를 묻는 것이 목적이므로 그것이 유일한 정직한 확인이다.
/// </para>
///
/// <para>
/// <b>경로는 FileService 와 똑같은 방법으로 찾는다</b>(<c>Storage:LocalPath</c>,
/// 없으면 <c>&lt;실행 폴더&gt;/Uploads</c>). 다르게 찾으면 점검은 통과하는데
/// 실제 업로드는 실패하는, 최악의 어긋남이 생긴다.
/// </para>
///
/// <para>
/// <b>상태를 세 갈래로 나눈다.</b> 원인에 따라 할 일이 다르기 때문이다.
/// </para>
/// <list type="bullet">
///   <item>
///     경로가 없거나 접근 불가 → <c>Unhealthy</c>.
///     내주는 것도 보관하는 것도 안 되므로 서비스가 제 기능을 못 한다.
///   </item>
///   <item>
///     있지만 쓸 수 없음 → <c>Degraded</c>.
///     이미 있는 파일 <b>내보내기는 되지만</b> 업로드가 막힌다.
///   </item>
///   <item>
///     쓸 수 있지만 여유 공간이 매우 적음 → <c>Degraded</c>.
///     지금은 되지만 곧 멈춘다. 미리 알아야 하는 종류의 문제다.
///   </item>
/// </list>
/// </remarks>
public static class StorageHealthCheck
{
    /// <summary>점검 이름. 상태 화면이 이 이름으로 줄을 만든다.</summary>
    public const string Name = "storage";

    /// <summary>
    /// 이보다 여유가 적으면 알린다.
    /// 원본 사진·영상이 오가는 저장소라 1GB 는 곧 바닥난다는 뜻이다.
    /// </summary>
    private const long LowFreeBytes = 1L * 1024 * 1024 * 1024;

    /// <summary>
    /// <c>FileService</c> 와 같은 규칙으로 저장 경로를 찾는다.
    /// </summary>
    /// <remarks>
    /// 두 곳이 갈라지면 점검이 거짓을 말한다. 규칙을 바꿀 때는 반드시 함께 바꾼다.
    /// </remarks>
    public static string ResolvePath(IConfiguration configuration) =>
        configuration["Storage:LocalPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "Uploads");

    /// <summary>점검 본체. <c>AddDependencyCheck</c> 에 넘겨 쓴다.</summary>
    public static Task<HealthCheckResult> ProbeAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var path = ResolvePath(configuration);

        var data = new Dictionary<string, object>
        {
            // 화면이 "어느 경로인지" 를 함께 보여 준다. 설정을 고칠 때 바로 찾아갈 수 있다.
            ["path"] = path,
        };

        if (!Directory.Exists(path))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"저장 경로가 없습니다: {path}", data: data));
        }

        // 여유 공간. 경로가 있는 드라이브 기준이다.
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                data["freeGb"] = Math.Round(drive.AvailableFreeSpace / (double)(1024 * 1024 * 1024), 1);
                data["totalGb"] = Math.Round(drive.TotalSize / (double)(1024 * 1024 * 1024), 1);
            }
        }
        catch
        {
            // 여유 공간을 못 읽는 것이 곧 고장은 아니다. 쓰기 확인이 본론이다.
        }

        // ── 실제로 써 본다 ────────────────────────────────────
        //
        // 존재·권한만 봐서는 읽기 전용 마운트나 디스크 꽉 참을 알 수 없다.
        // 아주 작은 파일을 만들고 바로 지운다. 이름에 GUID 를 넣어 동시 점검끼리 부딪히지 않게 한다.
        var probeFile = Path.Combine(path, $".health-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probeFile, new byte[] { 0x4a });
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "저장 경로가 있지만 쓸 수 없습니다. 이미 올린 파일 내보내기는 되지만 "
                + $"업로드가 실패합니다. ({ex.GetBaseException().Message})",
                data: data));
        }
        finally
        {
            // 지우지 못해도 점검 결과를 바꾸지 않는다 — 쓰기는 이미 성공했다.
            try { if (File.Exists(probeFile)) File.Delete(probeFile); } catch { /* 무시 */ }
        }

        if (data.TryGetValue("freeGb", out var freeObj)
            && freeObj is double freeGb
            && freeGb * 1024 * 1024 * 1024 < LowFreeBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"저장소 여유 공간이 {freeGb}GB 뿐입니다. 곧 업로드가 실패할 수 있습니다.",
                data: data));
        }

        var free = data.TryGetValue("freeGb", out var f) ? $" (여유 {f}GB)" : string.Empty;
        return Task.FromResult(HealthCheckResult.Healthy(
            $"저장소에 읽고 쓸 수 있습니다.{free}", data: data));
    }
}
