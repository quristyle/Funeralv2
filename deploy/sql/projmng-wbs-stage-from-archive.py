"""WBS 보관본(wbs-db-*.zip 의 manifest.json + data/json)을 받침 스키마 wbs_import 에 싣는 SQL 을 만든다.

보관본의 ddl/schema.sql 은 모든 이름이 `public.` 으로 박혀 있어 projmng 에 그대로 못 푼다.
그래서 적재 스크립트(projmng-wbs-data-load.sql)가 읽는 표 열넷만 JSON 에서 CSV 로 옮기고
\\copy 로 넣는다. 칸 차례와 형은 manifest 의 것을 그대로 쓴다.

    python3 -I projmng-wbs-stage-from-archive.py <풀어 둔 wbs-db 폴더> <출력 폴더>
    psql -h <host> -U projmng -d projmng -f <출력 폴더>/stage.sql
"""
import csv
import json
import os
import sys

TABLES = {
    'dev_user', 'dev_docs', 'dev_user_pref',
    'hhip_wbs_wrk2', 'hhip_wbs_task', 'hhip_wbs_pv', 'hhip_wbs_pv_task', 'hhip_wbs_pv_node',
    'if_system', 'if_attr_def', 'if_master', 'if_step', 'if_note', 'if_attr',
}

src, out = sys.argv[1], os.path.abspath(sys.argv[2])
os.makedirs(out, exist_ok=True)
manifest = json.load(open(os.path.join(src, 'manifest.json'), encoding='utf-8-sig'))

ddl = ['CREATE SCHEMA IF NOT EXISTS wbs_import;']
load = []

for t in manifest['table_list']:
    if t['table'] not in TABLES:
        continue

    cols = [c.strip() for c in t['columns'].split(', ')]
    names = [c.split(' ', 1)[0] for c in cols]
    ddl.append(f"CREATE TABLE wbs_import.{t['table']} ({', '.join(cols)});")

    rows = json.load(open(os.path.join(src, t['file']), encoding='utf-8-sig'))
    # 건수가 manifest 와 다르면 압축이 덜 풀린 것이다 — 조용히 적게 넣지 않는다.
    if len(rows) != t['rows']:
        sys.exit(f"{t['table']}: JSON {len(rows)}건 ≠ manifest {t['rows']}건")

    path = os.path.join(out, t['table'] + '.csv')
    with open(path, 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        for r in rows:
            vals = []
            for n in names:
                v = r.get(n)
                if v is None:
                    vals.append('\\N')
                elif isinstance(v, (dict, list)):  # jsonb 칸
                    vals.append(json.dumps(v, ensure_ascii=False))
                elif isinstance(v, bool):
                    vals.append('true' if v else 'false')
                else:
                    vals.append(str(v))
            w.writerow(vals)

    load.append(f"\\copy wbs_import.{t['table']} ({', '.join(names)}) "
                f"from '{path}' with (format csv, null '\\N')")
    print(t['table'], len(rows))

with open(os.path.join(out, 'stage.sql'), 'w', encoding='utf-8') as f:
    f.write('\\set ON_ERROR_STOP on\nBEGIN;\n' + '\n'.join(ddl) + '\n' + '\n'.join(load) + '\nCOMMIT;\n')
