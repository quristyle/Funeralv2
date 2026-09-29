import re

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor.cs', 'r') as f:
    content = f.read()

old_comment = """    /// <param name="byResolved">기간을 완료 시각으로 재나. 거짓이면 접수 시각이다.</param>
    private string ManageHref("""

new_comment = """    /// <param name="byResolved">기간을 완료 시각으로 재나. 거짓이면 접수 시각이다.</param>
    /// <param name="overrideCompanyId">조회할 고객사 (없으면 화면 필터 사용).</param>
    /// <param name="adminId">조회할 담당자 번호.</param>
    private string ManageHref("""

content = content.replace(old_comment, new_comment)

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor.cs', 'w') as f:
    f.write(content)

