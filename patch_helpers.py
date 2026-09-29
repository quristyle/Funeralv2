import re

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor.cs', 'r') as f:
    content = f.read()

helpers = """    private string ManageCompanyHref(string companyId) => ManageHref(overrideCompanyId: companyId);
    private string ManageAdminHref(int adminId) => ManageHref(adminId: adminId);
"""

# Insert helpers after ManageHref
if "private string ManageCompanyHref" not in content:
    content = content.replace('    private static string Day(DateTime at) => at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);', helpers + '\n    private static string Day(DateTime at) => at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);')

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor.cs', 'w') as f:
    f.write(content)
