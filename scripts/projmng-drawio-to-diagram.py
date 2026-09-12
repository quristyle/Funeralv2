#!/usr/bin/env python3
"""
옛 도구(draw.io)로 그린 유즈케이스를 지금 도구가 읽는 JSON 으로 바꾼다.

[왜 필요한가]

유즈케이스 화면(`/projmng/design/use-case`)이 읽는
`projmng.dev_proj_prop`(`prop_type = 'USE_CASE'`)에는 **20건이 전부
`<mxGraphModel …>` XML** 로 들어 있다. draw.io 의 저장 형식이다.

지금 화면이 쓰는 형식은 `ErdModel` JSON 이라(`web/.../Api/ErdModel.cs`)
그 XML 을 파서에 넣으면 **조용히 빈 그림**이 된다. 그래서 화면은
「저장된 그림이 없습니다」로 보이고, 그것을 「아직 안 그렸구나」로 읽은
사람이 새로 그려 저장하면 **옛 그림이 덮어써진다.** 되돌릴 수 없다.

한동안은 여는 꺾쇠로 그것을 알아채 **저장을 막아** 두기만 했다
(`ErdModel.IsLegacyDrawing`). 막는 것은 잃지 않게 할 뿐 보이게 하지는
않는다. 이 스크립트가 그 자료를 실제로 옮긴다.

[무엇을 어떻게 옮기나]

    draw.io                                지금 형식
    ────────────────────────────────────────────────────────────
    mxCell[vertex=1]                   →   entities[]  (manual: true)
      value  (HTML)                    →     name      (태그를 털어 낸 글자)
      mxGeometry x/y/width/height      →     x/y/w/h
    mxCell[edge=1] (source·target 있음)→   relations[] {from, to, label}
    mxCell[edge=1] (끝점이 좌표뿐)      →   **버린다** (아래)

`manual: true` 로 넣는 이유는 셋이다 — 이름을 캔버스에서 고칠 수 있어야 하고
(표에서 온 도형은 DB 가 정본이라 못 고친다), 「사라진 표」 판정에서 빠져야
하고, 라벨을 HTML 이 아니라 글자로 그려야 한다.

[버리는 것]

  · **끝점이 좌표뿐인 선.** draw.io 는 아무 도형에도 안 붙은 선을 좌표
    두 개로 적는데, 지금 형식의 관계선은 도형 아이디 둘로만 표현된다.
    붙일 도형이 없으므로 옮길 자리가 없다.
  · **그림(`shape=image`).** 외부 주소(rosedata.com)를 가리키는 이미지라
    지금 형식에 담을 칸이 없다. 자리와 크기는 남기고 이름을 「(이미지)」로,
    주소를 `desc` 에 적어 둔다 — 나중에 다시 그릴 때 무엇이 있었는지는 알
    수 있다.
  · **색·모양·테두리.** 지금 형식에 칸이 없다. 자리·크기·글자·이어짐만 남는다.

[줄을 끊어 둔다]

draw.io 는 긴 라벨을 상자 폭에 맞춰 저절로 접었다. 지금 도구는 **글자
라벨을 접지 않는다** — 접는 일은 라벨을 HTML 로 그릴 때만 하는데, 손으로
만든 도형은 더블클릭해 이름을 고치는 대상이라 글자 그대로 둔다. 그래서
여기서 상자 폭에 맞춰 줄을 끊어 담는다. 눈에 보이던 접힘이 실제 줄바꿈이
되는 것이라 모양은 원본에 가까워지고, 상자를 늘린 뒤에는 사람이 그 자리에서
고치면 된다.

**띄어쓰기에서만 끊고, 눈에 띄게 넘칠 때만 끊는다.** 글자 사이에서 끊으면
표 이름이 두 동강 나고(`t_mg_srt_target_res` / `ult_dtl`), 아슬아슬한 줄까지
손대면 원래 한 줄이던 것이 두 줄이 된다.

무엇이 얼마나 버려졌는지는 돌릴 때마다 줄마다 찍는다. **조용히 줄어드는 것이
가장 나쁘다.**

[원본을 지우지 않는다]

바꾸기 전에 같은 줄을 `prop_type = 'USE_CASE_MXGRAPH'` 로 한 벌 복사해 둔다.
표의 유일 제약이 `(prj_rid, prop_cd, prop_type)` 이라 부딪히지 않고, 화면은
`USE_CASE` 만 읽으므로 눈에 띄지도 않는다. 되돌리려면 한 줄이면 된다.

    UPDATE projmng.dev_proj_prop t
       SET prop_val = b.prop_val
      FROM projmng.dev_proj_prop b
     WHERE b.prj_rid = t.prj_rid AND b.prop_cd = t.prop_cd
       AND b.prop_type = 'USE_CASE_MXGRAPH' AND t.prop_type = 'USE_CASE';

[돌리는 법]

    python3 scripts/projmng-drawio-to-diagram.py            # 무엇이 바뀌는지만 본다
    python3 scripts/projmng-drawio-to-diagram.py --apply    # 실제로 쓴다

접속 정보는 ProjMngServer 의 `appsettings.Local.json`(git 제외)에서 꺼낸다.
"""

import argparse
import html
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SETTINGS = ROOT / "microservices/ProjMngServer/appsettings.Local.json"

PROP_TYPE = "USE_CASE"

# 원본을 남겨 둘 자리. 표마다 갈래를 담는 칸이 달라서 이름도 따로 둔다 —
# 유즈케이스는 `prop_type` 이 이미 `USE_CASE` 로 좁혀져 있어 그 갈래를
# 그대로 잇고, DB 속성은 `db_ptype` 이 빈 칸이라 짧게 적는다.
BACKUP_TYPE = "USE_CASE_MXGRAPH"
DB_BACKUP_TYPE = "MXGRAPH"

# 옮긴 그림을 캔버스 왼쪽 위에서 이만큼 띄운다. draw.io 는 음수 좌표를 쓰지만
# (스무 건 중 셋이 그렇다) 지금 캔버스의 원점은 왼쪽 위라, 그대로 두면 그림이
# 화면 밖에서 시작한다. 그림 전체를 같은 만큼 옮기므로 배치는 그대로다.
MARGIN = 40

# ── 글자를 상자 폭에 맞춰 끊는 값들 ──────────────────────────
#
# draw.io 는 긴 라벨을 상자 폭에 맞춰 **저절로 접어** 그렸다. 지금 도구는
# 그러지 않는다 — 접는 일은 maxgraph 가 라벨을 HTML 로 그릴 때만 하고,
# 손으로 만든 도형의 라벨은 글자 그대로라(더블클릭해 고치는 대상이다)
# 한 줄로 뻗는다. 그대로 두면 긴 글자가 옆 도형 위로 넘어간다.
#
# 그래서 **옮기는 쪽에서 줄을 끊어 둔다.** 줄바꿈이 들어 있으면 그리는 쪽은
# 그대로 그린다. draw.io 가 눈으로 보여 주던 접힘을 실제 줄바꿈으로 굳히는
# 것이라, 보이는 모양은 원본에 가까워진다.
#
# 글자 폭은 떠 있는 화면에서 잰 값이다(라벨 글꼴 14px 기준) — 한글 12.88px,
# 영문·숫자 7.79px. 크기 단계를 바꾸면 글꼴도 따라 바뀌므로 딱 맞지는
# 않는다. 넉넉하게 잡아 **덜 끊는 쪽으로** 기운다.
FONT_PX = 14.0
CJK_RATIO = 0.92
ASCII_RATIO = 0.556

# 이보다 좁은 상자는 건드리지 않는다. 한 글자도 안 들어가는 폭에서 끊으면
# 글자마다 줄이 바뀌어 오히려 못 읽는다.
MIN_WRAP_W = 48

# 상자 폭의 이 배를 넘을 때만 끊는다. 아슬아슬한 줄까지 손대면 원래 한 줄이던
# 것이 두 줄이 되어 그린 사람의 뜻과 멀어진다(`fold` 머리말).
FOLD_AT = 1.35


# ── draw.io 읽기 ────────────────────────────────────────────

def plain(value: str | None) -> str:
    """
    라벨에서 글자만 뽑는다.

    draw.io 는 라벨에 HTML 을 넣는다 — 줄바꿈이 `<br>` 이고 강조가 `<b>`·`<em>`
    이다. 지금 도구의 손그림 도형은 **글자를 그대로** 그리므로(태그를 넣으면
    더블클릭했을 때 편집기에 태그가 보인다) 털어 내야 한다.
    """
    if not value:
        return ""

    text = re.sub(r"<br\s*/?>", "\n", value, flags=re.IGNORECASE)
    text = re.sub(r"</(div|p|li)\s*>", "\n", text, flags=re.IGNORECASE)
    text = re.sub(r"<[^>]+>", "", text)
    text = html.unescape(text)

    # 줄 끝 공백과 빈 줄을 정리한다. draw.io 라벨은 `<div>` 로 줄을 나누는
    # 습관 때문에 끝에 빈 줄이 잘 남는다.
    lines = [line.strip() for line in text.replace("\r", "").split("\n")]

    return "\n".join(line for line in lines if line).strip()


def text_width(text: str) -> float:
    return sum(
        FONT_PX * (CJK_RATIO if ord(ch) >= 0x1100 else ASCII_RATIO)
        for ch in text)


def fold(text: str, width: float) -> str:
    """
    상자 폭을 크게 넘는 줄을 <b>띄어쓰기에서만</b> 끊는다.

    <b>이미 들어 있는 줄바꿈은 그대로 둔다</b> — 그린 사람이 일부러 나눈
    자리다.

    [띄어쓰기가 없으면 건드리지 않는다]

    글자 사이에서 끊으면 표 이름이 `t_mg_srt_target_res` / `ult_dtl` 로
    갈라진다. 조금 넘쳐 보이는 것보다 <b>이름이 두 동강 나는 쪽이 훨씬
    나쁘다</b> — 읽는 사람이 표를 못 찾는다.

    [조금 넘치는 것은 두고 본다]

    글자 폭은 잰 값에서 어림한 것이고 크기 단계에 따라 달라진다. 아슬아슬한
    줄까지 끊으면 **원래 한 줄이던 것이 두 줄이 되어** 그린 사람의 뜻과
    멀어진다. 눈에 띄게 넘칠 때만(`FOLD_AT` 배) 손댄다.
    """
    if width < MIN_WRAP_W:
        return text

    out = []

    for line in text.split("\n"):
        if text_width(line) <= width * FOLD_AT or " " not in line.strip():
            out.append(line)
            continue

        room = ""

        for word in line.split(" "):
            candidate = f"{room} {word}".strip()

            if room and text_width(candidate) > width:
                out.append(room)
                room = word
            else:
                room = candidate

        if room:
            out.append(room)

    return "\n".join(out)


def number(node, key: str) -> float:
    try:
        return float(node.get(key) or 0)
    except (TypeError, ValueError):
        return 0.0


def image_url(style: str) -> str | None:
    found = re.search(r"image=([^;]+)", style or "")

    return found.group(1) if found else None


def convert(xml: str) -> tuple[dict, dict]:
    """XML 한 건을 모델과 집계로 바꾼다."""
    root = ET.fromstring(xml).find("root")
    tally = {"shapes": 0, "links": 0, "loose": 0, "images": 0, "unnamed": 0, "folded": 0}

    if root is None:
        return {"entities": [], "relations": []}, tally

    entities = []
    boxes = {}

    for cell in root.iter("mxCell"):
        if cell.get("vertex") != "1":
            continue

        geometry = cell.find("mxGeometry")

        if geometry is None:
            continue

        style = cell.get("style") or ""
        name = plain(cell.get("value"))
        desc = None

        if (url := image_url(style)) is not None:
            tally["images"] += 1

            # 그림 자체는 옮길 자리가 없다(머리말). 자리와 주소만 남긴다.
            name = name or "(이미지)"
            desc = url

        if not name:
            tally["unnamed"] += 1

        # 상자 폭에 맞춰 줄을 끊는다(머리말). 안쪽 여백 몫으로 8px 뺀다.
        folded = fold(name, round(number(geometry, "width")) - 8)

        if folded != name:
            tally["folded"] += 1
            name = folded

        entity = {
            "id": cell.get("id"),
            "name": name,
            "manual": True,
            "x": round(number(geometry, "x")),
            "y": round(number(geometry, "y")),
            "w": round(number(geometry, "width")),
            "h": round(number(geometry, "height")),
        }

        if desc:
            entity["desc"] = desc

        entities.append(entity)
        boxes[entity["id"]] = entity
        tally["shapes"] += 1

    relations = []

    for cell in root.iter("mxCell"):
        if cell.get("edge") != "1":
            continue

        source, target = cell.get("source"), cell.get("target")

        # 끝점이 좌표뿐인 선은 옮길 수 없다(머리말).
        if source not in boxes or target not in boxes:
            tally["loose"] += 1
            continue

        relation = {"from": source, "to": target}

        if label := plain(cell.get("value")):
            relation["label"] = label

        relations.append(relation)
        tally["links"] += 1

    shift(entities)

    return {"entities": entities, "relations": relations}, tally


def shift(entities: list[dict]) -> None:
    """
    그림 전체를 왼쪽 위로 당긴다. 도형 사이의 거리는 그대로다.
    """
    if not entities:
        return

    left = min(e["x"] for e in entities)
    top = min(e["y"] for e in entities)

    for entity in entities:
        entity["x"] += MARGIN - left
        entity["y"] += MARGIN - top


# ── DB ──────────────────────────────────────────────────────

def connection() -> dict:
    text = SETTINGS.read_text(encoding="utf-8")

    # 이 설정 파일에는 주석이 섞여 있어 json 모듈이 바로 읽지 못한다.
    found = re.search(r'"jsini"\s*:\s*"([^"]+)"', text)

    if not found:
        sys.exit(f"{SETTINGS} 에서 연결 문자열 'jsini' 를 찾지 못했습니다.")

    return dict(p.split("=", 1) for p in found.group(1).split(";") if "=" in p)


def psql(parts: dict, *args: str, stdin: str | None = None) -> str:
    env = dict(os.environ, PGPASSWORD=parts["Password"])
    done = subprocess.run(
        ["psql", "-h", parts["Host"], "-p", parts["Port"], "-U", parts["Username"],
         "-d", parts["Database"], "-v", "ON_ERROR_STOP=1", *args],
        env=env, input=stdin, capture_output=True, text=True)

    if done.returncode != 0:
        sys.exit(done.stderr.strip() or "psql 이 실패했습니다.")

    return done.stdout


def csv_rows(out: str, width: int) -> list[tuple]:
    import csv
    import io

    csv.field_size_limit(10 ** 7)

    return [tuple(row) for row in csv.reader(io.StringIO(out)) if len(row) == width]


def usecase_rows(parts: dict) -> list[dict]:
    """
    유즈케이스에서 바꿀 줄. <b>이미 JSON 인 것은 건너뛴다</b> — 두 번 돌려도
    안전해야 한다.
    """
    out = psql(parts, "-At", "-c", f"""
        copy (select prj_rid, prop_cd, prop_val
                from projmng.dev_proj_prop
               where prop_type = '{PROP_TYPE}'
                 and ltrim(prop_val) like '<%'
               order by prj_rid, prop_cd)
        to stdout with (format csv)
    """)

    return [
        {"owner": prj_rid, "name": prop_cd, "xml": prop_val,
         "key": f"prj_rid = {quote(prj_rid)} and prop_cd = {quote(prop_cd)}"}
        for prj_rid, prop_cd, prop_val in csv_rows(out, 3)
    ]


def dbprop_rows(parts: dict) -> list[dict]:
    """
    DB 접속 속성에서 바꿀 줄.

    <b>남겨 둔 원본은 건너뛴다</b> — 그것도 옛 도구 XML 이라, 안 거르면 두 번째
    돌릴 때 <b>보관본까지 옮겨 버린다.</b> 되돌릴 곳이 없어진다.

    <b>줄마다 <c>db_prid</c> 로 집는다.</b> 이 표에는 제약도 인덱스도 없어서
    <c>(db_rid, db_pkey)</c> 가 겹치는 줄이 실제로 있다 — <c>(4, 'ER Diagram')</c>
    이 둘이고 <b>내용이 서로 다르다</b>. 그 짝으로 고치면 한 번에 두 줄이
    같은 값으로 덮여 한쪽이 사라진다. <c>db_prid</c> 는 서른 줄 전부에서
    유일하다(확인했다).
    """
    out = psql(parts, "-At", "-c", """
        copy (select db_prid, db_rid, db_pkey, db_pvalue
                from projmng.dev_db_prop
               where ltrim(db_pvalue) like '<%'
                 and coalesce(db_ptype, '') <> '{DB_BACKUP_TYPE}'
               order by db_pkey, db_rid, db_prid)
        to stdout with (format csv)
    """.replace("{DB_BACKUP_TYPE}", DB_BACKUP_TYPE))

    return [
        {"owner": f"db {db_rid}", "name": f"{db_pkey} #{db_prid}", "xml": db_pvalue,
         "key": f"db_prid = {db_prid}", "prid": db_prid, "pkey": db_pkey}
        for db_prid, db_rid, db_pkey, db_pvalue in csv_rows(out, 4)
    ]


def quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def statements(target: str, row: dict, payload: str) -> str:
    """한 줄에 대한 「원본 복사 + 값 바꾸기」."""
    if target == "usecase":
        return f"""
            insert into projmng.dev_proj_prop
                   (prj_rid, prop_cd, prop_val, prop_comm, prop_use_yn, prop_type)
            select prj_rid, prop_cd, prop_val, prop_comm, prop_use_yn, {quote(BACKUP_TYPE)}
              from projmng.dev_proj_prop
             where {row['key']} and prop_type = {quote(PROP_TYPE)}
               and not exists (select 1 from projmng.dev_proj_prop b
                                where b.prj_rid = dev_proj_prop.prj_rid
                                  and b.prop_cd = dev_proj_prop.prop_cd
                                  and b.prop_type = {quote(BACKUP_TYPE)});

            update projmng.dev_proj_prop
               set prop_val = {quote(payload)}
             where {row['key']} and prop_type = {quote(PROP_TYPE)};
        """

    # DB 속성. 원본은 **같은 이름의 새 줄**로 남긴다 — 이 표에는 갈래를 나눌
    # 칸(`db_ptype`)이 따로 있어서 거기에 표시해 두면 화면에서도 구분된다.
    # 어느 줄에서 나온 것인지는 설명 칸에 적는다(다시 돌려도 두 번 안 만든다).
    mark = quote(f"원본: db_prid={row['prid']}")

    return f"""
        insert into projmng.dev_db_prop
               (db_rid, db_prid, db_pkey, db_pvalue, db_pcomment, db_ptype, cre_dt, mod_dt)
        select p.db_rid,
               (select coalesce(max(db_prid), 0) + 1 from projmng.dev_db_prop),
               p.db_pkey, p.db_pvalue, {mark}, {quote(DB_BACKUP_TYPE)}, now(), now()
          from projmng.dev_db_prop p
         where p.{row['key']}
           and not exists (select 1 from projmng.dev_db_prop b
                            where b.db_ptype = {quote(DB_BACKUP_TYPE)}
                              and b.db_pcomment = {mark});

        update projmng.dev_db_prop
           set db_pvalue = {quote(payload)}, mod_dt = now()
         where {row['key']};
    """


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true",
                        help="실제로 DB 에 쓴다. 안 주면 무엇이 바뀌는지만 보여 준다.")
    parser.add_argument("--only", choices=["usecase", "dbprop"],
                        help="한쪽만 돌린다. 안 주면 둘 다 본다.")
    args = parser.parse_args()

    parts = connection()

    readers = {"usecase": usecase_rows, "dbprop": dbprop_rows}
    targets = [args.only] if args.only else ["usecase", "dbprop"]

    labels = {"usecase": "유즈케이스 (dev_proj_prop)", "dbprop": "DB 속성 (dev_db_prop)"}
    touched = 0

    for target in targets:
        rows = readers[target](parts)

        print(f"\n■ {labels[target]}")

        if not rows:
            print("  옛 도구 형식으로 남은 줄이 없습니다.")
            continue

        print(f"  {'어디':<8}{'이름':<24}{'도형':>5}{'연결선':>7}{'버린선':>7}"
              f"{'이미지':>7}{'이름없음':>9}{'줄끊음':>7}")
        print("  " + "─" * 73)

        sql = []
        total = {"shapes": 0, "links": 0, "loose": 0, "images": 0, "unnamed": 0, "folded": 0}

        for row in rows:
            model, tally = convert(row["xml"])

            for key in total:
                total[key] += tally[key]

            print(f"  {row['owner']:<8}{row['name']:<24}{tally['shapes']:>5}{tally['links']:>7}"
                  f"{tally['loose']:>7}{tally['images']:>7}{tally['unnamed']:>9}{tally['folded']:>7}")

            sql.append(statements(
                target, row, json.dumps(model, ensure_ascii=False, indent=2)))

        print("  " + "─" * 73)
        print(f"  {'합계':<30}{total['shapes']:>5}{total['links']:>7}"
              f"{total['loose']:>7}{total['images']:>7}{total['unnamed']:>9}{total['folded']:>7}")

        if not args.apply:
            print(f"  {len(rows)}건을 바꿀 수 있습니다.")
            continue

        # 한 표가 한 트랜잭션이다. 중간에 끊기면 일부만 JSON 이 되고, 그러면
        # 어느 줄이 옮겨진 것인지 사람이 대조해야 한다.
        psql(parts, "-q", stdin="begin;\n" + "\n".join(sql) + "\ncommit;\n")

        print(f"  {len(rows)}건을 옮겼습니다. 원본은 남아 있습니다(머리말).")
        touched += len(rows)

    if not args.apply:
        print("\n실제로 쓰려면 --apply 를 주십시오.")
    elif touched == 0:
        print("\n바꾼 것이 없습니다.")


if __name__ == "__main__":
    main()
