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
 * 휴대폰에서 **줄을 손가락으로 눌렀다**는 것을 되알려 준다.
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
 * **상자의 id 를 받아 그 안에 선 편집기를 전부 찾는다**(`getEditors`). 비교
 * 편집기는 칸이 둘인데 **누른 자리를 되물을 수 있는 것은 칸 안쪽의 편집기**다 —
 * 비교 편집기 자신에게는 `getTargetAtClientPoint` 가 없다. 상자로 묶어 찾으면
 * 그 둘이 한꺼번에 걸리고, 같은 화면에 선 **남의 편집기**는 안 걸린다.
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

  // **한 편집기에 한 번만 건다.** 거는 자리(`AttachModelsAsync`)는 그리기
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

/**
 * 비교 편집기의 **가운데 가름쇠**를 지켜보고, 칸 이름표를 그 자리에 맞춘다.
 *
 * [왜 필요한가]
 *
 * Monaco 는 `enableSplitViewResizing` 하나로 좌우를 끌어 넓히는 길을 들고
 * 있다. 그런데 칸 이름표(「원문」·「번역 · 한국어」)는 **편집기 밖**에 따로
 * 선 줄이라, 끌어서 7:3 으로 만들면 이름표만 5:5 에 남아 **이름이 남의 칸을
 * 가리킨다.** 그 어긋남 때문에 한동안 끄는 길을 아예 닫아 두었다.
 *
 * 여기서 왼쪽 칸의 폭을 그대로 읽어 이름표 줄에 `--pm-xlat-left` 로 적어
 * 준다. 끄는 동안에도 따라오므로 이름표가 칸을 떠나지 않는다.
 *
 * [회로를 거치지 않는다]
 *
 * 끄는 동안 프레임마다 .NET 을 왕복하면 Blazor Server 에서는 이름표가
 * 손가락보다 한참 뒤에 따라온다. 자리를 맞추는 일은 **브라우저 안에서만**
 * 끝내고, 회로로는 손을 뗀 뒤의 **비율 하나**만 올려 보낸다(탭을 건너갔다
 * 와도 끌어 둔 자리가 남게 하려는 것이다 — `OnSplitMoved`).
 *
 * [무엇을 재는가]
 *
 * 왼쪽 칸(`.editor.original`)의 **오른쪽 끝**에서 틀의 왼쪽 끝을 뺀다.
 * 폭을 그냥 더하지 않는 까닭은 틀에 1px 테두리가 있어서다 — 폭만 세면
 * 이름표가 그만큼 왼쪽으로 밀린다.
 *
 * [왜 ResizeObserver 인가]
 *
 * Monaco 는 가름쇠가 움직였다는 것을 밖으로 알려 주지 않는다(`onDidLayoutChange`
 * 는 편집기 안쪽 치수다). 칸 자체의 크기를 지켜보면 **끌 때도 · 창이 바뀔
 * 때도 · 글자 크기가 바뀔 때도** 한 길로 걸린다.
 *
 * [첫 자리를 여기서 놓는 까닭]
 *
 * 끌어 둔 자리는 .NET 쪽이 들고 있는데(`AiLogXlatState.SplitRatio`) 그 값을
 * 편집기 옵션으로 넘길 수가 없다 — **BlazorMonaco 가 `splitViewDefaultRatio`
 * 를 `int?` 로 선언해 두었다.** Monaco 가 받는 것은 0~1 사이의 소수라 그
 * 칸으로는 0 과 1 밖에 못 보낸다.
 *
 * 그래서 숫자를 그대로 받아 여기서 `updateOptions` 로 민다. **갓 세운
 * 편집기에만 듣는다** — Monaco 는 사람이 한 번이라도 끈 자리(`_sashRatio`)를
 * 기본값보다 위에 두기 때문이다(재어 확인했다 — 끈 뒤에 옵션을 바꾸면
 * 아무 일도 안 난다). 부르는 자리가 `SetModel` 바로 뒤라 늘 갓 세운 편집기다.
 *
 * @param hostId 비교 편집기가 선 상자(div)의 id.
 * @param headsId 칸 이름표 줄의 id.
 * @param ratio 왼쪽 칸이 차지할 첫 몫(0~1).
 * @param caller `OnSplitMoved(비율)` 을 받을 .NET 부품.
 */
export function watchSplit(hostId, headsId, ratio, caller) {
  const host = document.getElementById(hostId);
  const heads = document.getElementById(headsId);

  if (!host || !heads) { return; }

  // **아직 안 섰을 수 있다.** 모델을 걸자마자 부르므로 비교 편집기의 속이
  // 한 박자 늦게 생긴다. 몇 번 더 들여다보고 포기한다.
  let tries = 0;

  const arm = () => {
    const left = host.querySelector('.editor.original');

    if (!left) {
      if (++tries < 40) { setTimeout(arm, 50); }
      return;
    }

    // **한 칸에 한 번만 건다.** 거는 자리(`AttachModelsAsync`)는 그리기
    // 뒷정리마다 지나간다. 편집기가 갈리면 칸도 새로 생기므로 그때는 다시 걸린다.
    if (left.dataset.jsiniSplit === '1') { return; }

    left.dataset.jsiniSplit = '1';

    // 끌어 두었던 자리로 되돌린다. 갓 세운 편집기에만 듣는다(머리말).
    applyRatio(host, ratio);

    // 이름표가 기댈 틀. 테두리를 들고 있는 바깥 상자다.
    const box = host.closest('.pm-xlat__editor') ?? host;

    /** 마지막으로 올려 보낸 비율. 같은 값을 거듭 올리지 않는다. */
    let sent = -1;

    const apply = () => {
      const l = left.getBoundingClientRect();
      const b = box.getBoundingClientRect();

      if (b.width <= 0) { return; }

      heads.style.setProperty('--pm-xlat-left', `${l.right - b.left}px`);

      // 올려 보내는 것은 **편집기가 쓰는 자**와 같아야 한다 — Monaco 는
      // 제 속 폭에 대한 비율로 들고 있다(`splitViewDefaultRatio`).
      const inner = host.getBoundingClientRect().width;

      if (inner <= 0) { return; }

      const ratio = Math.min(0.95, Math.max(0.05, l.width / inner));

      if (Math.abs(ratio - sent) < 0.005) { return; }

      sent = ratio;

      caller?.invokeMethodAsync?.('OnSplitMoved', ratio)
        .catch(() => { /* 회로가 끊겼다. 알릴 곳이 없다. */ });
    };

    new ResizeObserver(apply).observe(left);

    apply();
  };

  arm();
}

/** 갓 세운 비교 편집기를 끌어 두었던 자리로 되돌린다. */
function applyRatio(host, ratio) {
  if (!(ratio > 0.05 && ratio < 0.95) || Math.abs(ratio - 0.5) < 0.005) { return; }

  const diff = (window.monaco?.editor?.getDiffEditors?.() ?? [])
    .find((d) => host.contains(d.getContainerDomNode?.() ?? d.getDomNode?.()));

  try {
    diff?.updateOptions?.({ splitViewDefaultRatio: ratio });
  } catch {
    // 판올림으로 그 옵션이 없어졌다. 반반으로 선다 — 끌어서 고칠 수 있다.
  }
}
