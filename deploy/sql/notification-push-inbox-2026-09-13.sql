-- 알림함을 위해 발송 기록에 칸 둘을 더한다 — scom.push_send_logs
--
--   batch_id  한 번 보낸 것을 묶는 열쇠
--   read_at   받은 사람이 읽은 때
--
-- [왜 필요한가]
--
-- 「내 알림함」(/admin/push/history)은 헬프데스크 DB 의 알림 표를 읽고 있었다.
-- 포털에서 보낸 알림은 그 표에 안 들어가므로 **받는 사람 화면에도 안 보였다.**
--
-- 발송 기록(같은 표)을 그 화면의 출처로 쓴다. 다만 그 줄은 **기기 단위**다 —
-- 기기 둘을 쓰는 사람에게 같은 알림이 두 줄로 보이고, 하나만 읽음 처리하면
-- 나머지가 남는다. 그래서 한 번 보낸 것을 batch_id 로 묶고, 읽음은 그 묶음에
-- 딸린 그 사람의 줄을 전부 찍는다.
--
-- [옛 줄은 그대로 둔다]
--
-- batch_id 가 없는 줄(이 칸을 만들기 전에 쌓인 것)은 줄 자체를 열쇠로 삼는다.
-- 화면과 서버가 그 갈래를 살피므로 채워 넣지 않아도 된다.
--
-- 멱등하다. 되돌리려면 맨 아래 두 줄.

BEGIN;

ALTER TABLE scom.push_send_logs ADD COLUMN IF NOT EXISTS batch_id text;
ALTER TABLE scom.push_send_logs ADD COLUMN IF NOT EXISTS read_at  timestamp with time zone;

-- 알림함은 「내 것 중 안 읽은 것」을 먼저 묻는다.
CREATE INDEX IF NOT EXISTS "IX_push_send_logs_inbox"
    ON scom.push_send_logs (owner_type, owner_key, sent_at DESC);

COMMIT;

-- 확인
--   SELECT batch_id, count(*), min(read_at) FROM scom.push_send_logs GROUP BY batch_id;
--
-- 되돌리기
--   ALTER TABLE scom.push_send_logs DROP COLUMN batch_id;
--   ALTER TABLE scom.push_send_logs DROP COLUMN read_at;
