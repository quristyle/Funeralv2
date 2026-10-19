/**
 * 빠른 지시의 판 안 탭을 갈아탈 때 구르는 자리를 다스린다. 목록을 감추면 구른 자리가 깎이므로, 감추기 전에 적어 두고 돌아올 때 되돌린다.
 * 구르는 자리는 이름이 아니라 성질로 찾는다 — 화면과 헤더 서랍에서 다르게 사는데, 이름으로 찾으면 셸 클래스가 바뀔 때 조용히 멈춘다.
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

function listScrollBox(selector) {
  const root = document.querySelector(selector);
  if (!root) return null;

  const inner = root.querySelector('.pm-ask__scroll');
  if (inner) return inner;

  return scrollBox(selector);
}

/** 목록에서 보던 자리를 적어 둔다. 목록이 아직 보일 때 부른다. 이미 적어 둔 것은 덮지 않는다 — 탭에서 탭으로 갈아타는 길도 여기를 지난다. */
export function remember(selector) {
  const box = listScrollBox(selector);
  if (!box) return;

  if (!saved.has(box)) {
    saved.set(box, box.scrollTop);
  }
}

/** 고른 탭이 보이게 탭 줄을 옆으로 민다. scrollIntoView 는 쪽 전체도 움직여 세로 자리를 흩뜨리므로 가로 값만 직접 셈한다. */
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

  const box = listScrollBox(selector);
  if (!box) return;

  const top = saved.get(box);
  saved.delete(box);

  box.scrollTop = typeof top === 'number' ? top : 0;
}
