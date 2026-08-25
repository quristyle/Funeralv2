using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// F.A.Q 서비스 구현체
/// </summary>
/// <remarks>
/// F.A.Q 는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
///
/// 쓰기 권한은 화면을 믿지 않고 여기서 다시 확인한다. 화면의 `v-perm` 은
/// 버튼을 숨기는 장치일 뿐, 요청을 직접 보내면 그냥 통과하기 때문이다.
/// </remarks>
public class FaqService : IFaqService
{
    /// <summary>권한을 읽어 올 메뉴 경로</summary>
    private const string MenuPath = "/help/faq";

    private readonly AppDbContext _db;
    private readonly IMenuService _menus;

    public FaqService(AppDbContext db, IMenuService menus)
    {
        _db = db;
        _menus = menus;
    }

    public async Task<bool> CanManageAsync(string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);

        // 등록·수정·삭제 중 하나라도 있으면 관리자로 본다.
        // 셋을 따로 보면 "수정은 되는데 목록에 비활성이 안 보인다" 같은 어긋남이 생긴다.
        return perm.CanCreate || perm.CanUpdate || perm.CanDelete;
    }

    public async Task<FaqListDto> GetListAsync(string userId, string? keyword, string? category)
    {
        var canManage = await CanManageAsync(userId);

        var query = _db.Faqs.Where(f => !f.IsDeleted);

        // 비활성 항목은 관리자에게만 보인다.
        if (!canManage) query = query.Where(f => f.Status == 1);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim();
            query = query.Where(f => f.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(f =>
                EF.Functions.ILike(f.Question, $"%{kw}%") ||
                (f.Answer != null && EF.Functions.ILike(f.Answer, $"%{kw}%")));
        }

        var list = await query
            .OrderBy(f => f.Category)
            .ThenBy(f => f.OrderNo)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();

        // 분류 추천 목록은 검색 조건과 무관하게 전체에서 뽑는다.
        // 검색 결과에 없는 분류로도 등록할 수 있어야 한다.
        var categories = await _db.Faqs
            .Where(f => !f.IsDeleted && f.Category != null && f.Category != "")
            .Select(f => f.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return new FaqListDto
        {
            Items = list.Select(ToDto).ToList(),
            CanManage = canManage,
            Categories = categories
        };
    }

    public async Task<FaqDto?> GetByIdAsync(string userId, string id)
    {
        var canManage = await CanManageAsync(userId);

        var faq = await _db.Faqs
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted && (canManage || f.Status == 1));

        return faq is null ? null : ToDto(faq);
    }

    public async Task<FaqDto?> CreateAsync(SaveFaqDto request, string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        if (!perm.CanCreate) return null;

        var faq = new Faq
        {
            Category = Trim(request.Category),
            Question = request.Question.Trim(),
            // 본문은 화면을 믿지 않고 저장 전에 세탁한다.
            // F.A.Q 는 관리자만 쓰므로(바로 위에서 확인했다) 영상 넣기를 허용한다.
            Answer = RichTextSanitizer.Sanitize(request.Answer, allowEmbeds: true),
            OrderNo = request.OrderNo,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _db.Faqs.Add(faq);
        await _db.SaveChangesAsync();

        return ToDto(faq);
    }

    public async Task<FaqSaveResult> UpdateAsync(string id, SaveFaqDto request, string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        if (!perm.CanUpdate) return FaqSaveResult.Forbidden;

        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        if (faq is null) return FaqSaveResult.NotFound;

        faq.Category = Trim(request.Category);
        faq.Question = request.Question.Trim();
        faq.Answer = RichTextSanitizer.Sanitize(request.Answer, allowEmbeds: true);
        faq.OrderNo = request.OrderNo;
        faq.Status = request.Status;
        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = userId;

        await _db.SaveChangesAsync();
        return FaqSaveResult.Ok;
    }

    public async Task<FaqSaveResult> DeleteAsync(string id, string userId)
    {
        var perm = await _menus.GetEffectivePermissionAsync(userId, MenuPath);
        if (!perm.CanDelete) return FaqSaveResult.Forbidden;

        var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        if (faq is null) return FaqSaveResult.NotFound;

        faq.IsDeleted = true;
        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = userId;

        await _db.SaveChangesAsync();
        return FaqSaveResult.Ok;
    }

    /// <summary>빈 문자열은 null 로 모은다. 분류가 '' 와 null 로 갈리지 않게 한다.</summary>
    private static string? Trim(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static FaqDto ToDto(Faq f) => new()
    {
        Id = f.Id,
        Category = f.Category,
        Question = f.Question,
        Answer = f.Answer,
        OrderNo = f.OrderNo,
        Status = f.Status,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };
}
