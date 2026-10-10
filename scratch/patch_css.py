import re

with open('web/src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css', 'r') as f:
    css = f.read()

# 1. Add classes for badge size
css = css.replace('.pm-ask__kindhead .jsini-badge {\n  flex: 0 0 auto;\n}',
'''.pm-ask__kindhead .jsini-badge,
.pm-ask__limit .jsini-badge,
.pm-ai-limit .jsini-badge {
  flex: 0 0 auto;
  font-size: 0.75em;
  padding: 0.15em 0.4em;
}''')

# 2. Remove .pm-ai-limit__text
css = re.sub(r'\.pm-ai-limit__text \{[^}]+\}\n', '', css)

# 3. Remove .pm-ask__kindlimit
css = re.sub(r'/\* 한도 줄.*?\*/\n\.pm-ask__kindlimit \{[^}]+\}\n\n', '', css, flags=re.DOTALL)

# 4. Remove selected state for pm-ask__kindlimit
css = re.sub(r'\.dxbl-list-box-item-selected \.pm-ask__kindlimit,[^}]*li:hover > \* > \.pm-ask__kinditem \.pm-ask__kindlimit \{[^}]+\}\n\n', '', css, flags=re.DOTALL)

# 5. Remove .pm-ask__limittext
css = re.sub(r'\.pm-ask__limittext \{[^}]+\}\n', '', css)

with open('web/src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css', 'w') as f:
    f.write(css)

