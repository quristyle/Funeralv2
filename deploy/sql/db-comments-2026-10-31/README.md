# DB 표·칸 코멘트 (2026-10-31)

「jsini통합포탈」 프로젝트(`projmng.devdbinfo` prj_rid 1)에 등록된 DB 의 표·칸 가운데
**코멘트가 비어 있던 것**에 코멘트를 단 SQL 이다. 이미 있던 코멘트는 건드리지 않았다.
모두 운영에 반영했고, 반영 뒤 빈 코멘트 수가 전부 0 인 것을 조회로 확인했다.

| 파일 | DB · 스키마 | 표 | 칸 |
|---|---|---|---|
| jsiniportal-scom.sql | jsiniportal · scom | 10 | 532 |
| funeralv2-smfr.sql | funeralv2 · smfr | 1 | 68 |
| helpdesk-helpdesk.sql | helpdesk · helpdesk | 4 | 265 |
| projmng-projmng.sql | projmng · projmng | 7 | 408 |
| jsinisite-site.sql | jsinisite · site | 0 | 64 |
| ghub-ghub.sql | ghub · ghub | 15 | 271 |
| cargotrust-cargotrust.sql | cargotrust · cargotrust | 14 | 156 |
| funeralfr-smfr.sql | funeralfr.jsini.co.kr:15432/funeral · smfr (옛 장례식장) | 30 | 248 |
| funeralfr-index_watch.sql | 같은 DB · index_watch (pg_index_watch 도구 표) | 5 | 41 |

뜻은 각 서비스의 엔티티·쓰는 코드·`deploy/sql`·`docs` 에서 찾았다. 옛 장례식장(`t_*`)은
이 저장소에 쓰는 코드가 없어 지운 분석 문서(`git show 438e4704^:docs/analysis/40-old-funeral-migration.md`)
를 근거로 했고, 소스에서 뜻을 못 찾은 칸은 이름·타입으로 알 수 있는 만큼만 적었다.

빈 코멘트 수 확인:

```sql
select n.nspname,
       count(*) filter (where a.attnum = 1 and obj_description(c.oid,'pg_class') is null) as 표,
       count(*) filter (where col_description(c.oid,a.attnum) is null) as 칸
from pg_class c join pg_namespace n on n.oid = c.relnamespace
join pg_attribute a on a.attrelid = c.oid and a.attnum > 0 and not a.attisdropped
where c.relkind in ('r','p') and n.nspname not in ('pg_catalog','information_schema')
group by 1;
```
