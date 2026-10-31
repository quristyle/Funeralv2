/**
 * 빠른 지시·AI 작업 카드를 밀어서 확인 완료(오른쪽)·삭제(왼쪽)한다.
 * 끄는 동안은 브라우저가 혼자 그린다 — pointermove 를 서버로 보내면 손가락을 몇 백 ms 늦게 따라온다. 서버는 다 민 뒤 한 번만 듣는다.
 * 움직이는 값은 인라인 사용자 지정 속성으로 싣는다: Blazor 가 카드의 class 를 갈아 치우면 손짓 중에 JS 가 붙인 클래스가 지워진다.
 *   --pm-ask-swipe(부호 있는 거리) · --pm-ask-swipe-ms(끄는 동안 0) · --pm-ask-ready(문턱 넘음) · --pm-ask-alive(살아 있나)
 * 세로 스크롤은 touch-action: pan-y 가 브라우저에 넘긴다 — 가로가 확정된 뒤에만 막는다.
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

  /** 방향을 가렸나 · 가로로 끄는 중인가 · 어느 쪽인가(+1 확인완료 / -1 삭제). */
  let decided = false;
  let live = false;
  let dir = 0;

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
    dir = 0;
    dx = 0;
  };

  /** 그 방향으로 밀 수 있는 줄인가. */
  const allows = (el, d) =>
    d > 0 ? el.dataset.confirm !== '0' : el.dataset.delete === '1';

  root.addEventListener('pointerdown', (e) => {
    if (card || acking) return;
    if (e.pointerType === 'mouse' && e.button !== 0) return;

    // 밀 수 있는 카드에만 data-swipe 가 붙는다 — 확인할 것이 없는 건(아직 도는 건)은 화면이 가린다.
    const el = e.target.closest ? e.target.closest('.pm-ask__item[data-swipe]') : null;
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

    // preventDefault 하지 않는다 — 그냥 누른 것일 수 있고, 그때는 평소대로 카드가 열려야 한다.
  });

  root.addEventListener('pointermove', (e) => {
    if (!card || e.pointerId !== pointer) return;

    const mx = e.clientX - startX;
    const my = e.clientY - startY;

    if (!decided) {
      if (Math.abs(mx) < SLOP && Math.abs(my) < SLOP) return;

      decided = true;

      // 세로가 이겼다 — 스크롤이다.
      if (Math.abs(my) > Math.abs(mx)) {
        forget();
        return;
      }

      const d = mx > 0 ? 1 : -1;
      if (!allows(card, d)) {
        forget();
        return;
      }

      dir = d;
      live = true;
      card.setPointerCapture?.(pointer);
      set(card, 'user-select', 'none');
      set(card, '--pm-ask-swipe-ms', '0ms');
    }

    if (!live) return;

    // 가로로 끄는 것이 확정된 뒤에만 막는다 — 그래야 세로 스크롤이 산다.
    e.preventDefault();

    dx = dir > 0
      ? Math.max(0, Math.min(mx, width))
      : Math.min(0, Math.max(mx, -width));

    set(card, '--pm-ask-swipe', `${dx}px`);
    set(card, '--pm-ask-ready', Math.abs(dx) >= threshold(width) ? '1' : '0');
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
    // 이어서 카드를 연다.
    if (!moved) return;

    draggedAt = Date.now();
    set(el, '--pm-ask-swipe-ms', `${ACK_MS}ms`);

    if (!done) {
      set(el, '--pm-ask-swipe', '0px');
      set(el, '--pm-ask-ready', '0');
      return;
    }

    commit(el, way);
  };

  root.addEventListener('pointerup', release);
  root.addEventListener('pointercancel', release);

  /** 문턱을 넘겼다. 띠가 카드를 다 덮은 뒤에 스러지게 해 「무엇이 일어났는지」를 읽을 수 있게 한다. 서버는 그다음이다. */
  const commit = (el, way) => {
    acking = true;

    const key = Number(el.dataset.task);
    const w = el.clientWidth || width;

    set(el, '--pm-ask-swipe', `${way * w}px`);
    set(el, '--pm-ask-ready', '1');

    setTimeout(() => {
      set(el, '--pm-ask-alive', '0');

      setTimeout(async () => {
        acking = false;

        try {
          const methodName = way > 0 ? 'ConfirmSwipedAsync' : 'DeleteSwipedAsync';
          const stays = await dotnet.invokeMethodAsync(methodName, key);
          if (stays) {
            drop(el);
          }
        } catch (err) {
          // 회로가 닫혔거나 서버가 못 받았다. 카드를 제자리로 돌려 둔다 — 스러진 채 두면 처리되지 않은 건이 사라진다.
          drop(el);
        }
      }, FADE_MS);
    }, ACK_MS);
  };

  // 끌고 난 뒤의 click 을 막는다 — 안 막으면 손을 뗀 순간 민 자리에 상세 창이 뜬다.
  root.addEventListener('click', (e) => {
    if (Date.now() - draggedAt > 250) return;

    e.preventDefault();
    e.stopPropagation();
  }, true);

  // 완료 단추(데스크톱)는 오른쪽 끝까지 민 것과 같다 — 띠가 열리는 모습까지 같게 commit 으로 보낸다.
  root.addEventListener('click', (e) => {
    const btn = e.target.closest ? e.target.closest('.pm-ask__done') : null;
    if (!btn || !root.contains(btn)) return;

    e.preventDefault();
    e.stopPropagation();

    const el = btn.closest('.pm-ask__item[data-swipe]');
    if (!el || card || acking || !allows(el, 1)) return;

    width = el.clientWidth || 1;
    set(el, '--pm-ask-swipe-ms', `${ACK_MS}ms`);
    commit(el, 1);
  });
}
