/**
 * 「이어서 지시」 창의 글상자 임시저장 도우미.
 *
 * [무엇을 고친 것인가]
 *
 * 끝난 건에 이어서 시킬 것을 적는 창은 **창일 뿐**이라, 새로고침 한 번이면
 * 창째 사라지고 적던 글도 함께 없어졌다. 회로가 끊겨 Blazor 가 스스로
 * 새로고침하는 경우에도 같다. 「빠른 지시」의 큰 글상자는 이미 이 짓을
 * 막아 두었는데(`ask-draft.js`) 이 창만 빠져 있었다.
 *
 * [왜 JS 가 직접 localStorage 에 적나]
 *
 * `ask-draft.js` 머리말과 같은 까닭이다 — Blazor Server 는 모든 이벤트가
 * 웹소켓을 타고 서버로 왕복하는데, 연결이 끊긴 채로 계속 치면 그 입력은
 * 서버에 닿은 적이 없다. 그 상태에서 재연결에 실패해 화면이 새로고침되면
 * C# 이 들고 있던 값은 애초에 비어 있다. 브라우저의 `input` 이벤트는 연결
 * 상태와 무관하므로, 칠 때마다 여기서 바로 적어 두면 마지막 글자까지 산다.
 *
 * [왜 파일을 따로 두나]
 *
 * 담는 모양이 다르다. 빠른 지시는 글상자가 **하나**라 설정 덩어리에 글
 * 한 줄을 얹으면 됐지만(`ask-draft.js`), 이어서 지시는 **건마다 따로**
 * 적어 둬야 한다 — 열어 둔 건이 여럿일 수 있고, 한 건을 보낸 뒤에 그 건의
 * 것만 버려야 한다.
 *
 * [담는 모양]
 *
 *   localStorage["jsini-ai-continue-drafts:<로그인아이디>"] = {
 *     "<작업번호>": { Addition, RunnerKind, SavedAt }, …
 *   }
 *
 * 열쇠 이름을 PascalCase 로 적는 것은 **C# 쪽이 그대로 읽기 때문**이다
 * (`AiContinueDraft`). Blazor 의 JSON 설정이 대소문자를 가리지 않으므로
 * 읽기는 어느 쪽이든 되지만, 여기서 적은 것과 저쪽이 적은 것이 한 덩이에
 * 섞이므로 **한 가지로 통일해 둔다.**
 *
 * [고쳐 쓰는 것은 전부 여기다]
 *
 * C# 이 따로 `setItem` 을 하지 않는다. 저쪽이 제 손에 든 덩어리를 통째로
 * 다시 적으면, 그 사이에 브라우저가 적어 둔 **방금 친 글자를 덮어쓴다** —
 * 고치려던 바로 그 사고다. 그래서 C# 은 읽기만 하고, 값을 바꿀 일이 있으면
 * 아래 함수를 불러 **읽고-합치고-적는 것을 한 번에** 시킨다.
 */

/** 이만큼 지난 임시본은 읽을 때 버린다. */
const KEEP_DAYS = 7;

/** 들고 있을 임시본 수. 넘으면 오래된 것부터 버린다. */
const MAX_ITEMS = 20;

/**
 * 글상자에 걸어 둔 처리기를 그 요소에 적어 두는 자리.
 *
 * **같은 글상자에 작업 번호만 바뀌어 다시 걸리는 일이 흔하다** — 「빠른 지시」의
 * 창은 한 벌뿐이고, 카드를 바꿔 누르면 그 한 벌이 다른 건을 담는다. 걸린 적이
 * 있으면 건너뛰게 두면, 두 번째로 연 건의 글이 **첫 건의 자리에 적힌다.**
 * 그래서 「걸렸나」가 아니라 「무엇이 걸려 있나」를 들고 있다가 떼고 다시 건다.
 */
const HANDLER = '__jsiniContinueDraftHandler';

/** 저장할 때 찍는 때. **현지 시각**이다 — `toISOString()` 은 UTC 라 되살릴 때 아홉 시간 어긋난다. */
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
    // 저장소가 막혔거나 적힌 것이 깨졌다. 빈 것으로 보고 넘어간다 —
    // 임시본 때문에 창이 안 열리면 안 된다.
    return {};
  }
}

/** 오래된 것 · 넘치는 것을 버린다. 방금 적은 것은 언제나 남는다(가장 최근이다). */
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
 * 한 건의 값을 고쳐 적는다. <b>읽고-합치고-적는 것을 한 번에</b> 한다.
 *
 * `addition` 과 `runnerKind` 는 `undefined` 로 넘기면 「그 칸은 건드리지
 * 말라」는 뜻이다. 글상자만 적는 쪽과 AI 칸만 적는 쪽이 서로를 지우면 안 된다.
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

  // **적을 것이 없으면 줄째 지운다.** AI 만 골라 두고 나간 건이 「되살릴
  // 것이 있다」로 뜨면, 창을 열 때마다 아무 내용 없는 안내 줄이 선다.
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

/**
 * 글상자에 실시간 감시를 건다. 같은 글상자에 두 번 걸지 않는다.
 *
 * 창이 막 열린 참이라 글상자가 아직 DOM 에 없을 수 있어 <b>몇 번 다시
 * 찾아본다</b> — 한 번 보고 없으면 그 창은 끝까지 안 적힌다.
 */
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
