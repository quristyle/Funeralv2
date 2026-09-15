/*
    떠오르는 라벨이 **칸이 비었는지** 알 수 있게 해 준다.

    ────────────────────────────────────────────────────────────
    [하는 일은 한 줄이다]

    자리표시 글(`placeholder`)이 없는 입력 칸에 **공백 한 칸**을 넣어 준다.
    그게 전부다. 떠오르고 내려앉는 것은 전부 CSS 가 한다(app.css `떠오르는 라벨`).

    ────────────────────────────────────────────────────────────
    [왜 그것이 필요한가]

    CSS 에는 「이 칸에 값이 있느냐」를 묻는 선택자가 없다. 대신
    `:placeholder-shown` 이 있는데, 이것은 **자리표시 글이 지금 보이느냐**를
    묻는다 — 값이 없을 때만 보이므로 뜻이 같다.

    그런데 **자리표시 글이 아예 없으면 그 선택자는 영영 안 맞는다.** 그러면
    `:not(:placeholder-shown)` 이 늘 참이 되어 라벨이 **처음부터 떠 있다.**
    우리 화면의 입력 칸 대부분이 `NullText` 를 안 쓰므로 그냥 두면 거의 전부가
    그렇게 된다.

    공백 한 칸을 넣어 두면 빈 칸에서 `:placeholder-shown` 이 맞고, 글자를
    치는 순간 안 맞는다. **보이는 것은 아무것도 없다** — 공백이니까.

    ────────────────────────────────────────────────────────────
    [이 파일이 안 실려도 화면이 깨지지 않는다]

    그때는 라벨이 **늘 테두리에 걸친 상태**로 남는다. 읽을 수 있고 자리도
    그대로다 — 잃는 것은 내려앉는 움직임뿐이다. 실패가 조용한 쪽으로 기운다.

    ────────────────────────────────────────────────────────────
    [Blazor 가 나중에 그리는 칸까지 본다]

    화면 대부분이 회로가 붙은 뒤에 그려지고, 팝업 편집 폼은 **단추를 누른 뒤에야**
    생긴다. 그래서 한 번 훑고 끝내지 않고 문서를 지켜본다.

    DevExpress 가 `NullText` 로 자리표시 글을 넣은 칸은 건드리지 않는다 —
    그 글이 보이는 것은 라벨이 떠오른 뒤(포커스)이고, 그 규칙도 CSS 에 있다.
*/

(() => {
    'use strict';

    /** 값이 없을 때만 맞아야 하는 칸들. DevExpress 의 모든 글자 편집기가 이 클래스다. */
    const SELECTOR = '.dxbl-text-edit-input:not([placeholder]), .dxbl-memo-input:not([placeholder])';

    /** 보이지 않는 자리표시 글. 빈 문자열이면 브라우저가 「없음」으로 친다. */
    const BLANK = ' ';

    let queued = false;

    function stamp() {
        queued = false;

        for (const field of document.querySelectorAll(SELECTOR)) {
            field.setAttribute('placeholder', BLANK);
        }
    }

    // 한 박자 모아서 한 번만 훑는다. 표 한 장이 다시 그려질 때 변경 알림이
    // 수백 번 오는데, 그때마다 문서 전체를 훑으면 그 값이 눈에 띈다.
    //
    // `requestAnimationFrame` 을 쓰지 않는다 — 보이지 않는 탭에서는 콜백이
    // 오지 않아, 다른 탭에서 열어 둔 화면의 라벨이 뜬 채로 굳는다.
    function schedule() {
        if (queued) return;
        queued = true;
        setTimeout(stamp, 0);
    }

    function start() {
        stamp();

        // 우리가 붙이는 것도 `placeholder` 속성이라 알림이 한 번 더 온다.
        // 그때 훑으면 `:not([placeholder])` 에 걸리는 칸이 없어 곧 멎는다.
        new MutationObserver(schedule).observe(document.body, {
            childList: true,
            subtree: true,
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
