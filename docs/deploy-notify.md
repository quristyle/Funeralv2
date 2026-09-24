# 배포 알림 — 새 이미지가 반영되면 슈퍼관리자에게 PWA 푸시

배포가 끝나 운영 서버에 새 이미지가 올라가면 슈퍼관리자
(`SYSTEM_ADMINISTRATOR`) 전원의 브라우저·휴대폰에 웹푸시 알림이 뜬다.

```
main 푸시
  → GitHub Actions: 이미지 열둘 빌드 → GHCR
  → self-hosted 러너: docker compose pull · up -d
  → 관통 검증 (게이트웨이 · 포털 · PWA 자원 · 소개 사이트)
  → [여기] POST /api/notification/deploy-event
  → NotificationServer: 슈퍼관리자 조회 → 웹푸시 발송
```

## 왜 파이프라인이 알려 주나

「새 이미지가 반영된 시각」을 확실히 아는 것은 `docker compose up -d` 를 부른
쪽뿐이다. 서버가 스스로 알아내려면 도커 소켓의 컨테이너 태그를 주기적으로 훑어
이전 값과 비교해야 하는데, 그러면

* 폴링 간격만큼 늦고,
* 알림을 보내는 서비스(`notify`) 자신이 **방금 재시작된 참**이라 이전 값을 잊은 채
  깨어난다 — 매 배포마다 「전부 바뀌었다」로 보이거나, 기준점을 DB 에 따로
  들고 있어야 한다.

부르는 쪽이 한 줄 알려 주는 편이 정확하고 싸다. 게다가 커밋 SHA·제목·작성자를
그대로 실을 수 있어 알림 본문이 「무엇이 올라갔나」에 답한다.

## 실패한 배포도 알린다

워크플로 단계는 `if: always()` 다. 성공만 알리면 알림이 안 온 것이
「배포가 없었다」인지 「배포가 깨졌다」인지 구분되지 않는데, 둘 중 알아야 할
쪽은 후자다. `job.status` 가 `success` 가 아니면 제목이 **[배포] 실패했습니다** 로 간다.

이 단계 자체는 **배포를 깨뜨리지 않는다.** 알림이 못 갔다고 이미 반영된 배포를
실패로 표시하면, 고치러 들어간 사람이 「배포가 안 됐다」로 읽는다.

## 인증 — 왜 공유 비밀인가

배포 러너에게는 **계정이 없다.** 사람이 로그인해 받는 토큰을 워크플로에 넣어 둘
수는 없으므로, AuthServer 의 배포 보고(`X-Release-Token`)와 같은 방식으로
공유 비밀 하나(`X-Deploy-Token`)를 쓴다.

| 곳 | 값 |
|---|---|
| GitHub 저장소 시크릿 | `DEPLOY_NOTIFY_TOKEN` |
| 운영 서버 | `/srv/jsini/config/NotificationServer/appsettings.Local.json` 의 `DeployNotify:Token` |
| 개발 장비 | `scripts/secrets.env` 의 `DeployNotify__Token` |

세 값이 같아야 한다. 새로 만들려면 `openssl rand -hex 32`.

**토큰이 없으면 엔드포인트가 통째로 닫힌다(503).** 「비었으면 인증을 건너뛴다」로
동작하면 설정을 잊은 장비가 그대로 공개 발송구가 된다 — 그 실수는 조용하고,
그래서 위험하다. 워크플로 쪽도 시크릿이 없으면 부르지 않고 건너뛴다.

## 게이트웨이 경로

`notify` 컨테이너는 호스트에 포트를 열지 않는다(`docker-compose.prod.yml` 머리말).
그래서 러너는 게이트웨이 루프백(`127.0.0.1:5265`)으로 부른다 — 관통 검증 단계가
이미 쓰는 주소다.

게이트웨이의 `notification-deploy-event-route` 는 **알림 경로 중 익명으로 여는
유일한 하나**이고 POST 로 못박혀 있으며 `public-write`(분당 3회)로 조인다.
워크플로의 재시도가 세 번뿐인 것은 그 때문이다 — 더 두드리면 자기가 429 를 맞는다.

## 받는 사람

`scom.role_accounts.role_id = 'SYSTEM_ADMINISTRATOR'` 인 계정 전원
(`DeployNotify:RoleId` 로 바꿀 수 있다).

* 알림을 받으려면 그 사람이 포털에서 **PWA 알림을 구독**해 두어야 한다
  (장례식장 &gt; 설정 &gt; 환경설정, `/funeral/setting/environment`) —
  2026-09-25 까지는 포털관리의 「알림 설정」(`/admin/push/setting`)이 같은 판을
  열고 있었고, 그 화면을 걷어내고 환경설정 하나로 합쳤다.
* 푸시를 끈 사람은 `PushSender` 가 알아서 거른다.
* 못 보낸 것도 `scom.push_send_logs` 에 사유와 함께 남는다 — 「저 사람만 왜 안 왔나」의
  답이 거기 있다 (포털관리 &gt; 알림 &gt; 발송 이력).

알림을 누르면 **배포 현황**(`/admin/status/deploy`)이 열린다. 태그가 `deploy` 로
고정이라 연달아 배포해도 알림이 쌓이지 않고 마지막 것으로 갈린다.

## 손으로 확인하기

운영 서버에서:

```bash
curl -s -X POST http://127.0.0.1:5265/api/notification/deploy-event \
  -H "Content-Type: application/json" \
  -H "X-Deploy-Token: $DEPLOY_NOTIFY_TOKEN" \
  -d '{"status":"success","sha":"0123456","title":"손으로 보낸 시험","actor":"me"}'
```

응답의 `targets` 가 0 이면 슈퍼관리자 계정이 없는 것이고, `sent` 가 0 이면
아무도 구독하지 않았거나 전부 푸시를 꺼 둔 것이다 — `detail` 에 이유가 담긴다.

## 스키마 변경은 없다

읽는 표(`role_accounts` · `accounts`)와 쓰는 표(`push_send_logs`)가 모두 이미 있다.
운영 DB 에 반영할 마이그레이션이 없다.
