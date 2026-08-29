-- 회사 소개 사이트의 문구 (site_seed_placeholder.sql 을 대체한다)
--
-- ⚠ **`jsinisite` DB 에 돌린다.** 이 폴더의 다른 SQL 은 대부분 jsiniportal 용이다.
--
-- 반복 실행해도 안전하다 (같은 열쇠는 내용만 덮어쓴다).
--
-- ── 무엇이 바뀌었나 ────────────────────────────────────────
--
-- 앞의 임시 문구(site_seed_placeholder.sql)는 회사가 무엇을 파는지 모르는 상태에서
-- 썼다. 그래서 "하나의 인증으로 통합한다" 는 **우리 내부 아키텍처 이야기**가 되어 있었다.
-- 그것은 회사가 파는 것이 아니라 회사가 쓰는 방법이다.
--
-- 회사가 하는 일은 **시스템 납품 · 유지보수 · 소프트웨어 개발 · 업무시스템 업그레이드 ·
-- 보수 관리** 다. 그래서 첫 화면을 "만들고, 계속 함께 간다" 로 바꾸고,
-- 실제로 만들어 운영 중인 시스템 다섯을 '구축 사례' 페이지로 새로 세웠다.
--
-- ── 고객사 이름을 쓰지 않는다 ─────────────────────────────
--
-- 사례는 **분야로만** 적는다. 고객사명·도메인·링크를 넣지 않는다.
-- 레퍼런스 공개는 대개 계약서에 조항이 있고, 그 확인 없이 공개 사이트에 올리면
-- 되돌릴 수 없다. 동의를 받으면 `work.*` 의 subtitle 과 body 만 고치면 된다.
--
-- ── 내용의 출처 ───────────────────────────────────────────
--
-- 아래 다섯 사례의 기능 설명은 **짐작이 아니라 이 저장소에서 확인한 것**이다.
--
--   장례식장   scom.system_menus 의 실제 메뉴 + funeralv2 DB 의 smfr 표 16개
--   기상 감시  ghub DB 의 weather_* 표 15개 (microservices/GhubServer)
--   설비·수요반응
--              fronts/apps/jsini-portal/src/api/helpdesk/oadr.ts,
--              ApiGateway/appsettings.json 의 oadr-route,
--              HelpDeskServer/HealthCheckWorker.cs
--   헬프데스크 jinrecept DB 의 jsini 표 40개
--   프로젝트관리 projmng DB 의 projmng 표 21개
--
-- ⚠ **숫자는 일부러 넣지 않았다.** ghub 표에 행이 수십만 건 있지만 그것은
--    우리 쪽으로 옮겨 온 사본이라, 고객사의 운영 규모라고 말할 수 있는 값이 아니다.
--    "몇 개 사업장" 같은 숫자를 쓰려면 고객사에 확인해야 한다.
--
-- ⚠ **분야 이름(subtitle)은 시스템이 다루는 일로 적었다.** 고객사의 업종이 아니다.
--    업종으로 바꾸려면 (예: '에너지 유통') 그것도 공개 동의에 해당한다.
--
-- ⚠⚠ `contact.consent` (개인정보 수집·이용 동의)는 **여전히 법률 검토 전이다.**
--     이 파일에서 건드리지 않는다. site_seed_placeholder.sql 의 것이 그대로 남는다.
--     사이트를 실제로 공개하기 전에 반드시 확정해야 한다 (D-S7).
--
-- 언어는 행으로 나눈다 — (sectionkey, locale) · (slug, locale) 이 각각 유일하다.

BEGIN;

-- ── 홈 ──────────────────────────────────────────────────────
-- 첫 화면의 큰 글씨(히어로)는 DB 가 아니라 코드에 있다 —
-- fronts/apps/jsini-site/src/i18n/messages.ts 의 `hero`.
-- 여기 세 블록은 그 아래에 붙는다.
INSERT INTO site.sections (id, sectionkey, locale, title, subtitle, body, sortorder, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'home.build', 'ko', '만듭니다',
   '현장에서 쓰는 화면부터 서버까지',
   '업무를 듣고, 화면과 서버를 만들어 납품합니다. 현장에 놓이는 장비까지 함께 다루는 일도 있습니다 — 빈소 안내 화면처럼 사람이 직접 보는 것이 시스템의 일부인 경우입니다.',
   1, true, 'ContentSeed'),

  (gen_random_uuid(), 'home.stay', 'ko', '계속 함께 갑니다',
   '납품이 끝이 아닙니다',
   '만든 시스템은 저희가 계속 봅니다. 유지보수와 보수 관리를 이어 가고, 서버가 살아 있는지 자동으로 확인합니다.'
   || E'\n\n' ||
   '연락할 곳이 필요합니다. 그래서 **헬프데스크를 직접 만들어 운영합니다.** 요청이 어디까지 갔는지 남고, 담당자가 바뀌어도 기록이 남습니다.',
   2, true, 'ContentSeed'),

  (gen_random_uuid(), 'home.upgrade', 'ko', '멈추지 않고 바꿉니다',
   '이미 돌고 있는 시스템도',
   '쓰던 시스템을 세워 놓고 옮기는 방식은 쓰지 않습니다. 옆에 붙여 만들고, 확인한 뒤에 넘깁니다.'
   || E'\n\n' ||
   '오래된 업무 시스템을 지금 쓰는 기술로 바꾸는 일도 같은 방식입니다. 한 번에 갈아엎지 않고 화면 단위로 옮깁니다.',
   3, true, 'ContentSeed'),

  (gen_random_uuid(), 'home.build', 'en', 'We build',
   'From the screens on the floor to the servers',
   'We listen to how the work is actually done, then build and deliver the screens and the servers. Sometimes that includes the hardware on site — where what people look at is itself part of the system.',
   1, true, 'ContentSeed'),

  (gen_random_uuid(), 'home.stay', 'en', 'We stay',
   'Delivery is not the end',
   'What we build, we keep watching. Maintenance and upkeep continue after handover, and health checks tell us when something stops answering.'
   || E'\n\n' ||
   'You need somewhere to reach us. That is why we **built and run our own help desk** — requests leave a trail, and the trail outlives whoever was on duty.',
   2, true, 'ContentSeed'),

  (gen_random_uuid(), 'home.upgrade', 'en', 'We change things without stopping them',
   'Including systems already running',
   'We do not take a working system down in order to move it. We build alongside, verify, then hand over.'
   || E'\n\n' ||
   'Modernising an ageing system works the same way — screen by screen, never all at once.',
   3, true, 'ContentSeed')
ON CONFLICT (sectionkey, locale) DO UPDATE
  SET title = excluded.title, subtitle = excluded.subtitle, body = excluded.body,
      sortorder = excluded.sortorder, ispublished = excluded.ispublished,
      createdby = excluded.createdby,   -- 임시 문구 표시(PlaceholderSeed)를 걷어낸다
      updatedat = now(), updatedby = 'ContentSeed';

-- 열쇠가 바뀌어 위 문장에 덮이지 않는 옛 블록들을 내린다.
-- (지우지 않고 비공개로만 돌린다 — 문구를 되살리고 싶을 때가 있다)
--
--   home.identity/scale/record  통합 이야기를 하던 첫 문구
--   work.facility               '설비 모니터링·리포트' 로 잘못 적었던 03.
--                               실제로 보고 나니 리포트가 아니라 유틸리티 공급
--                               계통 감시와 정산이었다. work.utility 로 다시 썼다.
--   work.weather                '사업장 기상 감시 시스템' 으로 좁게 적었던 02.
--                               로그인해 보니 기상은 그 시스템의 **한 모듈**이고,
--                               실제로는 날씨 · 안전지표 · 식단 · 생일 · 업무 시스템
--                               바로가기를 묶은 사내 포털이었다. work.hub 로 다시 썼다.
--
-- 표 구조만 보고 두 번 틀렸다. 표는 무엇을 저장하는지 알려 주지만
-- 무엇을 하는 시스템인지는 알려 주지 않는다.
UPDATE site.sections
   SET ispublished = false, updatedat = now(), updatedby = 'ContentSeed'
 WHERE sectionkey IN ('home.identity', 'home.scale', 'home.record',
                      'work.facility', 'work.weather');

-- ── 회사소개 ────────────────────────────────────────────────
INSERT INTO site.sections (id, sectionkey, locale, title, subtitle, body, sortorder, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'about.overview', 'ko', '무엇을 하는 회사인가', NULL,
   'JSINI 는 업무 시스템을 만들어 납품하고, 그 뒤로도 함께 운영하는 회사입니다.'
   || E'\n\n' ||
   '만드는 일과 지키는 일을 나누지 않습니다. 만든 사람이 계속 보는 편이 빠르고, 왜 그렇게 만들었는지를 아는 사람이 고치는 편이 안전하기 때문입니다.'
   || E'\n\n' ||
   '새로 만드는 것만 하지 않습니다. 이미 돌고 있는 시스템을 그대로 살려 두고 이어받는 일, 오래된 것을 지금 기술로 바꾸는 일이 저희 일의 절반입니다.',
   1, true, 'ContentSeed'),

  (gen_random_uuid(), 'about.business', 'ko', '사업 영역', NULL,
   '**시스템 납품** 업무를 듣고 화면과 서버를 만들어 넘깁니다. 현장 장비가 함께 필요한 경우도 다룹니다.'
   || E'\n\n' ||
   '**유지보수** 넘긴 뒤에도 계속 봅니다. 헬프데스크로 접수받고, 서버 상태를 자동으로 확인합니다.'
   || E'\n\n' ||
   '**소프트웨어 개발** 업무에 맞는 것을 새로 만듭니다. 기성품으로 안 되는 자리를 채웁니다.'
   || E'\n\n' ||
   '**업무시스템 업그레이드** 오래된 시스템을 멈추지 않고 지금 기술로 바꿉니다. 한 번에 갈아엎지 않고 화면 단위로 옮깁니다.'
   || E'\n\n' ||
   '**보수 관리** 서버 · 데이터베이스 · 배포까지 함께 맡습니다. 문제가 생긴 뒤가 아니라 생기기 전에 봅니다.',
   2, true, 'ContentSeed'),

  (gen_random_uuid(), 'about.how', 'ko', '일하는 방식', NULL,
   '**멈추지 않게 붙입니다.** 쓰고 있는 시스템을 세우고 옮기는 방식은 쓰지 않습니다. 옆에 붙이고, 확인한 뒤에 넘깁니다.'
   || E'\n\n' ||
   '**고친 이유를 남깁니다.** 무엇을 고쳤는지가 아니라 왜 그렇게 했는지를 남깁니다. 사람이 바뀌어도 그 판단을 다시 하지 않게 하려는 것입니다.'
   || E'\n\n' ||
   '**되돌릴 수 있게 합니다.** 한 번에 크게 바꾸지 않고, 켜고 끌 수 있는 상태로 올립니다.'
   || E'\n\n' ||
   '**남의 이름으로 말하지 않습니다.** 고객사의 동의 없이 이름을 실적으로 쓰지 않습니다. 이 사이트의 사례를 분야로만 적은 것도 그래서입니다.',
   3, true, 'ContentSeed'),

  (gen_random_uuid(), 'about.overview', 'en', 'What we do', NULL,
   'JSINI builds business systems, delivers them, and keeps running them alongside our clients.'
   || E'\n\n' ||
   'We do not separate building from keeping. Whoever built it is faster at watching it, and whoever knows why it was built that way is safer at changing it.'
   || E'\n\n' ||
   'Not only greenfield work. Taking over systems that are already running, and modernising ageing ones, is half of what we do.',
   1, true, 'ContentSeed'),

  (gen_random_uuid(), 'about.business', 'en', 'What we offer', NULL,
   '**Delivery** We learn how the work is done, then build and hand over the screens and the servers — including on-site hardware where it matters.'
   || E'\n\n' ||
   '**Maintenance** We keep watching after handover. Requests come through our help desk; health checks watch the servers.'
   || E'\n\n' ||
   '**Software development** Built to fit the work, for the places an off-the-shelf product cannot reach.'
   || E'\n\n' ||
   '**Modernisation** Ageing systems brought up to date without being taken down — screen by screen, never all at once.'
   || E'\n\n' ||
   '**Operations** Servers, databases and deployment too. Looked at before something breaks, not after.',
   2, true, 'ContentSeed'),

  (gen_random_uuid(), 'about.how', 'en', 'How we work', NULL,
   '**We join without stopping anything.** We do not take a running system down to move it. We build alongside, verify, then hand over.'
   || E'\n\n' ||
   '**We write down why.** Not what changed, but why it was decided that way — so the next person does not have to make the call again.'
   || E'\n\n' ||
   '**We keep it reversible.** Changes go up behind switches rather than all at once.'
   || E'\n\n' ||
   '**We do not trade on our clients'' names.** No client is listed as a credential without their agreement. That is why the work on this site is described by field rather than by name.',
   3, true, 'ContentSeed')
ON CONFLICT (sectionkey, locale) DO UPDATE
  SET title = excluded.title, subtitle = excluded.subtitle, body = excluded.body,
      sortorder = excluded.sortorder, ispublished = excluded.ispublished,
      createdby = excluded.createdby,
      updatedat = now(), updatedby = 'ContentSeed';

-- ── 구축 사례 (views/work.vue) ───────────────────────────────
-- subtitle 이 분야, title 이 시스템 성격이다. 고객사명은 어디에도 없다.
INSERT INTO site.sections (id, sectionkey, locale, title, subtitle, body, sortorder, ispublished, createdby)
VALUES
  (gen_random_uuid(), 'work.funeral', 'ko', '장례식장 관리 시스템', '장례 서비스',
   '건물 · 층 · 호실과 장비를 등록해 두고, 고인 · 상주 · 담당자 · 협력업체 · 시설을 빈소에 묶습니다. 빈소 현황과 호실 이력을 한 화면에서 봅니다.'
   || E'\n\n' ||
   '**빈소 안내 화면까지 이 시스템이 송출합니다.** 영상 · 음원 · 리본 문구 · 장식을 화면에서 정해 현장 장비로 내보냅니다. 종이와 사람 손으로 하던 일이 등록 한 번으로 끝납니다.'
   || E'\n\n' ||
   '사용 내역과 과금 내역이 쌓여, 정산을 따로 세지 않아도 됩니다.',
   1, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.hub', 'ko', '사내 통합 포털', '사내 포털 · 안전',
   '출근해서 처음 여는 화면입니다. 오늘 날씨와 기상 특보, 안전 지표, 식단, 생일이 한 장에 있고, 쓰는 업무 시스템으로 가는 문이 그 아래 모여 있습니다. **시스템마다 주소를 외우지 않아도 됩니다.**'
   || E'\n\n' ||
   '**가장 공들인 곳은 날씨입니다.** 기상청 예보(초단기 · 단기 · 중기)와 기상특보를 사업장 위치별로 계속 받아 둡니다. 위치를 예보 격자에 맞춰 두어 사업장마다 자기 자리의 값을 봅니다.'
   || E'\n\n' ||
   '**기준을 넘으면 그때부터 기록이 됩니다.** 사업장이 정한 기준값을 넘긴 순간이 이벤트로 남고, 무엇을 했는지가 대응 기록으로 이어 붙습니다. 특보가 언제 떴다 언제 풀렸는지도 함께 남습니다. 날씨를 보는 화면이 아니라 **날씨 때문에 무엇을 했는지 남기는 화면**입니다.'
   || E'\n\n' ||
   '안전 지표도 같은 자리에 둡니다. 점수와 순위가 매일 보이면 분기 보고서로 볼 때와 다르게 움직입니다.',
   2, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.utility', 'ko', '전기 · 용수 · 증기 공급 관리', '산업단지 유틸리티',
   '전기 · 용수 · 증기를 만들어 단지 안 여러 회사에 공급하는 사업입니다. 연료가 들어와 보일러와 터빈을 지나 압력이 다른 증기 헤더로 갈라지고, 옆에서는 원수가 정제되어 순수가 됩니다. **그 전부가 한 장의 계통도 위에서 실시간으로 움직입니다.**'
   || E'\n\n' ||
   '공급한 만큼이 검침으로 쌓이고, 그것이 그대로 공급량 · 매출 정산으로 이어집니다. 15분 단위 전기 사용량과 피크 확인까지 같은 자리에 있습니다.'
   || E'\n\n' ||
   '**단위가 다른 값을 한 그림에 겹쳐 봅니다.** 증기는 T/H, 전기는 kW, 용수는 T/D 라 자릿수가 아예 다릅니다. 한 축에 얹으면 큰 것 하나만 보이고 나머지는 바닥에 눕습니다. 그래서 계열마다 축을 따로 세웠습니다. 튄 값은 빼고 다시 볼 수 있고, 계열별 최저 · 최대 · 피크가 표에 함께 붙습니다.'
   || E'\n\n' ||
   '**파는 쪽과 쓰는 쪽이 같은 숫자를 봅니다.** 공급자용과 수용가용 화면이 나뉘어 있어, 정산 때 숫자를 맞추는 일이 없습니다.',
   3, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.helpdesk', 'ko', '헬프데스크', 'IT 유지보수 지원',
   '저희가 만들고 저희가 씁니다. 고객사별로 개선 요청을 접수하고, 담당 배정 · 일정 · 진행 상태를 한 줄기로 잇습니다. 요청이 어디까지 갔는지 묻지 않아도 보입니다.'
   || E'\n\n' ||
   '접수 · 검토 · 반영이 기록으로 남습니다. 담당자가 바뀌어도 그 기록이 남고, 첨부와 대화도 같이 남습니다.'
   || E'\n\n' ||
   '서버가 응답하지 않으면 알림이 갑니다. 사람이 알아채기 전에 먼저 압니다.',
   4, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.projmng', 'ko', '프로젝트 · 개발 관리', '소프트웨어 개발 관리',
   '프로젝트와 WBS 를 두고 일정과 담당을 관리합니다. 여기까지는 흔한 도구와 같습니다.'
   || E'\n\n' ||
   '**다른 점은 설계도가 프로젝트 안에 산다는 것입니다.** 구성도 · 흐름도 · ERD 를 그리는 편집기가 시스템 안에 들어 있어, 도면이 별도 파일로 떠돌지 않고 일정 · 소스 · DB 정보와 같은 탭 줄에 놓입니다.'
   || E'\n\n' ||
   '어떤 화면이 어떤 표를 쓰는지, 표의 컬럼이 무엇을 뜻하는지도 그 옆에 있습니다. **인수인계 때 코드만 넘어가고 맥락은 사라지는 일**을 막으려고 이렇게 두었습니다.',
   5, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.funeral', 'en', 'Funeral hall management', 'Funeral services',
   'Buildings, floors, rooms and equipment are registered once; the deceased, the family, the staff on duty, contractors and facilities are then tied to a hall. Current state and room history read from one screen.'
   || E'\n\n' ||
   '**The guidance displays are driven from the same system.** Video, audio, ribbon text and decoration are chosen on screen and pushed to the equipment on site — work that used to be done on paper, by hand.'
   || E'\n\n' ||
   'Usage and billing accumulate as you go, so settlement is not counted twice.',
   1, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.hub', 'en', 'Company intranet hub', 'Intranet · site safety',
   'The first screen of the working day. Today''s weather and warnings, safety scores, the canteen menu and birthdays on one page — and below them, the doors into every system people actually use. **No one has to remember addresses.**'
   || E'\n\n' ||
   '**The weather is where most of the work went.** Forecasts (nowcast, short and medium range) and official warnings are collected continuously per site, each location mapped to the forecast grid so every site reads the numbers for its own ground.'
   || E'\n\n' ||
   '**The record starts when a threshold is crossed.** The moment a site''s own limit is exceeded becomes an event, and what was done about it is attached to it. When a warning was raised and when it was lifted is kept alongside. Not a screen for looking at the weather — a screen for recording **what the weather made you do**.'
   || E'\n\n' ||
   'Safety scores sit in the same place. A number seen daily moves differently from one seen in a quarterly report.',
   2, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.utility', 'en', 'Utility supply: live monitoring and settlement', 'Industrial estate utilities',
   'Steam, electricity and treated water, produced and supplied to the companies across an industrial estate. Fuel enters, passes boilers and turbines, and divides into steam headers at different pressures while raw water is refined alongside. **All of it moves live on a single process diagram.**'
   || E'\n\n' ||
   'Not only the diagram. What each company draws is metered, and that meter reading carries straight through to supply volume and billing. Fifteen-minute electricity use and peak checks sit in the same place.'
   || E'\n\n' ||
   '**Both sides read the same number.** Supplier and consumer have their own screens over one set of readings, so settlement is not an argument about figures.',
   3, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.helpdesk', 'en', 'Help desk', 'IT support and maintenance',
   'We built it and we use it. Improvement requests are logged per client, then carried through assignment, scheduling and status in one thread. You can see how far a request has got without asking.'
   || E'\n\n' ||
   'Intake, review and delivery all leave a record — one that outlives whoever was on duty, attachments and conversation included.'
   || E'\n\n' ||
   'If a server stops answering, a notification goes out. We know before anyone has to notice.',
   4, true, 'ContentSeed'),

  (gen_random_uuid(), 'work.projmng', 'en', 'Project and development management', 'Software delivery management',
   'Projects and work breakdown, with schedule and ownership. So far, like any such tool.'
   || E'\n\n' ||
   '**What differs is that the drawings live inside the project.** An editor for architecture, flow and ER diagrams is built into the system, so designs do not drift off into loose files — they sit in the same row of tabs as the schedule, the source and the database notes.'
   || E'\n\n' ||
   'Which screen uses which table, and what a column actually means, is kept there too. All of it exists to stop **a handover passing on the code and losing the reasoning**.',
   5, true, 'ContentSeed')
ON CONFLICT (sectionkey, locale) DO UPDATE
  SET title = excluded.title, subtitle = excluded.subtitle, body = excluded.body,
      sortorder = excluded.sortorder, ispublished = excluded.ispublished,
      createdby = excluded.createdby,
      updatedat = now(), updatedby = 'ContentSeed';

COMMIT;

-- 확인
--   SELECT locale, sectionkey, title, ispublished FROM site.sections ORDER BY sectionkey, locale;
--   SELECT sectionkey, title, subtitle FROM site.sections WHERE locale='ko' AND sectionkey LIKE 'work.%' ORDER BY sortorder;
--
-- 되돌리려면
--   DELETE FROM site.sections WHERE createdby = 'ContentSeed';
--   UPDATE site.sections SET ispublished = true WHERE sectionkey IN ('home.identity','home.scale','home.record');
--   \i docs/sql/site_seed_placeholder.sql
