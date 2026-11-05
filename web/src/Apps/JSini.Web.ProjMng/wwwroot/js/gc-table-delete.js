/**
 * 프로젝트 일정표를 표로 볼 때, 고른 줄에서 Delete 글쇠를 누르면 서버에 지우기를 묻는다(확인창은 서버가 띄운다).
 * 문서에 건다 — 줄을 누르면 그 줄이 입력칸으로 다시 그려져서 촛점이 body 로 빠지는 일이 있다.
 * 고른 줄은 칸이 곧바로 입력칸(.pm-inline-edit)이 되고 고치는 즉시 저장된다. 그 칸을 눌러 들어가기만 한 Delete 는
 * 줄 지우기로 받고, 그 칸에서 글자를 치기 시작했거나 글을 골라 두었으면 글자 지우기로 둔다.
 * 날짜 칸의 마스크가 글쇠를 먼저 가로채므로 잡는 단계(capture)에서 받는다.
 */

const EDITABLE = 'input, textarea, select, [contenteditable=""], [contenteditable="true"]';
const INLINE = '.pm-inline-edit';

/** 손대지 않고 넘기는 글쇠 — 이것만 눌렀으면 아직 글을 고치기 시작한 것이 아니다. */
const PASSIVE = new Set(['Delete', 'Tab', 'Escape', 'Shift', 'Control', 'Alt', 'Meta', 'CapsLock']);

/** 화면 유지(keep-alive)로 일정표가 둘 살 수 있다 — 판마다 따로 건다. */
const handlers = new Map();

export function attach(main, dotnet) {
  if (!main || handlers.has(main)) return;

  // 촛점을 받은 뒤 글을 고치기 시작한 입력칸. 촛점을 다시 받으면 지운다.
  const typing = new WeakSet();

  const onFocus = (e) => {
    if (e.target instanceof Element) typing.delete(e.target);
  };

  const onKey = (e) => {
    const t = e.target;

    if (e.key !== 'Delete') {
      if (t instanceof Element && !PASSIVE.has(e.key) && t.closest(INLINE)) typing.add(t);
      return;
    }

    if (e.repeat || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return;

    // 숨은 화면(keep-alive)의 판은 크기가 0 이다. 표가 아니면(달력 보기) 표 판이 없다.
    const table = main.querySelector('.pm-gc-swipe');
    if (!table || main.offsetParent === null) return;

    const onBody = t === document.body || t === document.documentElement;
    if (!onBody && !(t instanceof Node && table.contains(t))) return;

    if (t instanceof Element && t.closest(EDITABLE)) {
      // 칸별 검색 줄 같은 표의 다른 입력칸은 늘 글자 지우기다.
      if (!t.closest(INLINE) || typing.has(t) || hasSelection(t)) return;
    }

    e.preventDefault();
    e.stopPropagation();
    dotnet.invokeMethodAsync('DeleteByKey').catch(() => {});
  };

  document.addEventListener('focusin', onFocus, true);
  document.addEventListener('keydown', onKey, true);
  handlers.set(main, { onKey, onFocus });
}

export function detach(main) {
  const h = handlers.get(main);
  if (!h) return;

  document.removeEventListener('focusin', h.onFocus, true);
  document.removeEventListener('keydown', h.onKey, true);
  handlers.delete(main);
}

/** 끌어 골라 둔 글이 있나 — 있으면 Delete 는 그 글을 지우라는 뜻이다.
 * 날짜 칸은 누르기만 해도 마스크가 한 마디를 골라 두므로 따지지 않는다. */
function hasSelection(el) {
  if (el.closest('dxbl-date-edit')) return false;

  try {
    return typeof el.selectionStart === 'number' && el.selectionStart !== el.selectionEnd;
  } catch {
    return false;
  }
}
