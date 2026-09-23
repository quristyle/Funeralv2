-- ============================================================
-- 쪽지 표를 만든다 — scom.notes
-- ============================================================
--
-- 사람이 사람에게 보내는 짧은 글이다. 보내면 앱 푸시와 메일로 두드리고,
-- 푸시 아이콘에는 **보낸 사람의 프로필 사진**이 걸린다.
--
-- ── 왜 발송 기록(push_send_logs)에 얹지 않았나 ───────────────
--
-- 그 표는 **보낸 흔적**이다 — 기기마다 한 줄이고, 못 보낸 까닭을 적는 자리이며,
-- 「내 알림함」이 batch_id 로 묶어 보여 준다. 그런데 그것은 어디까지나 알림이
-- 남긴 자국이라, 알림을 못 보내면(푸시를 껐거나 VAPID 가 없거나) 글도 함께
-- 사라진다.
--
-- 쪽지는 **글 자체가 본체다.** 두드림이 둘 다 막혀도 쪽지함에 그대로 있어야
-- 하고, 언젠가 발송 기록에 정리 규칙이 붙어도 남의 편지가 함께 지워지면 안 된다.
--
-- ── 지우기가 보낸 쪽·받은 쪽으로 갈린다 ──────────────────────
--
-- is_deleted 하나로 두면 보낸 사람이 자기 보낸함에서 치우는 순간 받은 사람의
-- 쪽지까지 사라진다. 편지를 부친 사람이 남의 우편함을 비울 수는 없다.
-- 양쪽이 다 치운 줄만 is_deleted 가 참이 된다.
--
-- 멱등하다. 되돌리려면 맨 아래 한 줄.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.notes (
    id               text                     PRIMARY KEY,

    sender_key       text                     NOT NULL,   -- 보낸 사람 로그인 아이디
    sender_name      text,                                -- 보낼 그때의 이름 (계정이 바뀌어도 안 흔들린다)
    receiver_key     text                     NOT NULL,   -- 받는 사람 로그인 아이디
    receiver_name    text,

    title            text,
    body             text,

    sent_at          timestamp with time zone NOT NULL DEFAULT now(),
    read_at          timestamp with time zone,            -- 받은 사람이 읽은 때

    push_sent        boolean                  NOT NULL DEFAULT false,
    email_sent       boolean                  NOT NULL DEFAULT false,
    notify_note      text,                                -- 두드림이 막힌 까닭 (사람이 읽는 말)

    sender_deleted   boolean                  NOT NULL DEFAULT false,
    receiver_deleted boolean                  NOT NULL DEFAULT false,

    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    created_by       text,
    updated_at       timestamp with time zone,
    updated_by       text,
    is_deleted       boolean                  NOT NULL DEFAULT false
);

-- 받은함과 보낸함이 훑는 길이 다르다. 색인 하나로 두면 보낸함이 표를 통째로 읽는다.
CREATE INDEX IF NOT EXISTS "IX_notes_receiver" ON scom.notes (receiver_key, sent_at DESC);
CREATE INDEX IF NOT EXISTS "IX_notes_sender"   ON scom.notes (sender_key,   sent_at DESC);

COMMIT;

-- 확인
--   SELECT count(*) FROM scom.notes;
--   \d scom.notes
--
-- 되돌리기
--   DROP TABLE scom.notes;
