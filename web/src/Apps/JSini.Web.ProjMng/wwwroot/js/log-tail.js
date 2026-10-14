/**
 * 로그 상자를 **바닥에 붙여 둔다.**
 *
 * [무엇을 고친 것인가]
 *
 * AI 작업 지시 화면의 로그는 2초마다 새 줄이 붙는데, 붙는 자리는 상자의
 * 아래쪽이고 보이는 자리는 처음 그대로였다. 그래서 **방금 찍힌 줄이 늘
 * 화면 밖**이었고, 지켜보는 동안 내내 손으로 굴려 내려야 했다.
 *
 * [바닥에 붙어 있을 때만 민다]
 *
 * 무조건 바닥으로 밀면 **위를 읽던 사람을 끌어내린다** — 한참 전 줄을 찾아
 * 올라가 읽는 중에 2초마다 아래로 채이면 아예 읽을 수가 없다.
 *
 * 그래서 요소마다 「지금 바닥에 붙어 있나」를 기억해 두고, 붙어 있을 때만
 * 민다. 사람이 위로 굴리면 그 순간 떨어진 것으로 보고 놓아 주고, 다시
 * 바닥까지 내려오면 그때부터 또 따라간다 — 터미널과 CI 로그가 하는 그대로다.
 *
 * [왜 CSS 가 아닌가]
 *
 * `flex-direction: column-reverse` 로도 바닥에 붙일 수는 있다. 다만 그러면
 * **놓아 주는 쪽을 못 만든다** — 위를 읽는 중에도 새 줄이 오면 그대로
 * 붙어 버린다. 이 화면에서 필요한 것은 「따라가되 방해하지 않는다」쪽이다.
 */

/**
 * 요소마다 「바닥에 붙어 있나」. `WeakMap` 이라 요소가 사라지면 함께 버려진다 —
 * 화면을 열고 닫을 때마다 쌓이지 않는다.
 */
const stuck = new WeakMap();

/**
 * 바닥으로 쳐 주는 여유(px).
 *
 * 0 으로 두면 안 된다. 줄 높이가 소수점이라 딱 떨어지지 않는 브라우저가 있고,
 * 그러면 **바닥까지 내려도 다시 안 따라간다.**
 */
const SLACK = 24;

function atBottom(el) {
  return el.scrollHeight - el.scrollTop - el.clientHeight <= SLACK;
}

/**
 * 로그가 바뀐 뒤에 부른다. 붙어 있으면 바닥으로 민다.
 *
 * 탭이 숨어 있는 동안에는 높이가 0 이라 미는 것이 먹지 않는다. 그래서 화면은
 * **로그 탭을 열 때도 이것을 부른다** — 그때 비로소 바닥으로 간다.
 */
export function follow(el) {
  if (!el) {
    return;
  }

  if (!stuck.has(el)) {
    stuck.set(el, true);

    // 사람이 굴린 것과 우리가 민 것을 가리지 않는다 — 어느 쪽이든 결과가
    // 「지금 바닥에 있나」이고, 우리가 민 뒤에는 그 답이 참이다.
    el.addEventListener('scroll', () => stuck.set(el, atBottom(el)), { passive: true });
  }

  if (stuck.get(el)) {
    el.scrollTop = el.scrollHeight;
  }
}

/**
 * **구르는 것이 상자가 아니라 쪽일 때** 쓴다.
 *
 * [왜 둘로 갈렸나]
 *
 * 휴대폰의 로그는 제 안에서 구르지 않는다 — 받은 줄을 전부 그리고 높이를
 * 내용에 맡긴다(`projmng.css` 의 `.pm-xlat__list`). 구르는 자리는 그 상자를
 * 담고 있는 쪽(`.jsini-shell__content`)이라, 위의 `follow` 가 미는 `scrollTop`
 * 은 **갈 곳이 없다**(상자가 제 내용만큼이라 늘 0 이다).
 *
 * 그래서 여기서는 **상자를 담은 구르는 조상**을 찾아 그것을 민다. 미는 양은
 * 「상자의 아래끝이 보이는 자리에 올 만큼」이다.
 *
 * [붙박인 단추 줄만큼 덜 내린다]
 *
 * 화면 아래에는 단추 줄이 떠 있다(`.jsini-actbar` — 이어서 지시·재시도·확인).
 * 그 줄은 구르는 동안 바닥에 붙어 있으므로, 상자의 아래끝을 화면 아래끝에
 * 딱 맞추면 **마지막 몇 줄이 그 줄 뒤에 깔린다.** 줄 높이만큼 덜 내린다.
 *
 * [놓아 주는 규칙은 위와 같다]
 *
 * 사람이 위를 읽는 중이면 따라가지 않는다. 다만 「바닥에 붙어 있나」를
 * **부를 때 재면 안 된다** — 부르는 때가 이미 새 줄이 붙은 뒤라, 한 번에 쉰
 * 줄이 붙은 판에서는 늘 「멀리 있다」가 되어 놓아 주게 된다. 굴림이 날 때
 * 재어 둔다.
 */
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
    // 더 내릴 곳이 없으면 브라우저가 알아서 끝에서 멈춘다. 거기서는 단추
    // 줄이 제자리(흐름의 끝)로 돌아가므로 마지막 줄이 가려지지 않는다.
    box.scrollTop += gap;
  }
}

/**
 * 이 요소를 담고 있는 **구르는 조상.** 못 찾으면 문서 자신이다.
 *
 * 요소 자신은 보지 않는다 — 가로로만 구르는 판(`--nowrap`)이 제가 주인인
 * 것으로 잡히면 세로로는 한 픽셀도 안 움직인다.
 */
function scrollerOf(el) {
  for (let p = el.parentElement; p; p = p.parentElement) {
    const how = getComputedStyle(p).overflowY;

    if (how === 'auto' || how === 'scroll') {
      return p;
    }
  }

  return document.scrollingElement;
}

/**
 * 구르는 자리의 **글이 설 수 있는** 아래끝(화면 좌표).
 *
 * 테두리 상자의 아래끝이 아니다. 셸은 본문 아래쪽에 **하단 띠 몫**을 안쪽
 * 여백으로 비워 두는데(app.css — 그 띠는 `position: fixed` 라 그 자리를
 * 덮는다), 거기까지 내리면 마지막 줄이 띠 뒤로 들어간다.
 */
function viewBottom(box) {
  if (box === document.scrollingElement) {
    return window.innerHeight || document.documentElement.clientHeight;
  }

  const pad = parseFloat(getComputedStyle(box).paddingBottom) || 0;

  return box.getBoundingClientRect().bottom - pad;
}

/**
 * 마지막 줄이 설 수 있는 **바닥선.**
 *
 * 붙박인 단추 줄(`.jsini-actbar`)이 떠 있으면 **그 윗금**이다. 줄의 높이를
 * 빼는 것으로는 안 된다 — 그 줄은 화면 맨 아래가 아니라 위에서 센 자리에
 * 붙어 서기 때문이다(재어 보면 390×844 에서 바닥에서 133px — 하단 띠 몫
 * 60px 위다). 높이로 세면 그만큼 어긋나 **마지막 네댓 줄이 다시 가린다.**
 *
 * 쪽을 끝까지 내려 그 줄이 제자리(흐름의 끝)로 돌아갔으면 가리는 것이
 * 없으므로 바닥선은 그대로다.
 */
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

/**
 * 굴림이 날 때마다 「끝이 보이나」를 적어 둔다.
 *
 * **요소가 사라지면 스스로 떨어진다.** 쪽이 구르는 자리일 때 듣는 쪽은
 * `window` 라, 떼어 주지 않으면 로그를 열 때마다 하나씩 쌓인다.
 */
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
