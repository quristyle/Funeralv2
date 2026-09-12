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
