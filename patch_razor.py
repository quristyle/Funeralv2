import re

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor', 'r') as f:
    content = f.read()

company_old = """                <article class="jsini-card hd-card">
                    <header class="hd-card__head">
                        <span class="hd-card__name" title="@c.CompanyName">@c.CompanyName</span>
                        <span class="hd-card__lead">@Num(c.Total)<small>건</small></span>
                    </header>"""

company_new = """                <article class="jsini-card hd-card hd-card--link">
                    <a class="hd-card__hit" href="@ManageCompanyHref(c.CompanyId)" aria-label="@($"{c.CompanyName} {c.Total}건 — 요청 목록 열기")"></a>
                    <header class="hd-card__head">
                        <span class="hd-card__name" title="@c.CompanyName">@c.CompanyName</span>
                        <span class="hd-card__lead">@Num(c.Total)<small>건</small></span>
                    </header>"""

admin_old = """                <article class="jsini-card hd-card">
                    <header class="hd-card__head">
                        <span class="hd-card__avatar" aria-hidden="true">@Initial(a.AdminName)</span>
                        <span class="hd-card__name" title="@a.AdminName">@a.AdminName</span>
                        <span class="hd-card__lead">@Num(a.Completed)<small>완료</small></span>
                    </header>"""

admin_new = """                <article class="jsini-card hd-card hd-card--link">
                    <a class="hd-card__hit" href="@ManageAdminHref(a.AdminId)" aria-label="@($"{a.AdminName} {a.Completed}건 완료 — 요청 목록 열기")"></a>
                    <header class="hd-card__head">
                        <span class="hd-card__avatar" aria-hidden="true">@Initial(a.AdminName)</span>
                        <span class="hd-card__name" title="@a.AdminName">@a.AdminName</span>
                        <span class="hd-card__lead">@Num(a.Completed)<small>완료</small></span>
                    </header>"""

content = content.replace(company_old, company_new)
content = content.replace(admin_old, admin_new)

with open('./web/src/Apps/JSini.Web.HelpDesk/Components/Pages/HelpDeskDashboard.razor', 'w') as f:
    f.write(content)
