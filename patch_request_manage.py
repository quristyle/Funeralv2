import re

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/RequestManage.razor.cs', 'r') as f:
    content = f.read()

# Add AdminQuery property
old_prop = """    [SupplyParameterFromQuery(Name = "open")] public string? OpenQuery { get; set; }"""
new_prop = """    [SupplyParameterFromQuery(Name = "open")] public string? OpenQuery { get; set; }

    /// <summary>담당자.</summary>
    [SupplyParameterFromQuery(Name = "admin")] public string? AdminQuery { get; set; }"""

content = content.replace(old_prop, new_prop)

# Update ApplyQueryConditions
old_carried = """        var carried = statuses.Count > 0
            || from is not null
            || to is not null
            || basis is not null
            || !string.IsNullOrWhiteSpace(CompanyQuery)
            || !string.IsNullOrWhiteSpace(OpenQuery);"""

new_carried = """        var carried = statuses.Count > 0
            || from is not null
            || to is not null
            || basis is not null
            || !string.IsNullOrWhiteSpace(CompanyQuery)
            || !string.IsNullOrWhiteSpace(OpenQuery)
            || !string.IsNullOrWhiteSpace(AdminQuery);"""

content = content.replace(old_carried, new_carried)

old_company_assign = """        _basis = basis ?? RequestedBasis;
        _companyId = string.IsNullOrWhiteSpace(CompanyQuery) ? null : CompanyQuery;"""

new_company_assign = """        _basis = basis ?? RequestedBasis;
        _companyId = string.IsNullOrWhiteSpace(CompanyQuery) ? null : CompanyQuery;
        _adminId = string.IsNullOrWhiteSpace(AdminQuery) ? null : AdminQuery;"""

content = content.replace(old_company_assign, new_company_assign)

# There's a comment: "요청자·담당자·제목은 주소로 안 받는다. 현황판 타일이 그 셋으로는 세지 않기 때문이고"
# Let's replace "담당자" out of it
old_comment = """    /// 요청자·담당자·제목은 주소로 안 받는다. 현황판 타일이 그 셋으로는 세지
    /// 않기 때문이고,"""
new_comment = """    /// 요청자·제목은 주소로 안 받는다. 현황판 타일이 그것으로는 세지
    /// 않기 때문이고,"""

content = content.replace(old_comment, new_comment)

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/RequestManage.razor.cs', 'w') as f:
    f.write(content)

