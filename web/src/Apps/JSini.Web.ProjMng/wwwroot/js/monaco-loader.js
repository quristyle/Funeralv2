// Monaco 를 **쓰는 화면에서만** 불러온다.
//
// [왜 <head> 에 적지 않나]
//
// BlazorMonaco 는 스크립트 세 장을 전역에 두라고 한다. 그중 editor.main.js 가
// 3MB 가 넘는다. 셸의 <head> 는 한 벌이라 거기 적으면 **편집기가 없는 화면까지
// 전부** 그것을 받는다 — 포털에서 편집기를 쓰는 화면은 아홉 개뿐이다.
//
// [왜 감싸는 부품이 늦게 넣어 주면 안 되나]
//
// BlazorMonaco 의 StandaloneCodeEditor 는 **자기 첫 렌더에서** window.monaco 를
// 부른다. Blazor 는 OnAfterRender 를 자식부터 부르므로, 감싸개가 "그린 뒤에
// 넣어 주는" 방식으로는 언제나 늦는다. 그래서 감싸개가 **다 받은 뒤에야**
// 편집기를 그린다(CodeEditor.razor 의 `_ready`).
//
// 없을 때 나는 일이 나쁘다 — 그 화면만 깨지는 것이 아니라 **회로가 끊겨**
// 그 순간부터 아무 단추도 안 눌린다.

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

/**
 * `window.monaco` 가 생길 때까지 기다린다.
 *
 * **스크립트의 load 이벤트로는 부족하다.** editor.main.js 는 AMD 모듈이라
 * 파일을 다 받은 뒤에도 loader 가 모듈을 엮는 동안 `monaco` 가 아직 없다.
 * 그 틈에 편집기를 그리면 `monaco is not defined` 로 **회로가 끊긴다** —
 * 실제로 그렇게 한 번 깨졌다.
 */
function waitForMonaco(timeoutMs = 15000) {
  const until = performance.now() + timeoutMs;

  return new Promise((resolve, reject) => {
    // **`requestAnimationFrame` 을 쓰지 않는다.** 보이지 않는 탭에서는 그
    // 콜백이 오지 않아 여기서 영영 기다리게 된다 — 편집기가 「불러오는 중」에
    // 멈춘 채로 남는다. `setTimeout` 은 탭이 가려져 있어도 온다(느려질 뿐이다).
    const tick = () => {
      if (window.monaco?.editor) { resolve(); return; }
      if (performance.now() > until) { reject(new Error('monaco 로딩 시간 초과')); return; }
      setTimeout(tick, 30);
    };

    tick();
  });
}

/**
 * 다 받을 때까지 기다린다. 여러 편집기가 같은 화면에 있어도 한 번만 받는다.
 */
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

/**
 * 본문 글자 크기(px). **Monaco 는 CSS 변수를 못 읽는다** — `fontSize` 는
 * 숫자로만 받고, 내부 레이아웃(줄 높이·커서·여백)을 그 숫자로 직접 잰다.
 * `--jsini-fs-*` 사다리를 걸어도 편집기만 12px 로 남는 이유가 그것이다.
 *
 * `body` 를 재는 것은 사다리의 본문 단(`--jsini-fs-md`)이 거기 걸려 있어서다
 * (app.css). 테마 크기를 바꾸면 이 값이 따라 바뀌므로, 바뀔 때마다 다시 재서
 * `updateOptions` 로 밀어 넣는다.
 */
export function baseFontPx() {
  const px = parseFloat(getComputedStyle(document.body).fontSize);

  return Number.isFinite(px) && px > 0 ? px : 14;
}

/** 손가락이 이만큼 넘게 움직였으면 누름이 아니라 **굴리기**다(px). */
const TAP_SLOP_PX = 12;

/** 이보다 오래 짚고 있었으면 누름이 아니라 **길게 누르기**다(ms). */
const TAP_HOLD_MS = 700;

/**
 * **줄을 손가락으로 눌렀다**는 것을 되알려 준다.
 *
 * [왜 따로 받아야 하나]
 *
 * Monaco 는 손가락 누름에 `onMouseDown` 을 **울리지 않는다.** 터치가 있는
 * 브라우저에서는 손짓(Gesture)을 쥐는 다른 길로 갈라지고, 그 길은 누름을
 * **커서 옮기기로만** 쓴 뒤 `preventDefault` 로 흉내 마우스 사건까지 막는다.
 * 재어 확인했다 — 터치를 흉내 낸 크롬에서 한 번 눌렀을 때 올라온 것은
 *
 *   pointerdown · touchstart · touchend · 커서 옮김(5번 줄)
 *
 * 이 전부였고, `onMouseDown` 도 `onMouseUp` 도 **한 번도 안 왔다.** 같은
 * 자리를 마우스로 누르면 `onMouseDown(5번 줄, type 6)` 이 바로 온다.
 *
 * 그래서 「줄을 눌러 그 한 줄만 옮긴다」가 **휴대폰에서만 통째로 죽어
 * 있었다**(PC 브라우저를 좁혀 만든 반응형 화면은 멀쩡했다 — 거기서는 손이
 * 아니라 마우스가 누른다).
 *
 * [커서 사건으로 하지 않는 까닭]
 *
 * 누르면 커서는 움직인다(`onDidChangeCursorPosition`). 그것을 번역 신호로
 * 쓰면 **줄 번호 칸을 눌렀는지 글자를 눌렀는지 못 가린다** — 마우스 쪽은
 * 누른 자리의 종류(`MouseTargetType`)를 보고 줄 번호 누름을 지나치는데,
 * 커서 사건에는 그 값이 없다. 두 길이 다른 규칙으로 갈라서면 **같은 화면이
 * 기기마다 다르게 군다.** 여기서 자리를 직접 되물어(`getTargetAtClientPoint`)
 * 마우스 쪽과 **같은 값**을 올려 보낸다.
 *
 * [어느 편집기를 지켜보나]
 *
 * **상자의 id 를 받아 그 안에 선 편집기를 찾는다**(`getEditors`). 편집기를
 * 그대로 넘겨받지 않는 까닭은 `.NET` 쪽이 쥔 것이 BlazorMonaco 의 감싸개라
 * 브라우저의 편집기 객체가 아니어서다. 상자로 묶어 찾으면 같은 화면에 선
 * **남의 편집기**(로그 탭 · 결과 차수)는 안 걸린다.
 *
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

  // **한 편집기에 한 번만 건다.** 거는 자리(`AttachModelAsync`)는 그리기
  // 뒷정리마다 지나가므로, 표시가 없으면 같은 누름이 여러 번 올라간다.
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

  // 움직이기 시작하면 그 길로 굴리는 것이다. **그때 이미 누름이 아니다** —
  // 손을 뗀 자리가 처음 자리와 가깝더라도(왕복) 번역이 돌면 안 된다.
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

    // 길게 누르기는 고르기(또는 맥락 차림표)다. Monaco 가 가르는 자리와
    // 같은 때(700ms)로 둔다.
    if (performance.now() - from.at > TAP_HOLD_MS) { return; }

    const spot = editor.getTargetAtClientPoint(touch.clientX, touch.clientY);
    const line = spot?.position?.lineNumber ?? 0;

    if (line <= 0) { return; }

    // 자리의 종류를 그대로 올린다 — 줄 번호 칸을 지나칠지는 **마우스 쪽과
    // 같은 자리에서** 가린다(`PickLineAsync`).
    caller.invokeMethodAsync('OnLineTappedAsync', line, spot.type ?? 0)
      .catch(() => { /* 회로가 끊겼다. 알릴 곳이 없다. */ });
  }, { passive: true });
}
