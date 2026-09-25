/**
 * 화면 확대(핀치 줌) 잠금 — **휴대폰·태블릿에서만** 건다.
 *
 * ── 왜 필요한가 ───────────────────────────────────────────────
 *
 * 포털은 홈 화면에 앱으로 설치돼서 쓰인다(PWA). 그 안에서 두 손가락이
 * 스치면 화면이 통째로 확대되는데, **되돌리는 길이 잘 안 보인다** —
 * 브라우저 창이 아니라서 주소창도 없고, 축소하려면 다시 두 손가락으로
 * 정확히 오므려야 한다. 그리드를 옆으로 미는 동작이나 달력을 넘기는
 * 동작에서 손가락 둘이 닿는 일이 잦아, 쓰려던 적이 없는 확대가 걸린 채
 * 남는다. 「화면이 커진 채로 안 돌아온다」가 그것이다.
 *
 * ── 넓은 화면은 건드리지 않는다 ───────────────────────────────
 *
 * 자리에 앉아 쓰는 컴퓨터에서 Ctrl+휠·Ctrl+＋ 로 키우는 것은 **눈이 불편한
 * 사람이 쓰는 길**이고, 브라우저 확대는 여기서 막을 수도 없다(그 조작은
 * 페이지에 이벤트로 오지 않는다). 그래서 이 파일이 보는 것은 손가락으로
 * 만지는 좁은 화면뿐이다 — `(pointer: coarse)` 이고 1023px 이하.
 * 경계값은 `MainLayout` 의 태블릿 경계와 같다.
 *
 * ── 세 겹으로 막는다 ──────────────────────────────────────────
 *
 * 하나로는 안 된다. 브라우저마다 듣는 것이 다르다.
 *
 *   1. `<meta name="viewport">` 의 `user-scalable=no, maximum-scale=1`
 *      — iOS 의 **홈 화면 앱(standalone)** 과 옛 안드로이드 WebView 가 듣는다.
 *      크롬은 접근성을 이유로 **이것을 무시한다**(48버전부터). 그래서 이것만
 *      적어 두고 「막았다」고 하면 안드로이드에서는 아무 일도 안 일어난다.
 *
 *   2. `touch-action: pan-x pan-y` — **크롬에서 실제로 듣는 한 줄**이다.
 *      밀기는 그대로 두고 확대만 뺀다. `none` 으로 두면 안 된다 — 그러면
 *      스크롤까지 죽어서 화면이 굳은 것처럼 보인다.
 *
 *   3. `gesturestart`/`gesturechange`/`gestureend` 막기 — 사파리(WebKit)
 *      전용 이벤트다. touch-action 을 안 보던 옛 iOS 까지 덮는다.
 *
 * ── 끌 수 있어야 한다 ─────────────────────────────────────────
 *
 * 작은 글씨를 확대해서 읽는 사람이 있다. 환경설정의 스위치가 이 열쇠를
 * 지우면 잠금이 풀린다 — 「푼 것」을 적어 두고 **없는 것이 기본(잠금)**이다
 * (`PortalBoot.ZoomUnlockedKey` 와 같은 꼴: 있으면 그렇다는 뜻).
 *
 * ── 회로보다 먼저 돈다 ────────────────────────────────────────
 *
 * `<head>` 에서 동기로 돈다. 로그인 화면은 회로조차 없는 정적 SSR 이고,
 * 확대는 **회로가 붙기 전에도** 걸리기 때문이다. C# 쪽은 사람이 스위치를
 * 누를 때만 `jsiniZoom.lock` 을 불러 지금 화면에 곧바로 반영한다.
 */
(function () {
    'use strict';

    /** 확대를 **풀어 두었는가**. 있으면 그렇다는 뜻이고, 없는 것이 기본이다. */
    var KEY = 'jsini-zoom-unlocked';

    /** 손가락으로 만지는 좁은 화면. 경계는 MainLayout 의 태블릿 경계와 같다. */
    var NARROW = '(max-width: 1023px)';

    /** 지금 잠가 두었는가. 이벤트 처리기가 이것만 본다. */
    var locked = false;

    /** 사람이 풀어 두었는가. 저장소를 막아 둔 브라우저에서는 「안 풀었다」다. */
    function unlocked() {
        try {
            return !!window.localStorage.getItem(KEY);
        } catch (e) {
            return false;
        }
    }

    /**
     * 지금 화면이 「휴대폰·태블릿」인가.
     *
     * 굵은 손가락(`pointer: coarse`)**이면서** 좁아야 한다. 둘 중 하나만
     * 보면 안 된다 — 폭만 보면 창을 좁게 줄여 둔 데스크톱까지 걸리고,
     * 손가락만 보면 터치 되는 큰 모니터가 걸린다.
     */
    function mobile() {
        if (!window.matchMedia) {
            return false;
        }

        var coarse = window.matchMedia('(pointer: coarse)').matches
            || navigator.maxTouchPoints > 0;

        return coarse && window.matchMedia(NARROW).matches;
    }

    /**
     * `<meta name="viewport">` 를 고쳐 쓴다. 없으면 만든다 —
     * 이 스크립트가 `App.razor` 의 그 태그 **뒤**에 있으므로 보통은 있다.
     */
    function viewport(on) {
        var meta = document.querySelector('meta[name="viewport"]');

        if (!meta) {
            meta = document.createElement('meta');
            meta.setAttribute('name', 'viewport');
            document.head.appendChild(meta);
        }

        meta.setAttribute('content', on
            ? 'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'
            : 'width=device-width, initial-scale=1.0');
    }

    /**
     * 지금 상태를 화면에 바른다. `want` 를 주지 않으면 저장해 둔 값으로 판단한다.
     *
     * **넓은 화면에서는 무엇을 골랐든 걸지 않는다.** 휴대폰에서 잠가 둔 사람이
     * 같은 브라우저 프로필로 큰 모니터에 앉았을 때 Ctrl+＋ 가 막히면 안 된다.
     */
    function lock(want) {
        var wanted = (want === undefined || want === null) ? !unlocked() : !!want;

        locked = wanted && mobile();

        viewport(locked);

        // 크롬에서 실제로 듣는 한 줄. 풀 때는 빈 값으로 되돌려 둔다 —
        // `auto` 를 박아 두면 아래쪽에서 touch-action 을 쓰는 화면
        // (projmng.css 의 `pan-y`)과 겹칠 때 어느 쪽이 이겼는지 헷갈린다.
        document.documentElement.style.touchAction = locked ? 'pan-x pan-y' : '';
    }

    /** 사파리(WebKit) 전용 확대 제스처. 잠갔을 때만 막는다. */
    function onGesture(e) {
        if (locked) {
            e.preventDefault();
        }
    }

    ['gesturestart', 'gesturechange', 'gestureend'].forEach(function (name) {
        // **`passive: false` 여야 한다.** 기본값으로 붙이면 브라우저가
        // preventDefault 를 무시하고 조용히 확대한다.
        document.addEventListener(name, onGesture, { passive: false });
    });

    // 화면을 돌리거나 창을 줄이면 「휴대폰인가」의 답이 바뀐다. 다시 바른다.
    window.addEventListener('resize', function () { lock(); });
    window.addEventListener('orientationchange', function () { lock(); });

    lock();

    // C# 쪽이 스위치에서 부른다(`PortalBoot.SetZoomUnlockedAsync`).
    // 저장은 그쪽이 하고 여기는 지금 화면에 바르기만 한다.
    window.jsiniZoom = { lock: lock, locked: function () { return locked; } };
})();
