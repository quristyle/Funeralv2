-- ============================================================
-- 위치 기록 표를 만든다 — scom.location_tracks
-- ============================================================
--
-- 포털이 사람의 좌표를 받아 두는 자리는 지금까지 하나였다 —
-- `scom.notification_preferences` 의 `weather_lat` · `weather_lon`.
-- 그것은 **지금 어디 있나** 한 줄이라 덮어쓰기다. 「내 위치 날씨」를 보내는
-- 데에는 그걸로 충분했다.
--
-- 이 표는 **지나온 자리**다. 한 번 잴 때마다 한 줄이 쌓이고 지우지 않는다.
--
-- ── 왜 그 표에 칸을 더하지 않았나 ───────────────────────────
--
-- 저쪽은 사람 하나에 한 행인 **설정 표**다. 이력은 사람 하나에 하루 스물몇
-- 줄이라 모양이 아예 다르고, 설정 표에 얹으면 스위치 하나를 읽는 조회가
-- 수만 줄을 지나게 된다.
--
-- 그래서 「지금 어디」의 정본은 **그대로 저쪽**이다. 이 표는 곁에 쌓이는
-- 기록이고, 지도·목록을 그리는 화면(`/admin/location/my-track`)만 읽는다.
--
-- ── 언제 한 줄이 생기나 ──────────────────────────────────────
--
-- 브라우저가 **방금 재어** 보낸 좌표일 때만이다
-- (`PUT /notifications/preferences/me` 의 `weatherLocated = true`).
-- 스위치 하나를 눌러도 설정 전체가 올라오는데(`ToggleAsync`) 그때마다 줄을
-- 쌓으면 「움직이지 않았는데 기록이 는다」가 된다.
--
-- 재는 쪽은 포털이 열려 있는 동안 한 시간마다 한 번이다(`GeoLocator`).
-- **위치를 허용하지 않은 사람은 한 줄도 생기지 않는다** — 브라우저가 좌표를
-- 아예 안 주기 때문이고, 그것이 이 기능의 동의 경계다.
--
-- ── `moved` 는 왜 적어 두나 ──────────────────────────────────
--
-- 저장하는 쪽이 이미 판단한 값이다(300m — `GeoLocator.MoveThresholdMeters`).
-- 화면은 머문 자리를 다시 묶지만(200m), 그 묶음이 맞는지 눈으로 견줄 단서가
-- 하나는 있어야 한다. 셈해서 버리면 나중에 「왜 여기서 갈라졌나」를 물을 곳이 없다.
--
-- 멱등하다. 되돌리려면 맨 아래 한 줄.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.location_tracks (
    id           text                     PRIMARY KEY,

    owner_type   text                     NOT NULL,   -- 포털 계정은 'jsini'
    owner_key    text                     NOT NULL,   -- 로그인 아이디

    lat          double precision         NOT NULL,
    lon          double precision         NOT NULL,
    accuracy     double precision,                    -- 브라우저가 말한 오차(m). 모르면 빈다
    place        text,                                -- 그때 알아낸 지역 이름. 비는 줄이 흔하다
    moved        boolean                  NOT NULL DEFAULT false,  -- 직전 좌표에서 300m 넘게 옮겼나

    recorded_at  timestamp with time zone NOT NULL DEFAULT now(),

    created_at   timestamp with time zone NOT NULL DEFAULT now(),
    created_by   text,
    updated_at   timestamp with time zone,
    updated_by   text,
    is_deleted   boolean                  NOT NULL DEFAULT false
);

-- 조회는 **언제나 「나의 · 그날」** 이다. 주인과 시각을 한 색인에 담아야
-- 하루치를 뽑는 데 표를 통째로 읽지 않는다.
CREATE INDEX IF NOT EXISTS "IX_location_tracks_owner_time"
    ON scom.location_tracks (owner_type, owner_key, recorded_at);

COMMIT;

-- 확인
--   \d scom.location_tracks
--   SELECT count(*) FROM scom.location_tracks;
--
-- 되돌리기
--   DROP TABLE scom.location_tracks;
