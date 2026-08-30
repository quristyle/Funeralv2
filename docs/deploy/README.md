# 자동배포 자료 모음

jsini.co.kr · portal.jsini.co.kr 자동배포(2026-08-30 구축)에 관한 자료의 위치.

| 자료 | 위치 |
|---|---|
| **구축 가이드 (정본)** — 성공 명령어 단계별 기록, 운영·롤백 요령 | [docs/analysis/39-jsini-deploy-setup-guide.md](../analysis/39-jsini-deploy-setup-guide.md) |
| 구축 당시 진행 보드 (스냅샷) | [deploy-board.html](deploy-board.html) |
| 워크플로 정의 | [.github/workflows/](../../.github/workflows/) — deploy-backend · deploy-site · deploy-portal |
| Dockerfile · 운영 compose 원본 | [deploy/docker/](../../deploy/docker/) |
| 배포 현황 화면 메뉴 SQL | [docs/sql/deploy_status_menu.sql](../sql/deploy_status_menu.sql) |

**지금 상태를 보는 곳**: 포털 → 시스템 → 상태관리 → **배포 현황**
(GitHub Actions 이력 · 러너 · 운영 컨테이너 태그를 한눈에. 관리자 계열 역할만 보인다.)

평상시 배포는 `git push` (main) 하나다 — 경로 필터가 바뀐 부분만 골라 돌린다.
