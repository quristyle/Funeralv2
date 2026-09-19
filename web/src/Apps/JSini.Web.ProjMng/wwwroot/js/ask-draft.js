/**
 * 빠른 지시(AiAsk) 글상자 실시간 임시저장 도우미.
 *
 * [왜 JS 가 직접 localStorage 에 적나]
 *
 * Blazor Server 는 모든 이벤트가 웹소켓을 타고 서버로 왕복한다.
 * 그런데 엘리베이터나 지하철, 회선 불안정으로 서버와의 연결이 끊긴 상태에서
 * 사용자가 계속 글을 적으면, 그 입력은 서버로 가지 못한다.
 * 그 상태에서 Blazor 가 재연결에 실패해 화면을 새로고침(location.reload)하면
 * C# 측에는 입력이 닿은 적이 없으므로 전부 사라진다.
 *
 * 브라우저 DOM 의 `input` 이벤트는 서버 연결 상태와 전혀 무관하게 동작한다.
 * 사용자가 키를 누를 때마다 브라우저가 즉시 localStorage 에 적어 두면,
 * 통신이 완전히 끊긴 채 새로고침이 일어나도 방금 친 마지막 글자까지
 * 완벽하게 살아남는다.
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
