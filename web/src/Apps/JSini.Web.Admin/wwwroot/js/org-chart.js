/*
    조직도 캔버스의 화면 이동·확대.

    ────────────────────────────────────────────────────────────
    [왜 이 일만 JS 인가]

    노드 배치·연결선·드래그 이동은 전부 Blazor(C#)가 한다. 여기 있는 것은
    **끌어서 움직이고 휠로 키우는 것** 하나뿐이다.

    그 하나를 회로로 넘기면 마우스를 한 번 움직일 때마다 서버로 갔다 온다.
    Blazor Server 라서 그 왕복이 곧 렉이고, 조직도는 사람이 계속 끌어 보는
    화면이라 제일 티가 난다. 화면 변환은 브라우저 안에서 끝낸다 —
    C# 은 「맞춰라」(fit) 만 시킨다.

    ────────────────────────────────────────────────────────────
    [노드 위에서 시작한 끌기는 건드리지 않는다]

    노드 카드는 HTML5 드래그로 **부서를 옮기는** 손잡이다. 캔버스가 그 끌기를
    가로채면 부서를 옮기려는데 화면만 움직인다. 그래서 `.ad-orgc__node` 와
    `.no-pan` 위에서 시작한 것은 캔버스가 보지 않는다.

    ────────────────────────────────────────────────────────────
    [손가락도 같은 길로 받는다]

    터치 브라우저는 끌기에서 mousemove 를 만들어 주지 않는다. 포인터 이벤트로
    받으면 마우스·손가락·펜이 한 벌로 처리된다. 두 손가락이면 확대다.
    캔버스에 `touch-action: none` 이 걸려 있어야 브라우저의 쪽 스크롤과
    겹치지 않는다(admin.css).
*/

const MIN_ZOOM = 0.3;
const MAX_ZOOM = 2.5;

/** 캔버스 가장자리에 두는 여백. 「화면 맞춤」이 이만큼 띄운다. */
const PAD = 32;

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

/**
 * 캔버스를 붙인다.
 *
 * @param viewport 잘라 내는 바깥 상자 (스크롤 없음)
 * @param stage    안에서 움직이는 판. 크기는 Blazor 가 인라인 스타일로 정해 준다.
 */
export function create(viewport, stage) {
    const state = { zoom: 1, x: 0, y: 0 };

    /** 지금 눌려 있는 포인터들. 둘이면 확대다. */
    const pointers = new Map();

    let panFrom = null;
    let pinchFrom = null;

    function apply() {
        stage.style.transformOrigin = '0 0';
        stage.style.transform =
            `translate(${state.x}px, ${state.y}px) scale(${state.zoom})`;
    }

    /** 화면의 한 점을 붙잡은 채로 배율을 바꾼다. 커서 아래 것이 제자리에 남는다. */
    function zoomAt(nextZoom, screenX, screenY) {
        const next = clamp(nextZoom, MIN_ZOOM, MAX_ZOOM);
        if (next === state.zoom) return;

        const rect = viewport.getBoundingClientRect();
        const cx = screenX - rect.left;
        const cy = screenY - rect.top;

        state.x = cx - (cx - state.x) * (next / state.zoom);
        state.y = cy - (cy - state.y) * (next / state.zoom);
        state.zoom = next;
        apply();
    }

    function onWheel(e) {
        e.preventDefault();
        zoomAt(state.zoom * (e.deltaY < 0 ? 1.1 : 1 / 1.1), e.clientX, e.clientY);
    }

    function onPointerDown(e) {
        // 노드 카드와 조작 부품은 자기 몫이 있다. 캔버스가 가로채지 않는다.
        if (e.target.closest?.('.ad-orgc__node, .no-pan')) return;

        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        viewport.setPointerCapture?.(e.pointerId);

        if (pointers.size === 1) {
            panFrom = { x: e.clientX - state.x, y: e.clientY - state.y };
            pinchFrom = null;
            viewport.classList.add('ad-orgc--grabbing');
        } else if (pointers.size === 2) {
            panFrom = null;
            pinchFrom = { dist: pointerDistance(), zoom: state.zoom };
        }
    }

    function onPointerMove(e) {
        if (!pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

        if (pointers.size === 1 && panFrom) {
            state.x = e.clientX - panFrom.x;
            state.y = e.clientY - panFrom.y;
            apply();
            return;
        }

        if (pointers.size === 2 && pinchFrom && pinchFrom.dist > 0) {
            const mid = pointerMidpoint();
            zoomAt(pinchFrom.zoom * (pointerDistance() / pinchFrom.dist), mid.x, mid.y);
        }
    }

    function onPointerUp(e) {
        pointers.delete(e.pointerId);
        viewport.releasePointerCapture?.(e.pointerId);

        if (pointers.size === 0) {
            panFrom = null;
            pinchFrom = null;
            viewport.classList.remove('ad-orgc--grabbing');
        } else if (pointers.size === 1) {
            // 손가락 하나를 떼면 남은 하나로 이어서 끈다.
            const [only] = [...pointers.values()];
            panFrom = { x: only.x - state.x, y: only.y - state.y };
            pinchFrom = null;
        }
    }

    function pointerDistance() {
        const [a, b] = [...pointers.values()];
        return Math.hypot(a.x - b.x, a.y - b.y);
    }

    function pointerMidpoint() {
        const [a, b] = [...pointers.values()];
        return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    }

    viewport.addEventListener('wheel', onWheel, { passive: false });
    viewport.addEventListener('pointerdown', onPointerDown);
    viewport.addEventListener('pointermove', onPointerMove);
    viewport.addEventListener('pointerup', onPointerUp);
    viewport.addEventListener('pointercancel', onPointerUp);

    apply();

    /*
        [맞춤은 「해 달라」고 적어 두고 자리가 잡히면 한다]

        화면이 열리자마자 맞추면 **캔버스가 아직 제 크기가 아니다.** 실제로
        1230px 자리를 324px 로 재서 0.3 배까지 줄여 놓았다 — 자료도 배치도
        멀쩡한데 그림만 깨알같이 나오는, 원인을 찾기 나쁜 증상이다.

        그래서 요청을 적어 두고(`wantFit`) 다음 그림틀에서 시도하되, 자리가
        아직 없으면 그대로 남긴다. 캔버스 크기가 잡히는 순간 ResizeObserver 가
        같은 요청을 다시 실행한다. 맞추고 나면 요청을 지우므로, 사용자가 손으로
        확대해 둔 것을 창 크기가 바뀔 때마다 되돌리지는 않는다.
    */
    let wantFit = null;

    function runFit() {
        if (!wantFit) return;

        const vw = viewport.clientWidth;
        const vh = viewport.clientHeight;

        // 아직 자리가 없다. 요청은 남겨 둔다.
        if (vw < 40 || vh < 40) return;

        const cw = wantFit.width || stage.offsetWidth || 1;
        const ch = wantFit.height || stage.offsetHeight || 1;

        state.zoom = clamp(
            Math.min(1, (vw - PAD * 2) / cw, (vh - PAD * 2) / ch),
            MIN_ZOOM,
            MAX_ZOOM);

        const w = cw * state.zoom;
        const h = ch * state.zoom;

        // 다 들어가면 가운데, 넘치면 왼쪽 위부터 — 넘치는 것을 가운데 두면
        // 뿌리(회사)가 화면 밖으로 밀려난다.
        state.x = w <= vw ? (vw - w) / 2 : PAD;
        state.y = h <= vh ? (vh - h) / 2 : PAD;
        wantFit = null;
        apply();
    }

    const resizes = new ResizeObserver(runFit);
    resizes.observe(viewport);

    return {
        /**
         * 전체가 보이게 맞춘다.
         *
         * 원본(Vue)은 배율을 1 로 되돌리고 뿌리를 가운데 두었는데, 부서가 열몇
         * 개만 되어도 1 배로는 화면 밖으로 나간다 — 「화면 맞춤」을 누르고도
         * 다시 끌어야 했다. 여기서는 들어갈 만큼 줄인다(키우지는 않는다).
         *
         * 크기는 C# 이 알고 있는 값을 받는다. 판을 재려 들면 방금 그린 것인지
         * 이전 것인지 알 수 없다(측정 시점이 렌더와 엇갈린다).
         */
        fit(contentWidth, contentHeight) {
            wantFit = { width: contentWidth, height: contentHeight };

            // 지금 자리가 이미 잡혀 있으면 여기서 끝난다. 안 잡혔으면 다음
            // 그림틀에서 다시 본다 — **숨은 탭에서는 그 틀이 오지 않으므로**
            // (브라우저가 rAF 를 멈춘다) 즉시 한 번 해 보는 이 줄이 필요하다.
            runFit();
            requestAnimationFrame(runFit);
        },

        /** 가운데를 잡고 키운다. 손가락 확대가 어려운 자리를 위한 단추용. */
        zoomIn() {
            const rect = viewport.getBoundingClientRect();
            zoomAt(state.zoom * 1.2, rect.left + rect.width / 2, rect.top + rect.height / 2);
        },

        zoomOut() {
            const rect = viewport.getBoundingClientRect();
            zoomAt(state.zoom / 1.2, rect.left + rect.width / 2, rect.top + rect.height / 2);
        },

        dispose() {
            resizes.disconnect();
            viewport.removeEventListener('wheel', onWheel);
            viewport.removeEventListener('pointerdown', onPointerDown);
            viewport.removeEventListener('pointermove', onPointerMove);
            viewport.removeEventListener('pointerup', onPointerUp);
            viewport.removeEventListener('pointercancel', onPointerUp);
        },
    };
}
