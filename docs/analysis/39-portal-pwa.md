# 39. 포털 PWA — portal.jsini.co.kr 을 앱으로

2026-08-30. 업무 포털(jsini-portal)을 PWA 로 구성했다 — 모바일 홈 화면에 설치되고,
NotificationServer 의 웹푸시를 앱이 닫혀 있어도 받는다.

## 구성

| 것 | 자리 | 내용 |
|---|---|---|
| 매니페스트 | vite.config.ts `application.pwaOptions.manifest` | 이름 "JSini 포털" · standalone · ko · 아이콘 3벌 |
| 서비스워커 | vite-plugin-pwa(generateSW) → `/sw.js` | 프리캐시 없음(`globPatterns: []`) — 로그인 뒤에만 쓰는 화면이라 오프라인 가치가 낮고 청크가 크다 |
| 푸시 수신 | [public/push-sw.js](../../fronts/apps/jsini-portal/public/push-sw.js) | sw 가 `importScripts` 로 실행. **페이로드 키(title·body·url·icon·tag)는 NotificationServer 의 PushSender.BuildPayload 와의 약속** |
| 등록 | [src/pwa.ts](../../fronts/apps/jsini-portal/src/pwa.ts) ← main.ts | `virtual:pwa-register` 를 쓰지 않고 직접 등록 (개발 `/dev-sw.js?dev-sw` · 빌드 `/sw.js`). 가상 모듈은 workbox-window 의존이 붙고 개발 서버에서 동적 로드가 깨졌다 |
| 아이콘 | [docs/brand/generate.py](../brand/generate.py) 8-3절 | **손으로 그리지 않는다.** any(192·512)는 파비콘과 같은 깎인 블록, maskable(512)·apple-touch(180)는 꽉 찬 잉크 판 + 종이색 J |
| iOS 메타 | index.html | apple-touch-icon · theme-color(#0a0a0a) · 상태바 |

vben 의 vite-config 는 원래 `pwa: true` 가 기본인데 **기본 아이콘이 unpkg CDN** 이라
(외부 CDN 금지 — 준수사항 5 의 취지) pwaOptions 전체를 우리 것으로 덮었다.

## 구독 · 발송 흐름 (이미 있던 것과의 연결)

1. 사용자가 [알림 설정](/helpdesk/push/setting) 에서 [구독 등록] — 브라우저 권한 요청 후
   `pushManager.subscribe`(VAPID 공개키는 NotificationServer 가 준다) → 서버에 구독 저장.
   **이 화면은 전부터 있었지만 포털에 서비스워커가 없어 동작하지 못했다** — 이번에 살아났다.
2. NotificationServer `/notifications/push` 가 구독자에게 발송.
3. push-sw.js 가 받아 알림을 띄우고, 누르면 페이로드의 url 로 이동(열린 창 재사용).

## 운영 조건

- **HTTPS 필수.** 서비스워커·푸시는 보안 오리진에서만 돈다 (localhost 는 예외 —
  개발에서는 `devOptions.enabled` 로 dev-sw 가 돈다). portal.jsini.co.kr 은 TLS 로 서빙해야 한다.
- 새 버전 배포 시: registerType autoUpdate + 탭이 다시 보일 때 update() 확인이라
  강제 새로고침 없이 다음 방문/포커스에 갈아탄다.
- 검증한 것: 개발 서버에서 sw 활성(activated) · 매니페스트("JSini 포털" + 아이콘 3벌) ·
  dev/빌드 sw 모두 push-sw.js importScripts 포함 · 알림 설정 화면 "서비스 워커: 활성".
  브라우저 알림 권한은 자동화 환경이라 denied — 실기기에서 허용 후 구독하면 된다.
