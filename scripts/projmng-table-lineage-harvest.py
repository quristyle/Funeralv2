#!/usr/bin/env python3
"""프로젝트에 등록된 DB 접속마다 객체와 그 사이 연결을 모아 테이블 계통도 자리에 보관한다.

/projmng/db/list 에 단 접속(projmng.devdbinfo)을 읽어, PostgreSQL 접속마다 그 스키마에서

    객체   표 · 뷰 · 구체화 뷰 · 외부 표 · 프로시저 · 함수 (코멘트 · PK · 행 수 어림)
    연결   외래키            부모 표 → 자식 표      「읽기」  글귀 = 칸
           뷰가 읽는 것      표 → 뷰               「읽기」  글귀 = 뷰
           트리거            표 → 함수             「트리거」 글귀 = 트리거 이름
           함수 본문의 표    함수 → 표 (쓰기)       「처리」  글귀 = 쓰기
                             표 → 함수 (읽기)       「읽기」  글귀 = 읽기

을 모아 그 접속의 DB 속성(projmng.dev_db_prop, db_ptype='lineage') 세 줄에 담는다.

    table_lineage_catalog   객체 목록과 연결, 모은 때 · 출처(접속 정보 — 비밀번호는 안 담는다)
    table_lineage_seed      위 연결로 자동 배치한 처음 모습 — 화면의 「기본 배치로」가 이것을 올린다
    table_lineage           도면. **아직 없을 때만** 같은 것을 넣는다(--force 면 덮는다)

도면을 덮지 않는 까닭은 사람이 화면에서 고친 배치·업무항목이 거기 있기 때문이다.
카탈로그와 처음 모습은 늘 새로 쓴다 — 다시 돌려도 줄이 늘지 않는다.

함수 본문의 표는 이름을 글자로 찾은 어림이다(PL/pgSQL 은 의존을 DB 가 적어 두지 않는다).
주석이나 글자에 표 이름이 있으면 선이 하나 더 생길 수 있다.

접속 비밀번호는 devdbinfo 에서 읽어 psql 의 환경변수로만 넘긴다. 프로젝트관리 DB 비밀번호는
환경변수 PGPASSWORD 로 받는다.

    PGPASSWORD=… python3 -I scripts/projmng-table-lineage-harvest.py [--prj 1] [--db jsini-comm] [--force] [--dry-run]
"""
import argparse
import datetime
import json
import os
import re
import subprocess
import sys

PM = ["psql", "-h", "jin114.co.kr", "-p", "31015", "-U", "projmng", "-d", "projmng",
      "-v", "ON_ERROR_STOP=1", "-q", "-At"]
TAG = "$lineage$"
KINDS_PG = ("POSTGRESQL",)

LANE_W = 240
ROW_H = 80
PER_COL = 22
TOP = 60

HARVEST_SQL = r"""
WITH s AS (SELECT oid FROM pg_catalog.pg_namespace WHERE nspname = :'schema'),
ext AS (SELECT d.objid FROM pg_catalog.pg_depend d
         WHERE d.classid = 'pg_catalog.pg_proc'::regclass AND d.deptype = 'e'),
extrel AS (SELECT d.objid FROM pg_catalog.pg_depend d
            WHERE d.classid = 'pg_catalog.pg_class'::regclass AND d.deptype = 'e')
SELECT json_build_object(
  'version', current_setting('server_version'),
  'objects', (SELECT COALESCE(json_agg(o ORDER BY o.ord, o.name), '[]') FROM (
      SELECT CASE c.relkind WHEN 'r' THEN 'TABLE' WHEN 'p' THEN 'TABLE' WHEN 'v' THEN 'VIEW'
                            WHEN 'm' THEN 'MVIEW' ELSE 'FOREIGN' END AS type,
             c.relname AS name,
             obj_description(c.oid, 'pg_class') AS "desc",
             CASE WHEN c.relkind IN ('r', 'p', 'm') AND c.reltuples >= 0 THEN c.reltuples::bigint END AS rows,
             (SELECT string_agg(a.attname, ', ' ORDER BY array_position(i.indkey::int2[], a.attnum))
                FROM pg_catalog.pg_index i
                JOIN pg_catalog.pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY (i.indkey)
               WHERE i.indrelid = c.oid AND i.indisprimary) AS pk,
             CASE c.relkind WHEN 'v' THEN 2 WHEN 'm' THEN 3 WHEN 'f' THEN 4 ELSE 1 END AS ord
        FROM pg_catalog.pg_class c
       WHERE c.relnamespace = (SELECT oid FROM s)
         AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
         AND NOT c.relispartition
         AND c.oid NOT IN (SELECT objid FROM extrel)
      UNION ALL
      SELECT CASE WHEN bool_or(p.prokind = 'p') THEN 'PROCEDURE' ELSE 'FUNCTION' END,
             p.proname, max(obj_description(p.oid, 'pg_proc')), NULL, NULL,
             CASE WHEN bool_or(p.prokind = 'p') THEN 5 ELSE 6 END
        FROM pg_catalog.pg_proc p
       WHERE p.pronamespace = (SELECT oid FROM s)
         AND p.prokind IN ('f', 'p')
         AND p.oid NOT IN (SELECT objid FROM ext)
       GROUP BY p.proname) o),
  'fks', (SELECT COALESCE(json_agg(f ORDER BY f.child, f.name), '[]') FROM (
      SELECT con.conname AS name, cc.relname AS child, pc.relname AS parent,
             (SELECT string_agg(a.attname, ', ' ORDER BY k.n)
                FROM unnest(con.conkey) WITH ORDINALITY AS k(attnum, n)
                JOIN pg_catalog.pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum) AS cols
        FROM pg_catalog.pg_constraint con
        JOIN pg_catalog.pg_class cc ON cc.oid = con.conrelid
        JOIN pg_catalog.pg_class pc ON pc.oid = con.confrelid
       WHERE con.contype = 'f'
         AND cc.relnamespace = (SELECT oid FROM s)
         AND pc.relnamespace = (SELECT oid FROM s)) f),
  'viewdeps', (SELECT COALESCE(json_agg(DISTINCT jsonb_build_object('view', v.relname, 'src', t.relname)), '[]')
        FROM pg_catalog.pg_depend d
        JOIN pg_catalog.pg_rewrite r ON r.oid = d.objid
        JOIN pg_catalog.pg_class v ON v.oid = r.ev_class
        JOIN pg_catalog.pg_class t ON t.oid = d.refobjid
       WHERE d.classid = 'pg_catalog.pg_rewrite'::regclass
         AND d.refclassid = 'pg_catalog.pg_class'::regclass
         AND v.oid <> t.oid
         AND v.relnamespace = (SELECT oid FROM s)
         AND t.relnamespace = (SELECT oid FROM s)
         AND t.relkind IN ('r', 'p', 'v', 'm', 'f')),
  'triggers', (SELECT COALESCE(json_agg(json_build_object('name', tg.tgname, 'tbl', c.relname, 'fn', p.proname)), '[]')
        FROM pg_catalog.pg_trigger tg
        JOIN pg_catalog.pg_class c ON c.oid = tg.tgrelid
        JOIN pg_catalog.pg_proc p ON p.oid = tg.tgfoid
       WHERE NOT tg.tgisinternal
         AND c.relnamespace = (SELECT oid FROM s)
         AND p.pronamespace = (SELECT oid FROM s)),
  'routines', (SELECT COALESCE(json_agg(json_build_object('name', p.proname, 'src', p.prosrc)), '[]')
        FROM pg_catalog.pg_proc p
        JOIN pg_catalog.pg_language l ON l.oid = p.prolang
       WHERE p.pronamespace = (SELECT oid FROM s)
         AND p.prokind IN ('f', 'p')
         AND l.lanname IN ('plpgsql', 'sql')
         AND p.oid NOT IN (SELECT objid FROM ext))
)
"""


def pm(sql):
    return subprocess.run(PM, input=sql, text=True, capture_output=True, check=True).stdout.strip()


def connections(prj, only):
    rows = pm(f"""
SELECT json_agg(json_build_object('rid', db_rid, 'nick', db_nick, 'type', db_type, 'host', db_ip,
                                  'port', db_port, 'db', db_database, 'user', db_id, 'pwd', db_pwd,
                                  'schema', db_schema) ORDER BY db_rid)
  FROM projmng.devdbinfo WHERE prj_rid = {int(prj)}""")
    conns = json.loads(rows or "[]") or []
    return [c for c in conns if not only or c["nick"] in only]


def harvest(c):
    env = {**os.environ, "PGPASSWORD": c["pwd"] or "", "PGCONNECT_TIMEOUT": "8"}
    cmd = ["psql", "-h", c["host"], "-p", str(c["port"]), "-U", c["user"], "-d", c["db"],
           "-v", "ON_ERROR_STOP=1", "-v", f"schema={c['schema']}", "-q", "-At"]
    r = subprocess.run(cmd, input=HARVEST_SQL, text=True, capture_output=True, env=env, timeout=120)
    if r.returncode != 0:
        raise RuntimeError(r.stderr.strip().splitlines()[-1] if r.stderr.strip() else f"psql {r.returncode}")
    return json.loads(r.stdout)


WRITE_RE = r"(?:insert\s+into|update|delete\s+from|merge\s+into|truncate(?:\s+table)?|copy)\s+(?:only\s+)?"


def routine_edges(routines, names, schema):
    """함수 본문에서 표 이름을 찾아 쓰기·읽기를 가른다. 주석은 걷고 본다."""
    out = []
    for r in routines:
        src = r.get("src") or ""
        src = re.sub(r"--[^\n]*", " ", src)
        src = re.sub(r"/\*.*?\*/", " ", src, flags=re.S).lower()
        for name in names:
            if name == r["name"]:
                continue
            n = re.escape(name.lower())
            ref = rf'(?:(?:"?{re.escape(schema.lower())}"?)\.)?"?{n}"?(?![\w$])'
            if not re.search(rf"(?<![\w$.]){ref}", src):
                continue
            wrote = re.search(rf"{WRITE_RE}{ref}", src)
            out.append((r["name"], name, "write" if wrote else "read"))
    return out


def build(c, h, at):
    objects = h["objects"]
    by = {o["name"]: o for o in objects}
    tables = [o["name"] for o in objects if o["type"] in ("TABLE", "FOREIGN", "MVIEW")]
    views = [o["name"] for o in objects if o["type"] == "VIEW"]
    routines = [o["name"] for o in objects if o["type"] in ("PROCEDURE", "FUNCTION")]

    relations = []
    for f in h["fks"]:
        if f["parent"] != f["child"]:
            relations.append({"ty": "fk", "from": f["parent"], "to": f["child"], "label": f["cols"] or "", "via": f["name"]})
    for v in h["viewdeps"]:
        relations.append({"ty": "view", "from": v["src"], "to": v["view"], "label": "뷰", "via": v["view"]})
    for t in h["triggers"]:
        relations.append({"ty": "trigger", "from": t["tbl"], "to": t["fn"], "label": t["name"], "via": t["name"]})
    rel_names = set(tables) | set(views)
    for fn, tbl, how in routine_edges(h["routines"], sorted(rel_names), c["schema"]):
        if how == "write":
            relations.append({"ty": "write", "from": fn, "to": tbl, "label": "쓰기", "via": fn})
        else:
            relations.append({"ty": "read", "from": tbl, "to": fn, "label": "읽기", "via": fn})

    # 같은 두 칸 · 같은 갈래의 선은 하나로 — 글귀는 모은다(외래키가 둘이면 칸 이름 둘).
    edges, seen = [], {}
    ty_of = {"fk": "read", "view": "read", "trigger": "trigger", "write": "proc", "read": "read"}
    for r in relations:
        if r["from"] not in by or r["to"] not in by:
            continue
        key = (r["from"], r["to"], ty_of[r["ty"]])
        if key in seen:
            e = seen[key]
            if r["label"] and r["label"] not in e["label"].split(" / "):
                e["label"] = f"{e['label']} / {r['label']}" if e["label"] else r["label"]
            continue
        e = {"id": f"e{len(edges) + 1}", "source": r["from"], "target": r["to"],
             "sourceHandle": None, "targetHandle": None, "label": r["label"], "type": "way",
             "data": {"ty": key[2], "key": 1 if r["ty"] == "fk" else 0}}
        seen[key] = e
        edges.append(e)

    # 표의 단: 외래키로 참조하는 부모가 없으면 0, 있으면 가장 깊은 부모 + 1 (고리는 끊는다).
    parents = {t: set() for t in tables}
    children = {t: set() for t in tables}
    for f in h["fks"]:
        if f["child"] in parents and f["parent"] in parents and f["parent"] != f["child"]:
            parents[f["child"]].add(f["parent"])
            children[f["parent"]].add(f["child"])
    level = {}

    def depth(t, stack=()):
        if t in level:
            return level[t]
        if t in stack:
            return 0
        d = 0 if not parents[t] else 1 + max(depth(p, stack + (t,)) for p in parents[t])
        level[t] = d
        return d

    for t in tables:
        depth(t)

    lane_names, members = [], []
    alone = [t for t in tables if not parents[t] and not children[t]]
    linked = [t for t in tables if t not in alone]
    max_level = max([min(level[t], 3) for t in linked], default=-1)
    for lv in range(max_level + 1):
        names = {0: "기준 표 (참조받기만)", 1: "1단 참조 표", 2: "2단 참조 표", 3: "3단 이상 참조 표"}
        lane_names.append(names[lv])
        members.append(sorted(t for t in linked if min(level[t], 3) == lv))
    if alone:
        lane_names.append("외래키 없는 표")
        members.append(sorted(alone))
    if views:
        lane_names.append("뷰")
        members.append(sorted(views))
    if routines:
        lane_names.append("함수 · 프로시저")
        members.append(sorted(routines))

    def kind_of(o):
        if o["type"] == "VIEW":
            return "view"
        if o["type"] == "PROCEDURE":
            return "proc"
        if o["type"] == "FUNCTION":
            return "func"
        if o["type"] == "MVIEW":
            return "snap"
        if o["type"] == "FOREIGN":
            return "ext"
        return "master" if children[o["name"]] and not parents[o["name"]] else "ledger"

    writers = {}
    for r in relations:
        if r["ty"] in ("write", "trigger") and r["to"] in by and r["from"] in by:
            tbl = r["to"] if r["ty"] == "write" else r["from"]
            writers.setdefault(tbl, set()).add(r["from"] if r["ty"] == "write" else r["to"])

    lanes, nodes, x = [], [], 20
    for i, (lname, names) in enumerate(zip(lane_names, members)):
        cols = max(1, -(-len(names) // PER_COL))
        w = cols * LANE_W
        lanes.append({"i": i, "name": lname, "x": x, "w": w})
        for j, name in enumerate(names):
            o = by[name]
            data = {"id": name, "kr": o.get("desc") or "", "kind": kind_of(o), "col": i,
                    "pk": o.get("pk") or "", "rows": o.get("rows"), "owner": "", "note": "", "writers": sorted(writers.get(name, []))}
            nodes.append({"id": name, "type": "table",
                          "position": {"x": x + (j // PER_COL) * LANE_W + 20, "y": TOP + (j % PER_COL) * ROW_H},
                          "data": data})
        x += w

    source = {"nick": c["nick"], "type": c["type"], "host": c["host"], "port": c["port"],
              "database": c["db"], "schema": c["schema"], "user": c["user"], "server": h.get("version")}
    graph = {"from": "harvest", "made": at, "schema": c["schema"], "source": source, "v": 1,
             "lanes": lanes, "flows": [{"id": "all", "name": "전체"}], "nodes": nodes, "edges": edges}
    catalog = {"v": 1, "at": at, "source": source,
               "objects": [{"type": o["type"], "name": o["name"], "desc": o.get("desc") or "",
                            "pk": o.get("pk") or "", "rows": o.get("rows")} for o in objects],
               "relations": relations}
    return graph, catalog


def put(rid, key, value, comment, only_if_absent=False):
    raw = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
    if TAG in raw:
        sys.exit("본문에 인용 꼬리표가 들어 있습니다.")
    update = "" if only_if_absent else f"""
UPDATE projmng.dev_db_prop
   SET db_pvalue = {TAG}{raw}{TAG}, db_pcomment = '{comment}', db_ptype = 'lineage', mod_dt = now()
 WHERE db_rid = {rid} AND db_pkey = '{key}';"""
    out = pm(f"""
BEGIN;{update}
INSERT INTO projmng.dev_db_prop (db_rid, db_prid, db_pkey, db_pvalue, db_pcomment, db_ptype, mod_dt, cre_dt)
SELECT {rid}, (SELECT COALESCE(MAX(db_prid), 0) + 1 FROM projmng.dev_db_prop),
       '{key}', {TAG}{raw}{TAG}, '{comment}', 'lineage', now(), now()
 WHERE NOT EXISTS (SELECT 1 FROM projmng.dev_db_prop WHERE db_rid = {rid} AND db_pkey = '{key}')
RETURNING db_prid;
COMMIT;""")
    return bool(out.strip()), len(raw.encode("utf-8"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prj", type=int, default=1, help="프로젝트 번호 (기본 1 = jsini통합포탈)")
    ap.add_argument("--db", action="append", default=[], help="이 별칭만 (여러 번 줄 수 있다)")
    ap.add_argument("--force", action="store_true", help="도면(table_lineage)이 이미 있어도 덮는다")
    ap.add_argument("--dry-run", action="store_true", help="모으기만 하고 쓰지 않는다")
    args = ap.parse_args()

    at = datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).strftime("%Y-%m-%d %H:%M KST")
    conns = connections(args.prj, set(args.db))
    if not conns:
        sys.exit(f"프로젝트 {args.prj} 에 등록된 DB 접속이 없습니다.")

    fails = 0
    for c in conns:
        head = f"{c['nick']} (db_rid {c['rid']}, {c['type']} {c['host']}:{c['port']}/{c['db']} · {c['schema']})"
        if c["type"] not in KINDS_PG:
            print(f"  {head}: {c['type']} 은 아직 모으지 않습니다 — 건너뜀")
            continue
        try:
            h = harvest(c)
        except (RuntimeError, subprocess.TimeoutExpired) as ex:
            fails += 1
            print(f"  {head}: 붙지 못함 — {ex}")
            continue

        graph, catalog = build(c, h, at)
        kinds = {}
        for o in catalog["objects"]:
            kinds[o["type"]] = kinds.get(o["type"], 0) + 1
        rels = {}
        for r in catalog["relations"]:
            rels[r["ty"]] = rels.get(r["ty"], 0) + 1
        print(f"  {head}: 객체 {kinds} · 연결 {rels} · 칸 {len(graph['nodes'])} · 선 {len(graph['edges'])}")
        if args.dry_run:
            continue

        _, n1 = put(c["rid"], "table_lineage_catalog", catalog, "테이블 계통도 카탈로그 (대상 DB 에서 모음)")
        _, n2 = put(c["rid"], "table_lineage_seed", graph, "테이블 계통도 처음 배치 (대상 DB 에서 모음)")
        made, n3 = put(c["rid"], "table_lineage", graph, "테이블 계통도", only_if_absent=not args.force)
        print(f"    카탈로그 {n1:,} bytes · 처음 배치 {n2:,} bytes · 도면 "
              + (f"{n3:,} bytes {'덮음' if args.force else '새로 넣음'}" if made or args.force else "이미 있어 그대로 둠"))

    if fails:
        print(f"붙지 못한 접속 {fails}개 — 위 줄을 보십시오.")


if __name__ == "__main__":
    main()
