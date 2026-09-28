// 표가 구른 자리를 기억했다가 돌아오면 그 자리에 되돌린다 (CommGrd 의 StateKey).
//
// ─────────────────────────────────────────────────────────────
// [왜 C# 이 아니라 여기서 들고 있나]
//
// 구른 자리를 화면이 **떠나는 순간**에 재려면 Dispose 에서 JS 를 불러야
// 하는데, 그때는 표의 DOM 이 이미 걷히는 중이라 잰 값이 0 이거나 아예
// 던진다. 그래서 구를 때마다 여기서 적어 둔다 — 떠나는 순간을 붙잡을
// 필요가 없어진다.
//
// 적어 두는 곳이 이 모듈이라 **새로고침하면 사라진다.** ScreenState 와
// 같은 수명이고, 그게 맞다(F5 는 「처음부터」라는 뜻이다).
//
// ─────────────────────────────────────────────────────────────
// [구를 칸을 선택자로 찾지 않는다]
//
// 구르는 것은 우리가 그린 <div class="commgrd"> 가 아니라 DevExpress 가
// 그 안에 만든 칸이다. 그 칸의 클래스 이름(dxbl-grid-…)은 판올림마다
// 바뀔 수 있고, 바뀌어도 **아무 오류가 안 난다** — 돌아오면 표가 맨 위에
// 있을 뿐이라 고장난 줄을 모른다. 그래서 이름 대신 **성질**로 찾는다:
// 실제로 넘치고, 넘친 것을 구르게 둔 칸.

/** 열쇠 → 마지막으로 구른 자리. */
const tops = new Map();

function scroller(root) {
    if (!root) return null;

    for (const box of root.querySelectorAll('*')) {
        if (box.scrollHeight - box.clientHeight <= 1) continue;

        const overflow = getComputedStyle(box).overflowY;
        if (overflow === 'auto' || overflow === 'scroll') return box;
    }

    return null;
}

/**
 * 표 하나를 맡는다. 처음이면 되돌리고, 그 뒤로는 구를 때마다 적어 둔다.
 *
 * 돌려주는 값은 **다 끝났는가**다. 거짓이면 구를 칸이 아직 없다는 뜻이라
 * (줄이 덜 그려졌거나 가상 스크롤이 높이를 아직 안 잡았다) 부르는 쪽이
 * 다음 그림에 다시 부른다.
 */
export function sync(root, key) {
    const box = scroller(root);
    if (!box) return false;

    if (box.dataset.jsiniScrollKey !== key) {
        box.dataset.jsiniScrollKey = key;
        box.addEventListener('scroll', () => tops.set(key, box.scrollTop), { passive: true });
    }

    const top = tops.get(key) ?? 0;
    if (!top) return true;

    // 아직 그만큼 길지 않다. 지금 되돌리면 끝까지 밀렸다가 멈춘다.
    if (box.scrollHeight - box.clientHeight < top) return false;

    box.scrollTop = top;
    return true;
}
