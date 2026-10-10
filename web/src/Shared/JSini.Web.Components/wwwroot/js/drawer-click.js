/*
    오른쪽 서랍(`RightSideDrawer`)의 **바깥 클릭**과 **고정 표시**를 맡는다.

    [듣는 자리가 문서 하나다]

    판마다 손놀림을 걸지 않는다. 서랍은 셋이고 모두 셸이 사는 내내 마크업에
    남아 있어서, 판마다 걸면 같은 일을 하는 처리기가 셋 돌고 떼는 자리도 셋이
    된다. 문서에 하나만 걸고 **열려 있고 못 박히지 않은** 판만 추려 알린다.

    [못 박힌 판은 건드리지 않는다]

    `isPinned` 는 서랍이 `updateState` 로 알려 준 값이다. 참이면 바깥을 눌러도
    아무 일도 하지 않는다 — 고정핀의 뜻이 그것이다.

    [본문을 옆으로 미는 표시는 여기서 붙인다]

    못 박힌 판이 하나라도 펴져 있으면 `<body>` 에 `jsini-has-pinned-drawer` 가
    붙고, app.css 가 그 표시를 보고 본문에 오른쪽 여백을 준다(≥768px).
    서랍마다 제가 붙이면 **둘이 열렸다 하나만 닫을 때** 남은 하나가 펴져 있는데도
    표시가 걷힌다 — 그래서 셈은 늘 전체를 훑는다.
*/

const drawers = new Map();

function clickHandler(e) {
    for (const [id, state] of drawers.entries()) {
        if (!state.isOpen || state.isPinned) {
            continue;
        }

        // 판 안쪽을 누른 것은 바깥 클릭이 아니다.
        const el = document.getElementById(id);
        if (el && el.contains(e.target)) {
            continue;
        }

        // 회로가 이미 끊겼으면 조용히 넘어간다 — 이 판들은 모든 화면에 늘
        // 실려 있어서, 여기서 예외가 새면 창을 닫는 것만으로 오류가 난다.
        state.dotnet.invokeMethodAsync('CloseFromOutside').catch(() => {});
    }
}

document.addEventListener('mousedown', clickHandler);
document.addEventListener('touchstart', clickHandler, { passive: true });

export function init(id, dotnet) {
    drawers.set(id, { dotnet: dotnet, isOpen: false, isPinned: false });
}

export function updateState(id, isOpen, isPinned) {
    const state = drawers.get(id);
    if (state) {
        state.isOpen = isOpen;
        state.isPinned = isPinned;
    }
    updateBodyClass();
}

function updateBodyClass() {
    let hasPinned = false;
    for (const state of drawers.values()) {
        if (state.isOpen && state.isPinned) {
            hasPinned = true;
            break;
        }
    }
    document.body.classList.toggle('jsini-has-pinned-drawer', hasPinned);
}

export function dispose(id) {
    drawers.delete(id);
    updateBodyClass();
}
