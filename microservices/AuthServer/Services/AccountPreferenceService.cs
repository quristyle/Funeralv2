using System.Text;
using System.Text.Json;
using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 계정별 화면 환경설정 저장·조회.
/// </summary>
/// <remarks>
/// 서버는 내용을 해석하지 않는다. 프론트가 만든 JSON 을 문자열로 보관하고 그대로 돌려준다.
/// </remarks>
public class AccountPreferenceService : IAccountPreferenceService
{
    /// <summary>
    /// 받아 줄 최대 크기.
    /// </summary>
    /// <remarks>
    /// 사용자가 보낸 값을 그대로 보관하므로 상한이 없으면 DB 를 밀어 넣을 수 있다.
    /// 실제 설정 차이는 보통 1KB 안쪽이라 64KB 면 넉넉하다.
    /// </remarks>
    public const int MaxPayloadBytes = 64 * 1024;

    private static readonly JsonElement EmptyObject =
        JsonDocument.Parse("{}").RootElement.Clone();

    private readonly AppDbContext _db;
    private readonly ILogger<AccountPreferenceService> _logger;

    public AccountPreferenceService(AppDbContext db, ILogger<AccountPreferenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JsonElement> GetAsync(string userIdOrKey)
    {
        var row = await _db.AccountPreferences
            .AsNoTracking()
            .Where(p => p.Account != null
                        && (p.Account.UserId == userIdOrKey || p.Account.Id == userIdOrKey))
            .Select(p => p.Payload)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(row))
        {
            return EmptyObject;
        }

        // 저장된 값이 깨져 있어도 화면을 막지 않는다 — 기본값으로 시작하게 둔다.
        // 설정 하나 때문에 로그인 후 화면이 안 뜨는 것이 훨씬 나쁘다.
        try
        {
            return JsonDocument.Parse(row).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "저장된 환경설정을 읽지 못해 기본값으로 시작합니다: {UserId}", userIdOrKey);
            return EmptyObject;
        }
    }

    /// <inheritdoc />
    public async Task<SavePreferenceResult> SaveAsync(string userIdOrKey, JsonElement payload)
    {
        // 객체가 아니면 받지 않는다. 배열이나 숫자가 들어오면 프론트가 되돌려 읽을 때 깨진다.
        var json = payload.ValueKind == JsonValueKind.Object
            ? payload.GetRawText()
            : "{}";

        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
        {
            return SavePreferenceResult.TooLarge;
        }

        var account = await _db.Accounts
            .Where(a => a.UserId == userIdOrKey || a.Id == userIdOrKey)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();

        if (account is null)
        {
            return SavePreferenceResult.AccountNotFound;
        }

        var row = await _db.AccountPreferences
            .FirstOrDefaultAsync(p => p.AccountId == account.Id);

        // 감사 칸(created_at·created_by·updated_at·updated_by)은 손대지 않는다.
        // AppDbContext.HandleAuditing() 이 SaveChanges 때 모든 BaseEntity 에 채운다 —
        // 여기서 넣어도 그쪽이 덮어쓰므로 넣으면 읽는 사람만 헷갈린다.
        if (row is null)
        {
            _db.AccountPreferences.Add(new AccountPreference
            {
                AccountId = account.Id,
                Payload = json
            });
        }
        else
        {
            row.Payload = json;
        }

        await _db.SaveChangesAsync();
        return SavePreferenceResult.Success;
    }
}
