/**
 * 알림함 카드 — **오른쪽으로 밀면 읽음, 왼쪽으로 밀면 삭제**.
 *
 * [왜 JS 가 손짓을 직접 받나]
 *
 * Blazor Server 는 모든 이벤트가 웹소켓을 타고 서버로 왕복한다. 손가락을
 * 끄는 동안 `pointermove` 는 초당 수십 번 온다 — 그것을 서버로 보내면
 * 카드가 손가락을 몇 백 ms 씩 늦게 따라오고, 회선이 흔들리면 아예 멈춘다.
 * **끄는 동안은 브라우저 혼자 그리고**, 서버는 다 밀고 난 뒤 「이 건을
 * 읽음/삭제로 해 달라」 한 번만 듣는다. 빠른 지시 카드(`ask-swipe.js`)와
 * 같은 까닭이고 같은 짜임이다.
 *
 * [왜 클래스가 아니라 인라인 CSS 변수로 그리나]
 *
 * 알림함은 손짓 한 번에 목록이 다시 그려지는 판이다(읽음 처리가 그 줄을
 * 뺀다). Blazor 가 카드의 `class` 를 제 손으로 쥐고 있어서, 손짓 중에
 * 그 갱신이 한 번 끼면 **JS 가 붙인 클래스가 소리 없이 지워진다** —
 * 카드가 밀린 자리에 굳는다. `style` 은 Blazor 가 아예 그리지 않으므로
 * (카드에 `style` 속성이 없다) 그 자리는 건드리지 않는다.
 *
 *   --note-swipe     밀린 거리(px). **부호가 있다** — 양수는 오른쪽(읽음),
 *                    음수는 왼쪽(삭제). 본문이 그만큼 가고 그쪽 띠가 그만큼
 *                    열린다(띠의 폭은 CSS 의 `max()` 가 이 값에서 뽑는다).
 *   --note-swipe-ms  그 둘의 전환 시간. 끄는 동안은 0(손가락을 그대로
 *                    따라와야 한다), 손을 뗀 뒤에만 시간이 붙는다.
 *   --note-ready     문턱을 넘었나(0/1). 띠의 그림과 글자가 이 값으로 또렷해진다.
 *   --note-alive     카드가 살아 있나(1) 사라지는 중인가(0).
 *
 * [세로 스크롤을 뺏지 않는다]
 *
 * 카드에 `touch-action: pan-y` 를 건다(app.css). 세로로 끌면 브라우저가
 * 제 스크롤을 하고 우리에게는 `pointercancel` 이 온다 — 알림함은 백 건이
 * 쌓이는 목록이라 **굴리는 것이 미는 것보다 잦다.** 가로로 끌 때만 우리
 * 차례고, 그마저도 처음 몇 px 로 방향을 가린다.
 */

/** 이미 손짓을 받고 있는 판. 서랍은 하나뿐이지만 두 번 걸지 않게 둔다. */
const attached = new WeakSet();

/** 문턱. 좁은 휴대폰 화면에서도 손이 닿는 거리라야 한다. */
const threshold = (width) => Math.min(96, Math.max(44, width * 0.3));

/** 방향을 가리기 전에 지켜보는 거리. 탭과 끌기를 여기서 가른다. */
const SLOP = 6;

/** 띠가 다 열리고 카드가 스러지기까지. CSS 의 전환 시간과 같아야 한다. */
const ACT_MS = 200;
const FADE_MS = 180;

export function attachNoteSwipe(selector, dotnet) {
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

  /** 처리가 돌고 있다. 그 사이 다른 카드를 밀지 못하게 한다. */
  let busy = false;

  /** 마지막으로 끌기가 끝난 시각. 뒤따라오는 `click` 을 이것으로 막는다. */
  let draggedAt = 0;

  const set = (el, name, value) => el.style.setProperty(name, value);

  const drop = (el) => {
    el.style.removeProperty('--note-swipe');
    el.style.removeProperty('--note-swipe-ms');
    el.style.removeProperty('--note-ready');
    el.style.removeProperty('--note-alive');
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
    if (card || busy) return;
    if (e.pointerType === 'mouse' && e.button !== 0) return;

    const el = e.target.closest ? e.target.closest('.jsini-note-drawer__card[data-note]') : null;
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
    // 평소대로 그 알림이 가리키는 화면이 열려야 한다.
  });

  root.addEventListener('pointermove', (e) => {
    if (!card || e.pointerId !== pointer) return;

    const mx = e.clientX - startX;
    const my = e.clientY - startY;

    if (!decided) {
      if (Math.abs(mx) < SLOP && Math.abs(my) < SLOP) return;

      decided = true;

      // 세로가 이겼다 — 우리 일이 아니다. 목록을 굴리려던 손이다.
      if (Math.abs(my) > Math.abs(mx)) {
        forget();
        return;
      }

      live = true;
      card.setPointerCapture?.(pointer);
      set(card, 'user-select', 'none');
      set(card, '--note-swipe-ms', '0ms');
    }

    if (!live) return;

    // 가로로 끄는 것이 확정된 뒤에만 막는다 — 그래야 세로 스크롤이 산다.
    e.preventDefault();

    dx = Math.max(-width, Math.min(mx, width));
    set(card, '--note-swipe', `${dx}px`);
    set(card, '--note-ready', Math.abs(dx) >= threshold(width) ? '1' : '0');
  }, { passive: false });

  const release = (e) => {
    if (!card || e.pointerId !== pointer) return;

    const el = card;
    const moved = live;
    const far = Math.abs(dx) >= threshold(width);
    const right = dx > 0;
    const done = live && far && e.type === 'pointerup';

    forget();
    el.style.removeProperty('user-select');

    // 끌지 않았으면 탭이다. 손댄 것이 없으니 그대로 둔다 — `click` 이
    // 이어서 그 알림을 연다.
    if (!moved) return;

    draggedAt = Date.now();
    set(el, '--note-swipe-ms', `${ACT_MS}ms`);

    if (!done) {
      set(el, '--note-swipe', '0px');
      set(el, '--note-ready', '0');
      return;
    }

    commit(el, right);
  };

  root.addEventListener('pointerup', release);
  root.addEventListener('pointercancel', release);

  /**
   * 문턱을 넘겼다. **띠가 카드를 다 덮고 나서** 스러지게 한다 — 그 사이에
   * 「무엇이 일어났는지」(✓ 읽음 / 휴지통 삭제)를 읽을 수 있다. 서버는 그다음이다.
   */
  const commit = (el, right) => {
    busy = true;

    const id = el.dataset.note;
    const w = el.clientWidth || width;

    set(el, '--note-swipe', `${right ? w : -w}px`);
    set(el, '--note-ready', '1');

    setTimeout(() => {
      set(el, '--note-alive', '0');

      setTimeout(async () => {
        busy = false;

        try {
          await dotnet.invokeMethodAsync(
            right ? 'MarkReadSwipedAsync' : 'DeleteSwipedAsync', id);
        } catch {
          // 회로가 닫혔거나 서버가 못 받았다. 카드를 제자리로 돌려 둔다 —
          // 스러진 채로 두면 **처리되지도 않은 알림이 화면에서 사라진다.**
          drop(el);
        }
      }, FADE_MS);
    }, ACT_MS);
  };

  // 끌고 난 뒤에 따라오는 `click` 을 막는다. 안 막으면 손을 떼는 순간
  // 그 알림이 열려 **민 자리에서 화면이 바뀐다.**
  root.addEventListener('click', (e) => {
    if (Date.now() - draggedAt > 250) return;

    e.preventDefault();
    e.stopPropagation();
  }, true);
}
