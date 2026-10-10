/**
 * 빠른 지시 — 판 안의 탭을 갈아탈 때 **구르는 자리**를 다스린다.
 *
 * [왜 필요한가]
 *
 * 카드를 누르면 적는 자리와 목록이 통째로 감춰지고(`pm-ask__pane--off`)
 * 그 자리에 상세가 선다. 그런데 **구른 만큼은 그대로 남는다** — 목록
 * 아래쪽 카드를 누른 사람은 결과문의 한가운데부터 보게 된다. 짧은 건은
 * 브라우저가 끌어올려 주지만 긴 건에서는 그대로 남는다.
 *
 * [돌아올 때는 보던 자리로]
 *
 * 맨 위로 돌리기만 하면 이번에는 **목록으로 돌아올 때** 처음부터 다시
 * 굴려야 한다. 창이던 시절에는 닫으면 보던 자리가 그대로였으니 그것을
 * 잃는 것은 고침이 아니라 맞바꿈이다.
 *
 * [적어 두는 것은 **감추기 전**이다]
 *
 * 목록이 사라지고 나면 판이 짧아져 브라우저가 구른 자리를 **그 자리에서
 * 깎는다.** 그 뒤에 재면 깎인 값이 적히고, 돌아와도 엉뚱한 자리다.
 * 그래서 `remember` 는 화면이 바뀌기 전에 부른다(`AiAskPanel.PeekAsync`).
 *
 * [구르는 자리를 선택자로 찾지 않는다]
 *
 * 이 판이 사는 자리가 둘이다 — 화면(셸 본문이 구른다)과 헤더에서 여는
 * 서랍(제 판이 구른다). 이름으로 찾으면 한쪽에서만 듣고, 셸의 클래스
 * 이름이 바뀌는 날 **조용히** 아무 일도 안 하게 된다. 성질로 찾는다 —
 * 넘치고, 넘친 것을 구르게 둔 조상(`grid-scroll.js` 와 같은 수다).
 */

const saved = new WeakMap();

function scrollBox(selector) {
  const root = document.querySelector(selector);
  if (!root) return null;

  let box = root.parentElement;

  while (box && box !== document.body) {
    const how = getComputedStyle(box).overflowY;

    if ((how === 'auto' || how === 'scroll') && box.scrollHeight > box.clientHeight) {
      return box;
    }

    box = box.parentElement;
  }

  return document.scrollingElement || document.documentElement;
}

/**
 * 목록에서 보던 자리를 적어 둔다. **목록이 아직 보일 때** 부른다.
 * 이미 적어 둔 것이 있으면 덮지 않는다 — 탭에서 탭으로 갈아타는 길도
 * 여기를 지나는데, 덮으면 「목록에서 보던 자리」가 「직전 상세에서 보던
 * 자리」로 바뀐다.
 */
export function remember(selector) {
  const box = scrollBox(selector);
  if (!box) return;

  if (!saved.has(box)) {
    saved.set(box, box.scrollTop);
  }
}

/**
 * **고른 탭이 보이게** 탭 줄을 옆으로 민다.
 *
 * 탭 줄은 넘치면 옆으로 미는 자리다(`pm-ask__tabs`). 그래서 넷째 건을 열면
 * **방금 연 그 탭이 화면 밖**이고, 눌렀는데 아무 표시도 없는 것처럼 보인다.
 * `scrollIntoView` 는 **쪽 전체도 함께 움직여** 방금 맞춰 둔 세로 자리를
 * 흩뜨리므로 가로 값만 직접 셈한다.
 */
function showActiveTab(selector) {
  const root = document.querySelector(selector);
  if (!root) return;

  const strip = root.querySelector('.pm-ask__tabs');
  const on = strip && strip.querySelector('.pm-ask__tab--on');
  if (!strip || !on) return;

  const left = on.offsetLeft;
  const right = left + on.offsetWidth;

  if (left < strip.scrollLeft) {
    strip.scrollLeft = left;
  } else if (right > strip.scrollLeft + strip.clientWidth) {
    strip.scrollLeft = right - strip.clientWidth;
  }
}

/** 맨 위로. 상세를 열거나 다른 탭으로 갈아탔을 때다. */
export function toTop(selector) {
  const box = scrollBox(selector);
  if (box) box.scrollTop = 0;

  showActiveTab(selector);
}

/** 목록으로 돌아간다. 적어 둔 자리로 되돌리고 그 표시는 지운다. */
export function restore(selector) {
  showActiveTab(selector);

  const box = scrollBox(selector);
  if (!box) return;

  const top = saved.get(box);
  saved.delete(box);

  box.scrollTop = typeof top === 'number' ? top : 0;
}
