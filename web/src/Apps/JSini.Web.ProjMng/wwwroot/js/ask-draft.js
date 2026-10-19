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
