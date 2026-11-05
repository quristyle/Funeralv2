/*
    조직도 캔버스의 화면 이동·확대.

    ────────────────────────────────────────────────────────────
    [왜 이 일만 JS 인가]

    노드 배치·연결선·저장은 전부 Blazor(C#)가 한다. 여기 있는 것은
    **끌어서 움직이고 휠로 키우는 것**과 **카드를 끌어 다른 카드에 놓는 손짓**이다.

    그 하나를 회로로 넘기면 마우스를 한 번 움직일 때마다 서버로 갔다 온다.
    Blazor Server 라서 그 왕복이 곧 렉이고, 조직도는 사람이 계속 끌어 보는
    화면이라 제일 티가 난다. 화면 변환은 브라우저 안에서 끝낸다 —
    C# 은 「맞춰라」(fit) 만 시킨다.

    ────────────────────────────────────────────────────────────
    [노드 위에서 시작한 끌기는 건드리지 않는다]

    노드 카드는 **사람·부서를 옮기는** 손잡이다. 캔버스가 그 끌기를
    가로채면 옮기려는데 화면만 움직인다. 그래서 `.ad-orgc__node` 와
    `.no-pan` 위에서 시작한 것은 캔버스가 보지 않는다.

    ────────────────────────────────────────────────────────────
    [카드 옮기기는 HTML5 드래그가 아니라 포인터 이벤트다]

    `draggable` 은 파이어폭스·사파리가 단추가 든 카드에서 끌기를 시작하지 않고,
    손가락으로는 아예 안 도는 브라우저가 많다 — 크롬에서만 되어 「안 된다」가
    재현되지 않는다. 끄는 것은 `data-drag`(kind:id), 놓는 자리는 `data-drop`
    (C# 이 고른 노드 식별자)이 말하고, 놓은 순간에만 `DropFromJs` 로 회로에 간다.
    손가락은 옆 목록이 세로로 구르므로 0.35초 누르고 있어야 집힌다.

    ────────────────────────────────────────────────────────────
    [손가락도 같은 길로 받는다]

    터치 브라우저는 끌기에서 mousemove 를 만들어 주지 않는다. 포인터 이벤트로
    받으면 마우스·손가락·펜이 한 벌로 처리된다. 두 손가락이면 확대다.
    캔버스에 `touch-action: none` 이 걸려 있어야 브라우저의 쪽 스크롤과
    겹치지 않는다(admin.css).
*/

const MIN_ZOOM = 0.3;
const MAX_ZOOM = 2.5;

/* * 캔버스 가장자리에 두는 여백. 「화면 맞춤」이 이만큼 띄운다. */
const PAD = 32;

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

/* 캔버스를 붙인다. @param viewport 잘라 내는 바깥 상자 (스크롤 없음) @param stage 안에서 움직이는 판. 크기는 Blazor 가 인라인 스타일로
   정해 준다. */
export function create(viewport, stage, root, dotnet) {
    const state = { zoom: 1, x: 0, y: 0 };

    /* * 지금 눌려 있는 포인터들. 둘이면 확대다. */
    const pointers = new Map();

    let panFrom = null;
    let pinchFrom = null;

    function apply() {
        stage.style.transformOrigin = '0 0';
        stage.style.transform =
            `translate(${state.x}px, ${state.y}px) scale(${state.zoom})`;
    }

    /* * 화면의 한 점을 붙잡은 채로 배율을 바꾼다. 커서 아래 것이 제자리에 남는다. */
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

    // ── 카드 끌어 놓기 ──────────────────────────────────────

    const DRAG_SLOP = 5;
    const TOUCH_HOLD_MS = 350;

    /* * 눌렀지만 아직 끌기로 판정하지 않은 것. */
    let press = null;

    /* * 끌고 있는 것. */
    let drag = null;

    function clearPress() {
        if (press?.timer) clearTimeout(press.timer);
        press = null;
    }

    function onDragDown(e) {
        if (e.button !== 0 && e.pointerType === 'mouse') return;
        if (e.target.closest?.('button, a, input, .ad-orgc__btn')) return;

        const source = e.target.closest?.('[data-drag]');
        if (!source || !root.contains(source)) return;

        clearPress();
        press = { el: source, id: e.pointerId, x: e.clientX, y: e.clientY, ready: e.pointerType !== 'touch' };

        if (!press.ready) {
            const pending = press;
            pending.timer = setTimeout(() => {
                if (press === pending) beginDrag(pending.x, pending.y);
            }, TOUCH_HOLD_MS);
        }
    }

    function beginDrag(x, y) {
        const source = press.el;
        const rect = source.getBoundingClientRect();
        const ghost = source.cloneNode(true);

        ghost.classList.add('ad-orgc__ghost');
        ghost.removeAttribute('data-drag');
        ghost.removeAttribute('data-drop');
        ghost.style.width = `${rect.width}px`;
        ghost.style.height = `${rect.height}px`;
        document.body.appendChild(ghost);

        drag = {
            source,
            pointerId: press.id,
            key: source.getAttribute('data-drag'),
            dx: x - rect.left,
            dy: y - rect.top,
            ghost,
            over: null,
        };

        clearPress();
        source.classList.add('ad-orgc--dragging');
        root.classList.add('ad-orgc-wrap--dragging');
        moveDrag(x, y);
    }

    function targetAt(x, y) {
        const hit = document.elementFromPoint(x, y)?.closest?.('[data-drop]');
        if (!hit || !root.contains(hit) || hit === drag.source) return null;
        return hit;
    }

    function moveDrag(x, y) {
        drag.ghost.style.transform = `translate(${x - drag.dx}px, ${y - drag.dy}px)`;

        const over = targetAt(x, y);
        if (over === drag.over) return;

        drag.over?.classList.remove('ad-orgc__node--over');
        over?.classList.add('ad-orgc__node--over');
        drag.over = over;
    }

    function endDrag(commit) {
        const done = drag;
        drag = null;

        done.ghost.remove();
        done.source.classList.remove('ad-orgc--dragging');
        done.over?.classList.remove('ad-orgc__node--over');
        root.classList.remove('ad-orgc-wrap--dragging');

        // 놓은 직후 브라우저가 쏘는 click 이 카드의 단추(얼굴 카드 등)를 열지 않게 한 번 삼킨다.
        const swallow = (ev) => { ev.stopPropagation(); ev.preventDefault(); };
        window.addEventListener('click', swallow, { capture: true, once: true });
        setTimeout(() => window.removeEventListener('click', swallow, { capture: true }), 0);

        if (!commit || !done.over) return;

        const sep = done.key.indexOf(':');
        dotnet?.invokeMethodAsync(
            'DropFromJs',
            done.key.slice(0, sep),
            done.key.slice(sep + 1),
            done.over.getAttribute('data-drop'));
    }

    function onDragMove(e) {
        if (drag && e.pointerId === drag.pointerId) {
            e.preventDefault();
            moveDrag(e.clientX, e.clientY);
            return;
        }

        if (!press || e.pointerId !== press.id) return;

        const far = Math.hypot(e.clientX - press.x, e.clientY - press.y) > DRAG_SLOP;
        if (!far) return;

        if (press.ready) {
            beginDrag(e.clientX, e.clientY);
        } else {
            // 손가락이 기다리기 전에 움직였다 — 목록을 굴리려는 것이다.
            clearPress();
        }
    }

    function onDragUp(e) {
        if (drag && e.pointerId === drag.pointerId) {
            endDrag(e.type === 'pointerup');
            return;
        }

        if (press && e.pointerId === press.id) clearPress();
    }

    /* * 끄는 동안 손가락 굴림을 막는다. 옆 목록은 `pan-y` 라 막지 않으면 브라우저가 집어 간다. */
    function onTouchMove(e) {
        if (drag) e.preventDefault();
    }

    /* * 이미지·글자를 끌면 브라우저가 제 HTML5 끌기를 시작해 포인터 이벤트가 끊긴다. */
    function onNativeDrag(e) {
        if (e.target.closest?.('[data-drag]')) e.preventDefault();
    }

    function onKey(e) {
        if (e.key === 'Escape' && drag) endDrag(false);
    }

    root.addEventListener('pointerdown', onDragDown);
    window.addEventListener('pointermove', onDragMove, { passive: false });
    window.addEventListener('pointerup', onDragUp);
    window.addEventListener('pointercancel', onDragUp);
    root.addEventListener('touchmove', onTouchMove, { passive: false });
    root.addEventListener('dragstart', onNativeDrag);
    window.addEventListener('keydown', onKey);

    viewport.addEventListener('wheel', onWheel, { passive: false });
    viewport.addEventListener('pointerdown', onPointerDown);
    viewport.addEventListener('pointermove', onPointerMove);
    viewport.addEventListener('pointerup', onPointerUp);
    viewport.addEventListener('pointercancel', onPointerUp);

    apply();

    /* — 자료도 배치도 멀쩡한데 그림만 깨알같이 나오는, 원인을 찾기 나쁜 증상이다. 그래서 요청을 적어 두고(`wantFit`) 다음 그림틀에서 시도하되, 자리가 아직 없으면
       그대로 남긴다. 캔버스 크기가 잡히는 순간 ResizeObserver 가 같은 요청을 다시 실행한다. */
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

        // 다 들어가면 가운데, 넘치면 왼쪽 위부터 — 넘치는 것을 가운데 두면 뿌리(회사)가 화면 밖으로 밀려난다.
        state.x = w <= vw ? (vw - w) / 2 : PAD;
        state.y = h <= vh ? (vh - h) / 2 : PAD;
        wantFit = null;
        apply();
    }

    const resizes = new ResizeObserver(runFit);
    resizes.observe(viewport);

    return {
        /* 전체가 보이게 맞춘다. 원본(Vue)은 배율을 1 로 되돌리고 뿌리를 가운데 두었는데, 부서가 열몇 개만 되어도 1 배로는 화면 밖으로 나간다 — 「화면 맞춤」을 누르고도
           다시 끌어야 했다. 여기서는 들어갈 만큼 줄인다(키우지는 않는다). 크기는 C# 이 알고 있는 값을 받는다. */
        fit(contentWidth, contentHeight) {
            wantFit = { width: contentWidth, height: contentHeight };

            // 지금 자리가 이미 잡혀 있으면 여기서 끝난다. 안 잡혔으면 다음 그림틀에서 다시 본다 — **숨은 탭에서는 그 틀이 오지 않으므로** (브라우저가 rAF 를 멈춘다) 즉시 한 번
            // 해 보는 이 줄이 필요하다.
            runFit();
            requestAnimationFrame(runFit);
        },

        /* * 가운데를 잡고 키운다. 손가락 확대가 어려운 자리를 위한 단추용. */
        zoomIn() {
            const rect = viewport.getBoundingClientRect();
            zoomAt(state.zoom * 1.2, rect.left + rect.width / 2, rect.top + rect.height / 2);
        },

        zoomOut() {
            const rect = viewport.getBoundingClientRect();
            zoomAt(state.zoom / 1.2, rect.left + rect.width / 2, rect.top + rect.height / 2);
        },

        dispose() {
            if (drag) endDrag(false);
            clearPress();
            root.removeEventListener('pointerdown', onDragDown);
            window.removeEventListener('pointermove', onDragMove);
            window.removeEventListener('pointerup', onDragUp);
            window.removeEventListener('pointercancel', onDragUp);
            root.removeEventListener('touchmove', onTouchMove);
            root.removeEventListener('dragstart', onNativeDrag);
            window.removeEventListener('keydown', onKey);
            resizes.disconnect();
            viewport.removeEventListener('wheel', onWheel);
            viewport.removeEventListener('pointerdown', onPointerDown);
            viewport.removeEventListener('pointermove', onPointerMove);
            viewport.removeEventListener('pointerup', onPointerUp);
            viewport.removeEventListener('pointercancel', onPointerUp);
        },
    };
}
