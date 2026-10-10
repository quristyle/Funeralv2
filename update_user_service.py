import re

with open('./microservices/AuthServer/Services/UserService.cs', 'r') as f:
    content = f.read()

# 1. GetAccountsAsync mapping
content = re.sub(
    r'var emailDetail = a\.ProfileDetails\?\.FirstOrDefault\(p => p\.DetailType == "Email"\);\s*var phoneDetail = a\.ProfileDetails\?\.FirstOrDefault\(p => p\.DetailType == "Phone"\);',
    r'''var emailDetails = a.ProfileDetails?.Where(p => p.DetailType == "Email").Select(p => p.Content).Where(c => c != null).ToList() ?? new List<string>();
            var phoneDetails = a.ProfileDetails?.Where(p => p.DetailType == "Phone").Select(p => p.Content).Where(c => c != null).ToList() ?? new List<string>();
            var emailDetail = emailDetails.FirstOrDefault();
            var phoneDetail = phoneDetails.FirstOrDefault();''',
    content
)
content = re.sub(
    r'Email = emailDetail\?\.Content,\s*Phone = phoneDetail\?\.Content,',
    r'''Email = emailDetail,
                Emails = emailDetails!,
                Phone = phoneDetail,
                Phones = phoneDetails!,''',
    content
)

# 2. CreateAccountAsync
old_create = '''        if (!string.IsNullOrEmpty(dto.Email))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Email",
                Content = dto.Email,
                IsPrimary = true
            });
        }

        if (!string.IsNullOrEmpty(dto.Phone))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Phone",
                Content = dto.Phone,
                IsPrimary = true
            });
        }'''
new_create = '''        var emailsToSave = dto.Emails?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Email) && !emailsToSave.Contains(dto.Email))
            emailsToSave.Insert(0, dto.Email);
            
        foreach (var em in emailsToSave)
        {
            if (!string.IsNullOrWhiteSpace(em))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Email",
                    Content = em,
                    IsPrimary = em == emailsToSave.First()
                });
            }
        }

        var phonesToSave = dto.Phones?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Phone) && !phonesToSave.Contains(dto.Phone))
            phonesToSave.Insert(0, dto.Phone);
            
        foreach (var ph in phonesToSave)
        {
            if (!string.IsNullOrWhiteSpace(ph))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Phone",
                    Content = ph,
                    IsPrimary = ph == phonesToSave.First()
                });
            }
        }'''
content = content.replace(old_create, new_create)

# Create mapping part
content = re.sub(
    r'Email = dto\.Email,\s*Phone = dto\.Phone,',
    r'''Email = emailsToSave.FirstOrDefault(),
            Emails = emailsToSave,
            Phone = phonesToSave.FirstOrDefault(),
            Phones = phonesToSave,''',
    content
)

# 3. UpdateAccountAsync
old_update = '''        // Email 업데이트
        var emailDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
        if (emailDetail != null)
        {
            if (string.IsNullOrEmpty(dto.Email))
            {
                _db.AccountProfileDetails.Remove(emailDetail);
            }
            else
            {
                emailDetail.Content = dto.Email;
                _db.Entry(emailDetail).State = EntityState.Modified;
            }
        }
        else if (!string.IsNullOrEmpty(dto.Email))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Email",
                Content = dto.Email,
                IsPrimary = true
            });
        }

        // Phone 업데이트
        var phoneDetail = account.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
        if (phoneDetail != null)
        {
            if (string.IsNullOrEmpty(dto.Phone))
            {
                _db.AccountProfileDetails.Remove(phoneDetail);
            }
            else
            {
                phoneDetail.Content = dto.Phone;
                _db.Entry(phoneDetail).State = EntityState.Modified;
            }
        }
        else if (!string.IsNullOrEmpty(dto.Phone))
        {
            _db.AccountProfileDetails.Add(new AccountProfileDetail
            {
                AccountId = account.Id,
                DetailType = "Phone",
                Content = dto.Phone,
                IsPrimary = true
            });
        }'''
new_update = '''        // Email 업데이트
        var existingEmails = account.ProfileDetails?.Where(p => p.DetailType == "Email").ToList() ?? new List<AccountProfileDetail>();
        foreach (var e in existingEmails) _db.AccountProfileDetails.Remove(e);
        
        var emailsToSave = dto.Emails?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Email) && !emailsToSave.Contains(dto.Email))
            emailsToSave.Insert(0, dto.Email);
            
        foreach (var em in emailsToSave)
        {
            if (!string.IsNullOrWhiteSpace(em))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Email",
                    Content = em,
                    IsPrimary = em == emailsToSave.First()
                });
            }
        }

        // Phone 업데이트
        var existingPhones = account.ProfileDetails?.Where(p => p.DetailType == "Phone").ToList() ?? new List<AccountProfileDetail>();
        foreach (var p in existingPhones) _db.AccountProfileDetails.Remove(p);
        
        var phonesToSave = dto.Phones?.ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(dto.Phone) && !phonesToSave.Contains(dto.Phone))
            phonesToSave.Insert(0, dto.Phone);
            
        foreach (var ph in phonesToSave)
        {
            if (!string.IsNullOrWhiteSpace(ph))
            {
                _db.AccountProfileDetails.Add(new AccountProfileDetail
                {
                    AccountId = account.Id,
                    DetailType = "Phone",
                    Content = ph,
                    IsPrimary = ph == phonesToSave.First()
                });
            }
        }'''
content = content.replace(old_update, new_update)

with open('./microservices/AuthServer/Services/UserService.cs', 'w') as f:
    f.write(content)
