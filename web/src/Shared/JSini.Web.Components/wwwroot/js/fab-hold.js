/**
 * 떠다니는 단추(`.jsini-shell__fab`)를 **오래 누르면** 그 단추의 설정 창이 열린다.
 *
 * [왜 필요한가]
 *
 * 휴대폰 귀퉁이에 떠 있는 단추 둘(메뉴 · 요청 등록)은 자리를 옮기거나 감출
 * 수 있는데, 그 조작이 **환경설정 화면 안에만** 있었다. 단추가 가리는 것을
 * 치우려면 메뉴를 열고 → 장례식장 → 환경설정 → 「휴대폰」 묶음까지 내려가야
 * 하고, 거기서 고른 결과는 **돌아와야** 보인다. 가리는 것이 눈앞에 있는데
 * 손이 거기까지 가는 길이 없었다.
 *
 * 그래서 **그 단추 자신이 제 설정을 들고 있게** 한다. 길게 누르면 그 단추의
 * 자리와 감추기가 그 자리에서 열린다 — 휴대폰에서 아이콘을 길게 눌러 나오는
 * 그 차림표와 같은 셈이다.
 *
 * [왜 JS 가 손짓을 직접 받나]
 *
 * Blazor Server 는 모든 이벤트가 웹소켓을 타고 서버로 왕복한다. 「500ms 동안
 * 손가락이 그대로 있었나」를 서버가 재려면 `pointerdown`·`pointerup`·
 * `pointermove` 가 전부 회선을 타야 하고, **회선이 느릴수록 길게 누른 것이
 * 짧게 누른 것으로 읽힌다** — 시간을 재는 쪽과 손가락이 떨어져 있으면 안 된다.
 * 브라우저가 혼자 재고, 서버는 **다 눌렀을 때 한 번** 듣는다
 * (`note-swipe.js`·`ask-swipe.js` 와 같은 까닭이고 같은 짜임이다).
 *
 * [손을 뗄 때 따라오는 「가짜 누름」을 막는다 — 창이 **곧바로 닫히던** 자리]
 *
 * 창이 떠도 손가락은 아직 단추 위에 있다. 떼면 브라우저가 터치를 마우스로
 * 옮긴 가짜 이벤트(`mousedown`·`mouseup`·`click`)를 쏜다. 그런데 그때는
 * **창이 이미 그 자리를 덮고 있어서, 그 가짜 누름의 과녁이 단추가 아니라
 * 창의 덮개(`dxbl-modal-root`)다.** DevExpress 는 그것을 「바깥을 눌렀다」로
 * 읽고 **방금 띄운 창을 그 자리에서 닫는다.**
 *
 * 실제로 그랬다 — 누르고 있는 동안에는 창이 멀쩡히 떠 있다가 **손을 떼는
 * 순간 사라졌다**(CDP 로 재 보니 떼고 7ms 뒤). 과녁이 단추가 아니므로
 * 「그 단추 위에서 난 누름만 삼킨다」로는 걸리지 않는다.
 *
 * 그래서 **`touchend` 를 취소한다.** 터치 이벤트를 취소하면 브라우저가 가짜
 * 마우스 이벤트를 **아예 만들지 않는다** — 창을 닫던 그 누름도, 단추의 원래
 * 일(메뉴 여닫기 · 요청 등록으로 가기)을 깨우던 그 누름도 함께 사라진다.
 * 취소하려면 처리기가 `passive` 가 아니어야 한다.
 *
 * 마우스로 오래 누른 경우에는 `touchend` 가 없으므로 뒤따라오는 `click` 을
 * 잡아내기(capture) 단계에서 멈춘다 — Blazor 의 처리기는 그보다 뒤(버블)에
 * 있어서 여기서 끊으면 닿지 않는다.
 *
 * [바로가기 메뉴를 막는다]
 *
 * 안드로이드 크롬은 길게 누른 것을 `contextmenu` 로도 쏜다. 막지 않으면 우리
 * 창 위로 브라우저의 「새 탭에서 열기」 차림표가 겹쳐 뜬다.
 *
 * [한 번만 건다]
 *
 * 거는 자리가 `document` 라 단추가 다시 그려져도(메뉴가 여닫히면 그림이 ☰ ↔ ✕
 * 로 바뀐다) 걸어 둔 것이 살아 있다. 요청 등록 단추는 **화면에 따라 사라졌다
 * 나타나므로**, 단추에 직접 걸면 그때마다 다시 걸어야 한다.
 */

/** 이만큼 누르고 있으면 길게 누른 것이다(ms). 휴대폰 운영체제들이 쓰는 값이다. */
const HOLD_MS = 500;

/** 이만큼 움직이면 누르던 것이 아니라 끌던 것이다(px). */
const SLOP = 10;

/** 손을 뗀 뒤 이 시간 안에 오는 누름 하나는 삼킨다(ms). 마우스 갈래에만 쓴다. */
const SWALLOW_MS = 500;

/** 이미 걸었는가. 두 벌이 겹치면 창이 두 번 열린다. */
let attached = false;

/**
 * 알려 줄 상대. **늘 마지막에 건 것으로 바꾼다.**
 *
 * 셸의 레이아웃은 **업무를 옮길 때마다 새로 생긴다**(`MainLayout` 의
 * `Tabs.CanShow` 머리말). 그러면 이 창도 새로 생기고 앞의 것은 버려지는데,
 * ES 모듈은 한 번 실리면 그대로라 아래 `attached` 가 참인 채다 — 걸어 둔 것을
 * 그대로 두고 **상대만 바꿔 주지 않으면** 버려진 쪽을 계속 부르게 된다.
 * 그 호출은 `There is no tracked object with id…` 로 조용히 떨어지고,
 * 사람 눈에는 **업무를 한 번 옮긴 뒤부터 길게 눌러도 아무 일이 없는** 것으로
 * 보인다(첫 업무에서는 멀쩡하다).
 */
let host = null;

export function attachFabHold(dotnet) {
  host = dotnet;

  if (attached) return;
  attached = true;

  /** 지금 누르고 있는 단추. 없으면 `null`. */
  let target = null;
  let timer = 0;
  let startX = 0;
  let startY = 0;

  /**
   * 길게 눌러 창을 띄운 그 단추. **손을 뗄 때까지** 들고 있는다 —
   * 떼면서 오는 가짜 누름을 막을지가 이것으로 갈린다.
   */
  let pressed = null;

  /** 그 손을 뗀 시각. 마우스 갈래에서 뒤따라오는 `click` 을 이것으로 막는다. */
  let releasedAt = 0;

  /** 어느 단추인가. 둘 다 아니면 `null` — 그러면 아무 일도 하지 않는다. */
  const kindOf = (el) =>
    el.classList.contains('jsini-shell__fab--help') ? 'help'
      : el.classList.contains('jsini-shell__fab--menu') ? 'menu'
        : null;

  const clear = () => {
    if (timer) {
      clearTimeout(timer);
      timer = 0;
    }

    if (target) {
      target.classList.remove('is-held');
      target = null;
    }
  };

  const fire = () => {
    timer = 0;

    const el = target;
    target = null;

    if (!el) return;

    el.classList.remove('is-held');

    const kind = kindOf(el);
    if (!kind) return;

    pressed = el;

    // 손끝에 한 번 알린다. 창이 뜨기까지 회로를 한 번 왕복하므로, 이것이
    // 없으면 그 사이가 「길게 눌러도 아무 일이 없다」로 읽혀 손을 뗀다.
    // 지원하지 않는 기기(아이폰)에서는 조용히 넘어간다.
    try { navigator.vibrate?.(15); } catch { /* 막아 둔 브라우저가 있다 */ }

    host?.invokeMethodAsync('OpenForAsync', kind);
  };

  document.addEventListener('pointerdown', (e) => {
    clear();

    // 마우스는 왼쪽 단추만. 오른쪽은 아래 `contextmenu` 가 받는다.
    if (e.pointerType === 'mouse' && e.button !== 0) return;

    const el = e.target instanceof Element
      ? e.target.closest('.jsini-shell__fab')
      : null;

    if (!el || !kindOf(el)) return;

    target = el;
    startX = e.clientX;
    startY = e.clientY;

    // 누르고 있는 동안의 표시. 이것이 없으면 500ms 가 「먹통」으로 읽힌다.
    el.classList.add('is-held');

    timer = setTimeout(fire, HOLD_MS);
  }, true);

  document.addEventListener('pointermove', (e) => {
    if (!target) return;

    if (Math.abs(e.clientX - startX) > SLOP || Math.abs(e.clientY - startY) > SLOP) {
      clear();
    }
  }, true);

  const release = () => {
    if (pressed) {
      releasedAt = Date.now();
    }

    clear();
  };

  document.addEventListener('pointerup', release, true);
  document.addEventListener('pointercancel', release, true);

  // **창이 곧바로 닫히던 자리다**(머리말). 터치를 취소하면 브라우저가 가짜
  // 마우스 이벤트를 아예 만들지 않아, 창을 닫던 누름도 단추의 원래 일을
  // 깨우던 누름도 함께 사라진다. `passive: false` 라야 취소가 먹는다.
  //
  // 터치 이벤트의 **과녁은 손을 처음 댄 자리**다. 창이 그 위를 덮어도
  // 과녁이 그 단추 그대로라, 가짜 누름과 달리 여기서는 가릴 수 있다.
  document.addEventListener('touchend', (e) => {
    if (!pressed) return;

    pressed = null;
    releasedAt = 0;
    e.preventDefault();
  }, { capture: true, passive: false });

  // 마우스로 오래 누른 갈래. `touchend` 가 없으므로 진짜 `click` 이 온다.
  document.addEventListener('click', (e) => {
    if (!releasedAt || Date.now() - releasedAt > SWALLOW_MS) return;

    releasedAt = 0;
    pressed = null;
    e.stopPropagation();
    e.preventDefault();
  }, true);

  // 안드로이드의 길게 누르기와 데스크톱의 오른쪽 누름. 브라우저 차림표가
  // 우리 창을 덮지 않게 막는다.
  document.addEventListener('contextmenu', (e) => {
    if (e.target instanceof Element && e.target.closest('.jsini-shell__fab')) {
      e.preventDefault();
    }
  });
}
