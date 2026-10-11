-- ============================================================
-- 알림관리 — 이벤트 카탈로그와 역할별 정책 (2026-10-11)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/NotifyPolicyPage.razor
-- API:  notification/notification-policies/*  (NotificationServer)
-- 문서: docs/notify-policy.md
--
--   psql -h … -U funeralv2 -d jsiniportal -f notify-policy-2026-10-11.sql
--
-- 두 번 돌려도 안전하다(IF NOT EXISTS · ON CONFLICT).
--
-- ── 무엇을 푸는 표인가 ──────────────────────────────────────
--
-- 「어떤 롤에 어떤 이벤트를 연결하고 어떤 알림을 받게 할 것인가」를 적는다.
-- 지금까지 그 답은 **코드와 설정 파일에 흩어져** 있었다 —
-- 배포 알림은 `DeployNotify:RoleId`, 헬프데스크 요청은 소스에 박힌
-- `SYSTEM_ADMINISTRATOR`, AI 작업은 `AiTasks:RequestNotifyRoles`.
-- 하나를 바꾸려면 저장소를 고쳐 배포해야 했다.
--
-- ── 조용히 막지 않는다 ──────────────────────────────────────
--
-- `notification_policies` 에 줄이 없는 이벤트는 **제한이 없다**. 기본을
-- 「아무도 못 받음」으로 두면 이 표가 생긴 날 포털의 알림이 통째로 멎고,
-- 그 증상은 「알림이 안 온다」 하나라 원인이 이 표로 보이지 않는다.
-- 그래서 **정책 줄은 하나도 넣지 않는다** — 이벤트 목록만 깔고, 역할을
-- 매는 것은 화면에서 한다.
--
-- ── 이벤트 열다섯은 저장소를 센 결과다 ──────────────────────
--
-- 알림이 실제로 나가는 자리를 전부 찾아 적었다(docs/notify-policy.md 의 표).
-- 그중 넷은 `governed = false` 다 — **보이되 정책이 안 걸린다**:
--
--   NOTICE          사람이 그 자리에서 받는 사람을 고르는 발송이라
--                   회사 규칙이 가로챌 자리가 아니다
--   TEST            「눌렀는데 오나」를 보려고 보낸다. 정책이 막으면
--                   시험의 뜻이 없어진다
--   ACCOUNT_MAIL    비밀번호 재설정은 업무 메일이다. 막으면 사용자가
--                   얻는 것이 아무것도 없는데 화면에는 「보냈습니다」가 뜬다
--   HELPDESK_LEGACY 알림 서버를 안 거친다(HelpDeskServer 의 PushUtil ·
--                   EMailUtil 이 직접 보낸다). 목록에서 빼면 「그 알림은
--                   없다」로 읽히고, 걸 수 있는 것처럼 두면 껐는데도 온다
--
-- ── 메일이 나가는 길은 둘이다 (2026-10-11 덧붙임) ───────────
--
-- `email_from_push` 가 참인 다섯(DEPLOY · HELPDESK · BIRTHDAY · WEATHER ·
-- SUBSCRIPTION)은 **PushSender 가 푸시와 같은 내용을 메일로도 낸다.** 나머지는
-- 보내는 쪽이 `/emails/send` 로 제 틀의 메일을 따로 낸다 — 둘 다 참인 줄을
-- 만들면 같은 알림이 두 통 간다. 까닭은 notify-policy-email-2026-10-11.sql.
--
-- 되돌리려면 맨 아래 한 줄.
-- ============================================================

BEGIN;

-- ── 이벤트 카탈로그 ─────────────────────────────────────────
--
-- 식별자가 곧 이벤트 코드다(GUID 를 쓰지 않는다). 보내는 쪽이 적어 보내는
-- 글자로 바로 찾을 수 있어야 발송 경로에 조회가 하나 더 안 붙는다.

CREATE TABLE IF NOT EXISTS scom.notification_events (
    id             text                     PRIMARY KEY,

    name           character varying(128)   NOT NULL,
    description    character varying(512),
    category       character varying(64),            -- NOTI_CATEGORY 코드값. 없을 수 있다
    source         character varying(64),            -- 어느 서비스가 보내나

    target_kind    character varying(16)    NOT NULL DEFAULT 'ROLE',  -- ROLE · USER
    supports_push  boolean                  NOT NULL DEFAULT true,
    supports_email boolean                  NOT NULL DEFAULT false,

    -- 참이면 PushSender 가 앱 푸시와 **같은 내용**을 메일로도 낸다. 거짓이면
    -- 보내는 쪽이 /emails/send 로 제 틀의 메일을 따로 낸다 — 둘 다 참인 줄을
    -- 만들면 같은 알림이 두 통 간다(notify-policy-email-2026-10-11.sql).
    email_from_push boolean                 NOT NULL DEFAULT false,

    governed       boolean                  NOT NULL DEFAULT true,    -- 정책이 실제로 걸리나
    is_active      boolean                  NOT NULL DEFAULT true,    -- 끄면 아무에게도 안 간다
    order_no       integer                  NOT NULL DEFAULT 0,

    created_at     timestamp with time zone NOT NULL DEFAULT now(),
    created_by     text,
    updated_at     timestamp with time zone,
    updated_by     text,
    is_deleted     boolean                  NOT NULL DEFAULT false
);

-- **표가 이미 있으면 위의 CREATE 는 통째로 건너뛴다.** 그래서 나중에 생긴
-- 칸은 여기서 한 번 더 더해야 한다 — 안 그러면 아래 INSERT 가
--
--   ERROR: column "email_from_push" of relation "notification_events" does not exist
--
-- 로 넘어진다. 이 파일을 **먼저 깔아 둔 DB에 다시 돌리는 것**이 바로 그 경우다.
-- 이 줄이 있으면 notify-policy-email-2026-10-11.sql 과 **어느 쪽을 먼저 돌려도**
-- 결과가 같다.
ALTER TABLE scom.notification_events
    ADD COLUMN IF NOT EXISTS email_from_push boolean NOT NULL DEFAULT false;

COMMENT ON TABLE  scom.notification_events IS '알림 이벤트 카탈로그 — 어떤 일이 일어났을 때 보내는 알림인가';
COMMENT ON COLUMN scom.notification_events.target_kind IS 'ROLE 이면 정책 역할이 받는 사람, USER 면 정책 역할이 거름막';
COMMENT ON COLUMN scom.notification_events.governed IS '거짓이면 설정은 받아 두되 발송에는 안 걸린다';
COMMENT ON COLUMN scom.notification_events.email_from_push IS '참이면 PushSender 가 앱 푸시와 같은 내용을 메일로도 낸다. 거짓이면 보내는 쪽이 /emails/send 로 따로 낸다 — 둘 다 참이면 두 통 간다';

-- ── 역할별 정책 ─────────────────────────────────────────────
--
-- (이벤트, 역할)이 유일하다. 두 줄이 생기면 어느 쪽이 참인지 알 수 없고,
-- 그 틀림은 **껐는데 간다** 쪽이라 특히 나쁘다.

CREATE TABLE IF NOT EXISTS scom.notification_policies (
    id            text                     PRIMARY KEY,

    event_code    character varying(64)    NOT NULL,
    role_id       character varying(64)    NOT NULL,

    push_enabled  boolean                  NOT NULL DEFAULT true,
    email_enabled boolean                  NOT NULL DEFAULT false,
    is_active     boolean                  NOT NULL DEFAULT true,

    created_at    timestamp with time zone NOT NULL DEFAULT now(),
    created_by    text,
    updated_at    timestamp with time zone,
    updated_by    text,
    is_deleted    boolean                  NOT NULL DEFAULT false
);

COMMENT ON TABLE scom.notification_policies IS '알림 정책 — 이 역할은 이 이벤트를 이 길로 받는다';

CREATE UNIQUE INDEX IF NOT EXISTS "IX_notification_policies_event_code_role_id"
    ON scom.notification_policies (event_code, role_id);

-- 발송 경로에서 묻는 모양이 언제나 「이 이벤트의 줄 전부」다.
CREATE INDEX IF NOT EXISTS "IX_notification_policies_event_code"
    ON scom.notification_policies (event_code);

-- ── 이벤트 열다섯 ───────────────────────────────────────────
--
-- 이름·설명은 고쳐도 되므로 ON CONFLICT 에서 덮는다. **is_active 는 안
-- 덮는다** — 관리자가 꺼 둔 이벤트를 이 스크립트를 다시 돌렸다고 켜 버리면,
-- 「껐는데 다시 온다」가 되고 원인이 이 파일로 보이지 않는다.

INSERT INTO scom.notification_events (
    id, name, description, category, source,
    target_kind, supports_push, supports_email, email_from_push, governed, is_active, order_no,
    created_at, created_by
) VALUES
-- ── 역할로 가는 것 ──
('DEPLOY', '배포 완료',
 '운영 배포가 끝났을 때. 실패한 배포도 알린다.',
 'DEPLOY', 'NotificationServer', 'ROLE', true, true, true, true, true, 10, now(), 'notify-policy'),

('SIGNUP', '가입 신청 접수',
 '포털 가입 신청이 들어왔을 때. 소셜 가입도 같은 길로 온다.',
 'SIGNUP', 'AuthServer', 'ROLE', true, true, false, true, true, 20, now(), 'notify-policy'),

('HELPDESK', '헬프데스크 요청 등록',
 '새 요청이 올라왔을 때 처리할 사람에게. 이메일을 켜면 같은 내용이 메일로도 간다 — 헬프데스크가 제 관리자 목록으로 따로 보내는 메일(HELPDESK_LEGACY)과 겹칠 수 있다.',
 'HELPDESK', 'HelpDeskServer', 'ROLE', true, true, true, true, true, 30, now(), 'notify-policy'),

('AI_TASK', 'AI 작업 요청·결과',
 '작업을 요청했을 때 지켜보는 역할에게, 끝났을 때 시킨 사람과 그 역할에게. 시킨 본인에게 가는 몫은 정책이 가리지 않는다.',
 'AI_TASK', 'ProjMngServer', 'ROLE', true, true, false, true, true, 40, now(), 'notify-policy'),

('REPORT_MAIL', '보고서 메일',
 '시스템 모니터링 보고서를 주기로 보낼 때. 배치가 고른 역할을 정책이 대신한다.',
 NULL, 'AuthServer', 'ROLE', false, true, false, true, true, 50, now(), 'notify-policy'),

('SITE_INQUIRY', '소개 사이트 문의 접수',
 '회사 소개 사이트에서 문의가 들어왔을 때.',
 NULL, 'SiteServer', 'ROLE', false, true, false, true, true, 60, now(), 'notify-policy'),

-- ── 당사자에게 가는 것 ──
('NOTE', '쪽지 도착',
 '쪽지를 받았을 때. 앱 푸시가 기기에 안 닿으면 2시간 뒤 메일로 돌린다.',
 'NOTE', 'NotificationServer', 'USER', true, true, false, true, true, 110, now(), 'notify-policy'),

('HELPDESK_COMMENT', '내 요청글에 댓글',
 '내가 쓴 요청글에 댓글이 달렸을 때 글 주인에게.',
 'HELPDESK_COMMENT', 'HelpDeskServer', 'USER', true, true, false, true, true, 120, now(), 'notify-policy'),

('BIRTHDAY', '생일 축하 메시지',
 '동료가 보낸 생일 축하 메시지가 도착했을 때.',
 'BIRTHDAY', 'AuthServer', 'USER', true, true, true, true, true, 130, now(), 'notify-policy'),

('WEATHER', '기상 특보 · 내 위치 날씨',
 '기상 특보·실황 기준을 넘었을 때와 내 위치 날씨를 정한 시각에. 날씨 알림을 켠 사람에게만 가므로, 이메일도 그 사람들 중에서만 나간다.',
 'WEATHER', 'LifeEnvServer', 'USER', true, true, true, true, true, 140, now(), 'notify-policy'),

('SUBSCRIPTION', '새 기기 알림 구독',
 '누군가 새 브라우저·기기에서 알림을 구독했을 때 슈퍼관리자에게(등록한 본인은 뺀다).',
 'SUBSCRIPTION', 'NotificationServer', 'USER', true, true, true, true, true, 150, now(), 'notify-policy'),

-- ── 보이되 정책이 안 걸리는 것 (머리말 참고) ──
('NOTICE', '공지·안내 보내기',
 '포털관리의 메시지 발송. 보내는 사람이 그 자리에서 받는 사람을 고르므로 정책이 가로채지 않는다.',
 'NOTICE', 'NotificationServer', 'ROLE', true, true, false, false, true, 210, now(), 'notify-policy'),

('TEST', '시험 발송',
 '환경설정의 「시험 알림 보내기」. 「눌렀는데 오나」를 보는 자리라 정책이 막지 않는다.',
 'TEST', 'NotificationServer', 'USER', true, false, false, false, true, 220, now(), 'notify-policy'),

('ACCOUNT_MAIL', '계정 안내 메일',
 '비밀번호 재설정·계정 발급 안내. 업무 메일이라 끄는 자리를 두지 않는다.',
 NULL, 'AuthServer', 'USER', false, true, false, false, true, 230, now(), 'notify-policy'),

('HELPDESK_LEGACY', '헬프데스크 접수 · 완료 · 종료 · 서비스 점검',
 '알림 서버를 거치지 않고 헬프데스크가 직접 보낸다(PushUtil · EMailUtil). 정책이 아직 안 걸린다.',
 NULL, 'HelpDeskServer', 'USER', true, true, false, false, true, 240, now(), 'notify-policy')

ON CONFLICT (id) DO UPDATE SET
    name           = EXCLUDED.name,
    description    = EXCLUDED.description,
    category       = EXCLUDED.category,
    source         = EXCLUDED.source,
    target_kind    = EXCLUDED.target_kind,
    supports_push  = EXCLUDED.supports_push,
    supports_email = EXCLUDED.supports_email,
    email_from_push = EXCLUDED.email_from_push,
    governed       = EXCLUDED.governed,
    order_no       = EXCLUDED.order_no,
    is_deleted     = false,
    updated_at     = now(),
    updated_by     = 'notify-policy';

COMMIT;

-- 넣은 결과 확인
SELECT id, name, category, source, target_kind,
       supports_push, supports_email, email_from_push, governed, is_active, order_no
  FROM scom.notification_events
 WHERE NOT is_deleted
 ORDER BY order_no;

SELECT count(*) AS 정책줄 FROM scom.notification_policies WHERE NOT is_deleted;

-- 되돌리기 (정책까지 함께 사라진다 — 되돌릴 일이 생기면 화면 설정도 지워진다)
-- DROP TABLE IF EXISTS scom.notification_policies; DROP TABLE IF EXISTS scom.notification_events;
