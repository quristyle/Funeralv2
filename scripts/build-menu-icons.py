#!/usr/bin/env python3
"""
사이드바 메뉴 아이콘 CSS(`menu-icons.css`)를 만든다.

[왜 만들어 두고 커밋하나]

DB(`scom.system_menus.icon`)에 들어 있는 아이콘 이름은 iconify 이름이다
(`lucide:calendar-days` · `carbon:building` …). 옛 Vue 포털은 iconify 런타임이
그 이름으로 SVG 를 가져왔지만, 지금 프론트에는 iconify 가 없다 — 그리고
**넣지 않는다.** 메뉴 이름은 139가지뿐이고 거의 바뀌지 않아서, 필요한 것만
CSS mask 로 굳혀 두면 런타임 의존도 요청도 없어진다. 색은 `currentColor` 라
테마 스물둘을 그대로 따라온다(app.css 의 아이콘 묶음과 같은 방식).

[아이콘 목록은 DB 가 정본이다]

이 스크립트가 DB 를 직접 읽는다 — 접속 정보는 AuthServer 의
`appsettings.Local.json`(git 제외)에서 꺼낸다. 그것이 없는 장비에서는
아래 `--icons` 로 이름을 직접 준다.

    python3 scripts/build-menu-icons.py                    # DB 에서 목록을 읽는다
    python3 scripts/build-menu-icons.py --icons lucide:bell,mdi:menu

[메뉴에 새 아이콘을 쓰면]

CSS 에 그 이름이 없으면 **동그라미(fallback)** 가 나온다 — 빈 사각형이 아니다
(`.jsini-mi` 가 `--svg` 기본값을 들고 있다). 그러니 급하지 않고, 이 스크립트를
다시 돌려 커밋하면 제 그림이 나온다.

[받아 오는 곳]

iconify 의 CSS API(`https://api.iconify.design/<prefix>.css?icons=...`)다.
그쪽이 이미 mask 용으로 색을 박아 준 SVG 를 준다(`stroke='black'`).
받은 CSS 의 클래스 이름만 우리 것으로 바꿔 적는다.

라이선스: lucide(ISC) · carbon(Apache-2.0) · mdi(Apache-2.0) ·
material-symbols(Apache-2.0) · charm(MIT). 만들어진 파일 머리에도 적어 둔다.
"""

import argparse
import json
import os
import re
import subprocess
import sys
import urllib.request
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "web/src/Shared/JSini.Web.Components/wwwroot/menu-icons.css"
SETTINGS = ROOT / "microservices/AuthServer/appsettings.Local.json"

# 아이콘이 없는 메뉴와, CSS 에 없는 이름이 쓸 그림.
FALLBACK = "lucide:circle"

SQL = """
select distinct lower(trim(icon))
from scom.system_menus
where coalesce(trim(icon), '') <> ''
order by 1
"""


def icons_from_db() -> list[str]:
    settings = json.loads(SETTINGS.read_text(encoding="utf-8"))
    parts = dict(
        p.split("=", 1)
        for p in settings["ConnectionStrings"]["jsinicore"].split(";")
        if "=" in p
    )

    env = dict(os.environ, PGPASSWORD=parts["Password"])
    done = subprocess.run(
        ["psql", "-h", parts["Host"], "-p", parts["Port"],
         "-U", parts["Username"], "-d", parts["Database"], "-t", "-A", "-c", SQL],
        env=env, capture_output=True, text=True, check=True)

    return [line for line in done.stdout.splitlines() if line.strip()]


def fetch(prefix: str, names: list[str]) -> str:
    url = f"https://api.iconify.design/{prefix}.css?icons={','.join(sorted(names))}"

    # User-Agent 를 적어야 한다. 파이썬 기본값으로 부르면 403 이 온다.
    request = urllib.request.Request(url, headers={"User-Agent": "jsini-menu-icons/1.0"})

    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read().decode("utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--icons", help="쉼표로 나눈 iconify 이름. 없으면 DB 에서 읽는다")
    args = parser.parse_args()

    wanted = (
        [n.strip().lower() for n in args.icons.split(",") if n.strip()]
        if args.icons
        else icons_from_db()
    )

    if FALLBACK not in wanted:
        wanted.append(FALLBACK)

    by_prefix: dict[str, list[str]] = defaultdict(list)

    for name in wanted:
        if ":" not in name:
            print(f"건너뜀 — 접두사가 없다: {name}", file=sys.stderr)
            continue

        prefix, icon = name.split(":", 1)
        by_prefix[prefix].append(icon)

    # 이름 → mask url. iconify 가 준 CSS 에서 `--svg` 한 줄씩 꺼낸다.
    svg: dict[str, str] = {}

    for prefix, names in sorted(by_prefix.items()):
        css = fetch(prefix, names)

        for icon, url in re.findall(
                rf"\.icon--{re.escape(prefix)}--([a-z0-9-]+)\s*\{{\s*--svg:\s*(url\([^)]*\));",
                css):
            svg[f"{prefix}:{icon}"] = url

        missing = [n for n in names if f"{prefix}:{n}" not in svg]

        if missing:
            print(f"iconify 에 없는 이름 — {prefix}: {', '.join(missing)}", file=sys.stderr)

    if FALLBACK not in svg:
        print(f"기본 아이콘({FALLBACK})을 못 받았다. 멈춘다.", file=sys.stderr)
        return 1

    lines = [
        "/* ── 사이드바 메뉴 아이콘 ────────────────────────────────",
        "",
        "   **손으로 고치지 않는다.** `scripts/build-menu-icons.py` 가 만든다 —",
        f"   DB(`scom.system_menus.icon`)에 실제로 들어 있는 이름 {len(svg) - 1}가지다.",
        "",
        "   쓰는 모양은 클래스 둘이다(`MenuIcons.CssClass` 가 만들어 준다) —",
        "",
        "       <span class=\"jsini-mi jsini-mi--lucide-calendar-days\"></span>",
        "",
        "   앞엣것이 크기·색·mask 규칙을, 뒤엣것이 그림(`--svg`)만 갖는다. 그림을",
        "   못 찾으면 앞엣것의 기본값(동그라미)이 나온다 — 빈 사각형이 아니다.",
        "",
        "   아이콘: lucide(ISC) · carbon(Apache-2.0) · mdi(Apache-2.0) ·",
        "   material-symbols(Apache-2.0) · charm(MIT). iconify CSS API 로 받았다.",
        "   ---------------------------------------------------------- */",
        "",
        ".jsini-mi {",
        "  /* 이름을 못 찾았을 때의 그림. 이 기본값이 있어서 CSS 에 없는 아이콘이",
        "     통짜 사각형(mask 없는 background)으로 나오지 않는다. */",
        f"  --svg: {svg[FALLBACK]};",
        "",
        "  display: inline-block;",
        "  flex: 0 0 auto;",
        "  width: 1.15em;",
        "  height: 1.15em;",
        "  background-color: currentColor;",
        "  -webkit-mask-image: var(--svg);",
        "  mask-image: var(--svg);",
        "  -webkit-mask-repeat: no-repeat;",
        "  mask-repeat: no-repeat;",
        "  -webkit-mask-position: center;",
        "  mask-position: center;",
        "  -webkit-mask-size: contain;",
        "  mask-size: contain;",
        "}",
        "",
    ]

    for name in sorted(svg):
        klass = "jsini-mi--" + name.replace(":", "-")
        lines.append(f".{klass} {{ --svg: {svg[name]}; }}")

    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{OUT.relative_to(ROOT)} — 아이콘 {len(svg)}개, {OUT.stat().st_size / 1024:.1f}KB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
