/**
 * 연결이 끊겼을 때 뜨는 **띠**의 손놀림.
 *
 * 띠 자체는 셸이 그린다(`App.razor` 의 `#components-reconnect-modal`).
 * 프레임워크는 거기에 상태 클래스만 갈아 끼우고, 모양은 app.css 가 쥔다.
 * **여기서 맡는 것은 클래스만으로는 안 되는 넷이다.**
 *
 * [1] 누름
 *
 * 회로가 끊긴 뒤에 도는 코드라 C# 처리기는 아무 소용이 없다 — 누르는 순간
 * 서버로 갈 길이 없다. `window.Blazor.reconnect()` · `resumeCircuit()` 은
 * .NET 10 에서 공개된 전역이라 브라우저 안에서 그대로 부를 수 있다.
 *
 * [2] 서버에 하던 일이 남아 있지 않으면 **묻지 않고 새로고침한다**
 *
 * 서버가 다시 시작됐거나 너무 오래 끊겨 있으면 재연결이 거절된다(rejected).
 * 그때 화면은 이미 죽어 있다 — 단추도 메뉴도 아무것도 안 듣는다.
 *
 * [여기에 창이 있었다 — 2026-10-11 에 걷어냈다]
 *
 * 한동안 이 자리에서 화면을 덮는 대화상자를 띄우고 「5초 뒤에 화면을 새로
 * 엽니다」를 센 다음 열었다. 새로고침·잠시 멈추기를 사람이 고를 수 있었다.
 *
 * **고를 것이 없는 선택이었다.** 거절당했다는 것은 하던 일이 서버에 없다는
 * 뜻이라 어느 쪽을 눌러도 끝은 새로고침 하나다. 그 창이 실제로 한 일은
 * 죽은 화면 앞에서 다섯을 세는 것뿐이었고, 그동안 사람은 아무것도 못 했다.
 * 그래서 **세지 않고 그 자리에서 연다.** 묻는 창은 없다.
 *
 * 적어 둔 것이 사라지지 않는가 — 그 일은 이 자리가 아니라 화면이 맡는다.
 * 글을 길게 적는 자리(헬프데스크 요청·프로젝트 문의 …)는 이미 적는 동안
 * 브라우저에 임시로 담아 두고 새로 열린 뒤에 되살린다(`request-draft.js`
 * 따위). 새로고침을 미룬다고 그 밖의 입력이 돌아오지는 않는다.
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
 * 멈춰 선 뒤에도 30초마다, **화면이 다시 보이는 순간**, 그리고 **띠를
 * 눌렀을 때** 조용히 한 번 더 해 본다. 잠자던 휴대폰을 깨우면 대개 그
 * 자리에서 이어진다.
 *
 * [4] 끊긴 동안 **화면을 덮지 않는다** — 숨죽임 · 귀띔
 *
 * [0.9초로는 터널을 못 지난다]
 *
 * 한동안 여기는 0.9초짜리 투명 구간 하나였다. 회선이 한 번 튀는 것은 그 안에
 * 끝나니 사람은 아무것도 못 보고 하던 일을 이었다. 그런데 **터널은 0.9초가
 * 아니다** — 지하 구간 하나가 수십 초이고 승강기·주차장·건물 안쪽도 몇 초씩
 * 끊긴다. 그때마다 화면이 통째로 어두워지고 상자가 떴다. 서버는 멀쩡하고 곧
 * 저절로 이어지는데도 사람에게는 매번 사고로 보였다.
 *
 * 그래서 둘로 나눈다.
 *
 *   1. **숨죽임**(`--hush`, 2초) — 아무것도 안 보이고 아무것도 안 막는다.
 *      회선이 한 번 튄 것은 여기서 끝난다.
 *   2. **귀띔**(`--hint`) — 화면을 덮지 않는다. 바탕도 안 어둡고 위쪽에 작은
 *      띠 하나만 뜬다. **읽고 굴리는 것은 그대로 되고 누름만 삼킨다.**
 *
 * 그 위는 없다. 예전에는 「사람이 골라야 하는 자리」에서 상자까지 올라갔는데,
 * **고를 것이 없다는 것**이 위 [2]에서 밝혀졌다. 끊겨 있는 동안 끝까지 띠
 * 하나이고, 이어지면 띠가 사라지고, 하던 일이 서버에 없으면 새로 열린다.
 *
 * [삼킨 누름은 **삼켰다고 말한다**]
 *
 * 덮개를 걷었으니 끊긴 동안 단추가 그대로 보인다. 그런데 회로가 없으므로
 * 눌러도 아무 일이 없다 — 저장을 누르고 저장된 줄 아는 것이 이 바꿈에서
 * 가장 위험한 자리다. 그래서 누름을 **잡는 단계에서 삼키고**(capture),
 * 삼킨 자리에서 곧바로 귀띔으로 올라가 띠를 한 번 흔든다. 아무 일도 안
 * 일어난 것처럼 보이는 일은 없다.
 *
 * 다시 이어졌을 때 우리가 하는 일은 **없다.** 프레임워크가 `hide` 를 붙이고
 * 그 규칙이 띠를 끈다. 새로고침하지 않으므로 화면은 끊기기 전 그대로다.
 *
 * ── 띠를 **붙들어 두지 않는다** ───────────────────────────────
 *
 * [여기서 한 번 크게 밟았다 — 「상자는 뜨는데 단추가 하나도 안 눌린다」]
 *
 * 이 파일은 한동안 기동할 때 `getElementById` 를 **한 번만** 해서 그 요소에
 * 손을 걸어 두었다. 그런데 **향상된 이동(enhanced navigation)은 문서를 다시
 * 파싱하지 않고 <body> 를 기워 맞추면서 이 <div> 를 통째로 갈아 끼운다.**
 * 그러면
 *
 *   · 우리 손놀림은 **떨어져 나간 옛 요소**에 남는다 — 누름도, 자동 재시도도,
 *     상태 변화 수신도 전부 죽는다.
 *   · 그런데 프레임워크는 끊길 때마다 `getElementById` 를 새로 하므로
 *     **표시는 멀쩡히 뜬다.**
 *
 * 증상이 「뜨는데 눌리지 않는다」 하나로 보이고, 새로고침을 하고 나면
 * (문서가 새로 파싱되므로) 멀쩡해져서 재현조차 어렵다.
 * 실제로 헤드리스 크롬으로 리스너를 세어 보고야 잡았다.
 *
 * 그래서 이제 **요소를 기억하지 않는다.**
 *
 *   · 누름은 `document` 에 한 번만 걸고, 누른 자리에서 요소를 찾는다.
 *     요소가 몇 번 갈려도 이 길은 끊기지 않는다.
 *   · 상태 변화 이벤트는 **거품이 일지 않아**(`bubbles: false`) 그 요소에
 *     직접 걸어야 한다. 그래서 `enhancedload` 마다 지금 있는 것에 다시
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

  // 우리 것. 프레임워크는 이 이름을 모르므로 우리가 붙이고 우리가 뗀다.
  const HUSH = 'jsini-reconnect--hush';
  const HINT = 'jsini-reconnect--hint';
  const NUDGE = 'jsini-reconnect--nudge';

  // 띠에 손을 걸어 두었다는 표시. 향상된 이동으로 요소가 갈리면 새 요소에는
  // 이 표시가 없으므로 다시 건다.
  const BOUND = '__jsiniReconnectBound';

  // 멈춰 선 뒤 스스로 다시 해 보는 주기.
  const AUTO_RETRY_MS = 30000;

  // 숨죽임이 귀띔으로 바뀌기까지. 이 안에 이어지면 **아무것도 안 보인다.**
  // 회선이 한 번 튀는 것은 대개 1초 안에 돌아오므로 그 두 배를 둔다.
  const HUSH_MS = 2000;

  // 흔든 자국을 떼기까지(app.css 의 420ms 보다 조금 길게).
  const NUDGE_MS = 500;

  /** 프레임워크가 포기한 뒤 우리가 이어받을 일. 'reconnect' · 'resume' · null. */
  let stalled = null;
  let autoTimer = null;
  let inFlight = false;

  let reloading = false;

  /** 조용한 구간에 있는가(숨죽임 또는 귀띔). 누름을 삼킬지 여기서 가린다. */
  let quieting = false;
  let hushTimer = null;
  let nudgeTimer = null;

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

  // ── 조용한 구간 — 숨죽임 · 귀띔(머리말 [4]) ───────────────────

  /**
   * 조용한 구간을 **연다**. 이미 열려 있으면 다시 시작하지 않는다.
   *
   * 여는 자리가 하나가 아니다 — 프레임워크는 다시 잇는 동안 `show` 와
   * `retrying` 을 번갈아 보내므로, 올 때마다 다시 시작하면 **숨죽임이 영영
   * 안 끝난다.** 그래서 처음 한 번만 사다리를 놓는다.
   *
   * 다만 클래스는 매번 입힌다. 향상된 이동으로 요소가 갈리면 새 요소에는
   * 아무 표시가 없기 때문이다(머리말 — 요소를 붙들지 않는다).
   */
  function beginQuiet() {
    const box = dialog();
    if (!box) {
      return;
    }

    if (quieting) {
      // 사다리는 그대로 두고 지금 단계만 다시 입힌다.
      box.classList.add(hushTimer !== null ? HUSH : HINT);
      return;
    }

    quieting = true;
    box.classList.remove(HINT, NUDGE);
    box.classList.add(HUSH);

    hushTimer = setTimeout(() => {
      hushTimer = null;
      toHint();
    }, HUSH_MS);
  }

  /** 숨죽임을 접고 귀띔으로 올라간다. 시간이 차거나 누름을 삼켰을 때. */
  function toHint() {
    if (!quieting) {
      return;
    }

    if (hushTimer !== null) {
      clearTimeout(hushTimer);
      hushTimer = null;
    }

    const box = dialog();
    if (box) {
      box.classList.remove(HUSH);
      box.classList.add(HINT);
    }
  }

  /** 조용한 구간을 **닫는다**. 다시 이어졌거나 화면을 새로 열 때뿐이다. */
  function endQuiet() {
    quieting = false;

    if (hushTimer !== null) {
      clearTimeout(hushTimer);
      hushTimer = null;
    }

    if (nudgeTimer !== null) {
      clearTimeout(nudgeTimer);
      nudgeTimer = null;
    }

    const box = dialog();
    if (box) {
      box.classList.remove(HUSH, HINT, NUDGE);
    }
  }

  /**
   * 삼킨 누름을 알린다 — 띠를 한 번 흔든다.
   *
   * 숨죽임 중이었다면 **그 자리에서 귀띔으로 올린다.** 손을 댔다는 것은 더
   * 숨길 때가 아니라는 뜻이다.
   */
  function nudge() {
    toHint();

    const box = dialog();
    if (!box) {
      return;
    }

    if (nudgeTimer !== null) {
      clearTimeout(nudgeTimer);
    }

    // 뗐다가 다시 붙여야 애니메이션이 처음부터 돈다.
    box.classList.remove(NUDGE);
    void box.offsetWidth;
    box.classList.add(NUDGE);

    nudgeTimer = setTimeout(() => {
      nudgeTimer = null;
      const now = dialog();
      if (now) {
        now.classList.remove(NUDGE);
      }
    }, NUDGE_MS);
  }

  // ── 새로고침 ──────────────────────────────────────────────────

  /**
   * 화면을 새로 연다. **묻지 않는다**(머리말 [2]).
   *
   * 뒤에 보험을 하나 둔다. `location.reload()` 가 먹지 않는 판이 실제로
   * 있었고(향상된 이동으로 죽은 손놀림이 그 까닭이었지만, 그것 하나라고
   * 단정할 수 없다) 그때 화면은 **띠만 남은 채 굳은 것**으로 보인다.
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
   * 성공했을 때 여기서 하는 일은 없다. 프레임워크가 `onConnectionUp` 에서
   * 띠를 끄고, 화면은 끊기기 전 그대로 남는다.
   *
   * **조용한 구간은 건드리지 않는다.** 하는 동안에도 보이는 것은 띠 하나다.
   */
  async function attempt(kind) {
    const blazor = window.Blazor;
    if (inFlight || !blazor || !blazor.reconnect || !blazor.resumeCircuit) {
      return;
    }

    inFlight = true;
    clearTimer();

    // 누른 것이 먹혔다는 표시 — 숨죽임 중이었다면 띠를 꺼내 보여 준다.
    toHint();
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
          reloadNow();
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
  // 일곱 가지가 온다. 보이는 것은 어느 쪽이든 띠 하나뿐이라, 여기서 보는 것은
  // **클래스로 표현할 수 없는 것**만이다 — 프레임워크가 포기한 자리를
  // 이어받는 것과, 거절당했을 때 묻지 않고 새로 여는 것.
  function onStateChanged(event) {
    const detail = event.detail || {};
    const box = dialog();
    if (!box) {
      return;
    }

    // 끊겨 있는 동안은 **끝까지 조용한 구간**이다(머리말 [4]). 이어졌을 때만
    // 걷고, 하던 일이 서버에 없을 때는 걷기 전에 화면이 새로 열린다.
    if (detail.state === 'hide') {
      endQuiet();
    } else {
      beginQuiet();
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
        // 조용한 구간은 바로 위에서 `endQuiet()` 가 이미 걷었다.
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
   * 프레임워크가 떨어져 나간 옛 요소에 클래스를 칠하고, 화면에는
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

  /**
   * 조용한 동안의 누름을 **삼킨다**(머리말 [4]).
   *
   * 덮개를 걷었으므로 단추가 그대로 보이고 눌리기까지 한다 — 그런데 회로가
   * 없어 아무 데도 닿지 않는다. **저장을 누르고 저장된 줄 아는 것**이 이
   * 바꿈에서 가장 위험한 자리라, 잡는 단계에서 끊고 띠를 흔들어 알린다.
   *
   * 띠 자신은 통과시킨다 — 그쪽은 회로 없이 도는 우리 손놀림이다.
   */
  function swallow(event) {
    if (!quieting) {
      return;
    }

    const target = event.target;
    if (target && typeof target.closest === 'function'
        && target.closest('#' + DIALOG_ID)) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    nudge();
  }

  document.addEventListener('click', swallow, true);
  document.addEventListener('submit', swallow, true);

  // 누름은 **문서에 한 번만** 건다. 요소가 몇 번 갈려도 이 길은 안 끊긴다.
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

    // 띠를 눌렀다 — 30초를 기다리지 않고 그 자리에서 한 번 더 해 본다.
    // 되살리기를 기다리는 중이었으면 그쪽으로 간다.
    attempt(stalled === 'resume' ? 'resume' : 'reconnect');
  });

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
