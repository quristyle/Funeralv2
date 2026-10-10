import re

with open('./web/src/Apps/JSini.Web.Admin/Api/AdminModels.cs', 'r') as f:
    content = f.read()

# Add to AccountDto
content = content.replace(
    '    public string? Email { get; set; }\n    public string? Phone { get; set; }',
    '    public string? Email { get; set; }\n    public List<string> Emails { get; set; } = [];\n    public string? Phone { get; set; }\n    public List<string> Phones { get; set; } = [];'
)

# Add to SaveAccountDto
content = content.replace(
    '    public string UserName { get; set; } = string.Empty;\n    public string? Email { get; set; }\n    public string? Phone { get; set; }',
    '    public string UserName { get; set; } = string.Empty;\n    public string? Email { get; set; }\n    public List<string> Emails { get; set; } = [];\n    public string? Phone { get; set; }\n    public List<string> Phones { get; set; } = [];'
)

with open('./web/src/Apps/JSini.Web.Admin/Api/AdminModels.cs', 'w') as f:
    f.write(content)

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor.cs', 'r') as f:
    content = f.read()

content = content.replace(
    '            Email = e.Item.Email,\n            Phone = e.Item.Phone,',
    '            Email = e.Item.Email,\n            Emails = e.Item.Emails,\n            Phone = e.Item.Phone,\n            Phones = e.Item.Phones,'
)

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor.cs', 'w') as f:
    f.write(content)
