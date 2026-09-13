-- 푸시 발송 기록 표를 만든다 — scom.push_send_logs
--
-- 엔티티: microservices/NotificationServer/Entities/PushSendLog.cs
-- 쓰는 곳: PushSender (발송할 때마다) · /notifications/push/logs·stats·trend·failure-reasons
--
-- [왜 필요한가]
--
-- 알림 서비스는 구독 표만 만졌다(last_sent_at · failure_count). 그래서 포털관리의
-- 「메시지 발송」으로 보낸 알림이 「푸시 현황」·「발송 이력」에 **한 줄도 안 보였다.**
-- 그 화면들은 헬프데스크 DB 의 push_notification_logs 를 읽는데, 거기에 쓰는 것은
-- 헬프데스크 자신의 발송 코드뿐이다.
--
-- 남의 서비스 표에 끼어 쓰지 않는다 — DB 가 다르고, 그렇게 하면 발송하는 서비스가
-- 헬프데스크의 스키마 변경에 묶인다. **보낸 쪽이 자기 기록을 갖는다.**
--
-- [왜 EF 마이그레이션이 아닌가]
--
-- NotificationServer 에는 Migrations 폴더가 **아예 없다.** 구독·설정 표는 손으로
-- 만들어 두었고 이력(scom.__EFMigrationsHistory)은 AuthServer·FileServer 가 함께
-- 쓴다. 여기서 `dotnet ef migrations add` 를 하면 첫 마이그레이션에 **이미 있는
-- 표까지 전부** 들어가고, 그것이 그 공용 이력에 얹힌다. 표 하나 때문에 그 매듭을
-- 건드리지 않는다.
--
-- [안전한 성질]
--
--   · 멱등하다. 여러 번 돌려도 결과가 같다.
--   · 새 표만 만든다. 기존 표를 건드리지 않는다.
--   · 되돌리려면 맨 아래 주석 한 줄.
--
-- [개발과 운영이 같은 DB 다]
--
-- jsiniportal 은 개발 장비도 운영 호스트(jin114.co.kr:31015)를 본다. 그러니 이것을
-- 돌리는 것은 곧 **운영에 반영하는 것**이다. 표를 만드는 일이라 먼저 돌려도 되고,
-- 서비스가 옛 판이면 그 표가 비어 있을 뿐이다.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.push_send_logs (
    id              text                     PRIMARY KEY,
    sent_at         timestamp with time zone NOT NULL DEFAULT now(),

    -- 지금은 'push' 뿐이다. 같은 화면에서 이메일·문자·카카오도 보내므로
    -- 그쪽이 붙을 때 표를 새로 만들지 않으려고 칸을 미리 둔다.
    channel         text                     NOT NULL DEFAULT 'push',

    owner_type      text                     NOT NULL,
    owner_key       text                     NOT NULL,

    -- 못 보낸 줄에는 없다 — 구독이 없거나 수신을 꺼서 시도 자체를 안 한 경우다.
    endpoint        text,

    title           text,
    body            text,
    url             text,

    is_success      boolean                  NOT NULL DEFAULT false,

    -- 사람이 읽는 짧은 말이다(PushSender 의 Reason* 상수). 예외 메시지를 그대로
    -- 넣으면 같은 원인이 열 갈래로 흩어져 「실패 사유별 건수」가 뜻을 잃는다.
    failure_reason  text,

    sent_by         text
);

-- 목록도 통계도 기간이 첫 조건이다.
CREATE INDEX IF NOT EXISTS "IX_push_send_logs_sent_at"
    ON scom.push_send_logs (sent_at DESC);

-- 「이 사람에게 무엇이 갔나」도 자주 묻는다.
CREATE INDEX IF NOT EXISTS "IX_push_send_logs_owner"
    ON scom.push_send_logs (owner_type, owner_key);

COMMIT;

-- 확인
--
--   \d scom.push_send_logs
--   SELECT count(*) FROM scom.push_send_logs;
--   SELECT sent_at, owner_key, title, is_success, failure_reason
--     FROM scom.push_send_logs ORDER BY sent_at DESC LIMIT 20;
--
-- 되돌리기
--
--   DROP TABLE scom.push_send_logs;
