using System.Text.RegularExpressions;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// 지움 표시가 선 요청글·댓글을 <b>실제로</b> 지운다 — 시스템관리자만 부른다.
/// 줄만 지우면 FileServer 의 파일(첨부 · 본문에 붙인 그림)과 옛 로컬 그림 폴더가 주인 없이 남는다. 그래서 파일부터 지우고,
/// 파일 하나라도 못 지우면 줄을 남긴 채 멈춘다 — 줄이 있어야 다시 눌러 마저 지울 수 있다(이미 없는 파일은 지운 것으로 본다).
/// </summary>
public interface IRequestPurger {
  /// <summary>요청글과 그 댓글 · 첨부 · 그림을 지운다. 지움 표시가 없는 글은 거절한다. 없는 글이면 null.</summary>
  Task<PurgeResult?> PurgeRequestAsync(int requestId, HttpContext http);

  /// <summary>댓글 하나와 그 첨부 · 그림을 지운다. 지움 표시가 없는 댓글은 거절한다. 달린 답글은 지운 댓글의 부모로 옮겨 단다.</summary>
  Task<PurgeResult?> PurgeCommentAsync(int commentId, HttpContext http);
}

/// <summary>완전삭제 결과 — 지운 줄과 파일 수.</summary>
public sealed record PurgeResult(int Id, int Comments, int Attachments, int Files);

/// <inheritdoc />
public sealed class RequestPurger(
    AppDbContext db,
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    ILogger<RequestPurger> logger) : IRequestPurger {

  /// <summary>본문 HTML 에 박힌 FileServer 그림 주소(FileUploadEndpoints 의 /image 가 돌려준 값).</summary>
  private static readonly Regex BodyFileId = new(
      @"/api/file/(?:download|thumbnail|medium)/id/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
      RegexOptions.Compiled);

  private static readonly string[] CommentEntityTypes = ["ImprovementComment", "Comment"];

  public async Task<PurgeResult?> PurgeRequestAsync(int requestId, HttpContext http) {
    http.GetHelpdeskPrincipal().RequireSystemAdmin();
    var ct = http.RequestAborted;

    var req = await db.Requests.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == requestId, ct);
    if (req is null) return null;
    if (!req.IsDel) throw new InvalidOperationException("삭제 처리된 요청만 완전삭제할 수 있습니다.");

    var comments = await db.Comments.IgnoreQueryFilters().Where(c => c.RequestId == requestId).ToListAsync(ct);
    var commentIds = comments.Select(c => c.Id).ToList();

    var attachments = await db.Attachments
        .Where(a => (a.EntityType == "ImprovementRequest" && a.EntityId == requestId)
                    || (CommentEntityTypes.Contains(a.EntityType) && commentIds.Contains(a.EntityId)))
        .ToListAsync(ct);

    var texts = comments.Select(c => c.CommentText).Append(req.Description);
    var files = await RemoveFilesAsync(attachments, texts, http);

    RemoveImageDir(requestId.ToString());
    foreach (var id in commentIds) RemoveImageDir("cmt_" + id);

    await using (var tx = await db.Database.BeginTransactionAsync(ct)) {
      db.Attachments.RemoveRange(attachments);
      await db.SaveChangesAsync(ct);

      // 댓글끼리의 부모 외래키가 RESTRICT 라 한 번에 지우면 걸린다. 부모를 먼저 끊는다.
      await db.Comments.IgnoreQueryFilters().Where(c => c.RequestId == requestId)
          .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, (int?)null), ct);
      await db.Comments.IgnoreQueryFilters().Where(c => c.RequestId == requestId).ExecuteDeleteAsync(ct);
      await db.Requests.IgnoreQueryFilters().Where(r => r.Id == requestId).ExecuteDeleteAsync(ct);

      await tx.CommitAsync(ct);
    }

    logger.LogWarning("요청글 완전삭제: 요청={Id} 댓글={Comments} 첨부={Attachments} 파일={Files} 사람={Who}",
        requestId, comments.Count, attachments.Count, files, http.AuditUser());
    return new PurgeResult(requestId, comments.Count, attachments.Count, files);
  }

  public async Task<PurgeResult?> PurgeCommentAsync(int commentId, HttpContext http) {
    http.GetHelpdeskPrincipal().RequireSystemAdmin();
    var ct = http.RequestAborted;

    var comment = await db.Comments.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == commentId, ct);
    if (comment is null) return null;
    if (!comment.IsDel) throw new InvalidOperationException("삭제 처리된 댓글만 완전삭제할 수 있습니다.");

    var attachments = await db.Attachments
        .Where(a => CommentEntityTypes.Contains(a.EntityType) && a.EntityId == commentId)
        .ToListAsync(ct);

    var files = await RemoveFilesAsync(attachments, [comment.CommentText], http);
    RemoveImageDir("cmt_" + commentId);

    await using (var tx = await db.Database.BeginTransactionAsync(ct)) {
      db.Attachments.RemoveRange(attachments);
      await db.SaveChangesAsync(ct);

      // 답글은 지운 댓글의 부모로 옮긴다 — 끊으면 답글이 뿌리로 튀어 오르고, 그대로 두면 외래키가 막는다.
      await db.Comments.IgnoreQueryFilters().Where(c => c.ParentCommentId == commentId)
          .ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentCommentId, comment.ParentCommentId), ct);
      await db.Comments.IgnoreQueryFilters().Where(c => c.Id == commentId).ExecuteDeleteAsync(ct);

      await tx.CommitAsync(ct);
    }

    logger.LogWarning("댓글 완전삭제: 댓글={Id} 요청={Request} 첨부={Attachments} 파일={Files} 사람={Who}",
        commentId, comment.RequestId, attachments.Count, files, http.AuditUser());
    return new PurgeResult(commentId, 1, attachments.Count, files);
  }

  /// <summary>첨부와 본문 그림을 FileServer · 로컬 저장소에서 지운다. 지운(또는 이미 없던) 파일 수를 돌려준다.</summary>
  private async Task<int> RemoveFilesAsync(IEnumerable<Attachment> attachments, IEnumerable<string?> htmls, HttpContext http) {
    var fileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var count = 0;

    foreach (var a in attachments) {
      if (!string.IsNullOrWhiteSpace(a.FileId)) {
        fileIds.Add(a.FileId);
        continue;
      }

      // FileServer 로 옮기기 전의 첨부는 로컬 저장소에 있다. 저장 폴더 밖을 가리키는 줄은 건드리지 않는다.
      if (string.IsNullOrWhiteSpace(a.FilePath) || string.IsNullOrWhiteSpace(a.StoredFileName)) continue;
      var baseDir = Path.GetFullPath(Environment.GetEnvironmentVariable("FileStorage_BasePath") ?? "/home/lee/jinAttachment");
      var prefix = Path.EndsInDirectorySeparator(baseDir) ? baseDir : baseDir + Path.DirectorySeparatorChar;
      var path = Path.GetFullPath(Path.Combine(a.FilePath, a.StoredFileName));
      if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
      if (File.Exists(path)) File.Delete(path);
      count++;
    }

    foreach (var html in htmls) {
      if (string.IsNullOrEmpty(html)) continue;
      foreach (Match m in BodyFileId.Matches(html)) fileIds.Add(m.Groups[1].Value);
    }

    if (fileIds.Count == 0) return count;

    var client = httpFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(30);
    var auth = http.Request.Headers.Authorization.ToString();
    var failures = new List<string>();

    foreach (var id in fileIds) {
      using var req = new HttpRequestMessage(HttpMethod.Delete, $"{FileBaseUrl()}/{id}");
      if (!string.IsNullOrWhiteSpace(auth)) req.Headers.TryAddWithoutValidation("Authorization", auth);

      try {
        using var res = await client.SendAsync(req, http.RequestAborted);
        // 404 는 이미 없는 것이다 — 지난번 완전삭제가 중간에 멈춘 자리를 다시 누를 때 그렇다.
        if (res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.NotFound) {
          count++;
          continue;
        }

        var body = await res.Content.ReadAsStringAsync(http.RequestAborted);
        logger.LogError("FileServer 파일 삭제 실패 file={File} status={Status} body={Body}", id, (int)res.StatusCode, body);
        failures.Add($"{id} (HTTP {(int)res.StatusCode})");
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) {
        logger.LogError(ex, "FileServer 파일 삭제 중 오류 file={File}", id);
        failures.Add($"{id} ({ex.Message})");
      }
    }

    if (failures.Count > 0) {
      throw new InvalidOperationException(
          $"파일 {failures.Count}개를 지우지 못해 완전삭제를 멈췄습니다. 다시 시도해 주십시오: {string.Join(", ", failures)}");
    }

    return count;
  }

  /// <summary>FileServer 주소(게이트웨이 경유). 올리는 주소(FileServer:UploadUrl)에서 끝의 /upload 를 떼어 쓴다 — 설정을 하나 더 두지 않는다.</summary>
  private string FileBaseUrl() {
    var upload = configuration.GetValue<string>("FileServer:UploadUrl")
                 ?? Environment.GetEnvironmentVariable("FileServer_UploadUrl")
                 ?? "http://localhost:5265/api/file/upload";
    upload = upload.TrimEnd('/');
    return upload.EndsWith("/upload", StringComparison.OrdinalIgnoreCase) ? upload[..^"/upload".Length] : upload;
  }

  /// <summary>옛 길(base64 를 로컬 디스크에 떨구던 때)의 그림 폴더.</summary>
  private void RemoveImageDir(string subDir) {
    var storageBase = Environment.GetEnvironmentVariable("ImageStorage_BasePath") ?? "/home/lee/JinHelpContents/reqs";
    try {
      var dir = Path.Combine(storageBase, subDir);
      if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
    catch (Exception ex) {
      logger.LogWarning(ex, "그림 폴더를 지우지 못했습니다: {Dir}", subDir);
    }
  }
}
