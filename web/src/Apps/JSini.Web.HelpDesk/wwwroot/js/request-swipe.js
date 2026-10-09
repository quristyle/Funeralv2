/**
 * 요청 목록(휴대폰 카드) — **밀어서 접수 · 삭제**.
 *
 *   오른쪽으로 민다   접수 (왼쪽 가장자리에서 초록 띠가 열린다)
 *   왼쪽으로 민다     삭제 (오른쪽 가장자리에서 빨간 띠가 열린다)
 *
 * **「대기」인 줄만 민다.** 밀 수 있는 줄에만 `data-accept` · `data-remove` 가
 * 붙고(화면이 권한까지 함께 본다 · `RequestCards`), 붙지 않은 방향으로 끌면
 * 아무 일도 일어나지 않는다 — 한쪽만 붙는 자리가 실제로 있다(시스템관리자가
 * 아닌 담당자에게는 접수만 붙는다).
 *
 * [왜 JS 가 손짓을 직접 받나]
 *
 * Blazor Server 는 모든 이벤트가 웹소켓을 타고 서버로 왕복한다. 손가락을
 * 끄는 동안 `pointermove` 는 초당 수십 번 오는데 그것을 서버로 보내면 줄이
 * 손가락을 몇 백 ms 씩 늦게 따라온다. **끄는 동안은 브라우저 혼자 그리고**,
 * 서버는 다 밀고 난 뒤 「이 건을 접수(삭제)해 달라」 한 번만 듣는다.
 * 같은 판단을 빠른 지시 카드에서 먼저 했다(`ProjMng` 의 `ask-swipe.js`).
 *
 * [왜 클래스가 아니라 인라인 CSS 변수로 그리나]
 *
 * 이 목록은 「더보기」·조회·상태 갱신으로 다시 그려지고, 그때 Blazor 가 줄의
 * `class` 를 제 손으로 쥐고 있다 — 손짓 중에 그 갱신이 한 번 끼면 **JS 가
 * 붙인 클래스가 소리 없이 지워져 줄이 밀린 자리에 굳는다.** 그래서 움직이는
 * 값을 전부 인라인 사용자 지정 속성으로 싣는다. 줄에는 `style` 속성이 없어서
 * (`RequestCards`) Blazor 가 그 자리를 아예 그리지 않는다.
 *
 *   --hd-swipe       밀린 거리(px). **부호가 있다** — 양수가 오른쪽(접수).
 *   --hd-swipe-ms    전환 시간. 끄는 동안은 0(손가락을 그대로 따라와야 한다),
 *                    손을 뗀 뒤에만 시간이 붙는다.
 *   --hd-swipe-ready 문턱을 넘었나(0/1). 띠의 글자가 이 값으로 또렷해진다.
 *
 * [세로 스크롤을 뺏지 않는다]
 *
 * 줄 본문에 `touch-action: pan-y` 를 건다(helpdesk.css). 세로로 끌면 브라우저가
 * 제 스크롤을 하고 우리에게는 `pointercancel` 이 온다 — 이 목록은 쪽이 통째로
 * 구르는 자리라(`.hd-manage-cont--phone`) 그것을 막으면 목록을 못 넘긴다.
 * 처음 몇 px 로 방향을 가려 세로가 이기면 손을 뗀다.
 *
 * [묻지 않는다]
 *
 * 문턱을 넘기면 **그대로 처리된다.** 확인 창은 두지 않는다 — 밀어서 하는 일을
 * 만든 까닭이 왕복을 없애려는 것인데, 손짓마다 창이 뜨면 왕복만 짧아진 것이
 * 된다. 비낀 손가락을 막는 것은 문턱과 방향 잠금, 그리고 **밀 수 있는 줄을
 * 「대기」로 좁혀 둔 것**이다(`RequestCards`).
 *
 * 그래도 띠는 **열어 둔 채로** C# 을 부른다. 서버에 다녀오는 동안 줄이 제자리로
 * 돌아가 버리면 **무엇을 민 것인지가 화면에서 사라진다.** 답이 오면 그때
 * 닫는다 — 지워진 줄은 그 사이에 Blazor 가 이미 걷어냈다.
 */

/** 이미 손짓을 받고 있는 판. 두 번 걸면 한 번 민 것이 두 번 처리된다. */
const attached = new WeakSet();

/** 방향을 가리기 전에 지켜보는 거리. 탭과 끌기를 여기서 가른다. */
const SLOP = 6;

/** 손을 뗀 뒤 줄이 제자리로 돌아가는 시간. CSS 의 전환 시간과 같아야 한다. */
const BACK_MS = 180;

/**
 * 문턱. 줄 폭의 1/4 이되 손가락이 닿는 거리(48px)와 한 손 엄지가 가는 거리
 * (96px) 사이에 둔다 — 390px 기기에서 96px 다.
 */
const threshold = (width) => Math.min(96, Math.max(48, width * 0.25));

export function attachSwipe(selector, dotnet) {
  const root = document.querySelector(selector);
  if (!root || attached.has(root)) return;

  attached.add(root);

  /** 지금 끌고 있는 줄. 없으면 `null`. */
  let card = null;
  let pointer = -1;
  let startX = 0;
  let startY = 0;
  let width = 1;
  let dx = 0;

  /** 방향을 가렸나 · 가로로 끄는 중인가 · 어느 쪽인가(+1 접수 / -1 삭제). */
  let decided = false;
  let live = false;
  let dir = 0;

  /** 서버에 다녀오는 중이다. 그 사이 다른 줄을 밀지 못하게 한다. */
  let busy = false;

  /** 마지막으로 끌기가 끝난 시각. 뒤따라오는 `click` 을 이것으로 막는다. */
  let draggedAt = 0;

  const set = (el, name, value) => el.style.setProperty(name, value);

  /** 민 자취를 지운다. 줄이 그린 적 없는 모습으로 돌아간다. */
  const drop = (el) => {
    el.style.removeProperty('--hd-swipe');
    el.style.removeProperty('--hd-swipe-ms');
    el.style.removeProperty('--hd-swipe-ready');
    el.style.removeProperty('user-select');
  };

  /** 제자리로 되돌린다 — 전환 시간을 주어 밀린 것이 닫히는 것이 보이게. */
  const back = (el) => {
    set(el, '--hd-swipe-ms', `${BACK_MS}ms`);
    set(el, '--hd-swipe', '0px');
    set(el, '--hd-swipe-ready', '0');
    el.style.removeProperty('user-select');
  };

  const forget = () => {
    card = null;
    pointer = -1;
    decided = false;
    live = false;
    dir = 0;
    dx = 0;
  };

  /** 그 방향으로 밀 수 있는 줄인가. 화면이 상태와 권한을 보고 붙여 둔다. */
  const allows = (el, d) =>
    d > 0 ? el.dataset.accept === '1' : el.dataset.remove === '1';

  root.addEventListener('pointerdown', (e) => {
    if (card || busy) return;
    if (e.pointerType === 'mouse' && e.button !== 0) return;

    const el = e.target.closest ? e.target.closest('.hd-req-list__item[data-swipe]') : null;
    if (!el || !root.contains(el)) return;

    card = el;
    pointer = e.pointerId;
    startX = e.clientX;
    startY = e.clientY;
    width = el.clientWidth || 1;
    decided = false;
    live = false;
    dir = 0;
    dx = 0;

    // 여기서 `preventDefault` 하지 않는다. 그냥 누른 것일 수 있고, 그때는
    // 평소대로 상세가 열려야 한다.
  });

  root.addEventListener('pointermove', (e) => {
    if (!card || e.pointerId !== pointer) return;

    const mx = e.clientX - startX;
    const my = e.clientY - startY;

    if (!decided) {
      if (Math.abs(mx) < SLOP && Math.abs(my) < SLOP) return;

      decided = true;

      // 세로가 이겼다 — 목록을 굴리려는 것이다. 우리 일이 아니다.
      if (Math.abs(my) > Math.abs(mx)) {
        forget();
        return;
      }

      dir = mx > 0 ? 1 : -1;

      // 그 방향에 걸린 것이 없는 줄이다(「대기」가 아니거나 권한이 없다).
      if (!allows(card, dir)) {
        forget();
        return;
      }

      live = true;
      card.setPointerCapture?.(pointer);
      set(card, 'user-select', 'none');
      set(card, '--hd-swipe-ms', '0ms');
    }

    if (!live) return;

    // 가로로 끄는 것이 확정된 뒤에만 막는다 — 그래야 세로 스크롤이 산다.
    e.preventDefault();

    // **방향을 바꿔 가며 끌지 못한다.** 오른쪽으로 끌다 왼쪽으로 지나가면
    // 접수가 삭제로 바뀌는데, 그 둘은 되돌리는 값이 전혀 다르다.
    dx = dir > 0
      ? Math.max(0, Math.min(mx, width))
      : Math.min(0, Math.max(mx, -width));

    set(card, '--hd-swipe', `${dx}px`);
    set(card, '--hd-swipe-ready', Math.abs(dx) >= threshold(width) ? '1' : '0');
  }, { passive: false });

  const release = (e) => {
    if (!card || e.pointerId !== pointer) return;

    const el = card;
    const moved = live;
    const way = dir;
    const done = live && Math.abs(dx) >= threshold(width) && e.type === 'pointerup';

    forget();
    el.style.removeProperty('user-select');

    // 끌지 않았으면 탭이다. 손댄 것이 없으니 그대로 둔다 — `click` 이
    // 이어서 상세를 연다.
    if (!moved) return;

    draggedAt = Date.now();

    if (!done) {
      back(el);
      return;
    }

    commit(el, way);
  };

  root.addEventListener('pointerup', release);
  root.addEventListener('pointercancel', release);

  /**
   * 문턱을 넘겼다. 띠를 **끝까지 열어 둔 채** 서버에 보낸다 — 다녀오는
   * 동안에도 어느 줄을 민 것인지가 화면에 남아 있어야 한다.
   */
  const commit = async (el, way) => {
    busy = true;

    const id = Number(el.dataset.request);

    set(el, '--hd-swipe-ms', `${BACK_MS}ms`);
    set(el, '--hd-swipe', `${way * el.clientWidth}px`);
    set(el, '--hd-swipe-ready', '1');

    try {
      await dotnet.invokeMethodAsync('SwipedAsync', id, way > 0 ? 'accept' : 'remove');
    } catch (err) {
      // 회로가 닫혔거나 서버가 못 받았다. 줄을 제자리로 돌려 둔다 —
      // 열린 채로 두면 **처리되지도 않은 건이 처리된 것처럼 보인다.**
    } finally {
      busy = false;

      // 처리됐으면 그 줄은 이미 걷혔거나(삭제) 새 상태로 다시 그려졌다(접수).
      // 어느 쪽이든 열린 띠는 닫는다. 떨어져 나간 줄에 걸어도 해가 없다.
      back(el);
    }
  };

  // 끌고 난 뒤에 따라오는 `click` 을 막는다. 안 막으면 손을 떼는 순간
  // 상세 화면으로 떠나 **민 자리를 보지 못한다.**
  root.addEventListener('click', (e) => {
    if (Date.now() - draggedAt > 250) return;

    e.preventDefault();
    e.stopPropagation();
  }, true);
}
