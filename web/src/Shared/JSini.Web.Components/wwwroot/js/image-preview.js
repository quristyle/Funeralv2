/**
 * 그림 미리보기 — **확대·옮기기·회전·색 바꾸기를 브라우저 혼자 한다.**
 *
 * [왜 서버가 안 그리나]
 *
 * Blazor Server 는 모든 이벤트가 웹소켓을 타고 서버로 왕복한다. 손가락이나
 * 휠로 그림을 키우는 동안 `pointermove` · `wheel` 은 초당 수십 번 오는데,
 * 그것을 서버로 보내면 그림이 몇 백 ms 씩 늦게 따라오고 회선이 흔들리면 아예
 * 멈춘다. 알림함 카드(`note-swipe.js`)와 같은 까닭이고 같은 짜임이다 —
 * **끄는 동안은 브라우저가 그리고**, 서버는 「지금 몇 %인가」만 나중에 듣는다.
 *
 * [보이는 모습의 정본이 여기 하나다]
 *
 * 확대율은 휠로도 바뀌고 도구띠 단추로도 바뀐다. 두 곳에 상태를 두면 반드시
 * 어긋나므로 **여기 하나만 둔다.** C# 은 단추를 눌러 `command()` 를 부르고
 * 돌아온 값으로 도구띠를 다시 그릴 뿐이다.
 *
 * 그래서 C# 은 `<img>` 에 `style` 을 **한 글자도 그리지 않는다.** Blazor 는
 * 자기가 그린 속성만 갱신하므로, 그리지 않으면 여기서 붙인 인라인 스타일이
 * 다시 그려도 지워지지 않는다(note-swipe.js 머리말의 그 까닭이다).
 *
 * [본문 안의 그림도 눌리게 한다]
 *
 * 서식 편집기로 쓴 글은 `<img>` 가 HTML 안에 박혀 있어 화면이 하나씩 손댈
 * 수가 없다. `watch()` 가 문서에 문지기를 하나 세워, `data-imgview-scope`
 * 가 붙은 칸 안의 그림을 누르면 그 칸의 그림 **전부**를 목록으로 넘긴다 —
 * 그래야 미리보기 안에서 앞뒤로 넘길 수 있다.
 */

/** 확대율의 아래·위와 한 번에 움직이는 배율. */
const MIN_ZOOM = 0.1;
const MAX_ZOOM = 20;
const ZOOM_STEP = 1.2;

/** 밝기·대비의 아래·위와 한 칸(%). */
const MIN_LEVEL = 20;
const MAX_LEVEL = 300;
const LEVEL_STEP = 10;

/** 탭과 끌기를 가르는 거리(px). 이만큼도 안 움직였으면 누른 것으로 본다. */
const SLOP = 4;

/** 지금 열려 있는 미리보기. **한 번에 하나뿐이다.** */
let view = null;

/** 본문 그림을 대신 받아 주는 문지기. 회로마다 하나. */
let watcher = null;

/** 아무것도 안 한 상태. 그림을 갈아 끼울 때마다 여기서 다시 시작한다. */
const fresh = () => ({
  zoom: 1,
  x: 0,
  y: 0,
  rotate: 0,
  flipX: false,
  flipY: false,
  gray: false,
  invert: false,
  brightness: 100,
  contrast: 100,
});

const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

/** C# 이 받아 갈 값. 자리(`x`·`y`)는 도구띠에 안 쓰므로 빼고 보낸다. */
function snapshot() {
  if (!view) return null;

  const s = view.state;

  return {
    zoom: s.zoom,
    rotate: s.rotate,
    flipX: s.flipX,
    flipY: s.flipY,
    gray: s.gray,
    invert: s.invert,
    brightness: s.brightness,
    contrast: s.contrast,
  };
}

function filterOf(s) {
  const f = [];

  if (s.gray) f.push('grayscale(1)');
  if (s.invert) f.push('invert(1)');
  if (s.brightness !== 100) f.push(`brightness(${s.brightness}%)`);
  if (s.contrast !== 100) f.push(`contrast(${s.contrast}%)`);

  return f.length ? f.join(' ') : 'none';
}

function render() {
  if (!view) return;

  const s = view.state;
  const sx = s.zoom * (s.flipX ? -1 : 1);
  const sy = s.zoom * (s.flipY ? -1 : 1);

  view.img.style.transform =
    `translate(${s.x}px, ${s.y}px) scale(${sx}, ${sy}) rotate(${s.rotate}deg)`;
  view.img.style.filter = filterOf(s);
  view.img.style.cursor = s.zoom > 1.01 ? 'grab' : 'default';
}

/**
 * 화면의 한 점(`cx`·`cy`)을 **제자리에 둔 채** 확대율만 바꾼다.
 *
 * 그냥 배율만 키우면 손가락이나 커서가 짚고 있던 부분이 화면 밖으로 달아난다 —
 * 글자가 작은 화면 사진에서 그것이 제일 답답한 대목이다.
 */
function zoomAt(factor, cx, cy) {
  const s = view.state;
  const next = clamp(s.zoom * factor, MIN_ZOOM, MAX_ZOOM);

  if (next === s.zoom) return;

  const rect = view.stage.getBoundingClientRect();
  const px = cx - (rect.left + rect.width / 2);
  const py = cy - (rect.top + rect.height / 2);
  const k = next / s.zoom;

  s.x = px - (px - s.x) * k;
  s.y = py - (py - s.y) * k;
  s.zoom = next;
}

/**
 * 지금 각도에서 **화면에 꼭 맞는** 확대율.
 *
 * [세워 두면 잘린다 — 실제로 잘렸다]
 *
 * `max-width/height: 100%` 는 <b>돌리기 전</b> 치수에 걸린다. 가로로 긴 사진을
 * 90° 돌리면 폭과 높이가 뒤바뀌는데 브라우저는 자리를 다시 잡아 주지 않아서,
 * 800×500 짜리가 세로로 서면 위아래가 통째로 잘렸다(재어 봤다 — 무대 높이
 * 762px 에 800px 가 들어갔다).
 *
 * 그래서 넉 분의 일 돌린 상태에서는 **뒤바꾼 치수로 다시 잰다.** 1 을 넘기지
 * 않는 것은 작은 그림을 제멋대로 키우지 않기 위해서다.
 */
function fitZoom() {
  const w = view.img.clientWidth;
  const h = view.img.clientHeight;

  if (!w || !h || view.state.rotate % 180 === 0) {
    return 1;
  }

  const rect = view.stage.getBoundingClientRect();
  return Math.min(1, rect.width / h, rect.height / w);
}

/**
 * 돌린다. **돌린 뒤에도 보이던 만큼 보인다** — 화면에 맞던 그림은 맞은 채로,
 * 두 배로 보던 그림은 두 배인 채로 남는다(맞춤 배율의 비로 고쳐 잡는다).
 *
 * 끌어 옮겨 둔 자리는 되돌린다. 그 값은 돌리기 전 방향으로 잰 것이라
 * 그대로 두면 엉뚱한 쪽으로 밀린 채 선다.
 */
function turn(degrees) {
  const s = view.state;
  const before = fitZoom();

  s.rotate = (s.rotate + degrees) % 360;
  s.x = 0;
  s.y = 0;

  const after = fitZoom();

  if (before > 0) {
    s.zoom = clamp(s.zoom / before * after, MIN_ZOOM, MAX_ZOOM);
  }
}

/** 무대 한가운데를 잡고 확대한다. 단추로 키울 때 쓴다. */
function zoomCenter(factor) {
  const rect = view.stage.getBoundingClientRect();
  zoomAt(factor, rect.left + rect.width / 2, rect.top + rect.height / 2);
}

/**
 * 바뀐 값을 서버에 알린다. **그리기 한 번에 한 번만** 보낸다 —
 * 휠을 굴리는 동안 그대로 보내면 웹소켓이 확대율로 막힌다.
 */
function push() {
  if (!view || view.pushing) return;

  view.pushing = true;

  requestAnimationFrame(() => {
    if (!view) return;

    view.pushing = false;

    try {
      view.dotnet.invokeMethodAsync('OnState', snapshot());
    } catch {
      // 회로가 이미 닫혔다. 그림은 이미 브라우저에 그려져 있으므로 넘어간다.
    }
  });
}

/** 벌린 두 손가락 사이의 거리. 없으면 0. */
function pinchDist() {
  const p = [...view.pointers.values()];
  if (p.length < 2) return 0;

  return Math.hypot(p[0].x - p[1].x, p[0].y - p[1].y);
}

/** 두 손가락의 한가운데. 그 점을 붙잡고 확대한다. */
function pinchMid() {
  const p = [...view.pointers.values()];
  return { x: (p[0].x + p[1].x) / 2, y: (p[0].y + p[1].y) / 2 };
}

/**
 * 미리보기를 이 그림에 붙인다. 이미 붙어 있던 것은 떼고 시작한다.
 *
 * @param {HTMLElement} stage 그림이 노는 칸. 휠·손가락을 여기서 받는다.
 * @param {HTMLImageElement} img 그림.
 * @param {object} dotnet 되돌려 부를 C# 쪽.
 * @returns 처음 상태. C# 이 도구띠를 그것으로 그린다.
 */
export function attach(stage, img, dotnet) {
  detach();

  const v = {
    stage,
    img,
    dotnet,
    state: fresh(),
    pointers: new Map(),
    pinch: 0,
    moved: false,
    downTarget: null,
    pushing: false,
  };

  view = v;

  v.onWheel = (e) => {
    e.preventDefault();
    zoomAt(e.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP, e.clientX, e.clientY);
    render();
    push();
  };

  v.onDown = (e) => {
    if (e.button > 0) return;

    v.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    v.downTarget = e.target;
    v.moved = false;

    if (v.pointers.size === 2) v.pinch = pinchDist();

    try {
      stage.setPointerCapture(e.pointerId);
    } catch {
      // 잡지 못해도 끌기는 된다. 칸 밖으로 나갔을 때 놓칠 뿐이다.
    }
  };

  v.onMove = (e) => {
    const p = v.pointers.get(e.pointerId);
    if (!p) return;

    const dx = e.clientX - p.x;
    const dy = e.clientY - p.y;

    p.x = e.clientX;
    p.y = e.clientY;

    if (Math.abs(dx) > SLOP || Math.abs(dy) > SLOP) v.moved = true;

    if (v.pointers.size >= 2) {
      const d = pinchDist();

      if (v.pinch > 0 && d > 0) {
        const mid = pinchMid();
        zoomAt(d / v.pinch, mid.x, mid.y);
        v.pinch = d;
      }

      v.moved = true;
    } else {
      v.state.x += dx;
      v.state.y += dy;
    }

    render();
    push();
  };

  v.onUp = (e) => {
    v.pointers.delete(e.pointerId);

    if (v.pointers.size < 2) v.pinch = 0;

    // **빈자리를 눌러 닫는 것은 여기서 가른다.** C# 에 `@onclick` 으로 두면
    // 그림을 끌어 옮긴 손을 뗄 때도 닫힌다 — 끌기가 곧 클릭이기 때문이다.
    if (!v.moved && v.downTarget === stage && v.pointers.size === 0) {
      try {
        v.dotnet.invokeMethodAsync('OnBackdrop');
      } catch {
        // 회로가 닫혔다. 어차피 닫으려던 참이다.
      }
    }
  };

  v.onDblClick = (e) => {
    if (v.state.zoom > fitZoom() + 0.01) {
      v.state.zoom = fitZoom();
      v.state.x = 0;
      v.state.y = 0;
    } else {
      zoomAt(2.5, e.clientX, e.clientY);
    }

    render();
    push();
  };

  v.onKey = (e) => {
    if (e.altKey || e.ctrlKey || e.metaKey) return;

    switch (e.key) {
      case 'Escape': call('OnBackdrop'); break;
      case 'ArrowLeft': call('OnStep', -1); break;
      case 'ArrowRight': call('OnStep', 1); break;
      case '+': case '=': pressed('zoomIn'); break;
      case '-': case '_': pressed('zoomOut'); break;
      case '0': pressed('fit'); break;
      case '1': pressed('actual'); break;
      case 'r': case 'R': pressed(e.shiftKey ? 'rotateLeft' : 'rotateRight'); break;
      case 'g': case 'G': pressed('gray'); break;
      default: return;
    }

    e.preventDefault();
  };

  stage.addEventListener('wheel', v.onWheel, { passive: false });
  stage.addEventListener('pointerdown', v.onDown);
  stage.addEventListener('pointermove', v.onMove);
  stage.addEventListener('pointerup', v.onUp);
  stage.addEventListener('pointercancel', v.onUp);
  stage.addEventListener('dblclick', v.onDblClick);
  document.addEventListener('keydown', v.onKey);

  // 뒤 화면이 함께 구르지 않게 한다. 휴대폰에서 미리보기를 굴리면 그 아래
  // 목록이 딸려 가서, 닫고 나면 엉뚱한 자리에 서 있었다.
  document.body.classList.add('jsini-imgview-open');

  render();
  return snapshot();
}

/** 키로 부르는 단추. 값이 바뀌었으니 도구띠도 다시 그려야 한다. */
function pressed(name) {
  command(name);
  push();
}

function call(method, arg) {
  if (!view) return;

  try {
    view.dotnet.invokeMethodAsync(method, arg);
  } catch {
    // 회로가 닫혔다.
  }
}

/**
 * 도구띠의 단추 하나. 바뀐 상태를 돌려준다.
 *
 * 이름을 글자로 받는 이유는 단추가 열여섯 개라서다 — 하나씩 내보내면
 * C# 쪽도 열여섯 줄이 되고, 늘 때마다 두 곳을 고쳐야 한다.
 */
export function command(name) {
  if (!view) return null;

  const s = view.state;

  switch (name) {
    case 'zoomIn': zoomCenter(ZOOM_STEP); break;
    case 'zoomOut': zoomCenter(1 / ZOOM_STEP); break;

    case 'fit':
      s.zoom = fitZoom();
      s.x = 0;
      s.y = 0;
      break;

    // 원래 크기. 화면에 맞춰 줄여 놓은 배수만큼 되돌린다 —
    // `clientWidth` 는 그리기 전의 치수라 `transform` 에 흔들리지 않는다.
    case 'actual':
      s.zoom = clamp(
        view.img.naturalWidth > 0 && view.img.clientWidth > 0
          ? view.img.naturalWidth / view.img.clientWidth
          : 1,
        MIN_ZOOM,
        MAX_ZOOM);
      s.x = 0;
      s.y = 0;
      break;

    case 'rotateLeft': turn(270); break;
    case 'rotateRight': turn(90); break;

    case 'flipX': s.flipX = !s.flipX; break;
    case 'flipY': s.flipY = !s.flipY; break;

    case 'gray': s.gray = !s.gray; break;
    case 'invert': s.invert = !s.invert; break;

    case 'brighter': s.brightness = clamp(s.brightness + LEVEL_STEP, MIN_LEVEL, MAX_LEVEL); break;
    case 'darker': s.brightness = clamp(s.brightness - LEVEL_STEP, MIN_LEVEL, MAX_LEVEL); break;
    case 'more': s.contrast = clamp(s.contrast + LEVEL_STEP, MIN_LEVEL, MAX_LEVEL); break;
    case 'less': s.contrast = clamp(s.contrast - LEVEL_STEP, MIN_LEVEL, MAX_LEVEL); break;

    case 'reset': view.state = fresh(); break;

    default: return snapshot();
  }

  render();
  return snapshot();
}

/** 미리보기를 뗀다. 두 번 불러도 아무 일도 하지 않는다. */
export function detach() {
  if (!view) return;

  const v = view;
  view = null;

  v.stage.removeEventListener('wheel', v.onWheel);
  v.stage.removeEventListener('pointerdown', v.onDown);
  v.stage.removeEventListener('pointermove', v.onMove);
  v.stage.removeEventListener('pointerup', v.onUp);
  v.stage.removeEventListener('pointercancel', v.onUp);
  v.stage.removeEventListener('dblclick', v.onDblClick);
  document.removeEventListener('keydown', v.onKey);

  document.body.classList.remove('jsini-imgview-open');
}

/** 쓸 수 있는 그림인가 — 주소가 없는 `<img>` 는 목록에 넣지 않는다. */
const usable = (el) => !!(el.currentSrc || el.getAttribute('src'));

/** 미리보기로 열 주소. `data-imgview-src` 가 있으면 그것이 원본이다. */
const srcOf = (el) => el.dataset.imgviewSrc || el.currentSrc || el.src;

const titleOf = (el) => el.getAttribute('alt') || el.getAttribute('title') || '';

/**
 * `data-imgview-scope` 가 붙은 칸 안의 그림을 눌러 열 수 있게 한다.
 *
 * **잡는 자리가 문서 하나다.** 칸마다 걸면 본문이 다시 그려질 때
 * (댓글 하나만 늘어도 그렇다) 걸어 둔 것이 조용히 사라진다.
 */
export function watch(dotnet) {
  unwatch();

  const handler = (e) => {
    if (!(e.target instanceof Element)) return;

    const img = e.target.closest('img');
    if (!img || !usable(img)) return;

    // 미리보기 자신의 그림은 건드리지 않는다. 열려 있는 창이 다시 열린다.
    if (img.closest('.jsini-imgview')) return;

    const scope = img.closest('[data-imgview-scope]');
    if (!scope) return;

    const all = [...scope.querySelectorAll('img')].filter(usable);
    if (all.length === 0) return;

    // 링크로 감싼 그림이 있다(AI 지시의 첨부). 새 탭으로 달아나지 않게 막는다.
    e.preventDefault();
    e.stopPropagation();

    try {
      dotnet.invokeMethodAsync(
        'OpenFromDom',
        all.map(srcOf),
        all.map(titleOf),
        Math.max(0, all.indexOf(img)));
    } catch {
      // 회로가 닫혔다. 문지기는 다음 회로가 다시 세운다.
    }
  };

  // **잡기 단계다.** 화면이 같은 그림에 제 손짓을 걸어 두었을 수 있고
  // (표의 줄 누르기가 그렇다), 그때 우리가 먼저 받아야 한다.
  document.addEventListener('click', handler, true);
  watcher = handler;
}

export function unwatch() {
  if (!watcher) return;

  document.removeEventListener('click', watcher, true);
  watcher = null;
}
