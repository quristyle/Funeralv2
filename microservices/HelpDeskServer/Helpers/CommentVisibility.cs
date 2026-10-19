using HelpDeskServer.Models;

namespace HelpDeskServer.Helpers;

/// <summary>지워진 댓글 가운데 <b>어느 것을 화면에 남길지</b> 가리는 한 벌의 규칙. 댓글 삭제는 줄을 지우지 않고 지움 표시만 세운다</summary>
public static class CommentVisibility {
  /// <summary>지워진 줄 자리에 대신 내보내는 글.</summary>
  public const string DeletedText = "삭제된 댓글입니다.";

  /// <summary>화면에 남길 댓글만 추린다. 들어온 순서를 그대로 지킨다.</summary>
  public static List<ImprovementComment> Visible(IEnumerable<ImprovementComment>? comments) {
    var all = (comments ?? []).Where(c => c is not null).ToList();
    if (all.Count == 0) return all;

    var byId = new Dictionary<int, ImprovementComment>();
    foreach (var c in all) {
      // 같은 번호가 둘 오면 앞엣것을 쓴다 — 뒤엣것으로 덮으면 이미 타고
      // 올라간 길과 어긋난다.
      byId.TryAdd(c.Id, c);
    }

    var keep = new HashSet<int>();

    foreach (var c in all) {
      // 살아 있는 줄은 그대로 남고, 그 줄을 떠받치는 부모들도 함께 남는다.
      if (c.IsDel) continue;

      keep.Add(c.Id);

      var walker = c;
      var seen = new HashSet<int> { c.Id };

      while (walker.ParentCommentId is { } up && byId.TryGetValue(up, out var parent)) {
        // 자료가 스스로를 가리키거나 두 줄이 서로를 가리키면 고리다.
        // 끊지 않으면 여기서 영영 돈다.
        if (!seen.Add(up)) break;

        keep.Add(up);
        walker = parent;
      }
    }

    return all.Where(c => keep.Contains(c.Id)).ToList();
  }

  /// <summary>지워진 줄의 본문을 안내 문구로 바꾼다. <b>원문은 응답에 싣지 않는다.</b> 화면이 안 그린다고 내보내도 되는 것이 아니다</summary>
  public static List<ImprovementComment> Mask(List<ImprovementComment> comments) {
    foreach (var c in comments) {
      if (c.IsDel) c.CommentText = DeletedText;
    }

    return comments;
  }
}
