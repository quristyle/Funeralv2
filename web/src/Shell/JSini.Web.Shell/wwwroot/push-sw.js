/**
 * 서비스워커 — PWA 설치 요건과 웹푸시 수신을 맡는다.
 *
 * 이력: Vue 시절에는 워크박스가 만든 sw.js 가 이 파일을 importScripts 로
 * 실어 왔다(vite-plugin-pwa). 그 껍데기가 사라졌으므로 **여기가 서비스워커
 * 자체**다. 푸시 처리 부분은 옛 public/push-sw.js 를 그대로 옮긴 것이고,
 * 페이로드 키(title · body · url · icon · tag)는 NotificationServer 의
 * PushSender.BuildPayload 와의 약속이다 — **바꾸면 알림이 빈 채로 뜬다.**
 *
 * ── 캐시를 하지 않는다 ──────────────────────────────────────────
 *
 * 워크박스 시절에도 globPatterns 가 비어 있었다(프리캐시 없음). 지금은 그
 * 이유가 더 분명하다 — Blazor Server 는 화면을 **서버가 그려서 회로로**
 * 보낸다. 캐시해 둘 화면이라는 것이 아예 없고, 어설프게 HTML 을 캐시하면
 * 옛 프레임워크 파일을 가리키는 껍데기가 남아 회로가 안 붙는다.
 *
 * ── 그런데 fetch 처리기는 둔다 ─────────────────────────────────
 *
 * 브라우저가 "설치 가능한 앱" 으로 치려면 매니페스트 말고 **fetch 를 듣는
 * 서비스워커**가 있어야 한다. 아래 처리기는 respondWith 를 부르지 않으므로
 * 브라우저가 평소대로 망을 탄다 — 가로채는 것이 없다. 있다는 사실만 쓴다.
 * 지우면 홈 화면 추가가 조용히 사라진다.
 */

// 새 판을 기다리지 않고 바로 활성화한다(옛 registerType: 'autoUpdate' 와 같다).
// 서비스워커가 캐시를 들고 있지 않으므로 갈아 끼워도 잃는 것이 없다.
self.addEventListener('install', () => {
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    event.waitUntil(self.clients.claim());
});

// 설치 요건용. 위 머리말 참조 — 일부러 아무것도 하지 않는다.
self.addEventListener('fetch', () => { });

self.addEventListener('push', (event) => {
    // 페이로드가 JSON 이 아니거나 비어 있어도 알림 자체는 띄운다 —
    // 조용히 버리면 푸시 권한이 있는데 아무 일도 없는 것처럼 보인다.
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data ? event.data.text() : '' };
    }

    const title = data.title || 'JSini 포털';
    const options = {
        body: data.body || '',
        icon: data.icon || '/pwa-icon-192.png',
        badge: '/pwa-icon-192.png',
        tag: data.tag || undefined,
        data: { url: data.url || '/' },
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    const url = event.notification.data?.url || '/';

    // 이미 열린 창이 있으면 그 창을 앞으로 가져와 이동하고, 없으면 새로 연다.
    event.waitUntil(
        self.clients
            .matchAll({ type: 'window', includeUncontrolled: true })
            .then((windows) => {
                for (const win of windows) {
                    if ('focus' in win) {
                        win.focus();
                        if ('navigate' in win && url !== '/') {
                            win.navigate(url).catch(() => { });
                        }
                        return;
                    }
                }
                return self.clients.openWindow(url);
            }),
    );
});
