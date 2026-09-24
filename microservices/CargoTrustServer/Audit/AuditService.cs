using System.Text.Json;
using System.Text.Json.Serialization;
using CargoTrustServer.Data;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Audit;

/// <summary>
/// 감사 기록(설계안 30). 관리자가 바꾼 것은 전부, 사용자의 거래 수정·삭제도 남긴다 —
/// 분쟁에서 「처음엔 얼마라고 적었나」를 물을 수 있어야 한다.
///
/// 전·후는 엔티티의 **열 값 전부**를 열 이름 그대로 JSONB 에 담는다. 엔티티마다 모양을
/// 손으로 적으면 열이 늘 때 감사에서 빠지는 칸이 생긴다.
/// </summary>
public class AuditService(CargoTrustDbContext db, CurrentUser current)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>엔티티의 지금 열 값 — 바꾸기 전에 불러 두면 「전」이 된다.</summary>
    public Dictionary<string, object?> Snapshot(object entity)
    {
        var entry = db.Entry(entity);
        return entry.Properties.ToDictionary(p => p.Metadata.GetColumnName(), p => p.CurrentValue);
    }

    /// <summary>
    /// 기록 한 줄을 붙인다. **저장은 부르는 쪽의 SaveChanges 가 한다** — 바뀐 것과 기록이
    /// 한 트랜잭션에 묶여, 둘 중 하나만 남는 일이 없다.
    /// </summary>
    public void Add(string action, string targetType, long? targetId, object? before, object? after)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = current.UserId,
            ActorExternal = current.ExternalUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            BeforeData = before is null ? null : JsonSerializer.Serialize(before, Json),
            AfterData = after is null ? null : JsonSerializer.Serialize(after, Json),
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>바꾸기 전 스냅숏과 지금 값을 짝지어 붙인다.</summary>
    public void AddChange(string action, string targetType, long targetId, Dictionary<string, object?> before, object entity) =>
        Add(action, targetType, targetId, before, Snapshot(entity));
}

/// <summary>감사 대상 종류 — audit_log.target_type</summary>
public static class AuditTarget
{
    public const string Company = "COMPANY";
    public const string Transaction = "TRANSACTION";
    public const string Review = "REVIEW";
    public const string Report = "REPORT";
    public const string Dispute = "DISPUTE";
    public const string User = "USER";
}

/// <summary>AuditEntry</summary>
public record AuditEntryDto(
    long AuditId,
    long? ActorUserId,
    string? ActorName,
    string Action,
    string? TargetType,
    long? TargetId,
    string? BeforeData,
    string? AfterData,
    DateTimeOffset CreatedAt);

public static class AuditQueries
{
    /// <summary>감사 기록을 이름과 함께 읽는다. 관리자 목록과 대시보드가 같이 쓴다.</summary>
    public static async Task<List<AuditEntryDto>> ToEntriesAsync(this IQueryable<AuditLog> query, CargoTrustDbContext db, int take, CancellationToken ct)
    {
        return await query
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditId)
            .Take(take)
            .Select(a => new AuditEntryDto(
                a.AuditId,
                a.ActorUserId,
                db.Users.Where(u => u.UserId == a.ActorUserId).Select(u => u.DisplayName).FirstOrDefault() ?? a.ActorExternal,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.BeforeData,
                a.AfterData,
                a.CreatedAt))
            .ToListAsync(ct);
    }
}
