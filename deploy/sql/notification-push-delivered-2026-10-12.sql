-- 푸시 발송 기록에 「기기에 닿은 때」를 더한다 (2026-10-12)
--
-- [왜 필요한가 — 「보냈다」는 「닿았다」가 아니다]
--
-- is_success 는 **푸시 서비스(FCM 등)가 받아 주었다**는 뜻일 뿐이다. 기기가
-- 꺼져 있으면 푸시 서비스가 수명(TTL)까지 들고 있다가 조용히 버리고,
-- 브라우저 자료를 지운 사람의 구독은 한동안 멀쩡한 얼굴로 성공을 돌려준다.
-- 그래서 「기기 넷에 등록했는데 하나만 울린다」를 기록만 보고는 가릴 수가
-- 없었다 — 네 줄이 모두 is_success = true 로 남아 있기 때문이다.
--
-- 서비스워커가 알림을 띄운 뒤 POST /notifications/push/delivered 로
-- (묶음 아이디 + 제 구독 주소)를 적어 보내면 그 한 줄에만 시각이 찍힌다.
--
-- [기기의 상태다]
--
-- read_at · deleted_at 은 **사람**의 상태라 묶음(batch_id) 전체로 번지는데,
-- 이 칸은 번지지 않는다. 번지게 하면 바로 그 하나(「어느 기기가 못 받고
-- 있나」)를 못 보게 된다.
--
-- 알림 서비스에는 EF 마이그레이션이 없다(Program.cs 가 Migrate() 를 안 부른다).
-- 이 파일을 운영 DB(jsiniportal)에 직접 넣는다.

ALTER TABLE scom.push_send_logs
  ADD COLUMN IF NOT EXISTS delivered_at timestamptz NULL;

COMMENT ON COLUMN scom.push_send_logs.delivered_at IS
  '그 기기가 알림을 띄웠다고 되알려 온 때(UTC). 서비스워커의 보고이고, 비어 있다고 못 닿았다는 증거는 아니다.';

-- 「보냈는데 안 닿은 줄」을 묶음·기기로 집는 조회가 이 칸의 주 쓰임이다.
-- 부분 인덱스로 두는 까닭은 **안 닿은 줄만 찾기** 때문이다 — 닿은 줄은
-- 발송 이력에서 시각으로 훑지 따로 집지 않는다.
CREATE INDEX IF NOT EXISTS ix_push_send_logs_undelivered
  ON scom.push_send_logs (batch_id, endpoint)
  WHERE delivered_at IS NULL AND endpoint IS NOT NULL;
