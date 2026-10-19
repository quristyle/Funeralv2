/** 로그 상자를 바닥에 붙여 둔다. 붙어 있을 때만 민다 — 위를 읽던 사람을 2초마다 끌어내리면 안 된다. 바닥 추적을 CSS(column-reverse)로 하면 놓아 주는 쪽을 못 만든다. */

/** 요소마다 「바닥에 붙어 있나」. WeakMap 이라 요소가 사라지면 함께 버려진다. */
const stuck = new WeakMap();

/** 바닥으로 쳐 주는 여유(px). 0 이면 소수점 줄 높이에서 바닥까지 내려도 다시 안 따라간다. */
const SLACK = 24;

function atBottom(el) {
  return el.scrollHeight - el.scrollTop - el.clientHeight <= SLACK;
}

/** 로그가 바뀐 뒤에 부른다. 붙어 있으면 바닥으로 민다. 숨은 탭에서는 높이가 0 이라 로그 탭을 열 때도 부른다. */
export function follow(el) {
  if (!el) {
    return;
  }

  if (!stuck.has(el)) {
    stuck.set(el, true);

    // 사람이 굴린 것과 우리가 민 것을 가리지 않는다 — 어느 쪽이든 결과는 「지금 바닥에 있나」다.
    el.addEventListener('scroll', () => stuck.set(el, atBottom(el)), { passive: true });
  }

  if (stuck.get(el)) {
    el.scrollTop = el.scrollHeight;
  }
}

/** 구르는 것이 상자가 아니라 쪽일 때 쓴다. 휴대폰 로그는 제 안에서 구르지 않으므로 상자를 담은 구르는 조상을 민다. 붙박인 단추 줄만큼 덜 내린다. 붙어 있나는 굴림이 날 때 재어 둔다 — 부를 때 재면 새 줄이 붙은 뒤라 늘 멀리 있다고 나온다. */
export function followPage(el) {
  if (!el) {
    return;
  }

  const box = scrollerOf(el);

  if (!box) {
    return;
  }

  if (!stuck.has(el)) {
    stuck.set(el, true);
    watch(el, box);
  }

  if (!stuck.get(el)) {
    return;
  }

  const gap = tailGap(el, box);

  if (gap > 0) {
    // 더 내릴 곳이 없으면 브라우저가 끝에서 멈추고, 단추 줄이 제자리로 돌아가 마지막 줄이 가려지지 않는다.
    box.scrollTop += gap;
  }
}

/** 이 요소를 담은 구르는 조상. 못 찾으면 문서 자신이다. 요소 자신은 보지 않는다 — 가로로만 구르는 판이 주인으로 잡히면 세로로 안 움직인다. */
function scrollerOf(el) {
  for (let p = el.parentElement; p; p = p.parentElement) {
    const how = getComputedStyle(p).overflowY;

    if (how === 'auto' || how === 'scroll') {
      return p;
    }
  }

  return document.scrollingElement;
}

/** 구르는 자리의 글이 설 수 있는 아래끝(화면 좌표). 셸은 하단 띠 몫을 안쪽 여백으로 비워 두므로 테두리 아래끝이 아니라 그 위다. */
function viewBottom(box) {
  if (box === document.scrollingElement) {
    return window.innerHeight || document.documentElement.clientHeight;
  }

  const pad = parseFloat(getComputedStyle(box).paddingBottom) || 0;

  return box.getBoundingClientRect().bottom - pad;
}

/** 마지막 줄이 설 수 있는 바닥선. 붙박인 단추 줄이 떠 있으면 그 윗금이다 — 높이로 세면 그만큼 어긋나 마지막 줄을 다시 가린다. */
function floorOf(box) {
  const bottom = viewBottom(box);
  const bar = document.querySelector('.jsini-actbar');

  if (!bar) {
    return bottom;
  }

  const r = bar.getBoundingClientRect();

  return r.height > 0 && r.top > 0 && r.top < bottom ? r.top : bottom;
}

/**
 * 요소의 아래끝이 **바닥선보다 얼마나 더 아래**에 있나. 0 이하면 이미 보인다.
 */
function tailGap(el, box) {
  return el.getBoundingClientRect().bottom - floorOf(box);
}

/** 굴림이 날 때마다 「끝이 보이나」를 적어 둔다. 요소가 사라지면 스스로 떨어진다 — window 에 건 것은 떼어 주지 않으면 로그를 열 때마다 쌓인다. */
function watch(el, box) {
  const target = box === document.scrollingElement ? window : box;

  const onScroll = () => {
    if (!el.isConnected) {
      target.removeEventListener('scroll', onScroll);
      return;
    }

    stuck.set(el, tailGap(el, box) <= SLACK);
  };

  target.addEventListener('scroll', onScroll, { passive: true });
}
