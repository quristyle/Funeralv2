// 요청 등록 화면에서 쓰던 글을 브라우저에 적어 두는 자리. 회로가 끊기거나 새로고침이 나면 적던 글이 사라지므로 여기서 localStorage 에 바로 적는다.
// 본문은 편집기의 contenteditable 을 그대로 읽는다 — 붙여넣은 그림은 이미 파일 주소라 적는 것은 한 줄이다.
// 사건은 칸이 아니라 문서에서 받는다 — 편집기는 껍데기째 다시 만들어질 수 있어 칸에 걸면 조용히 아무것도 안 적힌다.

/// 입력이 이만큼(ms) 멈추면 적는다. 회로를 타지 않으므로 짧게 잡아도 된다.
const DELAY_MS = 400;

/// 이만큼 지난 것은 읽을 때 버린다 — 지난주에 쓰다 만 글이 오늘 열린 빈 화면에 얹히면 사고다.
const KEEP_DAYS = 7;

/// 한 벌이 이 글자 수를 넘으면 본문은 빼고 제목만 적는다. localStorage 는 도메인마다 5MB 안팎이라 넘치면 쓰기를 통째로 거절하므로 그 앞에서 줄인다.
const MAX_CHARS = 512 * 1024;

/// 지금 지켜보고 있는 화면들. 화면 하나에 하나다.
const watchers = new Set();

/// 화면에서 읽어 낼 수 없는 칸(요청자 고객 번호)을 C# 이 맡겨 둔 자리. 콤보는 화면에 이름이 보여 DOM 에서 긁으면 번호가 안 나오므로 고르는 즉시 C# 이 remember 로 넘긴다.
const extras = new Map();

/// 편집기의 글 쓰는 칸. 찾는 시점을 미루는 까닭은 `request-editor.js` 와 같다.
function areaIn(box) {
    return box ? box.querySelector('[contenteditable="true"]') : null;
}

/// 제목 칸. DxTextBox 가 그리는 input 을 껍데기 안에서 찾는다 — DevExpress 안쪽 이름을 짚지 않는다.
function inputIn(box) {
    return box ? box.querySelector('input') : null;
}

/// 빈 편집기인가. 글자 길이로 본다 — 그림만 있고 글자가 없으면 빈 글이 아니다. request-editor.js 의 readMarkup 과 같은 잣대여야 한다(다르면 등록과 임시 보관이 어긋난다).
function blank(area) {
    return area.innerText.trim() === '' && area.querySelectorAll('img').length === 0;
}

function drop(key) {
    try {
        localStorage.removeItem(key);
    } catch (e) {
        // 저장소가 막혀 있다. 지울 것도 없다.
    }
}

/// 적어 둔 것을 읽는다. 없거나 · 깨졌거나 · 오래됐으면 `null` 이고 그때는 지운다.
export function read(key) {
    let raw = null;

    try {
        raw = localStorage.getItem(key);
    } catch (e) {
        // 사생활 보호 모드 따위로 저장소를 못 읽는다. 임시 보관만 없는 것으로 본다.
        return null;
    }

    if (!raw) return null;

    let saved;

    try {
        saved = JSON.parse(raw);
    } catch (e) {
        drop(key);
        return null;
    }

    if (!saved || typeof saved !== 'object') {
        drop(key);
        return null;
    }

    const at = Number(saved.at) || 0;

    if (!at || Date.now() - at > KEEP_DAYS * 86400000) {
        drop(key);
        return null;
    }

    const title = typeof saved.t === 'string' ? saved.t : '';
    const html = typeof saved.h === 'string' ? saved.h : '';

    if (title.trim() === '' && html.trim() === '') {
        drop(key);
        return null;
    }

    // 요청자만 고르고 글은 안 쓴 것은 이미 버렸다 — 되살릴 것이 없는 한 벌이라서다. 여기 오는 요청자는 글에 딸려 온 것이다.
    const requester = typeof saved.r === 'string' && saved.r !== '' ? saved.r : null;

    // 읽어 간 값은 적는 쪽에도 되돌려 둔다 — 아니면 제목만 고쳐도 다음 write 가 요청자를 잃고 덮어쓴다.
    if (requester) {
        extras.set(key, requester);
    } else {
        extras.delete(key);
    }

    return { title, html, requester, savedAt: at };
}

/// 화면에서 못 읽는 칸(요청자)을 맡아 둔다. null·빈 값이면 잊는다. 맡기자마자 한 벌을 다시 적는다 — 고르기만 하고 글을 안 건드리면 input 이 없어 적을 기회가 없다.
export function remember(key, value) {
    const v = typeof value === 'string' && value !== '' ? value : null;

    if (v) {
        extras.set(key, v);
    } else {
        extras.delete(key);
    }

    for (const w of watchers) {
        if (w.key !== key) continue;

        if (w.timer) {
            clearTimeout(w.timer);
            w.timer = null;
        }

        write(w);
    }
}

// 되살리는 쪽은 여기 없다 — 편집기가 Markup 을 받아 그린다(innerHTML 직접 얹기는 헤드리스로 재어 보니 돌지 않아 걷었다).
// 읽어 가는 쪽만 편집기 값을 못 믿는다(readMarkup) — 그쪽은 값을 늦게 알려 주기 때문이다.

/// 적어 둔 것을 버린다. 지켜보기는 이어 간다 — 「지우고 새로 쓰기」 뒤에 다시 적는 글은 또 적어 두어야 한다.
export function forget(key) {
    for (const w of watchers) {
        if (w.key !== key) continue;

        if (w.timer) {
            clearTimeout(w.timer);
            w.timer = null;
        }
    }

    // 맡아 둔 요청자도 함께 버린다 — 남기면 비운 화면에 글을 다시 적는 순간 지운 요청자가 되살아나 남의 이름으로 뜬다.
    extras.delete(key);

    drop(key);
}

/// 지켜보기를 거둔다. alsoForget 이면 적어 둔 것도 버린다 — 등록에 성공한 뒤가 그 경우다(남기면 이미 등록한 글이 되살아나 두 번 들어간다).
export function stop(key, alsoForget) {
    for (const w of [...watchers]) {
        if (w.key === key) detach(w);
    }

    if (alsoForget) {
        extras.delete(key);
        drop(key);
    }
}

function detach(w) {
    if (w.timer) {
        clearTimeout(w.timer);
        w.timer = null;
    }

    document.removeEventListener('input', w.onInput, true);
    document.removeEventListener('focusout', w.onBlur, true);
    document.removeEventListener('visibilitychange', w.onHide);
    window.removeEventListener('pagehide', w.onHide);
    window.removeEventListener('beforeunload', w.onHide);

    watchers.delete(w);
}

/// 지금 화면에 있는 것을 적는다.
function write(w) {
    w.timer = null;

    const area = areaIn(document.querySelector(w.editorSelector));
    const title = inputIn(document.querySelector(w.titleSelector));

    // 편집기가 화면에서 사라졌다 — 적어 둔 것을 건드리지 않는다. 빈 글로 덮으면 떠나는 순간 임시본이 지워진다.
    if (!area || !title) return;

    const text = title.value || '';
    const html = blank(area) ? '' : area.innerHTML;

    // 요청자 하나만 고른 것은 적지 않는다 — 되살려도 글이 없어 안내만 뜬다. 입력한 적이 없는 초기화·복원 중에는 빈 판정으로 기존 보관 글을 지우지 않는다.
    if (text.trim() === '' && html === '') {
        if (w.hasUserTyped) {
            drop(w.key);
        }
        return;
    }

    const requester = extras.get(w.key) || '';

    let payload = JSON.stringify({ t: text, h: html, r: requester, at: Date.now() });
    let short = false;

    // 줄일 때도 요청자는 남긴다.
    if (payload.length > MAX_CHARS) {
        payload = JSON.stringify({ t: text, h: '', r: requester, at: Date.now() });
        short = true;
    }

    try {
        localStorage.setItem(w.key, payload);
    } catch (e) {
        // 저장소가 막혔거나 꽉 찼다.
        tell(w);
        return;
    }

    if (short) tell(w);
}

/// 적기를 기다리지 않고 바로 적는다(탭을 닫거나 가릴 때).
function flush(w) {
    if (!w.timer) return;

    clearTimeout(w.timer);
    write(w);
}

/// 임시 보관이 안 되고 있다고 화면에 한 번만 알린다 — 알리지 않으면 사람은 적히는 줄 알고 계속 쓴다.
function tell(w) {
    if (w.told || !w.owner) return;

    w.told = true;

    try {
        w.owner.invokeMethodAsync('OnDraftBlocked');
    } catch (e) {
        // 회로가 닫히는 중이다. 알릴 화면이 없다.
    }
}

/// 제목 칸과 편집기를 지켜보기 시작한다. 칸이 아직 없어도 건다 — 적을 때마다 이름으로 다시 찾는다.
export function attach(key, titleSelector, editorSelector, owner) {
    for (const w of watchers) {
        if (w.key === key) {
            w.owner = owner;
            return true;
        }
    }

    const w = { key, titleSelector, editorSelector, owner, timer: null, told: false, hasUserTyped: false };

    const mine = (e) => {
        const at = e.target instanceof Element ? e.target : null;
        return !!at && !!(at.closest(editorSelector) || at.closest(titleSelector));
    };

    w.onInput = (e) => {
        // 사람이 친 것만 센다 — 스크립트가 쏜 사건으로 빈 칸을 적으면 되살릴 글을 덮는다.
        if (!e.isTrusted || !mine(e)) return;

        w.hasUserTyped = true;
        if (w.timer) clearTimeout(w.timer);
        w.timer = setTimeout(() => write(w), DELAY_MS);
    };

    w.onBlur = (e) => {
        if (mine(e)) flush(w);
    };

    // 탭을 닫거나 옮기거나 새로고침할 때 기다리던 것을 바로 적는다 — 휴대폰은 가리는 것이 곧 앱이 접히는 것이라 여기서 안 적으면 마지막 몇 자가 빈다.
    w.onHide = () => flush(w);

    watchers.add(w);

    document.addEventListener('input', w.onInput, true);
    document.addEventListener('focusout', w.onBlur, true);
    document.addEventListener('visibilitychange', w.onHide);
    window.addEventListener('pagehide', w.onHide);
    window.addEventListener('beforeunload', w.onHide);

    return true;
}

/// 지금 화면에 있는 것을 바로 적는다. 지켜보기는 input 에 걸려 있어 C# 이 값을 넣은 때(AI 제목 고르기)는 알리지 않으면 빠진다.
/// 미루는 시간은 치는 글과 같다(DELAY_MS) — 한 틱만 미루면 DOM 이 옛것이라 빈 값이 적힌다. Blazor 의 묶음과 이 부름이 회로를 따로 건너와 순서를 믿을 수 없다.
export function touch(key) {
    for (const w of watchers) {
        if (w.key !== key) continue;

        w.hasUserTyped = true;
        if (w.timer) clearTimeout(w.timer);
        w.timer = setTimeout(() => write(w), DELAY_MS);
        return true;
    }

    return false;
}
