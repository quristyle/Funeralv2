using funeralv2Api.Data;
using funeralv2Api.DTOs;
using funeralv2Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace funeralv2Api.Services;

/// <inheritdoc cref="ISettingService"/>
public class SettingService : ISettingService
{
    private readonly AppDbContext _dbContext;

    public SettingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<List<AccountSettingDto>> GetSettingsAsync(string userId)
    {
        var saved = await _dbContext.AccountSettings
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        return SettingCatalog.Merge(saved);
    }

    /// <inheritdoc />
    public async Task<AccountSettingDto?> UpdateSettingAsync(string userId, string code, bool enabled)
    {
        // 목록에 없는 코드는 받지 않는다. 아무 문자열이나 저장되면 나중에 정리가 안 된다.
        if (SettingCatalog.Find(code) is null)
        {
            return null;
        }

        await UpsertAsync(userId, code, enabled);
        await _dbContext.SaveChangesAsync();

        var all = await GetSettingsAsync(userId);
        return all.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<List<AccountSettingDto>> UpdateSettingsAsync(string userId, Dictionary<string, bool> values)
    {
        foreach (var (code, enabled) in values)
        {
            if (SettingCatalog.Find(code) is null)
            {
                continue;
            }

            await UpsertAsync(userId, code, enabled);
        }

        await _dbContext.SaveChangesAsync();
        return await GetSettingsAsync(userId);
    }

    /// <summary>
    /// 있으면 고치고 없으면 만든다.
    /// </summary>
    /// <remarks>
    /// 같은 (사용자, 코드)로 행이 여럿 생기지 않게 유일 인덱스를 걸어 두었다
    /// (<c>docs/sql/funeralv2_old_migration.sql</c>). 그래도 옛 데이터를 옮겨 오는
    /// 등으로 중복이 생길 수 있어 읽을 때는 가장 최근 것을 고른다.
    /// </remarks>
    private async Task UpsertAsync(string userId, string code, bool enabled)
    {
        var row = await _dbContext.AccountSettings
            .Where(s => s.UserId == userId && s.SettingCode == code && !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .FirstOrDefaultAsync();

        if (row is null)
        {
            _dbContext.AccountSettings.Add(new AccountSetting
            {
                UserId = userId,
                SettingCode = code,
                SettingValue = SettingCatalog.ToStored(enabled),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
            });
            return;
        }

        row.SettingValue = SettingCatalog.ToStored(enabled);
        row.UpdatedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
    }
}
