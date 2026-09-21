/**
 * 연결이 끊겼을 때 뜨는 대화상자의 손놀림.
 *
 * 상자 자체는 셸이 그린다(`App.razor` 의 `#components-reconnect-modal`).
 * 프레임워크는 거기에 상태 클래스만 갈아 끼우고, 모양은 app.css 가 쥔다.
 * **여기서 맡는 것은 클래스만으로는 안 되는 셋이다.**
 *
 * [1] 단추
 *
 * 회로가 끊긴 뒤에 도는 코드라 C# 처리기는 아무 소용이 없다 — 누르는 순간
 * 서버로 갈 길이 없다. `window.Blazor.reconnect()` · `resumeCircuit()` 은
 * .NET 10 에서 공개된 전역이라 브라우저 안에서 그대로 부를 수 있다.
 *
 * [2] 스스로 다시 해 보기 — **여기가 이 파일을 만든 이유다**
 *
 * 프레임워크의 재시도는 서른 번에서 멈춘다(처음 열 번은 즉시, 다음 열 번은
 * 5초, 나머지 열 번은 30초 — 다 합쳐 **6분 남짓**). 그런데 서버는 끊긴 회로를
 * **15분** 붙들고 있다(`DisconnectedCircuitRetentionPeriod`). 그래서 지하철을
 * 십 분 타고 나오면 **서버에는 하던 일이 그대로 있는데 브라우저가 먼저
 * 포기한 상태**가 된다. 사람이 「다시 시도」를 눌러야만 살아난다.
 *
 * 멈춰 선 뒤에도 30초마다, 그리고 **화면이 다시 보이는 순간** 조용히 한 번
 * 더 해 본다. 잠자던 휴대폰을 깨우면 대개 그 자리에서 이어진다.
 * 서버가 「그런 회로 없다」고 답한 뒤(rejected)에는 더 하지 않는다 —
 * 그때는 정말 남아 있는 것이 없다.
 *
 * [3] 잠깐 끊긴 것에는 상자를 띄우지 않는다
 *
 * 회선이 한 번 튀는 것은 흔하고 대개 1초 안에 돌아온다. 그때마다 화면을
 * 덮어 버리면 하던 일이 끊긴 것처럼 느껴진다. 상자가 뜨는 순간 `--quiet` 를
 * 붙여 **0.9초 동안 투명하게** 두고(app.css), 그 안에 다시 이어지면 사람은
 * 아무것도 못 본 채 하던 일을 잇는다.
 *
 * 다시 이어졌을 때 우리가 하는 일은 **없다.** 프레임워크가 `hide` 를 붙이고
 * 그 규칙이 상자를 끈다. 새로고침하지 않으므로 화면은 끊기기 전 그대로다.
 */
(() => {
  'use strict';

  const dialog = document.getElementById('components-reconnect-modal');
  if (!dialog) {
    return;
  }

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

  // 우리 것 둘. 프레임워크는 이 이름을 모르므로 우리가 붙이고 우리가 뗀다.
  const QUIET = 'jsini-reconnect--quiet';
  const COUNTDOWN = 'jsini-reconnect--countdown';

  // 멈춰 선 뒤 스스로 다시 해 보는 주기.
  const AUTO_RETRY_MS = 30000;

  /** 프레임워크가 포기한 뒤 우리가 이어받을 일. 'reconnect' · 'resume' · null. */
  let stalled = null;
  let autoTimer = null;
  let inFlight = false;

  function setState(state) {
    dialog.classList.remove(...STATES);
    dialog.classList.add(state);
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
    dialog.classList.remove(COUNTDOWN);
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
  // 표현할 수 없는 것**만 본다 — 초읽기 줄을 켜고 끄는 것과, 프레임워크가
  // 포기한 자리를 이어받는 것.
  dialog.addEventListener('components-reconnect-state-changed', event => {
    const detail = event.detail || {};

    switch (detail.state) {
      case 'show':
        // 잠깐 끊긴 것이면 사람이 보기 전에 끝난다(머리말 [3]).
        dialog.classList.add(QUIET);
        dialog.classList.remove(COUNTDOWN);
        stopAuto();
        break;

      case 'retrying':
        // 초읽기는 셀 것이 남았을 때만 보인다. 0초에서도 남아 있으면
        // 「0초 뒤에 다시 시도합니다」가 굳어 버린다.
        dialog.classList.toggle(COUNTDOWN, detail.secondsToNextAttempt > 0);
        break;

      case 'failed':
        dialog.classList.remove(COUNTDOWN);
        startAuto('reconnect');
        break;

      case 'resume-failed':
        dialog.classList.remove(COUNTDOWN);
        startAuto('resume');
        break;

      case 'paused':
      case 'rejected':
        dialog.classList.remove(COUNTDOWN);
        stopAuto();
        break;

      case 'hide':
        dialog.classList.remove(COUNTDOWN, QUIET);
        stopAuto();
        break;
    }
  });

  // 화면이 다시 보이는 순간이 가장 잘 붙는 때다 — 휴대폰을 깨웠거나 탭으로
  // 돌아온 참이라 망이 막 살아난다. 기다리지 않고 바로 해 본다.
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && stalled !== null) {
      attempt(stalled);
    }
  });

  dialog.addEventListener('click', event => {
    const button = event.target.closest('[data-reconnect-action]');
    if (!button) {
      return;
    }

    event.preventDefault();
    const action = button.getAttribute('data-reconnect-action');

    if (action === 'reload') {
      // **여기서만 새로고침한다.** 사람이 골랐을 때다.
      location.reload();
      return;
    }

    attempt(action === 'resume' ? 'resume' : 'reconnect');
  });
})();
