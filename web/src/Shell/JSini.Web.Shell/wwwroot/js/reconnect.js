/**
 * 회로가 끊겼을 때 **보이지 않는 곳에서** 다시 잇는 손놀림.
 *
 * 셸은 `App.razor` 에 빈 `#components-reconnect-modal` 하나만 둔다. 그것이
 * 문서에 있으면 프레임워크는 자기 상자를 만들지 않고 거기에 상태 클래스만
 * 갈아 끼우고(`UserSpecifiedDisplay`), app.css 는 일곱 상태를 전부
 * `display: none` 으로 눌러 둔다. **그래서 끊겨도 화면에는 아무것도 안 뜬다.**
 *
 * [1] 왜 아무것도 안 보여 주나
 *
 * 한동안 여기가 화면을 덮는 대화상자였다(「5초 뒤에 화면을 새로 엽니다」).
 * 그 다음에는 위쪽의 작은 띠 하나였다(「연결이 잠시 끊겼습니다 · 다시 잇는 중」).
 * 둘 다 걷어냈다 — **사람이 거기서 할 일이 없기 때문이다.**
 *
 *   · 회선이 한 번 튀는 것은 1~2초 안에 저절로 이어진다.
 *   · 오래 끊긴 자리는 아래 [3]이 조용히 다시 이어 본다.
 *   · 정말 되돌릴 수 없는 자리(rejected)는 아래 [2]가 묻지 않고 새로 연다.
 *
 * 어느 쪽이든 사람이 고를 것이 없는데, 알림이 뜨면 그것 자체가 「사고가
 * 났다」로 읽혀 아무 일도 아닌 터널 몇 초마다 하던 일을 멈추게 했다.
 * 그래서 **끊긴 동안 화면은 끊기기 전 그대로다.**
 *
 * 적어 둔 것이 사라지지 않는가 — 그 일은 이 자리가 아니라 화면이 맡는다.
 * 글을 길게 적는 자리(헬프데스크 요청·프로젝트 문의 …)는 이미 적는 동안
 * 브라우저에 임시로 담아 두고 새로 열린 뒤에 되살린다(`request-draft.js`
 * 따위).
 *
 * [누름을 삼키지 않는다]
 *
 * 띠가 있던 시절에는 끊긴 동안의 누름을 잡는 단계에서 삼키고 띠를 흔들어
 * 알렸다(「저장을 누르고 저장된 줄 아는 것」을 막으려고). **띠가 없으면 그
 * 삼킴은 알릴 자리가 없다** — 막기만 하고 아무 말도 못 하는 셈이라, 회로가
 * 없어 어차피 아무 일도 안 일어나는 것과 결과가 같아진다. 오히려 회로 없이도
 * 도는 것(주소를 가진 링크·네이티브 폼)까지 함께 죽인다. 그래서 삼키지 않는다.
 *
 * [2] 서버에 하던 일이 남아 있지 않으면 **묻지 않고 새로고침한다**
 *
 * 서버가 다시 시작됐거나 너무 오래 끊겨 있으면 재연결이 거절된다(rejected).
 * 그때 화면은 이미 죽어 있다 — 단추도 메뉴도 아무것도 안 듣는다. 어느 단추를
 * 눌러도 끝은 새로고침 하나이므로 그 자리에서 연다.
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
 * 포기한 상태**가 된다. 그 자리를 여기가 이어받는다.
 *
 * 멈춰 선 뒤에도 30초마다, 그리고 **화면이 다시 보이는 순간**·**망이 돌아오는
 * 순간** 조용히 한 번 더 해 본다. 잠자던 휴대폰을 깨우면 대개 그 자리에서
 * 이어진다.
 *
 * ── 요소를 **붙들어 두지 않는다** ─────────────────────────────
 *
 * [여기서 한 번 크게 밟았다]
 *
 * 이 파일은 한동안 기동할 때 `getElementById` 를 **한 번만** 해서 그 요소에
 * 손을 걸어 두었다. 그런데 **향상된 이동(enhanced navigation)은 문서를 다시
 * 파싱하지 않고 <body> 를 기워 맞추면서 이 <div> 를 통째로 갈아 끼운다.**
 * 그러면 우리 손놀림은 **떨어져 나간 옛 요소**에 남는다 — 자동 재시도도,
 * 상태 변화 수신도 전부 죽는다. 그런데 프레임워크는 끊길 때마다
 * `getElementById` 를 새로 하므로 **겉으로는 멀쩡해 보인다.**
 *
 * 그래서 이제 요소를 기억하지 않는다. 상태 변화 이벤트는 **거품이 일지
 * 않아**(`bubbles: false`) 그 요소에 직접 걸어야 하므로, `enhancedload` 마다
 * 지금 있는 것에 다시 건다(이미 걸린 것에는 표시를 남겨 두 번 걸지 않는다).
 */
(() => {
  'use strict';

  const DIALOG_ID = 'components-reconnect-modal';

  // 띠에 손을 걸어 두었다는 표시. 향상된 이동으로 요소가 갈리면 새 요소에는
  // 이 표시가 없으므로 다시 건다.
  const BOUND = '__jsiniReconnectBound';

  // 멈춰 선 뒤 스스로 다시 해 보는 주기.
  const AUTO_RETRY_MS = 30000;

  /** 프레임워크가 포기한 뒤 우리가 이어받을 일. 'reconnect' · 'resume' · null. */
  let stalled = null;
  let autoTimer = null;
  let inFlight = false;

  let reloading = false;

  /** **붙들지 않는다**(머리말). 쓸 때마다 지금 문서에 있는 것을 찾는다. */
  const dialog = () => document.getElementById(DIALOG_ID);

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
   * 화면을 새로 연다. **묻지 않는다**(머리말 [2]).
   *
   * 뒤에 보험을 하나 둔다. `location.reload()` 가 먹지 않는 판이 실제로
   * 있었고(향상된 이동으로 죽은 손놀림이 그 까닭이었지만, 그것 하나라고
   * 단정할 수 없다) 그때 화면은 **굳은 것**으로 보인다.
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

  /**
   * 한 번 이어 본다.
   *
   * 순서는 기본 상자와 같다 — 먼저 **쓰던 회로에 다시 붙고**(reconnect),
   * 그것이 거절되면 서버에 남아 있는 상태로 **되살려 본다**(resume).
   * 둘 다 안 되면 그때가 정말 없는 것이다.
   *
   * 성공했을 때 여기서 하는 일은 없다 — 화면은 끊기기 전 그대로 남는다.
   * 보여 줄 것이 없으므로 **상태 클래스도 건드리지 않는다**(머리말 [1]).
   */
  async function attempt(kind) {
    const blazor = window.Blazor;
    if (inFlight || !blazor || !blazor.reconnect || !blazor.resumeCircuit) {
      return;
    }

    inFlight = true;
    clearTimer();

    try {
      if (kind === 'resume') {
        if (!await blazor.resumeCircuit()) {
          startAuto('resume');
          return;
        }
      } else if (!await blazor.reconnect()) {
        if (!await blazor.resumeCircuit()) {
          // 서버가 「그런 회로 없다」고 답했다. 더 해 봐야 소용이 없다.
          stopAuto();
          reloadNow();
          return;
        }
      }

      stopAuto();
    } catch (err) {
      // 서버에 닿지도 못했다. 잠시 뒤에 다시 해 본다.
      startAuto(kind);
    } finally {
      inFlight = false;
    }
  }

  // ── 프레임워크가 알려 주는 상태 변화 ──────────────────────────
  //
  // 일곱 가지가 온다. 보이는 것은 **어느 쪽이든 없으므로**, 여기서 보는 것은
  // 프레임워크가 포기한 자리를 이어받는 것과, 거절당했을 때 묻지 않고 새로
  // 여는 것뿐이다.
  function onStateChanged(event) {
    const detail = event.detail || {};
    if (!dialog()) {
      return;
    }

    switch (detail.state) {
      case 'show':
        stopAuto();
        break;

      case 'retrying':
        // 프레임워크가 제 고리를 돌리는 중이다. 끼어들지 않는다.
        break;

      case 'failed':
        startAuto('reconnect');
        break;

      case 'resume-failed':
        startAuto('resume');
        break;

      case 'paused':
        // 서버가 잠시 멈춰 두었다. 보고 있는 중이면 그 자리에서 이어받고,
        // 아니면 돌아오는 순간(visibilitychange)에 이어받는다.
        if (document.visibilityState === 'visible') {
          attempt('resume');
        } else {
          startAuto('resume');
        }
        break;

      case 'rejected':
        // 하던 일이 서버에 없다. 화면은 이미 죽었으므로 묻지 않고 새로 연다.
        stopAuto();
        reloadNow();
        break;

      case 'hide':
        // 다시 이어졌다. **여기서 끝난다** — 알릴 것도, 지울 표시도 없다.
        stopAuto();
        break;
    }
  }

  // ── 손을 건다 ─────────────────────────────────────────────────

  /**
   * 지금 문서에 있는 요소에 상태 변화 수신을 건다.
   *
   * 상태 변화 이벤트는 **거품이 일지 않아**(`CustomEvent` 기본값)
   * `document` 로는 못 받는다. 요소가 갈릴 때마다 다시 걸어야 하는 까닭이다.
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
   * 순간의 요소를 기억해 두고** 그 뒤로는 다시 찾지 않는다. 그래서
   * 「한 번 끊겼다 이어짐 → 향상된 이동 → 다시 끊김」 순서를 밟으면
   * 프레임워크가 떨어져 나간 옛 요소를 붙들고, **이 파일이 받아야 할 상태
   * 변화가 한 번도 안 온다** — 거절당해도 새로 열리지 않는다는 뜻이다.
   * 기억해 둔 것을 지워 다시 찾게 한다.
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

  // 화면이 다시 보이는 순간이 가장 잘 붙는 때다 — 휴대폰을 깨웠거나 탭으로
  // 돌아온 참이라 망이 막 살아난다. 기다리지 않고 바로 해 본다.
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && stalled !== null) {
      attempt(stalled);
    }
  });

  // 터널에서 나오는 그 순간이다. 프레임워크가 **포기한 뒤**일 때만 끼어든다 —
  // 아직 제 재시도 고리가 돌고 있으면 그쪽에 맡긴다(두 길이 동시에 회로를
  // 열려 들면 어느 쪽이 이겼는지 알 수 없게 된다).
  window.addEventListener('online', () => {
    if (stalled !== null) {
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
