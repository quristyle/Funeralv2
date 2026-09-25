/**
 * 연결이 끊겼을 때 뜨는 대화상자의 손놀림.
 *
 * 상자 자체는 셸이 그린다(`App.razor` 의 `#components-reconnect-modal`).
 * 프레임워크는 거기에 상태 클래스만 갈아 끼우고, 모양은 app.css 가 쥔다.
 * **여기서 맡는 것은 클래스만으로는 안 되는 넷이다.**
 *
 * [1] 단추
 *
 * 회로가 끊긴 뒤에 도는 코드라 C# 처리기는 아무 소용이 없다 — 누르는 순간
 * 서버로 갈 길이 없다. `window.Blazor.reconnect()` · `resumeCircuit()` 은
 * .NET 10 에서 공개된 전역이라 브라우저 안에서 그대로 부를 수 있다.
 *
 * [2] 서버에 하던 일이 남아 있지 않으면 **스스로 새로고침한다**
 *
 * 서버가 다시 시작됐거나 너무 오래 끊겨 있으면 재연결이 거절된다(rejected).
 * 그때 화면은 이미 죽어 있다 — 단추도 메뉴도 아무것도 안 듣는다. 예전에는
 * 여기서 「새로고침하십시오」라고 말만 하고 사람이 누르기를 기다렸는데,
 * **기본 상자가 그 자리에서 곧바로 새로고침해 주던 것**에 견주면 그냥
 * 죽은 화면 앞에 사람을 세워 두는 것이었다. 다섯을 세고 스스로 연다.
 *
 * 그 다섯 초는 적어 둔 것을 옮겨 적을 틈이다. 더 필요하면 「잠시 멈추기」로
 * 세는 것을 멈출 수 있고, 그때는 예전처럼 사람이 고를 때만 새로 연다.
 *
 * 거절이 아닌 상태(failed · resume-failed)에서는 **스스로 새로고침하지
 * 않는다.** 그때는 서버에 닿지도 못한 것이라, 새로 열어 봐야 브라우저의
 * 오류 화면이 뜨고 하던 것만 잃는다. 조용히 다시 이어 보는 편이 낫다.
 *
 * [3] 스스로 다시 해 보기
 *
 * 프레임워크의 재시도는 서른 번에서 멈춘다(처음 열 번은 즉시, 다음 열 번은
 * 5초, 나머지 열 번은 30초 — 다 합쳐 **6분 남짓**). 그런데 서버는 끊긴 회로를
 * **15분** 붙들고 있다(`DisconnectedCircuitRetentionPeriod`). 그래서 지하철을
 * 십 분 타고 나오면 **서버에는 하던 일이 그대로 있는데 브라우저가 먼저
 * 포기한 상태**가 된다. 사람이 「다시 시도」를 눌러야만 살아난다.
 *
 * 멈춰 선 뒤에도 30초마다, 그리고 **화면이 다시 보이는 순간** 조용히 한 번
 * 더 해 본다. 잠자던 휴대폰을 깨우면 대개 그 자리에서 이어진다.
 *
 * [4] 잠깐 끊긴 것에는 상자를 띄우지 않는다
 *
 * 회선이 한 번 튀는 것은 흔하고 대개 1초 안에 돌아온다. 그때마다 화면을
 * 덮어 버리면 하던 일이 끊긴 것처럼 느껴진다. 상자가 뜨는 순간 `--quiet` 를
 * 붙여 **0.9초 동안 투명하게** 두고(app.css), 그 안에 다시 이어지면 사람은
 * 아무것도 못 본 채 하던 일을 잇는다.
 *
 * 다시 이어졌을 때 우리가 하는 일은 **없다.** 프레임워크가 `hide` 를 붙이고
 * 그 규칙이 상자를 끈다. 새로고침하지 않으므로 화면은 끊기기 전 그대로다.
 *
 * ── 상자를 **붙들어 두지 않는다** ─────────────────────────────
 *
 * [여기서 한 번 크게 밟았다 — 「상자는 뜨는데 단추가 하나도 안 눌린다」]
 *
 * 이 파일은 한동안 기동할 때 `getElementById` 를 **한 번만** 해서 그 요소에
 * 손을 걸어 두었다. 그런데 **향상된 이동(enhanced navigation)은 문서를 다시
 * 파싱하지 않고 <body> 를 기워 맞추면서 이 <div> 를 통째로 갈아 끼운다.**
 * 그러면
 *
 *   · 우리 손놀림은 **떨어져 나간 옛 요소**에 남는다 — 단추도, 자동 재시도도,
 *     상태 변화 수신도 전부 죽는다.
 *   · 그런데 프레임워크는 끊길 때마다 `getElementById` 를 새로 하므로
 *     **상자는 멀쩡히 뜬다.**
 *
 * 증상이 「상자는 뜨는데 새로고침이 안 눌린다」 하나로 보이고, 새로고침을
 * 하고 나면 (문서가 새로 파싱되므로) 멀쩡해져서 재현조차 어렵다.
 * 실제로 헤드리스 크롬으로 재연결 상자의 리스너를 세어 보고야 잡았다.
 *
 * 그래서 이제 **요소를 기억하지 않는다.**
 *
 *   · 누름은 `document` 에 한 번만 걸고, 누른 자리에서 상자를 찾는다.
 *     요소가 몇 번 갈려도 이 길은 끊기지 않는다.
 *   · 상태 변화 이벤트는 **거품이 일지 않아**(`bubbles: false`) 상자에
 *     직접 걸어야 한다. 그래서 `enhancedload` 마다 지금 있는 상자에 다시
 *     건다(이미 걸린 것에는 표시를 남겨 두 번 걸지 않는다).
 */
(() => {
  'use strict';

  const DIALOG_ID = 'components-reconnect-modal';

  // 프레임워크가 갈아 끼우는 상태 클래스(UserSpecifiedDisplay). 손으로 상태를
  // 바꿀 때도 같은 이름을 쓴다 — 그래야 프레임워크가 다음에 지울 수 있다.
  const SHOW = 'components-reconnect-show';
  const HIDE = 'components-reconnect-hide';
  const RETRYING = 'components-reconnect-retrying';
  const PAUSED = 'components-reconnect-paused';
  const FAILED = 'components-reconnect-failed';
  const RESUME_FAILED = 'components-reconnect-resume-failed';
  const REJECTED = 'components-reconnect-rejected';
  const STATES = [SHOW, HIDE, RETRYING, PAUSED, FAILED, RESUME_FAILED, REJECTED];

  // 우리 것 셋. 프레임워크는 이 이름을 모르므로 우리가 붙이고 우리가 뗀다.
  const QUIET = 'jsini-reconnect--quiet';
  const COUNTDOWN = 'jsini-reconnect--countdown';
  const HELD = 'jsini-reconnect--held';

  // 상자에 손을 걸어 두었다는 표시. 향상된 이동으로 상자가 갈리면 새 요소에는
  // 이 표시가 없으므로 다시 건다.
  const BOUND = '__jsiniReconnectBound';

  // 멈춰 선 뒤 스스로 다시 해 보는 주기.
  const AUTO_RETRY_MS = 30000;

  // 거절당한 뒤 스스로 새로고침하기까지 세는 초.
  const RELOAD_COUNTDOWN_SEC = 5;

  /** 프레임워크가 포기한 뒤 우리가 이어받을 일. 'reconnect' · 'resume' · null. */
  let stalled = null;
  let autoTimer = null;
  let inFlight = false;

  /** 스스로 새로고침하기까지 남은 초. 세는 중이 아니면 null. */
  let reloadLeft = null;
  let reloadTimer = null;
  let reloading = false;

  /** **붙들지 않는다**(머리말). 쓸 때마다 지금 문서에 있는 것을 찾는다. */
  const dialog = () => document.getElementById(DIALOG_ID);

  function setState(state) {
    const box = dialog();
    if (!box) {
      return;
    }

    box.classList.remove(...STATES);
    box.classList.add(state);
  }

  function clearTimer() {
    if (autoTimer !== null) {
      clearTimeout(autoTimer);
      autoTimer = null;
    }
  }

  function startAuto(kind) {
    stalled = kind;
    clearTimer();
    autoTimer = setTimeout(() => attempt(kind), AUTO_RETRY_MS);
  }

  function stopAuto() {
    stalled = null;
    clearTimer();
  }

  // ── 새로고침 ──────────────────────────────────────────────────

  /**
   * 화면을 새로 연다.
   *
   * 뒤에 보험을 하나 둔다. `location.reload()` 가 먹지 않는 판이 실제로
   * 있었고(향상된 이동으로 죽은 손놀림이 그 까닭이었지만, 그것 하나라고
   * 단정할 수 없다) 그때 사람에게는 **단추가 고장 난 것**으로 보인다.
   * 4초 뒤에도 이 자리가 살아 있으면 한 번 더, 다른 길로 연다.
   */
  function reloadNow() {
    if (reloading) {
      return;
    }

    reloading = true;
    setTimeout(() => location.replace(location.href), 4000);
    location.reload();
  }

  function paintReloadSeconds() {
    const cell = document.getElementById('jsini-reconnect-reload-seconds');

    if (cell) {
      cell.innerText = String(reloadLeft);
    }
  }

  /** 거절당했다. 다섯을 세고 스스로 연다(머리말 [2]). */
  function startReloadCountdown() {
    if (reloadTimer !== null || reloading) {
      return;
    }

    reloadLeft = RELOAD_COUNTDOWN_SEC;
    paintReloadSeconds();

    reloadTimer = setInterval(() => {
      reloadLeft -= 1;
      paintReloadSeconds();

      if (reloadLeft <= 0) {
        stopReloadCountdown();
        reloadNow();
      }
    }, 1000);
  }

  function stopReloadCountdown() {
    if (reloadTimer !== null) {
      clearInterval(reloadTimer);
      reloadTimer = null;
    }

    reloadLeft = null;
  }

  /**
   * 한 번 이어 본다.
   *
   * 순서는 기본 상자와 같다 — 먼저 **쓰던 회로에 다시 붙고**(reconnect),
   * 그것이 거절되면 서버에 남아 있는 상태로 **되살려 본다**(resume).
   * 둘 다 안 되면 그때가 정말 없는 것이다.
   *
   * 성공했을 때 여기서 하는 일은 없다. 프레임워크가 `onConnectionUp` 에서
   * 상자를 끄고, 화면은 끊기기 전 그대로 남는다.
   */
  async function attempt(kind) {
    const blazor = window.Blazor;
    if (inFlight || !blazor || !blazor.reconnect || !blazor.resumeCircuit) {
      return;
    }

    inFlight = true;
    clearTimer();

    // 누른 것이 먹혔다는 표시. 도는 고리가 다시 보인다.
    const box = dialog();
    if (box) {
      box.classList.remove(COUNTDOWN);
    }
    setState(SHOW);

    try {
      if (kind === 'resume') {
        if (!await blazor.resumeCircuit()) {
          setState(RESUME_FAILED);
          startAuto('resume');
          return;
        }
      } else if (!await blazor.reconnect()) {
        if (!await blazor.resumeCircuit()) {
          // 서버가 「그런 회로 없다」고 답했다. 더 해 봐야 소용이 없다.
          setState(REJECTED);
          stopAuto();
          startReloadCountdown();
          return;
        }
      }

      stopAuto();
    } catch (err) {
      // 서버에 닿지도 못했다. 잠시 뒤에 다시 해 본다.
      setState(kind === 'resume' ? RESUME_FAILED : FAILED);
      startAuto(kind);
    } finally {
      inFlight = false;
    }
  }

  // ── 프레임워크가 알려 주는 상태 변화 ──────────────────────────
  //
  // 일곱 가지가 온다. 글과 단추를 고르는 것은 CSS 가 하고, 여기서는 **클래스로
  // 표현할 수 없는 것**만 본다 — 초읽기 줄을 켜고 끄는 것, 프레임워크가
  // 포기한 자리를 이어받는 것, 그리고 거절당했을 때 스스로 새로 여는 것.
  function onStateChanged(event) {
    const detail = event.detail || {};
    const box = dialog();
    if (!box) {
      return;
    }

    // 「잠깐 끊긴 것」의 투명함은 **다시 잇는 중일 때만** 쓴다. 사람이 읽고
    // 골라야 하는 자리에서까지 감추면 상자가 없는 것과 같다.
    if (detail.state !== 'show' && detail.state !== 'retrying') {
      box.classList.remove(QUIET);
    }

    switch (detail.state) {
      case 'show':
        // 잠깐 끊긴 것이면 사람이 보기 전에 끝난다(머리말 [4]).
        box.classList.add(QUIET);
        box.classList.remove(COUNTDOWN);
        stopAuto();
        break;

      case 'retrying':
        // 초읽기는 셀 것이 남았을 때만 보인다. 0초에서도 남아 있으면
        // 「0초 뒤에 다시 시도합니다」가 굳어 버린다.
        box.classList.toggle(COUNTDOWN, detail.secondsToNextAttempt > 0);
        break;

      case 'failed':
        box.classList.remove(COUNTDOWN);
        startAuto('reconnect');
        break;

      case 'resume-failed':
        box.classList.remove(COUNTDOWN);
        startAuto('resume');
        break;

      case 'paused':
        box.classList.remove(COUNTDOWN);
        stopAuto();
        break;

      case 'rejected':
        // 하던 일이 서버에 없다. 화면은 이미 죽었으므로 세고 나서 새로 연다.
        box.classList.remove(COUNTDOWN);
        stopAuto();
        startReloadCountdown();
        break;

      case 'hide':
        box.classList.remove(COUNTDOWN, QUIET, HELD);
        stopAuto();
        stopReloadCountdown();
        break;
    }
  }

  // ── 손을 건다 ─────────────────────────────────────────────────

  /**
   * 지금 문서에 있는 상자에 상태 변화 수신을 건다.
   *
   * 상태 변화 이벤트는 **거품이 일지 않아**(`CustomEvent` 기본값)
   * `document` 로는 못 받는다. 상자가 갈릴 때마다 다시 걸어야 하는 까닭이다.
   */
  function bind() {
    const box = dialog();
    if (!box || box[BOUND]) {
      return;
    }

    box[BOUND] = true;
    box.addEventListener('components-reconnect-state-changed', onStateChanged);
  }

  /**
   * 향상된 이동 뒤에 다시 건다.
   *
   * 프레임워크 쪽도 함께 본다. `DefaultReconnectionHandler` 는 **처음 끊긴
   * 순간의 상자를 기억해 두고** 그 뒤로는 다시 찾지 않는다. 그래서
   * 「한 번 끊겼다 이어짐 → 향상된 이동 → 다시 끊김」 순서를 밟으면
   * 프레임워크가 떨어져 나간 옛 상자에 클래스를 칠하고, 화면에는
   * **아무것도 안 뜬 채 굳는다.** 기억해 둔 것을 지워 다시 찾게 한다.
   *
   * 프레임워크 속살이라 이름이 바뀔 수 있다 — 그때는 아래가 조용히 아무 일도
   * 하지 않는다(있지도 않은 칸을 비우는 셈이라 해가 없다).
   */
  function rebind() {
    bind();

    const handler = window.Blazor && window.Blazor.defaultReconnectionHandler;
    if (!handler || handler._currentReconnectionProcess) {
      return;
    }

    const remembered = handler._reconnectionDisplay;
    if (remembered && remembered.dialog && remembered.dialog !== dialog()) {
      handler._reconnectionDisplay = undefined;
    }
  }

  // 누름은 **문서에 한 번만** 건다. 상자가 몇 번 갈려도 이 길은 안 끊긴다.
  document.addEventListener('click', event => {
    const target = event.target;
    if (!target || typeof target.closest !== 'function') {
      return;
    }

    const button = target.closest('[data-reconnect-action]');
    if (!button || !button.closest('#' + DIALOG_ID)) {
      return;
    }

    event.preventDefault();
    const action = button.getAttribute('data-reconnect-action');

    if (action === 'reload') {
      stopReloadCountdown();
      reloadNow();
      return;
    }

    if (action === 'hold') {
      // 스스로 여는 것을 멈춘다. 적어 둔 것을 옮겨 적을 시간을 달라는 뜻이다.
      stopReloadCountdown();
      const box = dialog();
      if (box) {
        box.classList.add(HELD);
      }
      return;
    }

    attempt(action === 'resume' ? 'resume' : 'reconnect');
  });

  // 화면이 다시 보이는 순간이 가장 잘 붙는 때다 — 휴대폰을 깨웠거나 탭으로
  // 돌아온 참이라 망이 막 살아난다. 기다리지 않고 바로 해 본다.
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && stalled !== null) {
      attempt(stalled);
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', hook);
  } else {
    hook();
  }

  function hook() {
    bind();

    if (window.Blazor && window.Blazor.addEventListener) {
      window.Blazor.addEventListener('enhancedload', rebind);
    }
  }
})();
