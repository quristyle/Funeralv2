using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Reviews;

/// <summary>거래 후기 — 거래에 딸리고, 거래 하나에 하나(설계안 11)</summary>
public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/transactions/{id:long}/review", Save)
            .WithTags("Review").WithSummary("후기 쓰기 — 있으면 고친다");
    }

    private static async Task<IResult> Save(
        CargoTrustDbContext db, CurrentUser me, AuditService audit,
        long id, ReviewSaveRequest req, CancellationToken ct)
    {
        var content = Check.Clean(req.Content);
        if (content is null) return ApiError.BadRequest("후기 내용을 입력하세요.");
        if (Check.MaxLength(content, ReviewMap.MaxContentLength, "후기") is { } lengthError)
            return ApiError.BadRequest(lengthError);

        var t = await db.Transactions.FirstOrDefaultAsync(x => x.TransactionId == id && !x.IsDeleted, ct);
        if (t is null) return ApiError.NotFound("거래를 찾을 수 없습니다.");
        if (t.UserId != me.UserId) return ApiError.Forbidden("본인이 등록한 거래에만 후기를 쓸 수 있습니다.");

        var now = DateTimeOffset.UtcNow;
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.TransactionId == id, ct);
        if (review is null)
        {
            review = new TransactionReview
            {
                TransactionId = id,
                UserId = me.UserId,
                Content = content,
                Status = ReviewVisibility.VISIBLE,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Reviews.Add(review);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // 같은 거래에 두 번이 겹쳤다 (uq_review_transaction). 다시 불러 고치게 한다.
                return ApiError.Conflict("후기가 방금 저장됐습니다. 다시 불러와 고쳐 주세요.");
            }
        }
        else
        {
            // 고친 후기는 남긴다 — 신고된 뒤에 내용을 바꿔 덮는 일을 되짚을 수 있어야 한다.
            // 관리자가 숨긴(HIDDEN) 후기는 고쳐도 숨긴 채로 둔다.
            var before = audit.Snapshot(review);
            review.Content = content;
            review.UpdatedAt = now;
            audit.AddChange("REVIEW_UPDATE", AuditTarget.Review, review.ReviewId, before, review);
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(ReviewMap.Review(review));
    }
}
