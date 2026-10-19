// Monaco 는 쓰는 화면에서만 불러온다 — <head> 에 두면 편집기 없는 화면까지 3MB 를 받는다.
// BlazorMonaco 는 첫 렌더에 window.monaco 를 부르므로, 감싸개는 다 받은 뒤에야 편집기를 그린다(CodeEditor.razor 의 _ready).
// 늦으면 회로가 끊겨 아무 단추도 안 눌린다.

/** 세 장의 주소. 순서가 있다 — loader 가 AMD 를 정의하고 editor.main 이 그것을 쓴다. */
const SOURCES = [
  '_content/BlazorMonaco/jsInterop.js',
  '_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js',
  '_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js',
];

/** 받는 중이거나 다 받은 약속. **한 번만 받는다.** */
let loading = null;

function addScript(src) {
  return new Promise((resolve, reject) => {
    // 다른 화면이 이미 넣어 두었으면 그것을 쓴다.
    const already = document.querySelector(`script[data-monaco="${src}"]`);
    if (already) {
      if (already.dataset.done === '1') { resolve(); return; }
      already.addEventListener('load', () => resolve());
      already.addEventListener('error', () => reject(new Error(src)));
      return;
    }

    const el = document.createElement('script');
    el.src = src;
    el.dataset.monaco = src;
    el.addEventListener('load', () => { el.dataset.done = '1'; resolve(); });
    el.addEventListener('error', () => reject(new Error(src)));
    document.head.appendChild(el);
  });
}

/** window.monaco 가 생길 때까지 기다린다. load 이벤트만으로는 AMD 모듈이 엮이기 전이라 모자라다. */
function waitForMonaco(timeoutMs = 15000) {
  const until = performance.now() + timeoutMs;

  return new Promise((resolve, reject) => {
    // **requestAnimationFrame 을 쓰지 않는다** — 숨은 탭에서는 콜백이 오지 않아 영영 기다린다. setTimeout 은 온다.
    const tick = () => {
      if (window.monaco?.editor) { resolve(); return; }
      if (performance.now() > until) { reject(new Error('monaco 로딩 시간 초과')); return; }
      setTimeout(tick, 30);
    };

    tick();
  });
}

/** 다 받을 때까지 기다린다. 편집기가 여럿이어도 한 번만 받는다. */
export function ensure() {
  if (window.monaco?.editor && window.blazorMonaco) { return Promise.resolve(); }

  loading ??= (async () => {
    for (const src of SOURCES) {
      await addScript(src);
    }

    await waitForMonaco();
  })();

  return loading;
}

/** 본문 글자 크기(px). Monaco 는 CSS 변수를 못 읽어 풀린 숫자로 받는다 — 테마가 바뀌면 다시 재서 updateOptions 로 민다. */
export function baseFontPx() {
  const px = parseFloat(getComputedStyle(document.body).fontSize);

  return Number.isFinite(px) && px > 0 ? px : 14;
}

/** 본문 글꼴. Monaco 는 var() 를 못 읽으니 풀린 값을 넘긴다. */
export function baseFontFamily() {
  return getComputedStyle(document.body).fontFamily || '';
}

let followingFont = false;

/** 글꼴이 바뀌면 떠 있는 번역 판(.pm-xlat)도 따라 바꾼다. 서랍은 회로에 알리지 않으므로 여기서 지켜본다. */
export function followBodyFont() {
  if (followingFont) { return; }
  followingFont = true;

  const remeasure = () => window.monaco?.editor?.remeasureFonts();

  new MutationObserver(() => {
    const family = baseFontFamily();

    for (const ed of window.monaco?.editor?.getEditors() ?? []) {
      if (ed.getContainerDomNode()?.closest('.pm-xlat')) {
        ed.updateOptions({ fontFamily: family });
      }
    }

    document.fonts?.ready.then(remeasure);
  }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-tb-font'] });

  document.fonts?.addEventListener('loadingdone', remeasure);
}

/** 손가락이 이만큼 넘게 움직였으면 누름이 아니라 **굴리기**다(px). */
const TAP_SLOP_PX = 12;

/** 이보다 오래 짚고 있었으면 누름이 아니라 **길게 누르기**다(ms). */
const TAP_HOLD_MS = 700;

/**
 * 줄을 손가락으로 눌렀음을 되알려 준다. Monaco 는 터치에서 onMouseDown 을 울리지 않으므로 직접 잡는다.
 * 자리는 getTargetAtClientPoint 로 되물어, 마우스 쪽과 같은 값을 올린다.
 * @param hostIds 편집기가 선 상자(div)의 id 들.
 * @param caller `OnLineTappedAsync(줄번호, 자리종류)` 를 받을 .NET 부품.
 */
export function watchTaps(hostIds, caller) {
  const all = window.monaco?.editor?.getEditors?.() ?? [];

  for (const hostId of hostIds ?? []) {
    const host = document.getElementById(hostId);

    if (!host) { continue; }

    for (const editor of all) {
      watchOne(host, editor, caller);
    }
  }
}

/** 편집기 하나를 지켜본다. 상자 밖의 편집기는 그냥 지나간다. */
function watchOne(host, editor, caller) {
  const node = editor?.getDomNode?.();

  if (!node || !host.contains(node)) { return; }

  // **한 편집기에 한 번만 건다.** 거는 자리가 그리기마다 지나가므로 표시로 막는다.
  if (node.dataset.jsiniTap === '1') { return; }

  node.dataset.jsiniTap = '1';

  /** 짚기 시작한 자리와 때. null 이면 **이미 누름이 아니다**(굴렸다). */
  let began = null;

  const strayed = (touch, from) =>
    Math.abs(touch.clientX - from.x) > TAP_SLOP_PX
    || Math.abs(touch.clientY - from.y) > TAP_SLOP_PX;

  // 손가락 하나만 받는다. 둘이면 집어 벌리는 것(확대)이라 누름이 아니다.
  node.addEventListener('touchstart', (e) => {
    const touch = e.touches.length === 1 ? e.touches[0] : null;

    began = touch
      ? { x: touch.clientX, y: touch.clientY, at: performance.now() }
      : null;
  }, { passive: true });

  // 움직이기 시작하면 굴리는 것이다 — 손을 뗀 자리가 처음과 가까워도(왕복) 번역하지 않는다.
  node.addEventListener('touchmove', (e) => {
    const touch = e.touches[0];

    if (began && touch && strayed(touch, began)) { began = null; }
  }, { passive: true });

  node.addEventListener('touchcancel', () => { began = null; }, { passive: true });

  node.addEventListener('touchend', (e) => {
    const from = began;
    began = null;

    const touch = e.changedTouches.length === 1 ? e.changedTouches[0] : null;

    if (!from || !touch || strayed(touch, from)) { return; }

    // 길게 누르기는 고르기(또는 맥락 차림표)다. Monaco 의 700ms 와 같은 때로 둔다.
    if (performance.now() - from.at > TAP_HOLD_MS) { return; }

    const spot = editor.getTargetAtClientPoint(touch.clientX, touch.clientY);
    const line = spot?.position?.lineNumber ?? 0;

    if (line <= 0) { return; }

    // 자리의 종류를 그대로 올린다 — 줄 번호 칸을 지나칠지는 마우스 쪽과 같은 자리에서 가린다.
    caller.invokeMethodAsync('OnLineTappedAsync', line, spot.type ?? 0)
      .catch(() => { /* 회로가 끊겼다. 알릴 곳이 없다. */ });
  }, { passive: true });
}
