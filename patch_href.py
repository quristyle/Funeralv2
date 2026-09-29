import re

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor.cs', 'r') as f:
    content = f.read()

# We need to add company and admin optional parameters to ManageHref.
# And maybe create ManageCompanyHref and ManageAdminHref.

old_manage_href = """    private string ManageHref(
        string? statuses = null,
        DateTime? from = null,
        DateTime? to = null,
        bool byResolved = false)"""

new_manage_href = """    private string ManageHref(
        string? statuses = null,
        DateTime? from = null,
        DateTime? to = null,
        bool byResolved = false,
        string? overrideCompanyId = null,
        int? adminId = null)"""

content = content.replace(old_manage_href, new_manage_href)

old_company_part = """        // **보고 있는 회사를 함께 싣는다.** 안 실으면 회사 하나로 좁혀 놓고
        // 타일을 눌렀을 때 전체가 열린다 — 숫자가 안 맞는다.
        // 고객 계정은 서버가 제 회사로 묶으므로(`Scope.CompanyScoped`) 안 싣는다.
        if (CanPickCompany && !string.IsNullOrEmpty(_companyId))
        {
            parts.Add($"company={Uri.EscapeDataString(_companyId)}");
        }"""

new_company_part = """        // **보고 있는 회사를 함께 싣는다.** 안 실으면 회사 하나로 좁혀 놓고
        // 타일을 눌렀을 때 전체가 열린다 — 숫자가 안 맞는다.
        // 고객 계정은 서버가 제 회사로 묶으므로(`Scope.CompanyScoped`) 안 싣는다.
        var targetCompanyId = overrideCompanyId ?? (CanPickCompany ? _companyId : null);
        if (!string.IsNullOrEmpty(targetCompanyId))
        {
            parts.Add($"company={Uri.EscapeDataString(targetCompanyId)}");
        }

        if (adminId.HasValue)
        {
            parts.Add($"admin={adminId.Value}");
        }"""

content = content.replace(old_company_part, new_company_part)

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor.cs', 'w') as f:
    f.write(content)
