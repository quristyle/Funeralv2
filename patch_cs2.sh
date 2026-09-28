sed -i -e '/private IReadOnlyList<BizOption> CompanyFilterOptions =>/i \
    private IReadOnlyList<BizOption> RequesterFilterOptions\
    {\
        get\
        {\
            var companyId = Context.IsSystemAdmin ? _companyId : Context.CompanyId;\
            var items = string.IsNullOrEmpty(companyId) \
                ? Context.CustomerItems \
                : Context.CustomerItems.Where(item => \
                    BizOptionService.GetText(item, "companyId") == companyId);\
            var filtered = items.Select(item => new BizOption(\
                BizOptionService.GetText(item, "userName") ?? "",\
                BizOptionService.GetText(item, "id")\
            )).ToList();\
            return [new("전체", null), .. filtered];\
        }\
    }\
' ./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/RequestManage.razor.cs
