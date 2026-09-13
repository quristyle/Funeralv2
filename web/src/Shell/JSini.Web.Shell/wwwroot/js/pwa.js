/**
 * PWA 배관 — 서비스워커 등록과 웹푸시 구독.
 *
 * 이력: Vue 의 src/pwa.ts(등록) + 알림 설정 화면의 구독 코드가 하던 일을
 * 한 파일로 합쳤다. 합친 이유는 **구독이 등록에 딸린 일**이기 때문이다 —
 * 등록이 끝나지 않은 상태로 구독을 부르면 조용히 실패하고, 사용자에게는
 * "스위치를 눌렀는데 아무 일도 없다" 로만 보인다.
 *
 * ── 왜 여기(셸)에 두나 ─────────────────────────────────────────
 *
 * 서비스워커의 범위(scope)는 **파일이 놓인 경로 아래**다. 이 배관이 부르는
 * /push-sw.js 가 사이트 루트에 있어야 모든 업무 화면을 관장한다. Razor 클래스
 * 라이브러리(JSini.Web.Components)에 두면 /_content/... 아래로 실려서
 * 그 폴더 밖을 관장하지 못한다 — 알림을 눌러도 업무 화면으로 못 간다.
 *
 * ── 오류를 삼키는 자리와 안 삼키는 자리 ────────────────────────
 *
 * 자동 등록(register)은 삼킨다. 지원하지 않는 브라우저나 비보안 오리진에서
 * 포털 자체가 죽으면 안 된다. 반대로 사용자가 단추를 눌러 부른 것
 * (subscribe/unsubscribe)은 **이유를 담아 돌려준다** — 거기서 삼키면
 * 화면이 할 말이 없어진다.
 */
(function () {
    'use strict';

    const SW_URL = '/push-sw.js';

    /** 서비스워커 등록. 실패해도 앱을 죽이지 않는다. */
    async function register() {
        if (!('serviceWorker' in navigator)) return null;

        try {
            const registration = await navigator.serviceWorker.register(SW_URL, { scope: '/' });

            // 오래 켜 두는 업무 화면이 많다 — 탭이 다시 보일 때 새 판을 확인한다.
            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible') {
                    registration.update().catch(() => { });
                }
            });

            return registration;
        } catch (error) {
            console.warn('[PWA] 서비스워커 등록 실패', error);
            return null;
        }
    }

    /**
     * VAPID 공개 키(base64url 문자열)를 applicationServerKey 가 받는
     * Uint8Array 로 바꾼다.
     *
     * 서버가 주는 것은 base64url 이고 atob 는 표준 base64 만 읽는다.
     * `-`·`_` 를 되돌리고 길이를 4의 배수로 채워야 한다 — 이 변환을
     * 빠뜨리면 subscribe 가 InvalidCharacterError 로 죽는다.
     */
    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const output = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i += 1) output[i] = raw.charCodeAt(i);
        return output;
    }

    /** 구독 키(ArrayBuffer)를 서버가 저장하는 base64url 문자열로. */
    function arrayBufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.length; i += 1) binary += String.fromCharCode(bytes[i]);
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    async function currentSubscription() {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
        const registration = await navigator.serviceWorker.ready;
        return registration.pushManager.getSubscription();
    }

    /**
     * 지금 이 브라우저의 상태. 화면이 단추 모양을 정하는 데 쓴다.
     *
     * `supported` 가 거짓인 경우가 실제로 있다 — iOS 사파리는 **홈 화면에
     * 추가한 뒤에만** 푸시를 준다. 그때 "구독 실패" 가 아니라 "홈 화면에
     * 추가해야 한다" 고 말할 수 있어야 한다.
     */
    async function status() {
        const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
        if (!supported) {
            return { supported: false, permission: 'unsupported', subscribed: false, endpoint: null };
        }

        const sub = await currentSubscription();
        return {
            supported: true,
            permission: Notification.permission,
            subscribed: !!sub,
            endpoint: sub ? sub.endpoint : null,
        };
    }

    /**
     * 구독을 만든다. 돌려주는 값을 그대로 서버에 올리면 된다
     * (POST /api/notification/notifications/subscriptions).
     */
    async function subscribe(vapidPublicKey) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            return { ok: false, error: '이 브라우저는 웹푸시를 지원하지 않습니다.' };
        }
        if (!vapidPublicKey) {
            return { ok: false, error: '서버에 푸시 발송 키(VAPID)가 없습니다.' };
        }

        // 권한 요청은 **사용자 동작에서 이어져야** 한다. 화면이 단추 클릭
        // 처리기에서 부르므로 그 사슬이 이어진다. 화면이 뜨자마자 부르면
        // 브라우저가 조용히 거절한다(Chrome 은 그것을 남용으로 센다).
        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            return { ok: false, error: '브라우저에서 알림 권한이 허용되지 않았습니다.' };
        }

        const registration = await navigator.serviceWorker.ready;

        // 이미 있으면 새로 만들지 않는다. 다시 만들면 endpoint 가 바뀌어
        // 서버에 죽은 구독이 하나 남는다.
        let sub = await registration.pushManager.getSubscription();

        if (sub) {
            // 서버의 VAPID 키가 바뀌었으면 옛 구독으로는 못 받는다. 키가
            // 다르면 끊고 다시 만든다 — 이것을 안 하면 키를 갈았을 때
            // "구독은 되어 있는데 알림이 안 온다" 가 된다.
            const applied = sub.options && sub.options.applicationServerKey
                ? arrayBufferToBase64Url(sub.options.applicationServerKey)
                : null;

            if (applied && applied !== vapidPublicKey.replace(/=+$/, '')) {
                await sub.unsubscribe().catch(() => { });
                sub = null;
            }
        }

        if (!sub) {
            try {
                sub = await registration.pushManager.subscribe({
                    // 크롬은 이 값이 참이 아니면 거절한다 — 보이지 않는
                    // 푸시(무음 푸시)를 허용하지 않는다.
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
                });
            } catch (error) {
                return { ok: false, error: '구독을 만들지 못했습니다: ' + (error && error.message ? error.message : error) };
            }
        }

        const json = sub.toJSON();
        return {
            ok: true,
            endpoint: sub.endpoint,
            p256dh: json.keys ? json.keys.p256dh : null,
            auth: json.keys ? json.keys.auth : null,
        };
    }

    /**
     * 구독을 끊는다.
     *
     * **endpoint 를 먼저 꺼내 돌려준다.** 끊고 나면 그 값을 알 수 없는데,
     * 서버에서 지우려면 그것이 열쇠다. 순서를 바꾸면 브라우저 쪽만 끊기고
     * 서버에는 죽은 구독이 남아 발송마다 실패가 쌓인다.
     */
    async function unsubscribe() {
        const sub = await currentSubscription();
        if (!sub) return { ok: true, endpoint: null };

        const endpoint = sub.endpoint;
        const removed = await sub.unsubscribe().catch(() => false);

        return removed
            ? { ok: true, endpoint: endpoint }
            : { ok: false, endpoint: endpoint, error: '브라우저에서 구독을 끊지 못했습니다.' };
    }

    window.jsiniPwa = { register, status, subscribe, unsubscribe };

    register();
})();
