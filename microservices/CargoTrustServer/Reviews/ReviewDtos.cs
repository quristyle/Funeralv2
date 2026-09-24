using CargoTrustServer.Common;
using CargoTrustServer.Data;

namespace CargoTrustServer.Reviews;

/// <summary>Review</summary>
public class ReviewDto
{
    public long ReviewId { get; set; }
    public long TransactionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public ReviewVisibility Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>후기 쓰기 — <c>{content}</c></summary>
public class ReviewSaveRequest
{
    public string? Content { get; set; }
}

public static class ReviewMap
{
    /// <summary>후기 한 편의 상한. TEXT 열이라 DB 는 막지 않지만, 공개 화면이 받아낼 길이를 정해 둔다.</summary>
    public const int MaxContentLength = 2000;

    public static T Review<T>(TransactionReview r) where T : ReviewDto, new() => new()
    {
        ReviewId = r.ReviewId,
        TransactionId = r.TransactionId,
        Content = r.Content,
        Status = r.Status,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    public static ReviewDto Review(TransactionReview r) => Review<ReviewDto>(r);
}
