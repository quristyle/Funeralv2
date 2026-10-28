/**
 * 프로젝트 일정표 월 격자에서 막대를 끌어 다른 날로 옮긴다.
 * 놓을 날은 칸이 아니라 주 줄 위의 가로 자리로 셈한다 — 칸 위에는 막대·날짜 숫자·「더보기」가 덮여 있어
 * 칸에 dragover 를 걸면 그것들 위에서는 놓을 수가 없다. 끄는 동안 서버를 부르지 않고 놓을 때 한 번만 부른다.
 * 강조는 data 속성으로 단다: Blazor 가 칸의 class 를 갈아 치우면 JS 가 붙인 클래스가 지워진다.
 */

const MIME = 'application/x-jsini-wbs';

let dotnet = null;
let drag = null;
let lit = null;

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

function light(hit) {
  const cell = hit ? hit.week.querySelectorAll('.pm-gc-week__days > .pm-gc-day')[hit.col] : null;
  if (cell === lit) return;

  lit?.removeAttribute('data-drop');
  lit = cell;
  lit?.setAttribute('data-drop', '');
}

function clear() {
  light(null);
  drag?.bar.removeAttribute('data-dragging');
  drag = null;
}

function onDragStart(e) {
  const bar = e.target.closest?.('.pm-gc-month .pm-gc-bar[data-wbs]');
  if (!bar || bar.getAttribute('draggable') !== 'true') return;

  const week = bar.closest('.pm-gc-week');
  const grab = week && dayAt(week, e.clientX);
  if (!grab) return;

  drag = { id: Number(bar.dataset.wbs), from: grab.date, bar };

  // 파이어폭스는 실어 둔 것이 없으면 끌기를 시작하지 않는다.
  e.dataTransfer.effectAllowed = 'move';
  e.dataTransfer.setData(MIME, String(drag.id));
  e.dataTransfer.setData('text/plain', bar.textContent.trim());

  bar.setAttribute('data-dragging', '');
}

function onDragOver(e) {
  if (!drag) return;

  const week = e.target.closest?.('.pm-gc-month .pm-gc-week');
  if (!week) {
    light(null);
    return;
  }

  e.preventDefault();
  e.dataTransfer.dropEffect = 'move';
  light(dayAt(week, e.clientX));
}

function onDrop(e) {
  if (!drag) return;

  const week = e.target.closest?.('.pm-gc-month .pm-gc-week');
  const hit = week && dayAt(week, e.clientX);
  const { id, from } = drag;

  e.preventDefault();
  clear();

  if (hit && hit.date !== from && dotnet) {
    dotnet.invokeMethodAsync('MoveByDrag', id, from, hit.date);
  }
}

function onDragLeave(e) {
  // 창 밖으로 나가면 강조를 걷는다. 칸 사이를 지날 때는 relatedTarget 이 남아 있다.
  if (drag && !e.relatedTarget) light(null);
}

let attached = false;

export function attach(ref) {
  dotnet = ref;
  if (attached) return;

  attached = true;
  document.addEventListener('dragstart', onDragStart);
  document.addEventListener('dragover', onDragOver);
  document.addEventListener('drop', onDrop);
  document.addEventListener('dragleave', onDragLeave);
  document.addEventListener('dragend', clear);
}

export function detach() {
  dotnet = null;
  attached = false;
  clear();
  document.removeEventListener('dragstart', onDragStart);
  document.removeEventListener('dragover', onDragOver);
  document.removeEventListener('drop', onDrop);
  document.removeEventListener('dragleave', onDragLeave);
  document.removeEventListener('dragend', clear);
}
