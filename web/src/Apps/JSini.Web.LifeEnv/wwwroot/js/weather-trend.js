/**
 * 예보 추이 차트 도우미 — 「손가락 화면인가」와 「카드 넘김」 둘.
 *
 * [왜 이것 하나 때문에 JS 가 필요한가]
 *
 * Blazor Server 는 브라우저 상태를 모른다. 화면 **너비**는 DevExpress 의
 * `DxLayoutBreakpoint` 가 알려 주지만(셸의 MainLayout 이 그렇게 쓴다),
 * 여기서 알아야 하는 것은 너비가 아니라 **손가락으로 보는 화면인가**이다.
 * 창을 좁힌 PC 는 여전히 마우스가 있어 말풍선이 쓸모 있고, 넓은 태블릿은
 * 마우스가 없어 말풍선이 손짓을 방해한다 — 너비로 가르면 둘 다 틀린다.
 *
 * 기준은 `(hover: none)` 하나로 맞춘다. 차트 옆의 「옆으로 쓸어 넘깁니다」
 * 안내가 css 에서 쓰는 것과 **같은 질의**다 — 안내가 보이는 화면과 넘기기가
 * 사는 화면이 어긋나면 안 된다.
 *
 * [넘기기는 우리가 만들지 않는다]
 *
 * 항목 넷을 카드 넷으로 늘어놓고 **브라우저의 가로 스크롤**에 맡긴다
 * (css 의 `scroll-snap`). 손가락을 따라 카드가 실제로 밀려가고, 떼면 제자리에
 * 붙는다 — 예전처럼 touchend 에서 거리를 재어 계열을 갈아 끼우는 흉내가
 * 아니다. 그래서 여기 있는 것은 **읽고 옮기는 일 둘**뿐이다.
 *
 * - `watchDeck` — 어느 카드가 보이는지 C# 에 알려 준다(단추 불을 맞추려고).
 * - `showSlide` — 단추를 누르면 그 카드로 밀어 준다.
 */

/** 손가락으로 보는 화면인가. 알 수 없으면 아니라고 답한다(말풍선을 남긴다). */
export function isTouchOnly() {
  try {
    return window.matchMedia('(hover: none)').matches;
  } catch (e) {
    return false;
  }
}

// ── 카드 넘김 ────────────────────────────────────────────

/**
 * 손을 뗀 뒤 이만큼 잠잠하면 「이 카드를 보고 있다」고 본다(ms).
 *
 * 스크롤 이벤트는 손가락 한 번에 수십 개가 온다. 그때마다 회로를 왕복하면
 * 넘기는 동안 화면이 굳는다 — 예전에 말풍선이 그래서 문제였다. 붙고 나서
 * 한 번만 알린다. `scrollend` 를 쓰지 않는 것은 사파리에 아직 없어서다.
 */
const SettleMs = 120;

/** 감시 중인 칸 → 거두는 함수. 같은 칸을 두 번 걸지 않으려는 것이다. */
const watched = new WeakMap();

/** 카드 목록. 칸 안의 다른 것(자리채움 따위)은 세지 않는다. */
function slides(deck) {
  return Array.from(deck.querySelectorAll(':scope > [data-slide]'));
}

/**
 * 지금 보이는 카드의 차례. 카드의 왼쪽 끝이 스크롤 위치에 가장 가까운 것이다
 * (css 가 `scroll-snap-align: start` 라 붙고 나면 정확히 0 이 된다).
 */
function shownIndex(deck) {
  const items = slides(deck);
  let best = 0;
  let gap = Infinity;

  for (let i = 0; i < items.length; i++) {
    const d = Math.abs(items[i].offsetLeft - deck.scrollLeft);

    if (d < gap) {
      gap = d;
      best = i;
    }
  }

  return best;
}

/** 움직임 없이 한 번에 옮길지 — 움직임을 줄여 달라고 한 사람에게는 그냥 놓는다. */
function still() {
  try {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  } catch (e) {
    return false;
  }
}

/**
 * 카드가 바뀌는 것을 지켜보다 C# 에 알린다.
 *
 * `index` 자리로 먼저 **움직임 없이** 옮겨 놓는다. 자료를 다시 읽으면 칸이
 * 새로 만들어져 스크롤이 0 으로 돌아가는데, 그때 눌린 단추는 그대로라 둘이
 * 어긋난다.
 */
export function watchDeck(deck, owner, index) {
  if (!deck) {
    return;
  }

  unwatchDeck(deck);
  jump(deck, index, 'auto');

  let timer = 0;
  let shown = shownIndex(deck);

  const onScroll = () => {
    clearTimeout(timer);

    timer = setTimeout(() => {
      const now = shownIndex(deck);

      if (now === shown) {
        return;
      }

      shown = now;

      // 회로가 이미 끊겼으면 조용히 넘어간다 — 카드는 그대로 굴러간다.
      owner.invokeMethodAsync('OnSlideShown', now).catch(() => {});
    }, SettleMs);
  };

  // 수동(passive)으로 듣는다. 스크롤을 막을 생각이 없으므로 브라우저가
  // 우리를 기다릴 이유도 없다.
  deck.addEventListener('scroll', onScroll, { passive: true });

  watched.set(deck, () => {
    clearTimeout(timer);
    deck.removeEventListener('scroll', onScroll);
  });
}

/** 지켜보기를 거둔다. 화면을 떠날 때 부른다. */
export function unwatchDeck(deck) {
  if (!deck) {
    return;
  }

  const stop = watched.get(deck);

  if (stop) {
    stop();
    watched.delete(deck);
  }
}

/** 단추를 눌렀을 때. 그 카드까지 밀어 준다 — 손으로 쓴 것과 같은 자리에 선다. */
export function showSlide(deck, index) {
  jump(deck, index, still() ? 'auto' : 'smooth');
}

function jump(deck, index, behavior) {
  if (!deck) {
    return;
  }

  const item = slides(deck)[index];

  if (!item) {
    return;
  }

  deck.scrollTo({ left: item.offsetLeft, behavior });
}
