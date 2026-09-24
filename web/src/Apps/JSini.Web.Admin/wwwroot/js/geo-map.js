/*
    여러 점을 한 지도에 찍는 판. 「위치 지도」(/admin/location/map) 전용이다.

    ────────────────────────────────────────────────────────────
    [왜 `LocationMap` 부품을 못 쓰나]

    설정 화면의 지도(`JSini.Web.Components/Settings/LocationMap.razor`)는
    OpenStreetMap 의 **끼워 넣기 판**(`export/embed.html`)을 iframe 으로 건다.
    그 판은 `marker=` 를 **하나만** 받는다 — 점이 둘 이상이면 표현할 방법이
    아예 없고, iframe 안쪽은 우리 문서가 아니라 **점을 눌렀는지 알 길도 없다.**
    이 화면은 점을 눌러 곧바로 쪽지를 보내는 것이 요점이라 둘 다 치명적이다.

    ────────────────────────────────────────────────────────────
    [그렇다고 지도 라이브러리를 들이지도 않았다]

    Leaflet·카카오·네이버는 각각 값이 붙는다 — 열쇠(API 키)를 배포에 실어야
    하거나, CDN 한 곳이 더 막히면 화면이 통째로 죽거나, 번들이 늘어난다.
    여기서 필요한 것은 **타일을 깔고 · 끌고 · 확대하고 · 점을 누르는 것** 넷뿐이고,
    그 넷은 아래 200줄이다. 타일은 이미 쓰고 있는 openstreetmap.org 에서 온다.

    ────────────────────────────────────────────────────────────
    [좌표를 픽셀로 옮기는 셈은 「웹 메르카토르」 하나다]

    확대 단계 z 에서 세상은 256 × 2^z 픽셀짜리 정사각형이다. 그 안의 자리만
    알면 타일도 점도 같은 셈으로 놓인다 — 그래서 상태는 **가운데의 세상
    픽셀 좌표(cx, cy)와 z** 셋뿐이다. 화면 크기가 바뀌어도 이 셋은 그대로고,
    다시 그리기만 하면 된다.

    ────────────────────────────────────────────────────────────
    [같은 자리에 선 사람들을 겹쳐 두지 않는다]

    한 사무실에 앉은 사람들은 좌표가 소수점 넷째 자리까지 같다. 그대로 찍으면
    **맨 위 한 사람만 누를 수 있고** 나머지는 그 아래 영원히 묻힌다. 겹치는
    점은 작은 원으로 벌려 놓는다(`spread`) — 자리가 조금 틀어지지만, 못 누르는
    것보다는 낫다. 벌린 것은 색이 아니라 **줄을 그어** 원래 자리를 가리킨다.
*/

/** 타일 한 장의 변. OSM 표준이다. */
const TILE = 256;

const MIN_ZOOM = 3;
const MAX_ZOOM = 18;

/** 점이 하나뿐일 때 쓰는 확대 단계 — 동네 이름이 읽히는 정도. */
const SINGLE_ZOOM = 15;

/** 「맞춤」이 가장자리에 두는 여백(픽셀). */
const PAD = 48;

/**
 * 겹친 점을 벌리는 반지름(픽셀). 점 지름이 22px 이라 그보다 커야 서로 안 문다.
 */
const SPREAD_RADIUS = 18;

function clamp(v, lo, hi) {
    return Math.max(lo, Math.min(hi, v));
}

/** 경도 → 세상 픽셀 x. */
function lonToX(lon, worldSize) {
    return (lon + 180) / 360 * worldSize;
}

/** 위도 → 세상 픽셀 y (웹 메르카토르). 극지방은 ±85.05 도에서 잘린다. */
function latToY(lat, worldSize) {
    const rad = clamp(lat, -85.05112878, 85.05112878) * Math.PI / 180;
    return (1 - Math.log(Math.tan(rad) + 1 / Math.cos(rad)) / Math.PI) / 2 * worldSize;
}

/**
 * 지도를 붙인다.
 *
 * @param host   타일과 점이 들어갈 상자. `position: relative; overflow: hidden` 이어야 한다.
 * @param dotnet 점을 눌렀을 때 부를 .NET 쪽 손잡이 (`PickMarker(key)`).
 */
export function create(host, dotnet) {
    let zoom = 11;

    // 가운데의 세상 픽셀 좌표. 처음에는 대한민국 가운데쯤을 본다 —
    // 점이 오면 `fit` 이 곧바로 옮긴다.
    let worldSize = TILE * Math.pow(2, zoom);
    let cx = lonToX(127.8, worldSize);
    let cy = latToY(36.3, worldSize);

    /** 찍을 점들. `{ key, lat, lon, name, muted }` */
    let markers = [];

    /** 지금 고른 점의 열쇠. 목록에서 고른 것도 여기로 온다. */
    let picked = null;

    const tiles = document.createElement('div');
    tiles.className = 'ad-geomap__tiles';
    host.appendChild(tiles);

    const pins = document.createElement('div');
    pins.className = 'ad-geomap__pins';
    host.appendChild(pins);

    /*
        타일 <img> 를 열쇠(z/x/y)로 들고 있는다. 매번 새로 만들면 끌 때마다
        같은 그림을 다시 붙이게 되고, 브라우저 캐시가 받쳐 주더라도 **한 번은
        빈 칸이 보인다** — 지도가 깜빡이는 것으로 나타난다.
    */
    const tileCache = new Map();

    function render() {
        const w = host.clientWidth;
        const h = host.clientHeight;

        // 아직 자리가 없다. ResizeObserver 가 곧 다시 부른다.
        if (w < 20 || h < 20) return;

        worldSize = TILE * Math.pow(2, zoom);

        // 세로는 세상 밖으로 못 나간다 — 나가면 회색 띠만 보인다.
        cy = clamp(cy, h / 2, Math.max(h / 2, worldSize - h / 2));

        const left = cx - w / 2;
        const top = cy - h / 2;

        const count = Math.pow(2, zoom);
        const x0 = Math.floor(left / TILE);
        const x1 = Math.floor((left + w) / TILE);
        const y0 = Math.max(0, Math.floor(top / TILE));
        const y1 = Math.min(count - 1, Math.floor((top + h) / TILE));

        const alive = new Set();

        for (let ty = y0; ty <= y1; ty++) {
            for (let tx = x0; tx <= x1; tx++) {
                // 동서로는 세상이 이어진다. 음수도 `count` 넘는 값도 감아 준다.
                const wrapped = ((tx % count) + count) % count;
                const key = `${zoom}/${wrapped}/${ty}`;

                let img = tileCache.get(key);

                if (!img) {
                    img = document.createElement('img');
                    img.className = 'ad-geomap__tile';
                    img.loading = 'lazy';
                    img.alt = '';
                    img.src = `https://tile.openstreetmap.org/${zoom}/${wrapped}/${ty}.png`;
                    tileCache.set(key, img);
                    tiles.appendChild(img);
                }

                // 같은 타일이 화면 양쪽에 겹쳐 보일 수는 없다(한 장에 한 자리).
                img.style.transform =
                    `translate(${tx * TILE - left}px, ${ty * TILE - top}px)`;

                alive.add(key);
            }
        }

        // 화면 밖으로 나간 타일은 떼어 낸다. 남겨 두면 크게 끌어 본 만큼
        // <img> 가 쌓여 수천 장이 된다.
        for (const [key, img] of tileCache) {
            if (!alive.has(key)) {
                img.remove();
                tileCache.delete(key);
            }
        }

        renderPins(left, top);
    }

    /**
     * 점을 놓는다. 좌표가 같은 것들은 작은 원으로 벌린다.
     */
    function renderPins(left, top) {
        pins.textContent = '';

        // 소수점 다섯째 자리(약 1m)가 같으면 같은 자리로 본다.
        const groups = new Map();

        for (const m of markers) {
            const key = `${m.lat.toFixed(5)},${m.lon.toFixed(5)}`;
            const group = groups.get(key);
            if (group) group.push(m);
            else groups.set(key, [m]);
        }

        for (const group of groups.values()) {
            const baseX = lonToX(group[0].lon, worldSize) - left;
            const baseY = latToY(group[0].lat, worldSize) - top;

            group.forEach((m, i) => {
                let dx = 0;
                let dy = 0;

                if (group.length > 1) {
                    const angle = (2 * Math.PI * i) / group.length;
                    // 여럿이면 원이 커야 서로 안 문다.
                    const r = SPREAD_RADIUS * Math.max(1, group.length / 6);
                    dx = Math.cos(angle) * r;
                    dy = Math.sin(angle) * r;

                    // 벌린 점은 원래 자리로 선을 긋는다 — 안 그으면 사람이
                    // 실제로 서 있지 않은 곳에 점이 있는 것으로 읽힌다.
                    const link = document.createElement('div');
                    link.className = 'ad-geomap__link';
                    link.style.left = `${baseX}px`;
                    link.style.top = `${baseY}px`;
                    link.style.width = `${Math.hypot(dx, dy)}px`;
                    link.style.transform = `rotate(${Math.atan2(dy, dx)}rad)`;
                    pins.appendChild(link);
                }

                const pin = document.createElement('button');
                pin.type = 'button';
                pin.className = 'ad-geomap__pin';
                if (m.muted) pin.classList.add('is-muted');
                if (m.key === picked) pin.classList.add('is-picked');
                pin.style.left = `${baseX + dx}px`;
                pin.style.top = `${baseY + dy}px`;
                pin.title = m.name;

                const label = document.createElement('span');
                label.className = 'ad-geomap__name';
                label.textContent = m.name;
                pin.appendChild(label);

                pin.addEventListener('click', (e) => {
                    e.stopPropagation();
                    picked = m.key;
                    render();
                    dotnet.invokeMethodAsync('PickMarker', m.key);
                });

                pins.appendChild(pin);
            });
        }
    }

    // ── 끌기 · 확대 ─────────────────────────────────────────
    //
    // 조직도 캔버스(`org-chart.js`)와 같은 까닭으로 포인터 이벤트를 쓴다 —
    // 마우스·손가락·펜이 한 벌로 처리된다.

    const pointers = new Map();
    let panFrom = null;
    let pinchFrom = null;

    /** 끈 거리. 이보다 적게 움직였으면 「누른 것」으로 본다. */
    const DRAG_SLOP = 4;
    let moved = 0;

    function onPointerDown(e) {
        // 점은 자기 몫(누르면 쪽지)이 있다. 지도가 가로채지 않는다.
        if (e.target.closest?.('.ad-geomap__pin')) return;

        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        host.setPointerCapture?.(e.pointerId);
        moved = 0;

        if (pointers.size === 1) {
            panFrom = { x: e.clientX, y: e.clientY, cx, cy };
            pinchFrom = null;
            host.classList.add('is-grabbing');
        } else if (pointers.size === 2) {
            panFrom = null;
            pinchFrom = { dist: pointerDistance(), zoom };
        }
    }

    function onPointerMove(e) {
        if (!pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

        if (pointers.size === 1 && panFrom) {
            const dx = e.clientX - panFrom.x;
            const dy = e.clientY - panFrom.y;
            moved = Math.max(moved, Math.hypot(dx, dy));
            cx = panFrom.cx - dx;
            cy = panFrom.cy - dy;
            render();
            return;
        }

        if (pointers.size === 2 && pinchFrom && pinchFrom.dist > 0) {
            const ratio = pointerDistance() / pinchFrom.dist;
            const next = Math.round(pinchFrom.zoom + Math.log2(ratio));
            const mid = pointerMidpoint();
            zoomAt(next, mid.x, mid.y);
        }
    }

    function onPointerUp(e) {
        pointers.delete(e.pointerId);
        host.releasePointerCapture?.(e.pointerId);

        if (pointers.size === 0) {
            // **끌지 않고 뗐으면 고른 것을 푼다.** 점 하나를 고른 채 두면
            // 옆의 목록이 그 사람을 계속 펴 놓고 있어, 지도를 둘러보는 동안
            // 엉뚱한 사람의 카드를 보게 된다.
            if (panFrom && moved < DRAG_SLOP && picked !== null) {
                picked = null;
                render();
                dotnet.invokeMethodAsync('PickMarker', null);
            }

            panFrom = null;
            pinchFrom = null;
            host.classList.remove('is-grabbing');
        } else if (pointers.size === 1) {
            const [only] = [...pointers.values()];
            panFrom = { x: only.x, y: only.y, cx, cy };
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

    /** 화면의 한 점을 붙잡은 채 확대 단계를 바꾼다. 커서 아래 것이 제자리에 남는다. */
    function zoomAt(nextZoom, screenX, screenY) {
        const next = clamp(Math.round(nextZoom), MIN_ZOOM, MAX_ZOOM);
        if (next === zoom) return;

        const rect = host.getBoundingClientRect();
        const px = screenX - rect.left - host.clientWidth / 2;
        const py = screenY - rect.top - host.clientHeight / 2;

        const scale = Math.pow(2, next - zoom);

        // 붙잡은 점의 세상 좌표는 그대로여야 한다.
        cx = (cx + px) * scale - px;
        cy = (cy + py) * scale - py;
        zoom = next;
        render();
    }

    function onWheel(e) {
        e.preventDefault();
        zoomAt(zoom + (e.deltaY < 0 ? 1 : -1), e.clientX, e.clientY);
    }

    host.addEventListener('wheel', onWheel, { passive: false });
    host.addEventListener('pointerdown', onPointerDown);
    host.addEventListener('pointermove', onPointerMove);
    host.addEventListener('pointerup', onPointerUp);
    host.addEventListener('pointercancel', onPointerUp);

    const resizes = new ResizeObserver(() => render());
    resizes.observe(host);

    render();

    return {
        /**
         * 찍을 점을 갈아 끼운다. `fit` 이 참이면 전부 보이게 맞춘다.
         *
         * **맞추는 것은 자료가 바뀔 때만이다.** 걸러 보기만 해도 매번 맞추면
         * 사람이 손으로 옮겨 둔 자리가 조건을 만질 때마다 되돌아간다.
         */
        setMarkers(items, fit) {
            markers = items ?? [];

            // 사라진 사람을 고른 채로 두지 않는다 — 그 점은 이제 없다.
            if (picked !== null && !markers.some(m => m.key === picked)) {
                picked = null;
            }

            if (fit) fitAll();
            else render();
        },

        /** 목록에서 고른 것을 지도에도 알린다. 그 자리로 옮기고 점을 키운다. */
        focus(key) {
            picked = key;

            const m = markers.find(x => x.key === key);

            if (m) {
                zoom = Math.max(zoom, SINGLE_ZOOM);
                worldSize = TILE * Math.pow(2, zoom);
                cx = lonToX(m.lon, worldSize);
                cy = latToY(m.lat, worldSize);
            }

            render();
        },

        /** 전부 보이게 맞춘다. 단추가 부른다. */
        fit: fitAll,

        /** 한 단계씩 확대·축소. 화면 가운데를 붙잡는다. */
        zoomBy(step) {
            const rect = host.getBoundingClientRect();
            zoomAt(zoom + step, rect.left + host.clientWidth / 2, rect.top + host.clientHeight / 2);
        },

        dispose() {
            resizes.disconnect();
            host.removeEventListener('wheel', onWheel);
            host.removeEventListener('pointerdown', onPointerDown);
            host.removeEventListener('pointermove', onPointerMove);
            host.removeEventListener('pointerup', onPointerUp);
            host.removeEventListener('pointercancel', onPointerUp);
            tiles.remove();
            pins.remove();
            tileCache.clear();
        },
    };

    /**
     * 점 전부가 들어오는 가장 가까운 확대 단계를 고른다.
     *
     * 점이 하나면 「들어오는 단계」가 최대치라 지구 반대편까지 보이게 된다 —
     * 그때는 동네가 읽히는 단계로 못박는다.
     */
    function fitAll() {
        if (markers.length === 0) {
            render();
            return;
        }

        const w = Math.max(host.clientWidth, 1);
        const h = Math.max(host.clientHeight, 1);

        const lats = markers.map(m => m.lat);
        const lons = markers.map(m => m.lon);

        const minLat = Math.min(...lats);
        const maxLat = Math.max(...lats);
        const minLon = Math.min(...lons);
        const maxLon = Math.max(...lons);

        let best = SINGLE_ZOOM;

        // **한자리에 모여 있으면 「다 들어오는 단계」가 최대치가 된다.**
        // 한 사무실에 앉은 사람들이 그렇다 — 좌표 차이가 0 이라 어느 단계에서도
        // 들어오고, 그대로 두면 건물 하나만 보이는 18 단계까지 당겨진다.
        // 그때는 점이 하나일 때와 같은 단계로 둔다(동네가 읽히는 정도).
        const spread = Math.max(
            Math.abs(Math.max(...lats) - Math.min(...lats)),
            Math.abs(Math.max(...lons) - Math.min(...lons)));

        if (markers.length > 1 && spread > 0.0005) {
            for (let z = MAX_ZOOM; z >= MIN_ZOOM; z--) {
                const size = TILE * Math.pow(2, z);
                const dx = Math.abs(lonToX(maxLon, size) - lonToX(minLon, size));
                const dy = Math.abs(latToY(minLat, size) - latToY(maxLat, size));

                if (dx <= w - PAD * 2 && dy <= h - PAD * 2) {
                    best = z;
                    break;
                }

                best = MIN_ZOOM;
            }
        }

        zoom = clamp(best, MIN_ZOOM, MAX_ZOOM);
        worldSize = TILE * Math.pow(2, zoom);
        cx = lonToX((minLon + maxLon) / 2, worldSize);
        cy = latToY((minLat + maxLat) / 2, worldSize);
        render();
    }
}
