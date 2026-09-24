/**
 * 서비스워커 — PWA 설치 요건과 웹푸시 수신을 맡는다.
 *
 * 이력: Vue 시절에는 워크박스가 만든 sw.js 가 이 파일을 importScripts 로
 * 실어 왔다(vite-plugin-pwa). 그 껍데기가 사라졌으므로 **여기가 서비스워커
 * 자체**다. 푸시 처리 부분은 옛 public/push-sw.js 를 그대로 옮긴 것이고,
 * 페이로드 키(title · body · url · icon · tag · nid · sentAt · expiresAt)는
 * NotificationServer 의 PushSender.BuildPayload 와의 약속이다 —
 * **바꾸면 알림이 빈 채로 뜬다.**
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

/**
 * ── 밀려 있던 알림은 한 줄로 묶는다 ────────────────────────────
 *
 * 웹푸시는 서버가 브라우저로 바로 꽂는 것이 아니다. 우리가 보낸 것은 브라우저
 * 제조사의 푸시 서비스(크롬이면 FCM)가 받아 두고 **브라우저와의 연결이 살아날
 * 때까지 들고 기다린다.** 그래서 며칠 안 켠 사람이 브라우저를 여는 순간 그동안의
 * 알림이 한 번에 내려온다 — 서버가 그때 보낸 것이 아니라 **그때 배달된** 것이다.
 *
 * 서버는 이제 수명(TTL)을 짧게 줘서 그 줄 자체를 짧게 만든다
 * (NotificationServer 의 PushDeliveryOptions). 다만 TTL 은 **부탁이지 보장이
 * 아니라서**, 기기가 절전에서 깰 때 시효가 지난 것이 섞여 오는 일이 남는다.
 * 여기가 그 마지막 그물이다.
 *
 * **버리지 않고 한 건으로 묶는 까닭**: push 이벤트를 받고 알림을 하나도 안 띄우면
 * 크롬이 대신 "이 사이트가 백그라운드에서 갱신되었습니다" 를 띄우고, 그것이 반복되면
 * 구독 자체를 끊는다. 묶음 하나는 그 규칙을 지키면서도 창을 하나만 쓴다.
 */
const STALE_TAG = 'jsini-stale-digest';

/** 「내 알림함」 — 묶음을 누르면 여기로 간다. 낱낱의 내용은 여기에 다 있다. */
const INBOX_URL = '/admin/push/history';

/**
 * 기기 시계가 서버보다 앞서 있을 수 있다. 2분은 그 여유다 —
 * 이것이 없으면 방금 온 알림이 시계 차이만큼 「지난 것」으로 접힌다.
 */
const CLOCK_SKEW_MS = 2 * 60 * 1000;

/** 서버가 정한 시효(expiresAt)를 넘겨 도착했나. 값이 없으면 판정하지 않는다. */
function isStale(data) {
    const expiresAt = Number(data && data.expiresAt);
    if (!Number.isFinite(expiresAt) || expiresAt <= 0) return false;
    return Date.now() > expiresAt + CLOCK_SKEW_MS;
}

/**
 * 지난 알림을 한 줄로 센다.
 *
 * 세는 값은 **화면에 떠 있는 묶음 자신**에게서 가져온다(getNotifications).
 * 서비스워커는 푸시 사이에 죽었다 살아나므로 변수에 들고 있으면 0 으로 돌아간다.
 * 사람이 묶음을 지웠으면 다시 1부터 세는 것이 맞다 — 그것이 「본 것」이다.
 */
async function showStaleDigest() {
    let count = 1;
    try {
        const shown = await self.registration.getNotifications({ tag: STALE_TAG });
        const before = Number(shown[0] && shown[0].data && shown[0].data.count);
        if (Number.isFinite(before) && before > 0) count = before + 1;
    } catch {
        // 못 세면 한 건으로 둔다. 묶음이 안 뜨는 것보다는 낫다.
    }

    await self.registration.showNotification(`지난 알림 ${count}건`, {
        body: '자리를 비운 사이에 온 알림입니다. 눌러서 알림함에서 확인하세요.',
        icon: '/pwa-icon-192.png',
        badge: '/pwa-icon-192.png',
        tag: STALE_TAG,
        // 이미 떠 있는 묶음을 조용히 고쳐 쓴다. 한 건 들어올 때마다 울리면
        // 창만 하나일 뿐 소리는 그대로 쏟아지는 것이라 뜻이 없다.
        renotify: false,
        silent: true,
        data: { url: INBOX_URL, nid: null, count },
    });
}

self.addEventListener('push', (event) => {
    // 페이로드가 JSON 이 아니거나 비어 있어도 알림 자체는 띄운다 —
    // 조용히 버리면 푸시 권한이 있는데 아무 일도 없는 것처럼 보인다.
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { body: event.data ? event.data.text() : '' };
    }

    // 시효가 지나 도착한 것은 낱낱이 띄우지 않는다. 위 머리말 참조.
    if (isStale(data)) {
        event.waitUntil(showStaleDigest());
        return;
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
