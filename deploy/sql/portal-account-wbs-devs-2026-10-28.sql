-- WBS 원장(projmng.wbs_work)의 담당자·개발자 사번 넷이 포털 계정에 없어 화면에 사번 그대로 보였다.
-- 이름·메일은 보관본 dev_user(docs/wbs_참고소스/wbs-db-20261027.zip)의 것이다.
-- 비밀번호는 아무도 모르는 값이고 처음부터 만료다 — 계정관리의 「비밀번호 초기화」로 발급해 전한다.
-- 부서는 같은 일을 하는 계정들이 있는 준 시스템 · 개발실이다. 이미 있는 아이디는 건너뛴다.
with src (login, nm, mail) as (values
    ('BP33969', '주우인', 'BP33969@bp.hd.com'),
    ('BP33971', '정상준', 'BP33971@bp.hd.com'),
    ('BP33972', '정일인', 'BP33972@bp.hd.com'),
    ('BP34173', '이선우', 'BP34173@bp.hd.com')
), ins as (
    insert into scom.accounts (id, user_id, user_name, real_name, password, created_at, created_by,
                               updated_at, updated_by, company_id, department_id, password_changed_at)
    select 'wbs-dev-' || lower(s.login), s.login, s.nm, s.nm, md5(random()::text || clock_timestamp()::text),
           now(), 'wbs-dev-import', now(), 'wbs-dev-import',
           'jsini', '8721f969-6695-485b-9b20-8442da3ae329', now() - interval '91 days'
      from src s
     where not exists (select 1 from scom.accounts a where lower(a.user_id) = lower(s.login))
    returning id, user_id
)
insert into scom.account_profile_details (id, account_id, detail_type, content, is_primary, created_at, created_by, updated_at, updated_by)
select i.id || '-' || lower(d.t), i.id, d.t, d.c, true, now(), 'wbs-dev-import', now(), 'wbs-dev-import'
  from ins i
  join src s on s.login = i.user_id
 cross join lateral (values ('Status', 'ACTIVE'), ('HomePath', '/workspace'),
                            ('Email', s.mail), ('MsaSource', 'wbs:dev_user:' || s.login)) d (t, c);
