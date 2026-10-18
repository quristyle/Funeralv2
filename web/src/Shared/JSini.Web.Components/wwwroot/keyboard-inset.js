/**
 * 휴대폰 키보드가 먹은 높이를 CSS 변수로 내려 준다.
 *
 * ── 무엇이 문제였나 ───────────────────────────────────────────
 *
 * 공통 팝업(`CommPopup`)은 휴대폰에서 화면을 꽉 채운다
 * (`jsini-popup--full-mobile`). 그런데 **글칸을 누르면 키보드가 올라오고,
 * 그 키보드는 창의 아래쪽을 덮어 버린다** — 「저장」과 「닫기」가 거기 있다.
 * 손가락으로 밀어도 닿지 않는다. 창의 키가 여전히 화면 전체이기 때문이다.
 *
 * 브라우저는 키보드가 올라와도 **레이아웃 뷰포트를 줄이지 않는다.**
 * `100vh` 도 `100dvh` 도 `100%` 도 그대로다(크롬의 기본값이
 * `interactive-widget=resizes-visual` 이고, iOS 사파리는 아예 다른 길로
 * 같은 결과를 낸다). 줄어드는 것은 **보이는 뷰포트(visual viewport)** 뿐이고,
 * 그 값을 아는 것은 `window.visualViewport` 하나다. 그래서 CSS 만으로는
 * 풀 수 없고 이 파일이 있다.
 *
 * ── 무엇을 내려 주나 ──────────────────────────────────────────
 *
 *   --jsini-vvh      지금 보이는 높이(px). 키보드를 뺀 값이다.
 *   --jsini-vv-top   보이는 뷰포트가 레이아웃 뷰포트보다 얼마나 내려가 있나.
 *                    `position: fixed` 는 레이아웃 뷰포트를 기준으로 서므로,
 *                    이 값을 `top` 에 주어야 창이 보이는 자리에 남는다
 *                    (iOS 는 키보드가 뜰 때 쪽 전체를 밀어 올린다).
 *   html.jsini-kb-open   키보드가 올라와 있는 동안만 붙는다.
 *
 * **치수를 바꾸는 CSS 는 그 클래스가 붙었을 때만 돈다.** 늘 걸어 두면
 * 주소창이 접히고 펴지는 것과 손가락 확대까지 창 크기를 흔들게 된다 —
 * 고치려던 것은 키보드 하나다.
 *
 * ── 문턱이 120px 인 까닭 ──────────────────────────────────────
 *
 * 보이는 높이는 키보드 말고도 줄어든다. 스크롤에 따라 접히는 주소창이
 * 그렇고(안드로이드 크롬에서 56px 안팎), 그것을 키보드로 치면 창이
 * 까닭 없이 들썩인다. 키보드는 어떤 기기에서도 그보다 훨씬 높다(200px 이상).
 *
 * 확대(`scale`)도 보이는 높이를 줄인다. 두 배로 키우면 절반이 된다.
 * 그래서 견주기 전에 배율을 되곱해 **원래 자로** 돌린다 — 안 그러면
 * 확대한 사람에게는 키보드가 늘 올라와 있는 것으로 보인다.
 *
 * ── 왜 `<head>` 의 평범한 스크립트인가 ────────────────────────
 *
 * 창을 띄우는 쪽이 부르는 모듈로 두면 **창마다 붙였다 떼게 된다.** 창은
 * 서른 개가 넘고 그중 하나라도 빠뜨리면 그 창에서만 단추가 가려진다.
 * 여기서 한 번 걸어 두면 어느 창이 열리든 값이 이미 서 있다.
 *
 * `defer` 다 — 첫 그림과 상관이 없고, 값이 없는 동안에도 CSS 의 되돌림
 * 값(`100dvh` · `0px`)이 지금까지와 똑같이 동작한다.
 */
(function () {
    'use strict';

    var vv = window.visualViewport;

    // 없는 브라우저(아주 옛 것)에서는 아무 일도 하지 않는다. CSS 가 되돌림
    // 값을 쓰므로 지금까지와 똑같이 보인다.
    if (!vv) {
        return;
    }

    /** 키보드로 볼 만큼 줄었는가를 가르는 문턱(px). 주소창과 구별한다. */
    var THRESHOLD = 120;

    var root = document.documentElement;
    var lastHeight = -1;
    var lastTop = -1;
    var lastOpen = null;
    var pending = false;

    /**
     * 글칸이 키보드에 먹혔으면 보이는 자리로 끌어올린다.
     *
     * 창이 줄어드는 것과 브라우저가 글칸을 보이게 미는 것은 **따로 논다** —
     * 브라우저는 줄기 전의 자로 한 번 밀어 두고 끝내므로, 우리가 창을 줄인
     * 뒤에는 글칸이 다시 밖으로 나가 있을 수 있다. 창 안이나 본문 폼의 글칸을 본다.
     */
    function keepFocusVisible() {
        var el = document.activeElement;

        if (!el || typeof el.closest !== 'function') {
            return;
        }

        if (!el.closest('.jsini-popup') && !el.closest('.hd-form') && !el.closest('.jsini-shell__content')) {
            return;
        }

        try {
            el.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        } catch (e) {
            // 옛 브라우저는 인자 꼴을 모른다. 못 밀어도 창은 이미 제자리다.
        }
    }

    function apply() {
        pending = false;

        var height = Math.round(vv.height);
        var top = Math.round(vv.offsetTop);

        if (height !== lastHeight) {
            root.style.setProperty('--jsini-vvh', height + 'px');
            lastHeight = height;
        }

        if (top !== lastTop) {
            root.style.setProperty('--jsini-vv-top', top + 'px');
            lastTop = top;
        }

        // 확대한 만큼 되곱해 원래 자로 돌린 뒤 견준다.
        var unscaled = height * (vv.scale || 1);
        var open = (window.innerHeight - unscaled) > THRESHOLD;

        if (open !== lastOpen) {
            root.classList.toggle('jsini-kb-open', open);
            lastOpen = open;

            // 키보드가 올라오는 동안에는 높이가 여러 번 바뀐다. 다 선 뒤에
            // 한 번만 민다.
            if (open) {
                window.setTimeout(keepFocusVisible, 200);
            }
        }
    }

    /** 키보드는 올라오는 내내 값을 쏟아 낸다. 한 그림에 한 번만 바른다. */
    function schedule() {
        if (pending) {
            return;
        }

        pending = true;
        window.requestAnimationFrame(apply);
    }

    vv.addEventListener('resize', schedule);

    // iOS 는 키보드를 띄우며 **쪽을 밀어 올린다** — 높이는 그대로고
    // `offsetTop` 만 바뀐다. 이것을 안 들으면 창이 화면 위로 반쯤 나간다.
    vv.addEventListener('scroll', schedule);

    window.addEventListener('orientationchange', schedule);

    apply();
})();
