/**
 * 서비스워커 — PWA 설치 요건과 웹푸시 수신을 맡는다.
 *
 * 이력: Vue 시절에는 워크박스가 만든 sw.js 가 이 파일을 importScripts 로
 * 실어 왔다(vite-plugin-pwa). 그 껍데기가 사라졌으므로 **여기가 서비스워커
 * 자체**다. 푸시 처리 부분은 옛 public/push-sw.js 를 그대로 옮긴 것이고,
 * 페이로드 키(title · body · url · icon · tag · nid)는 NotificationServer 의
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
        // icon 은 **누가 시킨 일인가**를 그린다. 보내는 쪽이 사람을 지목하면
        // (NotificationServer 의 PushMessageDto.iconOwnerKey) 서버가 그 사람의
        // 프로필 사진 주소(/files/avatar/{파일아이디})를 채워 보내고, 사진이
        // 없는 계정이면 사람 형상 그림자(/avatar-fallback.png)를 채워 보낸다.
        // 지목하지 않은 알림(배포 알림 등)은 여기 기본값인 앱 아이콘으로 뜬다.
        //
        // badge 는 아이콘이 무엇으로 바뀌든 **앱 아이콘 그대로** 둔다 —
        // 안드로이드의 상태 표시줄에 서는 작은 표시라, 여기까지 얼굴로 바꾸면
        // 어느 앱이 보낸 알림인지 알 수 없게 된다.
        body: data.body || '',
        icon: data.icon || '/pwa-icon-192.png',
        badge: '/pwa-icon-192.png',
        tag: data.tag || undefined,
        // nid 는 「내 알림함」의 열쇠(NotificationServer 의 batch_id)다. 눌렀을 때
        // 주소에 실어 보내면 화면이 그 한 건을 읽음으로 찍는다 — 아래 참조.
        data: { url: data.url || '/', nid: data.nid || null },
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

/**
 * 열 주소에 **읽음 표시**를 붙인다 (`?pushId=<nid>`).
 *
 * 알림을 눌러 화면까지 본 사람은 알림함에서도 읽은 것이어야 한다. 그런데
 * 서비스워커는 그 처리를 직접 부를 수 없다 — 알림 API 는 로그인한 사람의
 * 것이고, 이 포털의 토큰은 **서버가 들고 있어 브라우저로 내려오지 않는다**
 * (BFF). 그래서 여기서는 표시만 얹고, 그 표시를 본 화면이 서버 쪽에서
 * 처리한다 (`PushClickRead.razor`).
 *
 * 읽음이 **화면이 실제로 열렸을 때** 찍히는 것은 이 길의 덤이다. 여기서
 * 직접 찍으면 창이 안 뜨거나 도중에 닫혀도 읽은 것이 된다.
 *
 * 다른 출처의 주소에는 손대지 않는다 — 우리 화면이 아니면 표시를 읽을
 * 쪽도 없고, 남의 사이트 주소에 우리 열쇠를 실어 보낼 일도 아니다.
 */
function withReadMark(url, nid) {
    if (!nid) return url;

    try {
        const target = new URL(url, self.location.origin);
        if (target.origin !== self.location.origin) return url;

        target.searchParams.set('pushId', nid);
        return target.pathname + target.search + target.hash;
    } catch {
        return url;
    }
}

self.addEventListener('notificationclick', (event) => {
    event.notification.close();

    const nid = event.notification.data?.nid || null;
    const url = withReadMark(event.notification.data?.url || '/', nid);

    // 이미 열린 창이 있으면 그 창을 앞으로 가져와 이동하고, 없으면 새로 연다.
    //
    // **표시가 있으면 주소가 `/` 라도 이동한다.** 이동하지 않으면 그 창은
    // 표시를 못 보고, 그러면 읽음이 안 찍힌다.
    event.waitUntil(
        self.clients
            .matchAll({ type: 'window', includeUncontrolled: true })
            .then((windows) => {
                for (const win of windows) {
                    if ('focus' in win) {
                        win.focus();
                        if ('navigate' in win && (nid || url !== '/')) {
                            win.navigate(url).catch(() => { });
                        }
                        return;
                    }
                }
                return self.clients.openWindow(url);
            }),
    );
});
