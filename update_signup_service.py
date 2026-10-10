import re

with open('./microservices/AuthServer/Services/SignupService.cs', 'r') as f:
    content = f.read()

# Add to ISignupService
content = content.replace(
    '    Task<bool> RejectAsync(\n        string accountId, string approver, string? reason, CancellationToken ct = default);',
    '    Task<bool> RejectAsync(\n        string accountId, string approver, string? reason, CancellationToken ct = default);\n\n    /// <summary>소셜 신청을 기존 계정으로 결합한다. 연결은 옮기고 신청은 지운다.</summary>\n    Task<bool> MergeAsync(string pendingAccountId, string targetAccountId, string approver, CancellationToken ct = default);'
)

# Add to SignupService
content = content.replace(
    '    private static AccountProfileDetail Detail(string accountId, string type, string content) => new()',
    '''    public async Task<bool> MergeAsync(string pendingAccountId, string targetAccountId, string approver, CancellationToken ct = default)
    {
        var pendingAccount = await db.Accounts
            .Include(a => a.ProfileDetails)
            .FirstOrDefaultAsync(a => a.Id == pendingAccountId, ct);
        
        var targetAccount = await db.Accounts
            .FirstOrDefaultAsync(a => a.Id == targetAccountId, ct);
            
        if (pendingAccount == null || targetAccount == null) return false;
        
        var status = pendingAccount.ProfileDetails?.FirstOrDefault(p => p.DetailType == StatusDetail);
        if (status?.Content != StatusPending) return false;

        var socialLogins = await db.AccountSocialLogins
            .Where(l => l.AccountId == pendingAccountId)
            .ToListAsync(ct);
            
        foreach (var login in socialLogins)
        {
            login.AccountId = targetAccountId;
        }

        if (pendingAccount.ProfileDetails is { Count: > 0 })
        {
            db.AccountProfileDetails.RemoveRange(pendingAccount.ProfileDetails);
        }

        db.Accounts.Remove(pendingAccount);
        await db.SaveChangesAsync(ct);
        
        logger.LogInformation(
            "가입 신청 결합: {PendingId} -> {TargetId} (처리자 {Approver})",
            pendingAccountId, targetAccountId, approver);
            
        return true;
    }

    private static AccountProfileDetail Detail(string accountId, string type, string content) => new()'''
)

with open('./microservices/AuthServer/Services/SignupService.cs', 'w') as f:
    f.write(content)
