-- ============================================================
-- 보낸 메일 보관함을 만든다 — scom.portal_mails
-- ============================================================
--
-- 포털 「메일 보내기」(/admin/mail/send)로 사람이 써 보낸 메일 **한 통**이
-- 한 줄이다. 보낸 사람이 다시 열어 읽고, 고쳐서 다시 보내는 자리다.
--
-- ── 왜 발송 기록(push_send_logs)에 얹지 않았나 ───────────────
--
-- 그 표는 **받는 사람마다 한 줄**이고 본문은 500자 미리보기만 남는다. 「저
-- 사람에게 무엇이 나갔나」에 답하는 표라 그 모양이 맞다. 그런데 보낸 사람이
-- 자기가 쓴 메일을 다시 여는 자리에서는 그것으로 모자란다 —
--
--   * 참조(CC)를 적을 칸이 없다
--   * 첨부 이름이 없다
--   * 서식 본문 전문이 없다
--   * 한 통이 주소 수만큼 흩어져 있어 「내가 보낸 메일 목록」이 안 된다
--
-- 그래서 **보낸 글 자체**를 여기 둔다. 쪽지(scom.notes)를 발송 기록에 얹지
-- 않은 것과 같은 까닭이다 — 두드림이 남긴 자국과 글 자체는 수명이 다르다.
-- 발송 기록은 **그대로 함께 남는다**(한 통에 주소 수만큼). 둘을 잇는 끈이
-- batch_id 다.
--
-- ── 첨부는 이름만 적는다 ────────────────────────────────────
--
-- 메일 첨부는 보낼 때 바이트를 그대로 실어 보내고 어디에도 안 올린다. 그
-- 바이트를 보관함에 담으면 아무도 지우지 않는 파일이 보낼 때마다 쌓인다.
-- 보낸 사람이 나중에 묻는 것은 「무엇을 붙여 보냈나」이지 그 파일 자체가
-- 아니다 — 그것은 자기 장비에 있다.
--
-- ── preview 를 따로 두는 까닭 ───────────────────────────────
--
-- 본문이 바로 옆 칸에 있는데도 앞머리를 따로 적는다. 보낸함 목록은 서른
-- 줄씩 훑는 자리이고 서식 메일 본문은 한 통에 수십 KB 라, 목록을 그릴
-- 때마다 그것을 전부 끌어와 태그를 걷으면 읽지도 않을 글 1MB 가 왕복마다
-- 오간다.
--
-- ── 시각은 전부 UTC 다 ──────────────────────────────────────
--
-- timestamptz 이고 기본값도 now() 다. 한국 시각은 보여 주기 직전에 한 번만
-- 만든다(docs/utc-time.md).
--
-- 멱등하다. 되돌리려면 맨 아래 한 줄.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.portal_mails (
    id               text                     PRIMARY KEY,

    sender_key       text                     NOT NULL,   -- 보낸 사람 로그인 아이디 (이 표의 주인 키)
    sender_name      text,                                -- 보낼 그때의 이름 (계정이 바뀌어도 안 흔들린다)

    to_addresses     text                     NOT NULL,   -- 받는 사람. 쉼표로 이어 둔다
    cc_addresses     text,                                -- 참조. 숨은참조가 아니다

    subject          text,
    body             text,                                -- 본문 전문 (HTML)
    preview          text,                                -- 태그를 걷어낸 앞머리 500자 (머리말)
    is_html          boolean                  NOT NULL DEFAULT true,

    attachment_count integer                  NOT NULL DEFAULT 0,
    attachment_names text,                                -- 파일 이름만. 바이트는 담지 않는다
    attachment_bytes bigint                   NOT NULL DEFAULT 0,

    sent_at          timestamp with time zone NOT NULL DEFAULT now(),
    is_success       boolean                  NOT NULL DEFAULT false,
    failure_reason   text,                                -- 못 보낸 까닭 (사람이 읽는 말)

    batch_id         text,                                -- scom.push_send_logs 의 같은 발송을 묶는 열쇠
    has_ai_summary   boolean                  NOT NULL DEFAULT false,

    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    created_by       text,
    updated_at       timestamp with time zone,
    updated_by       text,
    is_deleted       boolean                  NOT NULL DEFAULT false
);

-- 보낸함은 **언제나 「내 것 · 최근 순」** 으로만 묻는다. 남의 보낸함을 여는
-- 길이 아예 없으므로 주인 키가 빠진 조회는 생길 수 없다.
CREATE INDEX IF NOT EXISTS "IX_portal_mails_sender"
    ON scom.portal_mails (sender_key, sent_at DESC);

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- \d scom.portal_mails
-- SELECT count(*) FROM scom.portal_mails;
--
-- 되돌리기
--   DROP TABLE scom.portal_mails;
