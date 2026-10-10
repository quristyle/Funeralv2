import re

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor', 'r') as f:
    content = f.read()

old_email = '''                <DxFormLayoutItem Caption="이메일" ColSpanMd="6">
                    <DxTextBox @bind-Text="a.Email" />
                </DxFormLayoutItem>'''
new_email = '''                <DxFormLayoutItem Caption="이메일" ColSpanMd="6">
                    <DxTagBox Data="@(new List<string>())"
                              Values="@(a.Emails)"
                              ValuesChanged="@((IEnumerable<string> values) => { a.Emails = values.ToList(); a.Email = a.Emails.FirstOrDefault(); })"
                              AllowCustomTags="true" />
                </DxFormLayoutItem>'''
content = content.replace(old_email, new_email)

old_phone = '''                <DxFormLayoutItem Caption="연락처" ColSpanMd="6">
                    <DxTextBox @bind-Text="a.Phone" />
                </DxFormLayoutItem>'''
new_phone = '''                <DxFormLayoutItem Caption="연락처" ColSpanMd="6">
                    <DxTagBox Data="@(new List<string>())"
                              Values="@(a.Phones)"
                              ValuesChanged="@((IEnumerable<string> values) => { a.Phones = values.ToList(); a.Phone = a.Phones.FirstOrDefault(); })"
                              AllowCustomTags="true" />
                </DxFormLayoutItem>'''
content = content.replace(old_phone, new_phone)

with open('./web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor', 'w') as f:
    f.write(content)
