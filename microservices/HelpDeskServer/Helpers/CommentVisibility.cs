using HelpDeskServer.Models;

namespace HelpDeskServer.Helpers;

/// <summary>
/// 지워진 댓글 가운데 <b>어느 것을 화면에 남길지</b> 가리는 한 벌의 규칙.
/// </summary>
/// <remarks>
/// <para>
/// 댓글 삭제는 줄을 지우지 않고 지움 표시만 세운다
/// (<c>DELETE /api/comments/{id}</c> → <c>IsDel = true</c>). 그래서 조회하는
/// 쪽마다 그 줄을 어떻게 다룰지 정해야 하는데, 양 끝은 둘 다 나쁘다.
/// </para>
///
/// <para>
/// <b>전부 빼면</b> 거기 달려 있던 답글이 부모를 잃는다. 프론트의
/// <c>CommentTree.Build</c> 는 부모를 못 찾은 줄을 버리지 않고 뿌리로
/// 올리므로, 답글이 난데없이 맨 위로 튀어 올라 오가던 말의 앞뒤가 어긋난다.
/// </para>
///
/// <para>
/// <b>전부 남기면</b> 아무도 답하지 않은 지운 댓글까지 「삭제된 댓글입니다.」
/// 한 줄로 화면에 쌓인다. 읽는 사람에게는 지운 것이 안 지워진 것으로 보인다.
/// </para>
///
/// <para>
/// 그래서 가운데를 고른다 — <b>답글을 떠받치고 있는 지운 댓글만 남긴다.</b>
/// 보이는 줄은 <i>안 지워졌거나, 아래 어딘가에 안 지워진 자손이 있는 줄</i>이고,
/// 지운 줄만 모인 가지는 뿌리까지 통째로 빠진다.
/// </para>
///
/// <para>
/// 남긴 줄도 <b>내용은 내보내지 않는다</b>(<see cref="Mask"/>) — 자리를
/// 지키는 것이 할 일이지 지운 글을 다시 보여 주는 것이 아니다.
/// </para>
/// </remarks>
public static class CommentVisibility {
  /// <summary>지워진 줄 자리에 대신 내보내는 글.</summary>
  public const string DeletedText = "삭제된 댓글입니다.";

  /// <summary>
  /// 화면에 남길 댓글만 추린다. 들어온 순서를 그대로 지킨다.
  /// </summary>
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

  /// <summary>
  /// 지워진 줄의 본문을 안내 문구로 바꾼다. <b>원문은 응답에 싣지 않는다.</b>
  /// </summary>
  /// <remarks>
  /// 화면이 안 그린다고 내보내도 되는 것이 아니다 — 응답 본문은 개발자 도구에
  /// 그대로 보인다.
  /// </remarks>
  public static List<ImprovementComment> Mask(List<ImprovementComment> comments) {
    foreach (var c in comments) {
      if (c.IsDel) c.CommentText = DeletedText;
    }

    return comments;
  }
}
