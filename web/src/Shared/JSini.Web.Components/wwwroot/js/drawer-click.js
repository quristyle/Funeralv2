/*
    오른쪽 서랍(`RightSideDrawer`)의 **바깥 클릭**, **고정 표시**, **너비 조절(Resize)**을 맡는다.

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
    이 여백 크기는 조절된 서랍 너비(`--jsini-drawer-width`)와 정확히 일치한다.

    [너비 조절 — 왼쪽 끝에서 마우스로 끈다]

    책상 화면에서는 서랍 왼쪽 테두리(`.jsini-right-drawer__resizer`)를 마우스로
    잡아 끌어 너비를 늘리거나 줄일 수 있다.
    마우스 드래그 중에는 `--jsini-drawer-width` 가 실시간으로 갱신되므로,
    핀으로 고정된 상태에서도 본문 컨텐츠 영역이 서랍 크기에 맞춰 실시간으로 줄어든다.
    너비는 브라우저(`localStorage`)에 보존된다.
*/

const drawers = new Map();
let isResizing = false;

const DEFAULT_WIDTH = 416; // 26rem (16px 기준)
const MIN_WIDTH = 300;
const MAX_WIDTH = 1200;
const STORAGE_KEY_PREFIX = 'jsini_drawer_width_';
const GLOBAL_STORAGE_KEY = 'jsini_right_drawer_width';

const FLOATING_SELECTOR = [
    'dx-dynamic-container',
    'dxbl-dropdown-root',
    'dxbl-branch',
    'dxbl-flyout-root',
    'dxbl-window-root',
    'dxbl-dropdown',
    'dxbl-popup',
    'dxbl-modal',
    '.dxbl-dropdown',
    '.dxbl-dropdown-dialog',
    '.dxbl-dropdown-root',
    '.dxbl-dropdown-listbox',
    '.dxbl-dropdownbase',
    '.dxbl-itemlist-dropdown',
    '.dxbl-edit-dropdown',
    '.dxbl-adaptive-dropdown',
    '.dxbl-adaptive-container',
    '.dxbl-popup',
    '.dxbl-popup-root',
    '.dxbl-popup-portal',
    '.dxbl-modal',
    '.dxbl-modal-root',
    '.dxbl-modal-dialog',
    '.dxbl-listbox',
    '.dxbl-list-box',
    '.dxbl-listbox-item',
    '.dxbl-list-box-item',
    '.dxbl-calendar'
].join(',');

function isDesktopViewport() {
    return typeof window !== 'undefined' && window.innerWidth >= 768;
}

function saveWidthToStorage(id, width) {
    try {
        if (id) {
            localStorage.setItem(STORAGE_KEY_PREFIX + id, String(width));
        }
        localStorage.setItem('jsini_drawer_width_jsini-quick-ask-drawer', String(width));
        localStorage.setItem(GLOBAL_STORAGE_KEY, String(width));
    } catch (_) {}
}

function getSavedWidth(id, minW = MIN_WIDTH, maxW = MAX_WIDTH, fallbackWidth = null) {
    let val = null;
    try {
        if (id) {
            const specific = localStorage.getItem(STORAGE_KEY_PREFIX + id);
            if (specific) val = Number(specific);
        }
        if (!val || isNaN(val)) {
            const global = localStorage.getItem(GLOBAL_STORAGE_KEY);
            if (global) val = Number(global);
        }
        if (!val || isNaN(val)) {
            const quickAsk = localStorage.getItem('jsini_drawer_width_jsini-quick-ask-drawer');
            if (quickAsk) val = Number(quickAsk);
        }
    } catch (_) {}
    if ((!val || isNaN(val)) && typeof fallbackWidth === 'number' && fallbackWidth > 0) {
        val = fallbackWidth;
    }
    if (typeof val === 'number' && !isNaN(val) && val > 0) {
        return Math.max(minW, Math.min(val, maxW));
    }
    return DEFAULT_WIDTH;
}

function applyDrawerWidth(el, width, updateCssVar = true) {
    if (updateCssVar) {
        document.documentElement.style.setProperty('--jsini-drawer-width', width + 'px');
    }
    if (!el) return;
    if (isDesktopViewport()) {
        el.style.width = width + 'px';
    } else {
        el.style.removeProperty('width');
    }
}

function syncAllDrawersWidth(width) {
    document.documentElement.style.setProperty('--jsini-drawer-width', width + 'px');
    for (const [drawerId, st] of drawers.entries()) {
        st.width = width;
        const dEl = document.getElementById(drawerId);
        if (dEl) {
            applyDrawerWidth(dEl, width, false);
        }
    }
}

function notifyDotNetWidthChanged(id, width) {
    const state = drawers.get(id);
    if (state && state.dotnet) {
        state.dotnet.invokeMethodAsync('OnWidthChanged', Math.round(width)).catch(() => {});
    }
}

function attachResizer(id, drawerEl, minWidth = MIN_WIDTH, maxWidth = MAX_WIDTH) {
    if (!drawerEl) return;
    const resizer = drawerEl.querySelector('.jsini-right-drawer__resizer');
    if (!resizer) return;
    if (resizer._attached) return;
    resizer._attached = true;

    resizer.addEventListener('mousedown', function (e) {
        if (e.button !== 0) return;
        e.preventDefault();
        e.stopPropagation();

        isResizing = true;
        document.body.classList.add('jsini-drawer-resizing');
        drawerEl.classList.add('jsini-right-drawer--resizing');

        const effectiveMin = minWidth || MIN_WIDTH;
        const effectiveMax = maxWidth || Math.max(effectiveMin, Math.floor(window.innerWidth * 0.85));
        let lastClamped = getSavedWidth(id, effectiveMin, effectiveMax);

        function onMouseMove(moveEvent) {
            if (!isResizing) return;
            moveEvent.preventDefault();

            // 서랍은 화면 오른쪽에 고정되어 있으므로
            // window.innerWidth - 마우스 X좌표가 곧 서랍의 너비가 된다.
            const rawWidth = window.innerWidth - moveEvent.clientX;
            const maxW = Math.min(effectiveMax, Math.max(effectiveMin, Math.floor(window.innerWidth * 0.9)));
            const clamped = Math.round(Math.max(effectiveMin, Math.min(rawWidth, maxW)));
            lastClamped = clamped;

            syncAllDrawersWidth(clamped);
            saveWidthToStorage(id, clamped);
        }

        function onMouseUp() {
            if (!isResizing) return;
            isResizing = false;

            document.body.classList.remove('jsini-drawer-resizing');
            drawerEl.classList.remove('jsini-right-drawer--resizing');

            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);

            const measured = drawerEl.offsetWidth;
            const finalWidth = Math.round(
                measured >= effectiveMin ? Math.min(measured, effectiveMax) : lastClamped
            );

            syncAllDrawersWidth(finalWidth);
            saveWidthToStorage(id, finalWidth);
            notifyDotNetWidthChanged(id, finalWidth);
        }

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
    });

    // 더블클릭 시 기본 크기로 복원
    resizer.addEventListener('dblclick', function (e) {
        e.preventDefault();
        e.stopPropagation();
        syncAllDrawersWidth(DEFAULT_WIDTH);
        saveWidthToStorage(id, DEFAULT_WIDTH);
        notifyDotNetWidthChanged(id, DEFAULT_WIDTH);
    });
}

function isInsideOrFloating(el, e) {
    if (!el) return false;
    if (isResizing) return true;

    // 판 안쪽을 누른 것은 바깥 클릭이 아니다.
    if (e.target && el.contains(e.target)) {
        return true;
    }

    // Shadow DOM 등을 거친 경우
    const path = typeof e.composedPath === 'function' ? e.composedPath() : [];
    if (path.includes(el)) {
        return true;
    }

    // DevExpress 동적 팝업·드롭다운(DxComboBox 목록 등) 내부를 누른 경우
    const targetEl = e.target && e.target.nodeType === Node.ELEMENT_NODE
        ? e.target
        : e.target?.parentElement;

    if (targetEl) {
        if (targetEl.closest(FLOATING_SELECTOR)) {
            return true;
        }
        // 서랍 리사이즈 핸들 자체를 누른 경우
        if (targetEl.closest('.jsini-right-drawer__resizer')) {
            return true;
        }
        // 서랍 여닫이 단추 자체(헤더 AI 아이콘, 빠른 지시 등)를 누른 경우
        if (targetEl.closest('.jsini-icon-robot, .jsini-icon-bell, .jsini-icon-bolt')) {
            return true;
        }
    }

    for (const node of path) {
        if (node && node.nodeType === Node.ELEMENT_NODE) {
            if (node.matches && node.matches(FLOATING_SELECTOR)) {
                return true;
            }
        }
    }

    return false;
}

function clickHandler(e) {
    if (isResizing) return;

    for (const [id, state] of drawers.entries()) {
        if (!state.isOpen || state.isPinned) {
            continue;
        }

        const el = document.getElementById(id);
        if (isInsideOrFloating(el, e)) {
            continue;
        }

        // 회로가 이미 끊겼으면 조용히 넘어간다 — 이 판들은 모든 화면에 늘
        // 실려 있어서, 여기서 예외가 새면 창을 닫는 것만으로 오류가 난다.
        state.dotnet.invokeMethodAsync('CloseFromOutside').catch(() => {});
    }
}

document.addEventListener('mousedown', clickHandler);
document.addEventListener('touchstart', clickHandler, { passive: true });

export function init(id, dotnet, minWidth = MIN_WIDTH, maxWidth = MAX_WIDTH, initialWidth = null, instanceId = null) {
    const savedWidth = getSavedWidth(id, minWidth, maxWidth, initialWidth);
    if (typeof initialWidth === 'number' && initialWidth > 0) {
        saveWidthToStorage(id, savedWidth);
    }

    drawers.set(id, {
        dotnet: dotnet,
        instanceId: instanceId,
        isOpen: false,
        isPinned: false,
        width: savedWidth,
        minWidth: minWidth,
        maxWidth: maxWidth
    });

    const el = document.getElementById(id);
    if (el) {
        applyDrawerWidth(el, savedWidth, true);
        attachResizer(id, el, minWidth, maxWidth);
    } else {
        document.documentElement.style.setProperty('--jsini-drawer-width', savedWidth + 'px');
    }
}

export function updateState(id, isOpen, isPinned, preferredWidth = null) {
    const state = drawers.get(id);
    const el = document.getElementById(id);

    if (el && state) {
        attachResizer(id, el, state.minWidth, state.maxWidth);
    }

    if (state) {
        state.isOpen = isOpen;
        state.isPinned = isPinned;

        const savedWidth = getSavedWidth(
            id,
            state.minWidth,
            state.maxWidth,
            preferredWidth || state.width
        );
        state.width = savedWidth;

        if (el) {
            applyDrawerWidth(el, savedWidth, isOpen);
        }
    }
    updateBodyClass();
}

function updateBodyClass() {
    let hasPinned = false;
    let pinnedWidth = DEFAULT_WIDTH;

    for (const [id, state] of drawers.entries()) {
        if (state.isOpen && state.isPinned) {
            hasPinned = true;
            pinnedWidth = getSavedWidth(id, state.minWidth, state.maxWidth, state.width);
            break;
        }
    }

    if (hasPinned) {
        document.documentElement.style.setProperty('--jsini-drawer-width', pinnedWidth + 'px');
    }
    document.body.classList.toggle('jsini-has-pinned-drawer', hasPinned);
}

export function dispose(id, instanceId = null) {
    const state = drawers.get(id);
    if (state && instanceId && state.instanceId && state.instanceId !== instanceId) {
        return;
    }
    drawers.delete(id);
    updateBodyClass();
}
