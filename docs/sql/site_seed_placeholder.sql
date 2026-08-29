-- 회사 소개 사이트의 문구를 임시 내용으로 채운다 (D-S7 · D-S8 · D-S9)
--
-- ⚠ **`jsinisite` DB 에 돌린다.** 이 폴더의 다른 SQL 은 대부분 jsiniportal 용이다.
--
-- ⚠ 이 문구는 **임시다.** 회사가 확정한 것이 아니다.
--    모든 행에 createdby = 'PlaceholderSeed' 를 남겨 두었으니 나중에 이것으로 찾아 바꾼다.
--
--      SELECT * FROM site.sections WHERE createdby = 'PlaceholderSeed';
--      SELECT * FROM site.posts    WHERE createdby = 'PlaceholderSeed';
--
--    지우고 다시 채우려면 아래 두 줄을 먼저 돌린다.
--      DELETE FROM site.sections WHERE createdby = 'PlaceholderSeed';
--      DELETE FROM site.posts    WHERE createdby = 'PlaceholderSeed';
--
-- ⚠⚠ 특히 `contact.consent` (개인정보 수집·이용 동의 문구)는 **법률 검토 없이 쓴 것이다.**
--     사이트를 실제로 공개하기 전에 반드시 확정해야 한다. 보관 기간을 3년으로 적어 두었는데
--     이것도 정해진 값이 아니다.
--
-- 무엇을 일부러 안 썼나
--   설립연도 · 임직원 수 · 고객사 이름 · 매출 같은 **확인할 수 없는 사실은 넣지 않았다.**
--   임시 문구가 사실처럼 굳어 버리면 나중에 고치는 것보다 나쁘다. 그래서 '연혁' 블록도
--   두지 않고 '일하는 방식' 으로 대신했다.
--
-- 언어는 행으로 나눈다 — (sectionkey, locale) · (slug, locale) 이 각각 유일하다.
--
-- 반복 실행해도 안전하다 (같은 열쇠는 내용만 덮어쓴다).

BEGIN;

-- ── 홈 ──────────────────────────────────────────────────────
INSERT INTO site.sections (id, sectionkey, locale, title, subtitle, body, sortorder, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'home.identity', 'ko', '하나의 신원',
   '인증 · 메뉴 · 권한을 한 곳에서',
   '시스템마다 계정을 따로 두면 사람이 늘 때마다 등록할 곳도 늘어납니다. 누가 무엇을 볼 수 있는지도 시스템마다 달라집니다.'
   || E'\n\n' ||
   '저희는 신원과 권한을 한 곳에 둡니다. 새 시스템이 붙어도 계정과 권한은 그 한 곳에서만 관리합니다.',
   1, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'home.scale', 'ko', '늘어나도 하나',
   '시스템은 늘고 관리는 늘지 않게',
   '장례식장 · 헬프데스크 · 프로젝트관리를 같은 방식으로 붙여 왔습니다. 화면과 메뉴는 각자의 것이지만, 들어오는 문과 권한은 하나입니다.'
   || E'\n\n' ||
   '다음 시스템도 같은 자리에 붙습니다.',
   2, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'home.record', 'ko', '남는 기록',
   '누가 언제 무엇을 했는지',
   '접속과 변경이 기록으로 남습니다. 문제가 생겼을 때 짐작으로 찾지 않고, 남은 것을 보고 찾습니다.',
   3, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'home.identity', 'en', 'One identity',
   'Sign-in, menus and permissions in one place',
   'When every system keeps its own accounts, each new person means one more place to register — and what each person may see ends up different from system to system.'
   || E'\n\n' ||
   'We keep identity and permissions in a single place. New systems join without adding another place to manage.',
   1, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'home.scale', 'en', 'More systems, one console',
   'Growth without growing the overhead',
   'Funeral halls, help desk and project management were all joined the same way. Their screens and menus stay their own; the front door and the permissions are shared.'
   || E'\n\n' ||
   'The next system joins in the same place.',
   2, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'home.record', 'en', 'A record that stays',
   'Who did what, and when',
   'Sign-ins and changes are recorded. When something goes wrong we read what was written down instead of guessing.',
   3, true, 'PlaceholderSeed')
ON CONFLICT (sectionkey, locale) DO UPDATE
  SET title = excluded.title, subtitle = excluded.subtitle, body = excluded.body,
      sortorder = excluded.sortorder, ispublished = excluded.ispublished,
      updatedat = now(), updatedby = 'PlaceholderSeed';

-- ── 회사소개 ────────────────────────────────────────────────
INSERT INTO site.sections (id, sectionkey, locale, title, subtitle, body, sortorder, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'about.overview', 'ko', '무엇을 하는 회사인가', NULL,
   'JSINI 는 업무 시스템을 만들고, 흩어진 시스템을 하나로 묶는 일을 합니다.'
   || E'\n\n' ||
   '새로 만드는 것만 하지 않습니다. 이미 돌고 있는 시스템을 그대로 살려 두고 신원과 권한만 한 곳으로 모으는 방식을 씁니다. 쓰던 화면을 버리지 않아도 되고, 멈추지 않아도 됩니다.',
   1, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'about.business', 'ko', '사업 영역', NULL,
   '· 업무 시스템 구축 — 현장에서 쓰는 화면부터 서버까지'
   || E'\n' ||
   '· 시스템 통합 — 이미 돌고 있는 시스템을 하나의 인증과 권한 아래로'
   || E'\n' ||
   '· 운영 — 붙인 뒤에도 함께 봅니다',
   2, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'about.how', 'ko', '일하는 방식', NULL,
   '**멈추지 않게 붙입니다.** 쓰고 있는 시스템을 세우고 옮기는 방식은 쓰지 않습니다. 옆에 붙이고, 확인한 뒤에 넘깁니다.'
   || E'\n\n' ||
   '**고친 이유를 남깁니다.** 무엇을 고쳤는지가 아니라 왜 그렇게 했는지를 남깁니다. 사람이 바뀌어도 그 판단을 다시 하지 않게 하려는 것입니다.'
   || E'\n\n' ||
   '**되돌릴 수 있게 합니다.** 한 번에 크게 바꾸지 않고, 켜고 끌 수 있는 상태로 올립니다.',
   3, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'about.overview', 'en', 'What we do', NULL,
   'JSINI builds business systems, and joins scattered ones into a single console.'
   || E'\n\n' ||
   'Not only greenfield work. We leave running systems in place and pull only identity and permissions into one place. Nothing has to be thrown away, and nothing has to stop.',
   1, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'about.business', 'en', 'What we offer', NULL,
   '· Building business systems — from the screens on the floor to the servers'
   || E'\n' ||
   '· Integration — bringing running systems under one sign-in and one permission model'
   || E'\n' ||
   '· Operations — we stay after the joining is done',
   2, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'about.how', 'en', 'How we work', NULL,
   '**We join without stopping anything.** We do not take a running system down to move it. We build alongside, verify, then hand over.'
   || E'\n\n' ||
   '**We write down why.** Not what changed, but why it was decided that way — so the next person does not have to make the call again.'
   || E'\n\n' ||
   '**We keep it reversible.** Changes go up behind switches rather than all at once.',
   3, true, 'PlaceholderSeed')
ON CONFLICT (sectionkey, locale) DO UPDATE
  SET title = excluded.title, subtitle = excluded.subtitle, body = excluded.body,
      sortorder = excluded.sortorder, ispublished = excluded.ispublished,
      updatedat = now(), updatedby = 'PlaceholderSeed';

-- ── 개인정보 수집·이용 동의 문구 (D-S7) ──────────────────────
-- ⚠ 법률 검토 없이 쓴 임시 문구다. 공개 전에 반드시 확정해야 한다.
--   화면(views/contact.vue)이 이 블록을 읽어 동의 체크박스 옆에 보여 준다.
--   블록이 없으면 화면에 박아 둔 같은 뜻의 문구가 대신 쓰인다.
INSERT INTO site.sections (id, sectionkey, locale, title, subtitle, body, sortorder, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'contact.consent', 'ko', '개인정보 수집·이용 동의', NULL,
   '**수집 항목** 이름, 이메일, 연락처(선택), 회사명(선택), 문의 내용'
   || E'\n\n' ||
   '**이용 목적** 문의에 대한 답변과 그에 따른 연락'
   || E'\n\n' ||
   '**보관 기간** 문의 접수일로부터 3년. 기간이 지나면 지웁니다.'
   || E'\n\n' ||
   '**동의하지 않을 권리** 동의하지 않으셔도 됩니다. 다만 동의 없이는 문의를 접수할 수 없으니, 그때는 이메일로 직접 연락해 주십시오.',
   1, true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'contact.consent', 'en', 'Consent to collection and use of personal data', NULL,
   '**What we collect** Name, email, phone (optional), company (optional), and your message'
   || E'\n\n' ||
   '**Why** To answer your enquiry and to contact you about it'
   || E'\n\n' ||
   '**How long we keep it** Three years from the date of the enquiry, then deleted'
   || E'\n\n' ||
   '**Your right to decline** You may decline. We cannot accept an enquiry without consent, so please email us directly instead.',
   1, true, 'PlaceholderSeed')
ON CONFLICT (sectionkey, locale) DO UPDATE
  SET title = excluded.title, subtitle = excluded.subtitle, body = excluded.body,
      sortorder = excluded.sortorder, ispublished = excluded.ispublished,
      updatedat = now(), updatedby = 'PlaceholderSeed';

-- ── 뉴스 (D-S8 · 임시) ──────────────────────────────────────
-- 날짜를 '오늘 기준 상대값' 으로 넣는다. 특정 날짜를 박아 두면 그것이 사실처럼 굳는다.
INSERT INTO site.posts (id, slug, locale, title, summary, body, publishedat, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'one-console', 'ko', '관리 포털을 하나로 묶었습니다',
   '장례식장 · 헬프데스크 · 프로젝트관리가 같은 인증과 권한 아래로 들어왔습니다.',
   '따로 돌던 세 시스템을 하나의 관리 포털 아래로 묶었습니다. 화면과 메뉴는 각자의 것을 그대로 쓰고, 계정과 권한만 한 곳에서 관리합니다.'
   || E'\n\n' ||
   '쓰고 있던 시스템을 세우지 않고 옆에 붙이는 방식으로 진행했습니다.',
   now() - interval '20 days', true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'brand-renewal', 'ko', '브랜드를 다시 정리했습니다',
   '심볼 · 워드마크 · 색을 규칙으로 못박고, 파일을 코드로 만들도록 바꿨습니다.',
   '로고와 색을 규칙으로 정리했습니다. 각진 기하 형태와 무채색만 씁니다.'
   || E'\n\n' ||
   '자산 파일은 손으로 고치지 않고 생성기로 만듭니다. 규칙이 바뀌면 파일 전체가 한 번에 따라옵니다.',
   now() - interval '5 days', true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'one-console', 'en', 'One console for three systems',
   'Funeral halls, help desk and project management now share one sign-in and one permission model.',
   'Three systems that used to run apart now sit under a single admin portal. Their screens and menus stay as they were; only accounts and permissions moved into one place.'
   || E'\n\n' ||
   'Nothing was taken down to do it — we built alongside and handed over.',
   now() - interval '20 days', true, 'PlaceholderSeed'),

  (gen_random_uuid(), 'brand-renewal', 'en', 'The brand, put in order',
   'Symbol, wordmark and colour are now rules, and the asset files are generated from code.',
   'The logo and palette are now written down as rules — angular geometry and neutral tones only.'
   || E'\n\n' ||
   'Asset files are produced by a generator rather than edited by hand, so a change to the rules updates every file at once.',
   now() - interval '5 days', true, 'PlaceholderSeed')
ON CONFLICT (slug, locale) DO UPDATE
  SET title = excluded.title, summary = excluded.summary, body = excluded.body,
      publishedat = excluded.publishedat, ispublished = excluded.ispublished,
      updatedat = now(), updatedby = 'PlaceholderSeed';

COMMIT;

-- 확인
--   SELECT locale, sectionkey, title FROM site.sections ORDER BY locale, sortorder, sectionkey;
--   SELECT locale, slug, title, publishedat FROM site.posts ORDER BY locale, publishedat DESC;
