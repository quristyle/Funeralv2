/**
 * 프로젝트 일정표 월 격자에서 막대를 끌어 다른 날로 옮긴다.
 * HTML5 끌어놓기(draggable)를 쓰지 않고 포인터 이벤트로 몬다 — 막대가 <button> 이라 파이어폭스·사파리는
 * draggable 단추에서 끌기를 시작하지 않는다. 손가락은 받지 않는다: 월 격자의 손가락 쓸기는 달 넘기기다.
 * 놓을 날은 칸이 아니라 주 줄 위의 가로 자리로 셈한다 — 칸 위에는 막대·날짜 숫자·「더보기」가 덮여 있다.
 * 끄는 동안 서버를 부르지 않고 놓을 때 한 번만 부른다.
 * 강조는 data 속성으로 단다: Blazor 가 칸의 class 를 갈아 치우면 JS 가 붙인 클래스가 지워진다.
 */

/** 이만큼 움직여야 끌기다. 덜 움직이고 떼면 그냥 누름이라 편집 창이 열린다. */
const THRESHOLD = 5;

/** 화면 유지(keep-alive)로 일정표가 둘 살 수 있다 — 격자의 data-drag-owner 로 제 화면을 찾는다. */
const owners = new Map();

let press = null;
let lit = null;
let ghost = null;

const iso = (d) => d.toISOString().slice(0, 10);

/** 주 줄과 가로 좌표로 그 날을 셈한다. 주 줄에는 첫날(일요일)이 data-start 로 적혀 있다. */
function dayAt(week, x) {
  const box = week.getBoundingClientRect();
  if (box.width <= 0) return null;

  const col = Math.min(6, Math.max(0, Math.floor(((x - box.left) / box.width) * 7)));
  const start = new Date(week.dataset.start + 'T00:00:00Z');
  start.setUTCDate(start.getUTCDate() + col);

  return { week, col, date: iso(start) };
}

/** 그림자 막대는 누름을 받지 않으므로 손 밑의 것이 그대로 잡힌다. 다른 격자(숨은 화면)에는 놓지 않는다. */
function hitAt(x, y) {
  const week = document.elementFromPoint(x, y)?.closest?.('.pm-gc-month .pm-gc-week');
  if (!week || week.closest('.pm-gc-month') !== press.month) return null;

  return dayAt(week, x);
}

function light(hit) {
  const cell = hit ? hit.week.querySelectorAll('.pm-gc-week__days > .pm-gc-day')[hit.col] : null;
  if (cell === lit) return;

  lit?.removeAttribute('data-drop');
  lit = cell;
  lit?.setAttribute('data-drop', '');
}

/** 손을 따라다니는 막대. body 에 붙으므로 격자에 걸린 높이·글자 변수를 값으로 옮겨 적는다. */
function makeGhost(bar) {
  const box = bar.getBoundingClientRect();
  const css = getComputedStyle(bar);
  const el = bar.cloneNode(true);

  el.removeAttribute('data-dragging');
  el.removeAttribute('id');
  el.setAttribute('aria-hidden', 'true');
  Object.assign(el.style, {
    position: 'fixed',
    left: '0',
    top: '0',
    zIndex: '10000',
    width: `${box.width}px`,
    height: `${box.height}px`,
    lineHeight: css.lineHeight,
    fontSize: css.fontSize,
    margin: '0',
    pointerEvents: 'none',
    opacity: '0.85',
    boxShadow: '0 2px 8px rgb(0 0 0 / 30%)',
  });
  document.body.appendChild(el);

  return el;
}

function moveGhost(x, y) {
  if (!ghost) return;
  ghost.style.transform = `translate(${x - press.dx}px, ${y - press.dy}px)`;
}

function begin() {
  press.dragging = true;
  press.bar.setAttribute('data-dragging', '');
  document.documentElement.setAttribute('data-pm-gc-dragging', '');
  ghost = makeGhost(press.bar);

  // 창 밖에서 떼도 pointerup 을 받는다.
  try { press.bar.setPointerCapture(press.pointerId); } catch { /* 이미 떨어진 포인터 */ }
}

function end() {
  light(null);
  ghost?.remove();
  ghost = null;
  document.documentElement.removeAttribute('data-pm-gc-dragging');

  if (press) {
    press.bar.removeAttribute('data-dragging');
    try { press.bar.releasePointerCapture(press.pointerId); } catch { /* 이미 놓였다 */ }
  }

  press = null;
}

/** 끈 뒤에 뒤따라 오는 click 이 막대의 편집 창을 열지 않게 한 번만 삼킨다. click 이 안 오면 다음 차례에 걷는다. */
function swallowClick() {
  const stop = (e) => {
    e.stopPropagation();
    e.preventDefault();
  };

  window.addEventListener('click', stop, { capture: true, once: true });
  setTimeout(() => window.removeEventListener('click', stop, { capture: true }), 0);
}

function onPointerDown(e) {
  if (e.button !== 0 || e.pointerType === 'touch' || press) return;

  const bar = e.target.closest?.('.pm-gc-month .pm-gc-bar[data-wbs][data-drag]');
  if (!bar || bar.disabled) return;

  const month = bar.closest('.pm-gc-month');
  const week = bar.closest('.pm-gc-week');
  const grab = week && dayAt(week, e.clientX);
  if (!grab || !owners.has(month?.dataset.dragOwner)) return;

  const box = bar.getBoundingClientRect();
  press = {
    id: Number(bar.dataset.wbs),
    from: grab.date,
    bar,
    month,
    pointerId: e.pointerId,
    x: e.clientX,
    y: e.clientY,
    dx: e.clientX - box.left,
    dy: e.clientY - box.top,
    dragging: false,
  };
}

function onPointerMove(e) {
  if (!press || e.pointerId !== press.pointerId) return;

  if (!press.dragging) {
    if (Math.hypot(e.clientX - press.x, e.clientY - press.y) < THRESHOLD) return;
    begin();
  }

  e.preventDefault();
  moveGhost(e.clientX, e.clientY);
  light(hitAt(e.clientX, e.clientY));
}

function onPointerUp(e) {
  if (!press || e.pointerId !== press.pointerId) return;

  const { id, from, month, dragging } = press;
  const hit = dragging ? hitAt(e.clientX, e.clientY) : null;
  end();

  // 끌지 않았으면 그냥 누름이다 — click 이 편집 창을 연다.
  if (!dragging) return;

  swallowClick();

  const dotnet = owners.get(month.dataset.dragOwner);
  if (hit && hit.date !== from && dotnet) {
    dotnet.invokeMethodAsync('MoveByDrag', id, from, hit.date);
  }
}

function onPointerCancel(e) {
  if (press && e.pointerId === press.pointerId) end();
}

function onKeyDown(e) {
  if (e.key === 'Escape' && press?.dragging) {
    e.preventDefault();
    swallowClick();
    end();
  }
}

const listeners = [
  ['pointerdown', onPointerDown],
  ['pointermove', onPointerMove],
  ['pointerup', onPointerUp],
  ['pointercancel', onPointerCancel],
  ['keydown', onKeyDown],
];

export function attach(ref, owner) {
  if (owners.size === 0) {
    for (const [type, fn] of listeners) document.addEventListener(type, fn);
  }

  owners.set(owner, ref);
}

/** 제 화면만 뗀다 — 화면 유지 중에는 옛 화면의 정리가 새 화면이 붙은 뒤에 올 수 있다. */
export function detach(owner) {
  owners.delete(owner);

  if (press && press.month.dataset.dragOwner === owner) end();
  if (owners.size > 0) return;

  end();
  for (const [type, fn] of listeners) document.removeEventListener(type, fn);
}
