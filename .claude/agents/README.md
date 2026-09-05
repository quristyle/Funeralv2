# 서브에이전트 구성

Claude Code가 작업 성격에 따라 자동으로 위임하거나, "backend-dev 에이전트로 해줘"처럼 지명해서 쓸 수 있는 전문 에이전트들이다.

| 에이전트 | 담당 | 비고 |
|---|---|---|
| `backend-dev` | `microservices/`, `ApiGateway/` (.NET 10 + EF Core) | 마이그레이션 추가 시 운영 DB 반영 보고 |
| `frontend-dev` | `web/` (.NET 10 Blazor + DevExpress, Piral MFE) | `@page` ↔ DB 메뉴 경로 대조 포함 |
| `player-dev` | `funeralv2_player/` (Flutter) | API 하위 호환 점검 포함 |
| `code-reviewer` | 커밋 전 변경분 리뷰 (읽기 전용) | 비밀값 유출을 최우선으로 점검 |
| `prod-ops` | 운영 서버(jin114.co.kr) SSH·컨테이너·로그 | 조회가 기본, 상태 변경은 승인 후 |
| `db-ops` | PostgreSQL 조회·마이그레이션 대조 | SELECT 만, 마이그레이션 이력 불완전 주의 |

## 운영 접근

`prod-ops` · `db-ops` 는 실제 운영 서버에 붙는다. 접속은 `~/.ssh/jsini_prod` 키로 하고
(`ssh -i ~/.ssh/jsini_prod -p 31010 lee@jin114.co.kr`), DB 는 그 서버 호스트의 **포트 31015** 다.
두 에이전트 모두 **조회를 기본**으로 하고 상태를 바꾸는 명령은 사용자 승인을 받도록 되어 있다.
비밀값은 서버의 `/srv/jsini/config/<서비스>/appsettings.Local.json` 에만 있으며 화면에 출력하지 않는다.

## 에이전트 추가 방법

이 디렉터리에 md 파일을 하나 만들면 된다:

```markdown
---
name: 에이전트-이름
description: 언제 이 에이전트를 쓰는지 (Claude가 위임 판단에 사용)
tools: Read, Grep, Glob, Bash   # 생략하면 모든 도구 사용 가능
---

시스템 프롬프트 본문 — 역할, 작업 원칙, 보고 형식.
```

- `description`이 위임 판단 기준이므로 "언제 쓰는지"를 구체적으로 적는다.
- 읽기 전용 에이전트(리뷰어 등)는 `tools`에서 Write/Edit를 빼서 강제한다.
- 프로젝트 공통 규칙은 루트 [CLAUDE.md](../../CLAUDE.md)에, 에이전트별 규칙은 여기에 둔다.
