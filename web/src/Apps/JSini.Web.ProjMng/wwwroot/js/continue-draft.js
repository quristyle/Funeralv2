/**
 * 「이어서 지시」 창의 글상자 임시저장. 창은 새로고침에 사라지므로 브라우저가 칠 때마다 적는다 — 연결이 끊긴 동안의 입력은 서버에 닿지 않아 C# 의 값은 비어 있다.
 * 열쇠는 PascalCase 로 적는다(C# AiContinueDraft 가 그대로 읽는다). 고쳐 쓰는 것은 전부 merge 가 한다 — 제 손의 덩어리를 통째로 다시 적으면 방금 친 글자가 덮인다.
 */

/** 이만큼 지난 임시본은 읽을 때 버린다. */
const KEEP_DAYS = 7;

/** 들고 있을 임시본 수. 넘으면 오래된 것부터 버린다. */
const MAX_ITEMS = 20;

/** 글상자에 건 처리기를 그 요소에 적어 둔다. 같은 글상자에 작업 번호만 바뀌어 다시 걸리므로 걸렸나가 아니라 무엇이 걸렸나를 들고 있다가 떼고 다시 건다 — 두 번째 건의 글이 첫 건의 자리에 적히지 않게. */
const HANDLER = '__jsiniContinueDraftHandler';

/** 저장할 때 찍는 때. 현지 시각이다 — toISOString() 은 UTC 라 되살릴 때 아홉 시간 어긋난다. */
function nowLocal() {
  const d = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
       + `T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

function load(storageKey) {
  try {
    const raw = localStorage.getItem(storageKey);
    if (!raw) return {};

    const map = JSON.parse(raw);
    return map && typeof map === 'object' ? map : {};
  } catch (e) {
    // 저장소가 막혔거나 깨졌다. 빈 것으로 보고 넘어간다 — 임시본 때문에 창이 안 열리면 안 된다.
    return {};
  }
}

/** 오래된 것 · 넘치는 것을 버린다. 방금 적은 것은 언제나 남는다. */
function prune(map) {
  const limit = new Date(Date.now() - KEEP_DAYS * 24 * 60 * 60 * 1000);

  const keys = Object.keys(map)
    .filter((k) => {
      const at = map[k] && map[k].SavedAt ? new Date(map[k].SavedAt) : null;
      return !at || isNaN(at) || at >= limit;
    })
    .sort((a, b) => new Date(map[b].SavedAt || 0) - new Date(map[a].SavedAt || 0));

  const kept = {};
  keys.slice(0, MAX_ITEMS).forEach((k) => { kept[k] = map[k]; });
  return kept;
}

function save(storageKey, map) {
  try {
    const kept = prune(map);

    if (Object.keys(kept).length === 0) {
      localStorage.removeItem(storageKey);
    } else {
      localStorage.setItem(storageKey, JSON.stringify(kept));
    }
  } catch (e) {
    // 용량을 넘겼거나 사생활 보호 모드다. 임시저장만 안 되는 것으로 둔다.
  }
}

/**
 * 한 건의 값을 고쳐 적는다. 읽고-합치고-적기를 한 번에 한다.
 * addition·runnerKind 가 undefined 면 그 칸은 건드리지 않는다 — 글만 적는 쪽과 AI 만 적는 쪽이 서로를 지우면 안 된다.
 */
function merge(storageKey, taskKey, addition, runnerKind) {
  const map = load(storageKey);
  const key = String(taskKey);
  const now = map[key] || {};

  if (addition !== undefined) {
    now.Addition = addition;
  }

  if (runnerKind !== undefined) {
    now.RunnerKind = runnerKind;
  }

  const empty = !(now.Addition && now.Addition.trim().length > 0);

  // 적을 것이 없으면 줄째 지운다 — AI 만 골라 두고 나간 건이 되살릴 것으로 뜨면 창을 열 때마다 빈 안내 줄이 선다.
  if (empty) {
    delete map[key];
  } else {
    now.SavedAt = nowLocal();
    map[key] = now;
  }

  save(storageKey, map);
}

/** 적어 둔 한 건을 돌려준다. 없으면 <c>null</c>. */
export function read(storageKey, taskKey) {
  const map = load(storageKey);
  const now = map[String(taskKey)];

  return now && now.Addition && now.Addition.trim().length > 0 ? now : null;
}

/** 이 회차를 맡을 AI 를 적어 둔다. 적은 글이 없으면 아무것도 남기지 않는다. */
export function saveKind(storageKey, taskKey, runnerKind) {
  merge(storageKey, taskKey, undefined, runnerKind);
}

/** 글상자에 실시간 감시를 건다. 같은 글상자에 두 번 걸지 않는다. 창이 막 열려 글상자가 아직 없을 수 있어 몇 번 다시 찾는다. */
export function attach(selector, storageKey, taskKey) {
  let left = 20;

  const tryAttach = () => {
    const container = document.querySelector(selector);
    const textarea = container ? (container.querySelector('textarea') || container) : null;

    if (!textarea) {
      if (left-- > 0) setTimeout(tryAttach, 50);
      return;
    }

    if (textarea[HANDLER]) {
      textarea.removeEventListener('input', textarea[HANDLER]);
    }

    let timer = null;

    const handler = () => {
      if (timer) clearTimeout(timer);
      timer = setTimeout(() => merge(storageKey, taskKey, textarea.value, undefined), 150);
    };

    textarea[HANDLER] = handler;
    textarea.addEventListener('input', handler);
  };

  tryAttach();
}

/** 한 건의 임시본을 버리고 글상자도 비운다. */
export function clear(selector, storageKey, taskKey) {
  const map = load(storageKey);
  delete map[String(taskKey)];
  save(storageKey, map);

  if (!selector) return;

  const container = document.querySelector(selector);
  const textarea = container ? (container.querySelector('textarea') || container) : null;

  if (textarea && 'value' in textarea) {
    textarea.value = '';
  }
}
