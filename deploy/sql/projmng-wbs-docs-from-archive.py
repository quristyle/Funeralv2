"""WBS 보관본 두 압축에서 개발자 정보(dev_docs)의 빠진 몫을 projmng 에 넣는 SQL 을 만든다.

먼저 넣은 적재(projmng-wbs-data-load.sql)는 살아 있는 쪽의 제목·본문만 옮겼다. 여기서 채우는 것은
  · 처음 쓴 때·사람(created_*) — 제목으로 짝을 짓고, 비어 있는 줄에만 넣는다
  · 휴지통에 있던 쪽 — 지운 때·사람까지 그대로
  · 붙인 문서(dev_doc_files) — 주소는 그대로, 올린 HTML 은 소스 압축의 api/data/doc_files/ 에서 꺼내 담는다
다시 돌려도 겹치지 않는다(제목 · 쪽+이름으로 거른다).

    python3 -I projmng-wbs-docs-from-archive.py <wbs-db.zip> <wbs-source.zip> <prj_rid> > out.sql
    psql -h <host> -U projmng -d projmng -v ON_ERROR_STOP=1 -f out.sql
"""
import json
import sys
import zipfile

db_zip, src_zip, prj = sys.argv[1], sys.argv[2], int(sys.argv[3])


def zname(info):
    """윈도우에서 만든 압축이라 이름이 UTF-8 표시 없이 들어 있다 — cp437 로 읽힌 것을 되돌리고 구분자를 / 로 맞춘다."""
    n = info.filename
    if not info.flag_bits & 0x800:
        try:
            n = n.encode('cp437').decode('utf-8')
        except UnicodeError:
            pass
    return n.replace('\\', '/')


def read_json(z, suffix):
    for i in z.infolist():
        if zname(i).endswith(suffix):
            return json.loads(z.read(i).decode('utf-8-sig'))
    sys.exit(f'{suffix} 가 압축에 없습니다')


def q(v):
    if v is None:
        return 'NULL'
    return "'" + str(v).replace("'", "''") + "'"


with zipfile.ZipFile(db_zip) as z:
    docs = read_json(z, 'data/json/dev_docs.json')
    files = read_json(z, 'data/json/dev_doc_files.json')

with zipfile.ZipFile(src_zip) as z:
    html_of = {zname(i).split('api/data/doc_files/', 1)[1]: i
               for i in z.infolist() if '/api/data/doc_files/' in zname(i)}
    bodies = {}
    for f in files:
        if f.get('url'):
            continue
        key = f"{f['doc_id']}/{f['file']}"
        if key not in html_of:
            sys.exit(f'올린 문서 {key} 가 소스 압축에 없습니다')
        bodies[f['id']] = z.read(html_of[key]).decode('utf-8-sig')

title_of = {d['id']: d['title'] for d in docs}
doc_by_title = f"(SELECT id FROM projmng.wbs_docs WHERE prj_rid = {prj} AND title = {{}} ORDER BY id LIMIT 1)"

out = ['BEGIN;']

for d in docs:
    if d['deleted_at'] is None and d['created_at'] is not None:
        out.append(
            f"UPDATE projmng.wbs_docs SET created_at = {q(d['created_at'])}, created_by = {q(d['created_by'])}"
            f" WHERE prj_rid = {prj} AND title = {q(d['title'])} AND created_at IS NULL;")

for d in docs:
    if d['deleted_at'] is None:
        continue
    out.append(
        "INSERT INTO projmng.wbs_docs (prj_rid, title, content, sort_order, updated_by, updated_at,"
        " created_at, created_by, deleted_at, deleted_by)"
        f" SELECT {prj}, {q(d['title'])}, {q(d['content'] or '')}, {d['sort_order']}, {q(d['updated_by'])},"
        f" {q(d['updated_at'])}, {q(d['created_at'])}, {q(d['created_by'])}, {q(d['deleted_at'])}, {q(d['deleted_by'])}"
        f" WHERE NOT EXISTS (SELECT 1 FROM projmng.wbs_docs x WHERE x.prj_rid = {prj} AND x.title = {q(d['title'])});")

for f in files:
    doc = doc_by_title.format(q(title_of[f['doc_id']]))
    body = bodies.get(f['id'])
    nbytes = len(body.encode('utf-8')) if body is not None else 0
    out.append(
        "INSERT INTO projmng.wbs_doc_files (doc_id, name, url, html, bytes, sort_order, uploaded_at, uploaded_by)"
        f" SELECT {doc}, {q(f['name'])}, {q(f.get('url'))}, {q(body)}, {nbytes}, {f['sort_order']},"
        f" {q(f['uploaded_at'])}, {q(f['uploaded_by'])}"
        f" WHERE {doc} IS NOT NULL AND NOT EXISTS (SELECT 1 FROM projmng.wbs_doc_files x"
        f" WHERE x.doc_id = {doc} AND x.name = {q(f['name'])});")

out.append('COMMIT;')
print('\n'.join(out))
print(f'-- 쪽 {len(docs)} · 휴지통 {sum(1 for d in docs if d["deleted_at"])} · 붙인 문서 {len(files)}', file=sys.stderr)
