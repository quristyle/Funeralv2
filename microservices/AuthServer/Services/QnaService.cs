using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// Q&amp;A 서비스 구현체
/// </summary>
/// <remarks>
/// 질문과 답글을 한 테이블에 담고 부모 관계로 잇는다(<see cref="QnaPost"/>).
/// 그래서 답글의 답글의 답글도 특별한 처리 없이 그대로 이어진다.
///
/// [보이는 범위]
/// 관리자는 전부, 그 외는 '공개된 글 + 내가 쓴 글' 만 본다.
/// 부모가 안 보이면 그 아래 답글도 함께 감춘다 — 무엇에 대한 답인지 알 수 없는
/// 답글만 떠 있으면 오히려 혼란스럽기 때문이다.
///
/// [권한]
/// 화면의 `v-perm` 은 버튼을 숨기는 장치일 뿐이다. 요청을 직접 보내면 통과하므로
/// 쓰기 권한은 여기서 다시 확인한다.
/// </remarks>
public class QnaService : IQnaService
{
    /// <summary>권한을 읽어 올 메뉴 경로</summary>
    private const string MenuPath = "/help/qna";

    private readonly AppDbContext _db;
    private readonly IMenuService _menus;

    public QnaService(AppDbContext db, IMenuService menus)
    {
        _db = db;
        _menus = menus;
    }

    public async Task<bool> CanManageAsync(string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        return perm.CanCust1;
    }

    public async Task<QnaListDto> GetListAsync(
        string userId, string? keyword, string? filter, int page, int pageSize)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        var canManage = perm.CanCust1;

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        // ── 볼 수 있는 질문(뿌리) 고르기 ────────────────────
        var roots = _db.QnaPosts.Where(p => !p.IsDeleted && p.ParentId == null);

        if (!canManage)
        {
            roots = roots.Where(p => p.IsPublic || p.AuthorId == userId);
        }

        switch (filter)
        {
            case "mine":
                roots = roots.Where(p => p.AuthorId == userId);
                break;

            // 공개 대기는 관리자에게만 뜻이 있다. 다른 사용자에게는 무시한다.
            case "pending" when canManage:
                roots = roots.Where(p => !p.IsPublic);
                break;

            case "unanswered":
                roots = roots.Where(p => !_db.QnaPosts
                    .Any(c => c.RootId == p.Id && !c.IsDeleted && c.IsAnswer));
                break;
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();

            // 스레드 안의 어느 글이든 걸리면 그 질문을 보여준다.
            // 단, 내가 볼 수 없는 글로는 검색되지 않게 같은 조건을 안쪽에도 건다.
            roots = roots.Where(p => _db.QnaPosts.Any(c =>
                c.RootId == p.Id && !c.IsDeleted &&
                (canManage || c.IsPublic || c.AuthorId == userId) &&
                ((c.Title != null && EF.Functions.ILike(c.Title, $"%{kw}%")) ||
                 EF.Functions.ILike(c.Content, $"%{kw}%"))));
        }

        var total = await roots.CountAsync();

        var pageRoots = await roots
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // ── 고른 스레드를 통째로 가져와 트리로 만든다 ───────
        var rootIds = pageRoots.Select(p => p.Id).ToList();

        var posts = await _db.QnaPosts
            .Where(p => rootIds.Contains(p.RootId) && !p.IsDeleted)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        // 작성자 사진은 글에 새겨 두지 않고 여기서 한 번에 읽는다.
        var avatars = await LoadAvatarsAsync(posts.Select(p => p.AuthorId));

        var items = pageRoots
            .Select(root => BuildThread(root, posts, userId, canManage, avatars))
            .Where(dto => dto is not null)
            .Select(dto => dto!)
            .ToList();

        return new QnaListDto
        {
            Items = items,
            CanManage = canManage,
            CanWrite = perm.CanCreate,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<QnaPostDto?> GetThreadAsync(string userId, string id)
    {
        var canManage = await CanManageAsync(userId);

        var anchor = await _db.QnaPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (anchor is null) return null;

        var posts = await _db.QnaPosts
            .Where(p => p.RootId == anchor.RootId && !p.IsDeleted)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        var root = posts.FirstOrDefault(p => p.Id == anchor.RootId);
        if (root is null) return null;

        var avatars = await LoadAvatarsAsync(posts.Select(p => p.AuthorId));

        return BuildThread(root, posts, userId, canManage, avatars);
    }

    public async Task<(QnaResult Result, QnaPostDto? Post)> CreateAsync(
        string userId, CreateQnaPostDto request)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        if (!perm.CanCreate) return (QnaResult.Forbidden, null);

        var canManage = perm.CanCust1;

        // Q&A 는 일반 사용자도 본문을 쓴다. 화면 편집기를 믿지 않고 여기서 세탁한다.
        // 세탁한 뒤에 비어 있는지 본다 — 편집기는 빈 본문도 `<p></p>` 로 보낸다.
        // 영상 넣기는 관리자가 쓴 글에만 허용한다.
        var content = RichTextSanitizer.Sanitize(request.Content, allowEmbeds: canManage)
                      ?? string.Empty;
        if (RichTextSanitizer.IsEmpty(content)) return (QnaResult.Invalid, null);

        var post = new QnaPost
        {
            Content = content,
            AuthorId = userId,
            AuthorName = await ResolveAuthorNameAsync(userId),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        if (string.IsNullOrWhiteSpace(request.ParentId))
        {
            // 질문(스레드 뿌리)
            if (string.IsNullOrWhiteSpace(request.Title)) return (QnaResult.Invalid, null);

            post.Title = request.Title.Trim();
            post.ParentId = null;
            post.RootId = post.Id;   // 뿌리는 자기 자신을 가리킨다
            post.Depth = 0;
        }
        else
        {
            var parent = await _db.QnaPosts
                .FirstOrDefaultAsync(p => p.Id == request.ParentId && !p.IsDeleted);

            if (parent is null) return (QnaResult.NotFound, null);

            // 볼 수 없는 글에는 답글을 달 수 없다.
            if (!IsVisible(parent, userId, canManage)) return (QnaResult.NotFound, null);

            // 답글은 제목을 쓰지 않는다. 스레드 제목은 뿌리 하나뿐이다.
            post.Title = null;
            post.ParentId = parent.Id;
            post.RootId = parent.RootId;
            post.Depth = parent.Depth + 1;

            // 관리자가 단 답글은 '답변' 으로 표시한다.
            post.IsAnswer = canManage;
        }

        // 공개 여부는 관리자만 정한다.
        // 일반 사용자의 글은 비공개로 들어간다 — 관리자가 공개해야 남에게 보인다.
        // 관리자의 글은 기본 공개다. 답변을 공개해야 질문자가 볼 수 있다.
        post.IsPublic = canManage && (request.IsPublic ?? true);

        _db.QnaPosts.Add(post);
        await _db.SaveChangesAsync();

        // 화면이 그 스레드만 다시 그릴 수 있게 스레드 전체를 돌려준다.
        var thread = await GetThreadAsync(userId, post.Id);
        if (thread is not null) return (QnaResult.Ok, thread);

        var avatars = await LoadAvatarsAsync([post.AuthorId]);
        return (QnaResult.Ok, ToDto(post, userId, canManage, avatars));
    }

    public async Task<QnaResult> UpdateAsync(string userId, string id, UpdateQnaPostDto request)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        var canManage = perm.CanCust1;

        var post = await _db.QnaPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return QnaResult.NotFound;

        var mine = post.AuthorId == userId;

        // 관리자는 남의 글도 고친다. 그 외에는 자기 글 + 수정 권한이 있어야 한다.
        if (!canManage && !(mine && perm.CanUpdate)) return QnaResult.Forbidden;

        // 영상 넣기는 관리자가 고칠 때만 허용한다.
        var content = RichTextSanitizer.Sanitize(request.Content, allowEmbeds: canManage)
                      ?? string.Empty;
        if (RichTextSanitizer.IsEmpty(content)) return QnaResult.Invalid;

        post.Content = content;

        // 제목은 질문(뿌리)만 갖는다. 답글에 온 제목은 무시한다.
        if (post.ParentId is null && !string.IsNullOrWhiteSpace(request.Title))
        {
            post.Title = request.Title.Trim();
        }

        // 공개 여부는 관리자만 바꾼다.
        if (canManage && request.IsPublic.HasValue)
        {
            post.IsPublic = request.IsPublic.Value;
        }

        post.UpdatedAt = DateTime.UtcNow;
        post.UpdatedBy = userId;

        await _db.SaveChangesAsync();
        return QnaResult.Ok;
    }

    public async Task<QnaResult> DeleteAsync(string userId, string id)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        var canManage = perm.CanCust1;

        var post = await _db.QnaPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return QnaResult.NotFound;

        var mine = post.AuthorId == userId;
        if (!canManage && !(mine && perm.CanDelete)) return QnaResult.Forbidden;

        // 답글을 남겨 두면 무엇에 대한 답인지 알 수 없는 글이 된다. 함께 지운다.
        var thread = await _db.QnaPosts
            .Where(p => p.RootId == post.RootId && !p.IsDeleted)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var target in CollectSubtree(post, thread))
        {
            target.IsDeleted = true;
            target.UpdatedAt = now;
            target.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync();
        return QnaResult.Ok;
    }

    public async Task<QnaResult> SetVisibilityAsync(string userId, string id, QnaVisibilityDto request)
    {
        if (!await CanManageAsync(userId)) return QnaResult.Forbidden;

        var post = await _db.QnaPosts.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (post is null) return QnaResult.NotFound;

        var now = DateTime.UtcNow;

        if (request.IncludeReplies)
        {
            var thread = await _db.QnaPosts
                .Where(p => p.RootId == post.RootId && !p.IsDeleted)
                .ToListAsync();

            foreach (var target in CollectSubtree(post, thread))
            {
                target.IsPublic = request.IsPublic;
                target.UpdatedAt = now;
                target.UpdatedBy = userId;
            }
        }
        else
        {
            post.IsPublic = request.IsPublic;
            post.UpdatedAt = now;
            post.UpdatedBy = userId;
        }

        await _db.SaveChangesAsync();
        return QnaResult.Ok;
    }

    // ============================================================
    // 내부 도우미
    // ============================================================

    /// <summary>
    /// 이 사용자에게 이 글이 보이는지.
    /// 관리자는 전부, 그 외는 공개된 글과 자기가 쓴 글만 본다.
    /// </summary>
    private static bool IsVisible(QnaPost post, string userId, bool canManage)
        => canManage || post.IsPublic || post.AuthorId == userId;

    /// <summary>
    /// 스레드 하나를 트리로 만든다. 보이지 않는 글은 그 아래 답글까지 함께 뺀다.
    /// 뿌리가 보이지 않으면 null 을 돌려준다.
    /// </summary>
    private static QnaPostDto? BuildThread(
        QnaPost root, List<QnaPost> posts, string userId, bool canManage,
        Dictionary<string, string> avatars)
    {
        if (!IsVisible(root, userId, canManage)) return null;

        var byParent = posts
            .Where(p => p.RootId == root.RootId && p.ParentId != null)
            .GroupBy(p => p.ParentId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CreatedAt).ToList());

        var dto = ToDto(root, userId, canManage, avatars);
        Attach(dto, root.Id);

        // 뿌리에만 요약값을 채운다. 목록에서 한 줄로 보여줄 값들이다.
        var visible = Flatten(dto).ToList();
        dto.ReplyCount = visible.Count - 1;
        dto.IsAnswered = visible.Any(p => p.IsAnswer);
        dto.LastPostedAt = visible.Max(p => p.CreatedAt);

        return dto;

        void Attach(QnaPostDto parentDto, string parentId)
        {
            if (!byParent.TryGetValue(parentId, out var children)) return;

            foreach (var child in children)
            {
                // 안 보이는 답글은 그 아래까지 통째로 뺀다.
                if (!IsVisible(child, userId, canManage)) continue;

                var childDto = ToDto(child, userId, canManage, avatars);
                parentDto.Children.Add(childDto);
                Attach(childDto, child.Id);
            }
        }
    }

    /// <summary>트리를 평평하게 훑는다. 요약값 계산에 쓴다.</summary>
    private static IEnumerable<QnaPostDto> Flatten(QnaPostDto node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Flatten(child)) yield return descendant;
        }
    }

    /// <summary>
    /// 자신과 모든 답글(깊이 제한 없음)을 모은다.
    /// 삭제·공개 변경을 스레드 아래로 함께 적용할 때 쓴다.
    /// </summary>
    private static List<QnaPost> CollectSubtree(QnaPost start, List<QnaPost> thread)
    {
        var result = new List<QnaPost> { start };
        var frontier = new List<string> { start.Id };

        while (frontier.Count > 0)
        {
            var children = thread
                .Where(p => p.ParentId != null && frontier.Contains(p.ParentId))
                .ToList();

            if (children.Count == 0) break;

            result.AddRange(children);
            frontier = children.Select(c => c.Id).ToList();
        }

        return result;
    }

    /// <summary>
    /// 글쓴이들의 프로필 사진 주소를 한 번에 읽는다.
    /// </summary>
    /// <remarks>
    /// 사진은 `account_profile_details` 의 `Avatar` 항목에 있다(계정 하나에 여러 항목이 있다).
    ///
    /// 글마다 계정을 따로 조회하면 스레드 하나에 답글 20개면 20번을 묻게 된다.
    /// 그래서 화면에 그릴 글의 작성자를 모아 한 번에 읽는다.
    ///
    /// 돌려주는 사전은 **계정 키와 로그인 아이디 둘 다**로 찾을 수 있게 담는다 —
    /// `qna_posts.author_id` 에 무엇이 들어 있어도 찾아진다.
    /// 사진이 없는 계정은 담지 않는다. 화면이 이름 첫 글자로 대신 그린다.
    /// </remarks>
    private async Task<Dictionary<string, string>> LoadAvatarsAsync(
        IEnumerable<string?> authorIds)
    {
        var ids = authorIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new Dictionary<string, string>();

        var rows = await _db.Accounts
            .Where(a => !a.IsDeleted && (ids.Contains(a.UserId) || ids.Contains(a.Id)))
            .Select(a => new
            {
                a.Id,
                a.UserId,
                Photo = a.ProfileDetails!
                    .Where(p => p.DetailType == "Avatar" && !p.IsDeleted)
                    .Select(p => p.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var map = new Dictionary<string, string>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Photo)) continue;

            map[row.Id] = row.Photo;
            if (!string.IsNullOrWhiteSpace(row.UserId)) map[row.UserId] = row.Photo;
        }

        return map;
    }

    /// <summary>
    /// 표시할 작성자 이름을 찾는다.
    /// 게이트웨이가 넘기는 값은 로그인 아이디라서 그대로 쓰면 화면이 딱딱하다.
    /// </summary>
    private async Task<string?> ResolveAuthorNameAsync(string userId)
    {
        var account = await _db.Accounts
            .Where(a => !a.IsDeleted && (a.UserId == userId || a.Id == userId))
            .Select(a => new { a.UserName, a.RealName })
            .FirstOrDefaultAsync();

        if (account is null) return userId;

        return string.IsNullOrWhiteSpace(account.UserName)
            ? (string.IsNullOrWhiteSpace(account.RealName) ? userId : account.RealName)
            : account.UserName;
    }

    private static QnaPostDto ToDto(
        QnaPost p, string userId, bool canManage, Dictionary<string, string> avatars)
    {
        var mine = p.AuthorId == userId;

        return new QnaPostDto
        {
            AuthorAvatar = p.AuthorId is not null && avatars.TryGetValue(p.AuthorId, out var photo)
                ? photo
                : null,
            Id = p.Id,
            ParentId = p.ParentId,
            RootId = p.RootId,
            Depth = p.Depth,
            Title = p.Title,
            Content = p.Content,
            IsPublic = p.IsPublic,
            IsAnswer = p.IsAnswer,
            AuthorId = p.AuthorId,
            AuthorName = p.AuthorName,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            IsMine = mine,
            CanEdit = canManage || mine
        };
    }
}
