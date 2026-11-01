// WBS 개발자 정보 — 목차 끌어 옮기기와 HTML 내려받기.
// 끌기는 브라우저 안에서 끝낸다. dragover 를 회로로 보내면 마우스가 움직일 때마다 왕복이고,
// 놓을 자리(위/아래)는 줄의 상자를 재야 갈리는데 서버는 그것을 모른다. 놓은 순간 한 번만 알린다.

/** 글자를 파일로 내려받는다. 서버를 거치지 않는다. 앞의 BOM 은 메모장이 한글을 깨지 않게 하려는 것이다. */
export function save(name, text) {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob(['﻿' + text], { type: 'text/html;charset=utf-8' }));
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 5000);
}

/**
 * 목차에 끌기를 건다. 줄은 [data-doc-id] 이고, 끌 수 있는지는 줄의 draggable 이 정한다(편집 중에는 화면이 끈다).
 * 판(el)은 늘 그려져 있는 상자라 줄이 다시 그려져도 손이 안 떨어진다.
 */
export function attachToc(el, dotnet) {
  let from = null;
  let over = null;

  const rowOf = (e) => e.target instanceof Element ? e.target.closest('[data-doc-id]') : null;

  const clear = () => {
    el.querySelectorAll('.is-dragging, .is-over-before, .is-over-after')
      .forEach((n) => n.classList.remove('is-dragging', 'is-over-before', 'is-over-after'));
    over = null;
  };

  const onStart = (e) => {
    const row = rowOf(e);
    if (!row || row.getAttribute('draggable') !== 'true') return;
    from = row.dataset.docId;
    row.classList.add('is-dragging');
    e.dataTransfer.effectAllowed = 'move';
    // 데이터가 비면 끌기를 시작조차 하지 않는 브라우저가 있다.
    e.dataTransfer.setData('text/plain', from);
  };

  const onOver = (e) => {
    const row = rowOf(e);
    if (from == null || !row || row.dataset.docId === from) return;
    e.preventDefault(); // 막지 않으면 놓을 수 없는 자리가 된다
    e.dataTransfer.dropEffect = 'move';
    const r = row.getBoundingClientRect();
    const pos = e.clientY < r.top + r.height / 2 ? 'before' : 'after';
    if (over === row && row.classList.contains('is-over-' + pos)) return;
    if (over) over.classList.remove('is-over-before', 'is-over-after');
    row.classList.add('is-over-' + pos);
    over = row;
  };

  const onDrop = (e) => {
    const row = rowOf(e);
    const src = from;
    const after = row?.classList.contains('is-over-after') ?? false;
    from = null;
    clear();
    if (src == null || !row || row.dataset.docId === src) return;
    e.preventDefault();
    dotnet.invokeMethodAsync('MoveDoc', Number(src), Number(row.dataset.docId), after);
  };

  const onEnd = () => {
    from = null;
    clear();
  };

  el.addEventListener('dragstart', onStart);
  el.addEventListener('dragover', onOver);
  el.addEventListener('drop', onDrop);
  el.addEventListener('dragend', onEnd);

  return {
    dispose() {
      el.removeEventListener('dragstart', onStart);
      el.removeEventListener('dragover', onOver);
      el.removeEventListener('drop', onDrop);
      el.removeEventListener('dragend', onEnd);
    },
  };
}
