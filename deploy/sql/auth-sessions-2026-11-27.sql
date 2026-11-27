-- 로그인 세션 표 (2026-11-27)
-- 접근·갱신 토큰이 SessionId 클레임으로 이 줄을 가리킨다. 로그아웃·비밀번호 변경이 줄을 닫으면 갱신이 거절된다.
-- 뽑은 곳: dotnet ef migrations script 20261123051506_AddCompanyUsageManagers AddAuthSessions (AuthServer)
-- 운영 반영: 2026-11-27 (jsiniportal)

START TRANSACTION;
CREATE TABLE scom.auth_sessions (
    id text NOT NULL,
    account_id text NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    last_refreshed_at timestamp with time zone,
    revoked_at timestamp with time zone,
    revoke_reason text,
    ip text,
    user_agent text,
    created_at timestamp with time zone NOT NULL,
    created_by text,
    updated_at timestamp with time zone,
    updated_by text,
    is_deleted boolean NOT NULL,
    CONSTRAINT "PK_auth_sessions" PRIMARY KEY (id),
    CONSTRAINT "FK_auth_sessions_accounts_account_id" FOREIGN KEY (account_id) REFERENCES scom.accounts (id) ON DELETE CASCADE
);
COMMENT ON TABLE scom.auth_sessions IS '로그인 한 번이 만든 세션. 접근·갱신 토큰이 모두 SessionId 클레임으로 이 줄을 가리킨다.';
COMMENT ON COLUMN scom.auth_sessions.account_id IS '세션의 주인.';
COMMENT ON COLUMN scom.auth_sessions.expires_at IS '이 세션이 끝나는 시각(UTC). 갱신해도 늘지 않는다 — 로그인 한 번이 사는 최대 길이다.';
COMMENT ON COLUMN scom.auth_sessions.last_refreshed_at IS '마지막으로 갱신한 시각(UTC).';
COMMENT ON COLUMN scom.auth_sessions.revoked_at IS '닫힌 시각(UTC). 비어 있으면 살아 있다.';
COMMENT ON COLUMN scom.auth_sessions.revoke_reason IS '닫힌 까닭(LOGOUT · PASSWORD_CHANGED · PASSWORD_RESET · ADMIN_PASSWORD_RESET).';
COMMENT ON COLUMN scom.auth_sessions.ip IS '로그인한 IP.';
COMMENT ON COLUMN scom.auth_sessions.user_agent IS '로그인한 브라우저·기기.';

CREATE INDEX "IX_auth_sessions_account_id" ON scom.auth_sessions (account_id);

INSERT INTO scom."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261127132618_AddAuthSessions', '10.0.11');

COMMIT;

