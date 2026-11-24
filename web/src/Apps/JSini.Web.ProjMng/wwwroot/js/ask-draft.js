/**
 * 빠른 지시 글상자의 실시간 임시저장. 회선이 끊긴 동안의 입력은 서버에 닿지 않으므로 브라우저 input 이벤트에서 바로 localStorage 에 적는다 — 끊긴 채 새로고침이 나도 마지막 글자가 산다.
 */

const attached = new WeakSet();

export function attachDraft(selector, storageKey) {
  const container = document.querySelector(selector);
  if (!container) return;

  const textarea = container.querySelector('textarea') || container;
  if (!textarea || attached.has(textarea)) return;

  attached.add(textarea);

  let timer = null;

  const save = () => {
    try {
      const text = textarea.value;
      const raw = localStorage.getItem(storageKey);
      let data = raw ? JSON.parse(raw) : {};

      if (text && text.trim().length > 0) {
        data.DraftText = text;
        data.DraftSavedAt = new Date().toISOString();
      } else {
        delete data.DraftText;
        delete data.DraftSavedAt;
      }

      localStorage.setItem(storageKey, JSON.stringify(data));
    } catch (e) {
      // localStorage 가 막혀 있거나 꽉 찬 경우
    }
  };

  textarea.addEventListener('input', () => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(save, 150);
  });
}

export function clearDraft(selector, storageKey) {
  try {
    const raw = localStorage.getItem(storageKey);
    if (raw) {
      const data = JSON.parse(raw);
      delete data.DraftText;
      delete data.DraftSavedAt;
      localStorage.setItem(storageKey, JSON.stringify(data));
    }
  } catch (e) {
  }

  const container = document.querySelector(selector);
  if (container) {
    const textarea = container.querySelector('textarea') || container;
    if (textarea) {
      textarea.value = '';
    }
  }
}

/**
 * 글상자에서 Ctrl+Enter(맥은 ⌘+Enter)를 「지시하기」로 넘긴다. Blazor 의 onkeydown 을 쓰지 않는 까닭: preventDefault 가 키를 가리지 않고 걸려 타자가 안 먹고, 값도 300ms 를 모아 올리므로 누른 순간의 textarea.value 를 함께 보낸다.
 */
const keyed = new WeakSet();

export function attachSubmitKey(selector, dotNetRef) {
  const container = document.querySelector(selector);
  if (!container) return;

  const textarea = container.querySelector('textarea') || container;
  if (!textarea || keyed.has(textarea)) return;

  keyed.add(textarea);

  textarea.addEventListener('keydown', (e) => {
    if (e.key !== 'Enter') return;
    if (!e.ctrlKey && !e.metaKey) return;

    // 한글 조합 중에는 키가 'Process'/229 로 와서 여기까지 오지 않는다.
    if (e.isComposing) return;

    e.preventDefault();
    e.stopPropagation();

    try {
      dotNetRef.invokeMethodAsync('SubmitFromKeyAsync', textarea.value);
    } catch (err) {
      // 회로가 이미 끊긴 경우
    }
  });
}

/**
 * 글상자에 붙여넣은(또는 끌어다 놓은) 그림을 숨긴 파일 칸(InputFile)으로 넘긴다. 바이트는 그 칸을 타고 회로로 가고, 서버가 첨부로 올린 뒤 insertImageMarks 로 글상자에 표지를 꽂는다.
 * 그림을 글에 data URI 로 박지 않는다 — 지시문은 실행기에 글로 가므로 그림이 첨부여야 모델이 본다. 이름은 여기서 짓는다: 클립보드 그림은 늘 image.png 라 지시문의 표지가 어느 첨부인지 가리킬 수 없다.
 */
const pasted = new WeakSet();
const pasteCarets = new WeakMap();

function clipImages(data) {
  if (!data) return [];

  const files = data.files ? Array.from(data.files) : [];
  const picked = files.filter(f => f && f.type && f.type.startsWith('image/'));
  if (picked.length > 0) return picked;

  // 캡처 도구에서 복사한 것은 files 가 비고 items 에만 드는 브라우저가 있다.
  if (!data.items) return [];

  return Array.from(data.items)
    .filter(i => i.kind === 'file' && i.type && i.type.startsWith('image/'))
    .map(i => i.getAsFile())
    .filter(Boolean);
}

function stamp() {
  const d = new Date();
  const p = n => String(n).padStart(2, '0');
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
}

function extOf(file) {
  const fromName = /\.([a-z0-9]{2,5})$/i.exec(file.name || '');
  if (fromName) return fromName[1].toLowerCase();

  const sub = (file.type.split('/')[1] || 'png').toLowerCase();
  return sub === 'jpeg' ? 'jpg' : sub.replace(/[^a-z0-9]/g, '') || 'png';
}

export function attachPasteImages(selector, inputId) {
  const container = document.querySelector(selector);
  if (!container) return;

  const textarea = container.querySelector('textarea') || container;
  if (!textarea || pasted.has(textarea)) return;

  pasted.add(textarea);

  const hand = (e, data) => {
    const images = clipImages(data);

    // 그림이 없으면 손대지 않는다 — 글자 붙여넣기는 브라우저가 한다.
    if (images.length === 0) return;

    const input = document.getElementById(inputId);
    if (!input) return;

    e.preventDefault();
    e.stopPropagation();

    // 올리는 동안 커서가 옮겨 가도 붙여넣은 자리에 표지를 꽂는다.
    pasteCarets.set(textarea, { start: textarea.selectionStart, end: textarea.selectionEnd });

    const base = stamp();
    const transfer = new DataTransfer();

    images.forEach((image, i) => {
      const name = `clipboard-${base}${images.length > 1 ? `-${i + 1}` : ''}.${extOf(image)}`;
      transfer.items.add(new File([image], name, { type: image.type || 'image/png' }));
    });

    input.files = transfer.files;
    input.dispatchEvent(new Event('change', { bubbles: true }));
  };

  textarea.addEventListener('paste', e => hand(e, e.clipboardData));
  textarea.addEventListener('dragover', e => {
    if (e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files')) e.preventDefault();
  });
  textarea.addEventListener('drop', e => hand(e, e.dataTransfer));
}

/**
 * 올라간 그림의 표지를 붙여넣은 자리에 꽂고 바뀐 글 전체를 돌려준다. C# 이 그 값을 글상자 값으로 삼는다 — 편집기가 합성 input 을 언제 올려 보낼지에 기대지 않는다.
 * input 사건은 임시저장(attachDraft)이 듣도록 쏜다.
 */
export function insertImageMarks(selector, marks) {
  const container = document.querySelector(selector);
  const textarea = container ? (container.querySelector('textarea') || container) : null;
  if (!textarea || typeof textarea.value !== 'string') return null;

  const value = textarea.value;
  const saved = pasteCarets.get(textarea);
  pasteCarets.delete(textarea);

  let start = saved ? saved.start : textarea.selectionStart;
  let end = saved ? saved.end : textarea.selectionEnd;
  if (typeof start !== 'number' || start > value.length) start = value.length;
  if (typeof end !== 'number' || end > value.length || end < start) end = start;

  // 표지는 제 줄에 선다 — 앞뒤 글자에 붙으면 이름이 문장에 섞여 읽힌다.
  const before = value.slice(0, start);
  const after = value.slice(end);
  const lead = before.length > 0 && !before.endsWith('\n') ? '\n' : '';
  const tail = after.length > 0 && !after.startsWith('\n') ? '\n' : '';
  const insert = `${lead}${marks}${tail}`;

  textarea.value = before + insert + after;

  const caret = before.length + insert.length;
  try {
    textarea.setSelectionRange(caret, caret);
  } catch (e) {
  }

  textarea.dispatchEvent(new Event('input', { bubbles: true }));
  return textarea.value;
}
