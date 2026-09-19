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

    /**
     * 「설치할 수 있다」는 브라우저의 신호.
     *
     * 이 사건은 **한 번 지나가면 다시 오지 않는다.** 나중에 단추로 설치
     * 창을 띄우려면 그때 받아 들고 있어야 해서, 기본 동작을 막고 보관한다
     * (막지 않으면 크롬이 자기 막대를 띄워 버리고 그 뒤에는 못 쓴다).
     *
     * **없다고 해서 설치가 안 되는 것은 아니다** — 이미 설치했거나, 파이어폭스
     * 처럼 이 사건을 안 주는 브라우저이거나, 사파리처럼 메뉴로만 설치하는
     * 경우다. 그래서 화면은 이 값을 「설치할 수 있다」에만 쓰고 없을 때는
     * 아무 말도 하지 않는다.
     */
    let installPrompt = null;

    window.addEventListener('beforeinstallprompt', (event) => {
        event.preventDefault();
        installPrompt = event;
    });

    // 설치가 끝나면 그 신호는 더 이상 뜻이 없다.
    window.addEventListener('appinstalled', () => {
        installPrompt = null;
    });

    /**
     * 지금 이 화면이 **설치된 앱으로 열려 있는가.**
     *
     * 표준은 `display-mode` 미디어 질의이고, iOS 사파리만 자기 방식
     * (`navigator.standalone`)을 쓴다 — 그쪽이 웹푸시를 홈 화면 앱에서만
     * 주기 때문에 **이 판정이 iOS 에서 가장 중요하다.**
     */
    function isStandalone() {
        const modes = ['standalone', 'window-controls-overlay', 'minimal-ui', 'fullscreen'];

        return modes.some((mode) => window.matchMedia('(display-mode: ' + mode + ')').matches)
            || window.navigator.standalone === true;
    }

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

    async function collectDeviceMetadata() {
        const ua = navigator.userAgent || '';
        const uaData = navigator.userAgentData;
        let highEntropy = null;

        if (uaData && typeof uaData.getHighEntropyValues === 'function') {
            try {
                highEntropy = await uaData.getHighEntropyValues([
                    'architecture', 'bitness', 'model', 'platform', 'platformVersion', 'fullVersionList'
                ]);
            } catch (_) {
                highEntropy = null;
            }
        }

        const displayModes = ['standalone', 'window-controls-overlay', 'minimal-ui', 'fullscreen'];
        const displayMode = displayModes.find((mode) =>
            window.matchMedia('(display-mode: ' + mode + ')').matches) || 'browser';
        const standalone = displayMode !== 'browser' || navigator.standalone === true;
        const isMobile = highEntropy && typeof highEntropy.mobile === 'boolean'
            ? highEntropy.mobile
            : /Android|iPhone|iPad|iPod|Mobile/i.test(ua);
        const screen = window.screen || {};
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;

        let deviceType = isMobile ? 'mobile' : 'desktop';
        if (/iPad/i.test(ua) || (isMobile && Math.min(screen.width || 0, screen.height || 0) >= 600)) {
            deviceType = 'tablet';
        }

        const browserBrand = highEntropy && highEntropy.fullVersionList && highEntropy.fullVersionList.length
            ? highEntropy.fullVersionList[highEntropy.fullVersionList.length - 1]
            : null;
        const browser = uaData && uaData.brands && uaData.brands.length
            ? (uaData.brands.find((brand) => !/Not.?A.?Brand/i.test(brand.brand)) || uaData.brands[0])
            : browserBrand;
        const browserMatch = [
            ['Edge', /Edg\/([\d.]+)/],
            ['Samsung Internet', /SamsungBrowser\/([\d.]+)/],
            ['Opera', /OPR\/([\d.]+)/],
            ['Whale', /Whale\/([\d.]+)/],
            ['Firefox', /Firefox\/([\d.]+)/],
            ['Chrome', /Chrome\/([\d.]+)/],
            ['Safari', /Version\/([\d.]+).*Safari\//],
        ].map(([name, pattern]) => [name, pattern.exec(ua)])
            .find(([, match]) => match);
        const uaBrowser = browserMatch ? [browserMatch[0], browserMatch[1][1]] : [null, null];
        const browserName = browser ? browser.brand : uaBrowser[0];
        const browserVersion = browser ? browser.version : uaBrowser[1];
        const platform = highEntropy && highEntropy.platform
            ? highEntropy.platform
            : (/Windows/i.test(ua) ? 'Windows'
                : /Android/i.test(ua) ? 'Android'
                    : /iPhone|iPad|iPod/i.test(ua) ? 'iOS'
                        : /Mac OS X/i.test(ua) ? 'macOS'
                            : /Linux/i.test(ua) ? 'Linux' : null);

        const androidModel = /Android[^;)]*;\s*(?:[a-z]{2}-[A-Z]{2};\s*)?([^;)]+?)\s+Build\//.exec(ua);
        const deviceModel = highEntropy && highEntropy.model
            ? highEntropy.model
            : (/iPhone/i.test(ua) ? 'iPhone'
                : /iPad/i.test(ua) ? 'iPad'
                    : androidModel ? androidModel[1].trim() : null);
        const deviceVendor = /Samsung|SM-/i.test(ua) ? 'Samsung'
            : /Pixel/i.test(ua) ? 'Google'
                : /Xiaomi|Redmi|Mi \d/i.test(ua) ? 'Xiaomi'
                    : /HUAWEI|HONOR/i.test(ua) ? 'Huawei'
                        : /LG[- ]/i.test(ua) ? 'LG'
                            : /Motorola|Moto /i.test(ua) ? 'Motorola'
                                : /iPhone|iPad|iPod/i.test(ua) ? 'Apple' : null;

        return {
            deviceType: deviceType,
            platform: platform,
            platformVersion: highEntropy && highEntropy.platformVersion ? highEntropy.platformVersion : null,
            deviceVendor: deviceVendor,
            deviceModel: deviceModel,
            browser: browserName,
            browserVersion: browserVersion,
            browserEngine: /AppleWebKit/i.test(ua) ? 'WebKit'
                : /Gecko/i.test(ua) ? 'Gecko'
                    : /Trident/i.test(ua) ? 'Trident' : null,
            isMobile: isMobile,
            isStandalone: standalone,
            displayMode: displayMode,
            screenWidth: Number.isFinite(screen.width) ? screen.width : null,
            screenHeight: Number.isFinite(screen.height) ? screen.height : null,
            viewportWidth: document.documentElement ? document.documentElement.clientWidth : null,
            viewportHeight: document.documentElement ? document.documentElement.clientHeight : null,
            devicePixelRatio: Number.isFinite(window.devicePixelRatio) ? window.devicePixelRatio : null,
            colorDepth: Number.isFinite(screen.colorDepth) ? screen.colorDepth : null,
            hardwareConcurrency: Number.isFinite(navigator.hardwareConcurrency) ? navigator.hardwareConcurrency : null,
            deviceMemoryGb: Number.isFinite(navigator.deviceMemory) ? navigator.deviceMemory : null,
            maxTouchPoints: Number.isFinite(navigator.maxTouchPoints) ? navigator.maxTouchPoints : null,
            language: navigator.language || null,
            languages: Array.isArray(navigator.languages) ? navigator.languages.join(', ') : null,
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || null,
            connectionType: connection && connection.type ? connection.type : null,
            effectiveConnectionType: connection && connection.effectiveType ? connection.effectiveType : null,
            userAgentDataJson: uaData ? JSON.stringify({
                brands: uaData.brands || null,
                mobile: uaData.mobile,
                platform: uaData.platform || null,
                architecture: highEntropy && highEntropy.architecture || null,
                bitness: highEntropy && highEntropy.bitness || null,
                model: highEntropy && highEntropy.model || null,
                platformVersion: highEntropy && highEntropy.platformVersion || null,
                fullVersionList: highEntropy && highEntropy.fullVersionList || null
            }) : null
        };
    }

    /**
     * 지금 이 브라우저의 구독. 없으면 null.
     *
     * **`ready` 를 기다리지 않는다.** 그 약속은 활성 워커가 생겨야 풀리는데,
     * 등록이 아예 안 된 브라우저에서는 **영영 풀리지 않는다**(거절도 안 한다).
     * 상태를 읽는 이 길이 거기서 멈추면 그것을 기다리는 화면도 함께 멈춘다 —
     * 로그인 뒤 권유 창이 그 값을 보고 뜰지 말지 정한다.
     *
     * `getRegistration()` 은 등록이 없으면 `undefined` 로 **풀린다.** 구독을
     * 읽는 데는 활성 워커가 필요 없고 등록 하나면 된다.
     */
    async function currentSubscription() {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;

        const registration = await navigator.serviceWorker.getRegistration();
        if (!registration) return null;

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
        const standalone = isStandalone();
        const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;

        if (!supported) {
            return {
                supported: false,
                permission: 'unsupported',
                subscribed: false,
                endpoint: null,
                standalone: standalone,
                serviceWorker: false,
                installable: false,
            };
        }

        // **등록(`registration.active`)으로 본다.** `controller` 는 처음 연
        // 문서에서 잠깐 비어 있어서(우리 워커가 `clients.claim()` 을 부르므로
        // 곧 채워진다) 그것으로 판정하면 「서비스워커 없음」이 한 번 스친다.
        const registration = 'serviceWorker' in navigator
            ? await navigator.serviceWorker.getRegistration()
            : null;

        const sub = await currentSubscription();

        return {
            supported: true,
            permission: Notification.permission,
            subscribed: !!sub,
            endpoint: sub ? sub.endpoint : null,
            standalone: standalone,
            serviceWorker: !!(registration && registration.active),
            installable: !!installPrompt,
        };
    }

    /**
     * 설치 창을 띄운다. **사용자가 단추를 누른 그 사슬에서 불러야** 브라우저가
     * 받아 준다 — 화면이 저절로 부르면 조용히 거절된다(구독 권한과 같다).
     */
    async function install() {
        if (!installPrompt) {
            return { ok: false, error: '이 브라우저에서는 설치 창을 띄울 수 없습니다. 브라우저 메뉴의 「앱 설치」·「홈 화면에 추가」를 쓰십시오.' };
        }

        try {
            installPrompt.prompt();

            const choice = await installPrompt.userChoice;

            // 한 번 쓴 신호는 다시 못 쓴다. 거절했으면 브라우저가 조건이
            // 맞을 때 새 신호를 준다.
            installPrompt = null;

            return { ok: choice && choice.outcome === 'accepted' };
        } catch (error) {
            return { ok: false, error: '설치 창을 띄우지 못했습니다: ' + (error && error.message ? error.message : error) };
        }
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
            metadata: await collectDeviceMetadata(),
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

    window.jsiniPwa = { register, status, subscribe, unsubscribe, install };

    register();
})();
