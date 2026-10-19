using System.Text;
using System.Text.Json;
using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 계정별 화면 환경설정 저장·조회.
/// </summary>
/// 서버는 내용을 해석하지 않는다 — 프론트가 만든 JSON 을 문자열로 보관하고 그대로 돌려준다.
public class AccountPreferenceService : IAccountPreferenceService
{
    /// <summary>
    /// 받아 줄 최대 크기.
    /// </summary>
    /// 사용자가 보낸 값을 그대로 보관하므로 상한이 있어야 한다. 실제 설정은 보통 1KB 안쪽이라 64KB 면 넉넉하다.
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
    public async Task<SavePreferenceResult> MergeAsync(string userIdOrKey, JsonElement patch)
    {
        // 객체가 아니면 섞을 것이 없다. SaveAsync 와 달리 **빈 것으로 덮어쓰지
        // 않는다** — 여기서 `{}` 로 떨어뜨리면 「한 칸만 고친다」고 부른 쪽이
        // 조용히 전부를 지우게 된다.
        if (patch.ValueKind != JsonValueKind.Object)
        {
            return SavePreferenceResult.Success;
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

        var merged = Merge(row?.Payload, patch);

        if (Encoding.UTF8.GetByteCount(merged) > MaxPayloadBytes)
        {
            return SavePreferenceResult.TooLarge;
        }

        if (row is null)
        {
            _db.AccountPreferences.Add(new AccountPreference
            {
                AccountId = account.Id,
                Payload = merged
            });
        }
        else
        {
            row.Payload = merged;
        }

        await _db.SaveChangesAsync();
        return SavePreferenceResult.Success;
    }

    /// <summary>
    /// 저장돼 있던 글자에 <paramref name="patch"/> 의 열쇠만 얹어 새 글자를 만든다.
    /// </summary>
    /// 값이 null 인 열쇠는 빼고 적는다 — 「기본값으로 되돌리기」가 그 길이다. 저장된 것이 깨져 있으면 없던 것으로 본다(GetAsync 와 같은 판단). 고치려는 호출이 막히면 사람이 되돌릴 길이 없다.
    private string Merge(string? stored, JsonElement patch)
    {
        var keep = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(stored))
        {
            try
            {
                using var doc = JsonDocument.Parse(stored);

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        keep[prop.Name] = prop.Value.Clone();
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "저장된 환경설정을 읽지 못해 보낸 것만 남깁니다.");
            }
        }

        foreach (var prop in patch.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Null)
            {
                keep.Remove(prop.Name);
            }
            else
            {
                keep[prop.Name] = prop.Value.Clone();
            }
        }

        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (var (name, value) in keep)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
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
