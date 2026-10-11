/**
 * 빠른 지시(AiAsk) 카드 — **오른쪽으로 밀어 사용자 확인 완료**.
 *
 * [왜 JS 가 손짓을 직접 받나]
 *
 * Blazor Server 는 모든 이벤트가 웹소켓을 타고 서버로 왕복한다. 손가락을
 * 끄는 동안 `pointermove` 는 초당 수십 번 온다 — 그것을 서버로 보내면
 * 카드가 손가락을 몇 백 ms 씩 늦게 따라오고, 회선이 흔들리는 자리
 * (이 화면의 전제다)에서는 아예 멈춘다. **끄는 동안은 브라우저 혼자 그리고**,
 * 서버는 다 밀고 난 뒤 「이 건을 확인 처리해 달라」 한 번만 듣는다.
 *
 * [왜 클래스가 아니라 인라인 CSS 변수로 그리나]
 *
 * 이 목록은 5초마다 다시 그려진다. Blazor 가 카드의 `class` 를 제 손으로
 * 쥐고 있어서, 손짓 중에 그 갱신이 한 번 끼면 **JS 가 붙인 클래스가
 * 소리 없이 지워진다** — 카드가 밀린 자리에 굳는다.
 *
 * 그래서 움직이는 값을 전부 인라인 사용자 지정 속성으로 싣는다. Blazor 는
 * `style` 을 아예 그리지 않으므로(카드에 `style` 속성이 없다) 그 자리는
 * 건드리지 않는다.
 *
 *   --pm-ask-swipe     밀린 거리(px). 카드 본문이 그만큼 오른쪽으로 가고
 *                      왼쪽의 확인 띠가 그만큼 열린다.
 *   --pm-ask-swipe-ms  그 둘의 전환 시간. 끄는 동안은 0(손가락을 그대로
 *                      따라와야 한다), 손을 뗀 뒤에만 시간이 붙는다.
 *   --pm-ask-ready     문턱을 넘었나(0/1). 확인 띠의 ✓ 가 이 값으로 커진다.
 *   --pm-ask-alive     카드가 살아 있나(1) 사라지는 중인가(0).
 *
 * [세로 스크롤을 뺏지 않는다]
 *
 * 카드에 `touch-action: pan-y` 를 건다(projmng.css). 세로로 끌면 브라우저가
 * 제 스크롤을 하고 우리에게는 `pointercancel` 이 온다. 가로로 끌 때만 우리
 * 차례다 — 그마저도 **처음 몇 px 로 방향을 가려** 세로가 이기면 손을 뗀다.
 */

/** 이미 손짓을 받고 있는 판. 같은 알맹이가 화면과 서랍에 함께 뜬다. */
const attached = new WeakSet();

/** 문턱. 좁은 카드(세 단)에서도 손이 닿는 거리라야 한다. */
const threshold = (width) => Math.min(88, Math.max(40, width * 0.38));

/** 방향을 가리기 전에 지켜보는 거리. 탭과 끌기를 여기서 가른다. */
const SLOP = 6;

/** 띠가 다 열리고 카드가 스러지기까지. CSS 의 전환 시간과 같아야 한다. */
const ACK_MS = 200;
const FADE_MS = 180;

export function attachSwipe(selector, dotnet) {
  const root = document.querySelector(selector);
  if (!root || attached.has(root)) return;

  attached.add(root);

  /** 지금 끌고 있는 카드. 없으면 `null`. */
  let card = null;
  let pointer = -1;
  let startX = 0;
  let startY = 0;
  let width = 1;
  let dx = 0;

  /** 방향을 가렸나 · 가로로 끄는 중인가. */
  let decided = false;
  let live = false;

  /** 확인 처리가 돌고 있다. 그 사이 다른 카드를 밀지 못하게 한다. */
  let acking = false;

  /** 마지막으로 끌기가 끝난 시각. 뒤따라오는 `click` 을 이것으로 막는다. */
  let draggedAt = 0;

  const set = (el, name, value) => el.style.setProperty(name, value);

  const drop = (el) => {
    el.style.removeProperty('--pm-ask-swipe');
    el.style.removeProperty('--pm-ask-swipe-ms');
    el.style.removeProperty('--pm-ask-ready');
    el.style.removeProperty('--pm-ask-alive');
    el.style.removeProperty('user-select');
  };

  const forget = () => {
    card = null;
    pointer = -1;
    decided = false;
    live = false;
    dx = 0;
  };

  root.addEventListener('pointerdown', (e) => {
    if (card || acking) return;
    if (e.pointerType === 'mouse' && e.button !== 0) return;

    // 밀 수 있는 카드에만 `data-swipe` 가 붙는다 — 아직 도는 건은 확인할
    // 것이 없다(화면이 `IsFinal` 로 가린다).
    const el = e.target.closest ? e.target.closest('.pm-ask__item[data-swipe]') : null;
    if (!el || !root.contains(el)) return;

    card = el;
    pointer = e.pointerId;
    startX = e.clientX;
    startY = e.clientY;
    width = el.clientWidth || 1;
    decided = false;
    live = false;
    dx = 0;

    // 여기서 `preventDefault` 하지 않는다. 그냥 누른 것일 수 있고, 그때는
    // 평소대로 카드가 열려야 한다.
  });

  root.addEventListener('pointermove', (e) => {
    if (!card || e.pointerId !== pointer) return;

    const mx = e.clientX - startX;
    const my = e.clientY - startY;

    if (!decided) {
      if (Math.abs(mx) < SLOP && Math.abs(my) < SLOP) return;

      decided = true;

      // 세로가 이겼거나 왼쪽으로 끈다 — 우리 일이 아니다. 왼쪽 밀기에
      // 아무것도 걸지 않은 것은 **되돌릴 길이 없는 동작을 양쪽에 두지
      // 않으려는 것**이다(확인은 화면에서 되돌리지 못한다).
      if (mx <= 0 || Math.abs(my) > Math.abs(mx)) {
        forget();
        return;
      }

      live = true;
      card.setPointerCapture?.(pointer);
      set(card, 'user-select', 'none');
      set(card, '--pm-ask-swipe-ms', '0ms');
    }

    if (!live) return;

    // 가로로 끄는 것이 확정된 뒤에만 막는다 — 그래야 세로 스크롤이 산다.
    e.preventDefault();

    dx = Math.max(0, Math.min(mx, width));
    set(card, '--pm-ask-swipe', `${dx}px`);
    set(card, '--pm-ask-ready', dx >= threshold(width) ? '1' : '0');
  }, { passive: false });

  const release = (e) => {
    if (!card || e.pointerId !== pointer) return;

    const el = card;
    const moved = live;
    const done = live && dx >= threshold(width) && e.type === 'pointerup';

    forget();
    el.style.removeProperty('user-select');

    // 끌지 않았으면 탭이다. 손댄 것이 없으니 그대로 둔다 — `click` 이
    // 이어서 카드를 연다.
    if (!moved) return;

    draggedAt = Date.now();
    set(el, '--pm-ask-swipe-ms', `${ACK_MS}ms`);

    if (!done) {
      set(el, '--pm-ask-swipe', '0px');
      set(el, '--pm-ask-ready', '0');
      return;
    }

    commit(el);
  };

  root.addEventListener('pointerup', release);
  root.addEventListener('pointercancel', release);

  /**
   * 문턱을 넘겼다. **띠가 카드를 다 덮고 나서** 스러지게 한다 — 그 사이에
   * 「무엇이 일어났는지」(✓ 확인 완료)를 읽을 수 있다. 서버는 그다음이다.
   */
  const commit = (el) => {
    acking = true;

    const key = Number(el.dataset.task);

    set(el, '--pm-ask-swipe', `${el.clientWidth}px`);
    set(el, '--pm-ask-ready', '1');

    setTimeout(() => {
      set(el, '--pm-ask-alive', '0');

      setTimeout(async () => {
        acking = false;

        try {
          await dotnet.invokeMethodAsync('ConfirmSwipedAsync', key);
        } catch (err) {
          // 회로가 닫혔거나 서버가 못 받았다. 카드를 제자리로 돌려 둔다 —
          // 스러진 채로 두면 **처리되지도 않은 건이 화면에서 사라진다.**
          drop(el);
        }
      }, FADE_MS);
    }, ACK_MS);
  };

  // 끌고 난 뒤에 따라오는 `click` 을 막는다. 안 막으면 손을 떼는 순간
  // 카드가 열려 **민 자리에 상세 창이 뜬다.**
  root.addEventListener('click', (e) => {
    if (Date.now() - draggedAt > 250) return;

    e.preventDefault();
    e.stopPropagation();
  }, true);
}
