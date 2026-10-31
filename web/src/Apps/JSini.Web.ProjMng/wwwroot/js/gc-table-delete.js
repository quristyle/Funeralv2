/**
 * 프로젝트 일정표를 표로 볼 때, 고른 줄에서 Delete 글쇠를 누르면 서버에 지우기를 묻는다(확인창은 서버가 띄운다).
 * 문서에 건다 — 줄을 누르면 그 줄이 입력칸으로 다시 그려져서 촛점이 body 로 빠지는 일이 있다.
 * 글자를 고치는 칸(입력·드롭다운·편집 영역)에서 친 Delete 는 글자 지우기라 건드리지 않는다.
 */

const EDITABLE = 'input, textarea, select, [contenteditable=""], [contenteditable="true"]';

/** 화면 유지(keep-alive)로 일정표가 둘 살 수 있다 — 판마다 따로 건다. */
const handlers = new Map();

export function attach(main, dotnet) {
  if (!main || handlers.has(main)) return;

  const onKey = (e) => {
    if (e.key !== 'Delete' || e.repeat || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return;

    const t = e.target;
    if (t instanceof Element && t.closest(EDITABLE)) return;

    // 숨은 화면(keep-alive)의 판은 크기가 0 이다. 표가 아니면(달력 보기) 표 판이 없다.
    const table = main.querySelector('.pm-gc-swipe');
    if (!table || main.offsetParent === null) return;

    const onBody = t === document.body || t === document.documentElement;
    if (!onBody && !(t instanceof Node && table.contains(t))) return;

    e.preventDefault();
    dotnet.invokeMethodAsync('DeleteByKey').catch(() => {});
  };

  document.addEventListener('keydown', onKey);
  handlers.set(main, onKey);
}

export function detach(main) {
  const onKey = handlers.get(main);
  if (!onKey) return;

  document.removeEventListener('keydown', onKey);
  handlers.delete(main);
}
