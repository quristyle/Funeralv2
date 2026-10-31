#!/usr/bin/env python3
"""옛 WBS 도구(#/tables2)의 테이블 계통도를 프로젝트관리 DB 속성으로 옮긴다.

원본은 docs/wbs_참고소스/wbs-source-*.zip 안의 api/data/vueflow/ 다.

    tables-m10.json        → hhip-m10 접속의 table_lineage        (도면)
    tables-m10.seed.json   → hhip-m10 접속의 table_lineage_seed   (「기본 배치로」가 되돌릴 처음 모습)
    tables-m10.items.json  → hhip-m10 접속의 table_lineage_items  (업무항목)
    (m20 도 같다)

도면 글은 한 글자도 번역하지 않는다 — 화면(js/table-lineage.js)이 같은 모양을 그대로 읽는다.
그릴 때만 쓰던 표시(dim · hot · near · found …)만 걷는다. 원본 M10 에는 그것이 저장돼 있다.

이미 있는 줄은 덮어쓴다(같은 db_rid · db_pkey). 다시 돌려도 줄이 늘지 않는다.
비밀번호는 환경변수 PGPASSWORD 로 받는다.

    PGPASSWORD=… python3 -I scripts/projmng-table-lineage-import.py [zip 경로]
"""
import glob
import json
import os
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PSQL = ["psql", "-h", "jin114.co.kr", "-p", "31015", "-U", "projmng", "-d", "projmng",
        "-v", "ON_ERROR_STOP=1", "-q", "-At"]
TRANSIENT = {"dim", "flash", "hot", "near", "found", "inItem", "missing"}
TAG = "$lineage$"

KEYS = {
    "": ("table_lineage", "테이블 계통도"),
    ".seed": ("table_lineage_seed", "테이블 계통도 처음 배치 (옛 WBS 도구에서 옮김)"),
    ".items": ("table_lineage_items", "테이블 계통도 업무항목"),
}


def psql(sql):
    return subprocess.run(PSQL, input=sql, text=True, capture_output=True, check=True).stdout.strip()


def read_zip(path):
    files = {}
    with zipfile.ZipFile(path) as z:
        for name in z.namelist():
            norm = name.replace("\\", "/")
            if "/api/data/vueflow/" not in norm or ".bak." in norm:
                continue
            base = norm.rsplit("/", 1)[-1]
            if base.startswith("tables-m") and base.endswith(".json"):
                files[base] = z.read(name).decode("utf-8")
    return files


def clean(raw, items):
    doc = json.loads(raw)
    if not items:
        for n in doc.get("nodes", []):
            data = n.get("data") or {}
            for k in TRANSIENT:
                data.pop(k, None)
    return json.dumps(doc, ensure_ascii=False, separators=(",", ":"))


def main():
    zips = sys.argv[1:] or sorted(glob.glob(os.path.join(ROOT, "docs", "wbs_참고소스", "wbs-source-*.zip")))
    if not zips:
        sys.exit("원본 zip 을 찾지 못했습니다.")
    files = read_zip(zips[-1])

    for schema in ("m10", "m20"):
        nick = f"hhip-{schema}"
        rid = psql(f"SELECT db_rid FROM projmng.devdbinfo WHERE db_nick = '{nick}'")
        if not rid:
            sys.exit(f"{nick} 접속이 없습니다 — deploy/sql/devdbinfo-hhip-mm-2026-10-31.sql 을 먼저 돌리십시오.")

        for suffix, (key, comment) in KEYS.items():
            raw = files.get(f"tables-{schema}{suffix}.json")
            if raw is None:
                print(f"  {nick} {key}: 원본 없음 — 건너뜀")
                continue
            value = clean(raw, suffix == ".items")
            if TAG in value:
                sys.exit("본문에 인용 꼬리표가 들어 있습니다.")

            psql(f"""
BEGIN;
UPDATE projmng.dev_db_prop
   SET db_pvalue = {TAG}{value}{TAG}, db_pcomment = '{comment}', db_ptype = 'lineage', mod_dt = now()
 WHERE db_rid = {rid} AND db_pkey = '{key}';
INSERT INTO projmng.dev_db_prop (db_rid, db_prid, db_pkey, db_pvalue, db_pcomment, db_ptype, mod_dt, cre_dt)
SELECT {rid}, (SELECT COALESCE(MAX(db_prid), 0) + 1 FROM projmng.dev_db_prop),
       '{key}', {TAG}{value}{TAG}, '{comment}', 'lineage', now(), now()
 WHERE NOT EXISTS (SELECT 1 FROM projmng.dev_db_prop WHERE db_rid = {rid} AND db_pkey = '{key}');
COMMIT;
""")
            doc = json.loads(value)
            size = (f"칸 {len(doc.get('nodes', []))} · 선 {len(doc.get('edges', []))}"
                    if suffix != ".items" else f"업무항목 {len(doc.get('items', []))}")
            print(f"  {nick} (db_rid {rid}) {key}: {size} · {len(value.encode('utf-8')):,} bytes")


if __name__ == "__main__":
    main()
