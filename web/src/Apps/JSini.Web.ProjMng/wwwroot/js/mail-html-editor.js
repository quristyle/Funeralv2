/**
 * 메일 본문 미리보기를 그 자리에서 고치는 편집기. iframe 문서를 designMode 로 연다.
 * DxHtmlEditor(Quill)를 쓰지 않는 까닭: 메일 본문은 표 안의 표와 인라인 스타일로 짜여 있는데
 * Quill 은 그것을 제 문서 모형으로 다시 써서 div 를 p 로 바꾸고 겹친 표를 풀어 버린다 — 보낸 메일의 모양이 무너진다.
 * 브라우저의 designMode 는 받은 HTML 을 그대로 두고 고친 자리만 바꾼다.
 * 타이핑마다 회로를 왕복하지 않게 모아서(debounce) 올리고, 보내기 직전에는 read() 로 바로 읽는다.
 */

const states = new WeakMap();

const DEBOUNCE_MS = 400;

/** 받은 HTML 이 문서 전체(<html>…)였으면 문서째, 조각이었으면 body 안만 돌려준다 — 받은 꼴 그대로 돌려줘야 머리의 <style> 이 안 사라진다. */
function serialize(frame) {
  const doc = frame.contentDocument;
  if (!doc?.body) return null;

  const src = frame.getAttribute('srcdoc') || '';
  if (/<html[\s>]/i.test(src)) {
    const doctype = /^\s*<!doctype/i.test(src) ? '<!DOCTYPE html>\n' : '';
    return doctype + doc.documentElement.outerHTML;
  }

  return doc.body.innerHTML;
}

function arm(frame, state) {
  const doc = frame.contentDocument;
  if (!doc?.body || state.armedDoc === doc) return;

  state.armedDoc = doc;
  doc.designMode = 'on';

  doc.addEventListener('input', () => {
    clearTimeout(state.timer);
    state.timer = setTimeout(() => push(frame, state), DEBOUNCE_MS);
  });

  // 링크를 눌러도 iframe 안에서 다른 쪽으로 넘어가지 않게 한다 — 고치려고 누른 것이다.
  doc.addEventListener('click', e => {
    if (e.target?.closest?.('a')) e.preventDefault();
  });

  frame.dataset.mailEditorArmed = '1';
}

function push(frame, state) {
  clearTimeout(state.timer);
  state.timer = null;

  const html = serialize(frame);
  if (html == null || html === state.last) return;

  state.last = html;
  state.dotnet?.invokeMethodAsync('OnEdited', html).catch(() => { });
}

/** iframe 과 도구줄을 건다. srcdoc 이 바뀌어 문서가 새로 서면 load 에서 다시 연다. */
export function attach(frame, toolbar, dotnet) {
  if (!frame) return false;

  let state = states.get(frame);
  if (!state) {
    state = { dotnet, timer: null, last: null, armedDoc: null };
    states.set(frame, state);

    frame.addEventListener('load', () => { state.last = null; arm(frame, state); });

    toolbar?.addEventListener('mousedown', e => {
      // 단추로 초점이 넘어가면 iframe 안의 고른 글이 풀린다.
      if (e.target.closest('[data-cmd]')) e.preventDefault();
    });

    toolbar?.addEventListener('click', e => {
      const btn = e.target.closest('[data-cmd]');
      if (!btn) return;
      exec(frame, btn.dataset.cmd, btn.dataset.value ?? null);
    });

    toolbar?.addEventListener('input', e => {
      const pick = e.target.closest('input[data-cmd]');
      if (pick) exec(frame, pick.dataset.cmd, pick.value);
    });
  }

  state.dotnet = dotnet;
  arm(frame, state);
  return true;
}

function exec(frame, cmd, value) {
  const doc = frame.contentDocument;
  const state = states.get(frame);
  if (!doc || !state) return;

  if (cmd === 'createLink') {
    value = window.prompt('링크 주소', 'https://');
    if (!value) return;
  }

  frame.contentWindow?.focus();
  doc.execCommand('styleWithCSS', false, true);
  doc.execCommand(cmd, false, value);
  push(frame, state);
}

/** 지금 화면의 HTML. 모아 둔 것을 기다리지 않는다 — 보내기 직전에 마지막 몇 자가 빠지지 않게. */
export function read(frame) {
  const state = frame ? states.get(frame) : null;
  if (!state) return null;

  clearTimeout(state.timer);
  state.timer = null;

  const html = serialize(frame);
  if (html != null) state.last = html;
  return html;
}

/** 커서 자리에 글자를 넣는다(치환자 넣기). 커서가 없으면 맨 끝에 붙는다. */
export function insertText(frame, text) {
  const doc = frame?.contentDocument;
  const state = frame ? states.get(frame) : null;
  if (!doc?.body || !state) return false;

  frame.contentWindow?.focus();
  const sel = doc.getSelection();
  if (!sel || sel.rangeCount === 0 || !doc.body.contains(sel.anchorNode)) {
    const range = doc.createRange();
    range.selectNodeContents(doc.body);
    range.collapse(false);
    sel?.removeAllRanges();
    sel?.addRange(range);
  }

  doc.execCommand('insertText', false, text);
  push(frame, state);
  return true;
}

export function detach(frame) {
  const state = frame ? states.get(frame) : null;
  if (!state) return;

  clearTimeout(state.timer);
  state.dotnet = null;
}
