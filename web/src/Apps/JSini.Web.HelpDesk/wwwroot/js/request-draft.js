// 요청 등록 화면에서 **쓰던 글을 브라우저에 적어 두는** 자리.
//
// [무엇을 고친 것인가]
//
// 제목과 본문을 한참 적다가 새로고침(F5 · 되돌아가기 · 회로가 끊겨 화면이
// 스스로 다시 뜨는 것)이 한 번 일어나면 **적던 것이 통째로 사라졌다.**
// 헬프데스크에 오는 글은 증상을 설명하느라 길고, 그림까지 붙이고 나서 잃으면
// 처음부터 다시 해야 한다.
//
// [왜 C# 이 아니라 여기서 적나]
//
// Blazor Server 는 글 한 자가 회로(웹소켓)를 타고 서버로 가야 화면 뒤의 값이
// 된다. 그런데 **잃는 경우의 절반이 바로 그 회로가 끊긴 때**다 — 지하철·
// 엘리베이터에서 연결이 끊긴 채 계속 적다가, 재연결에 실패한 Blazor 가
// `location.reload()` 로 화면을 새로 띄우는 그 순간이다. 그때 C# 쪽에는 그
// 글자가 **닿은 적이 없다.**
//
// 브라우저의 `input` 사건은 회로와 아무 상관이 없다. 여기서 바로 적어 두면
// 연결이 죽은 채로 새로고침이 나도 **마지막에 친 글자까지** 남는다.
// 프로젝트관리의 `ask-draft.js` 가 같은 까닭으로 같은 자리에 있다
// (「두 모듈이 쓰면 복제, 세 번째부터 승격」 — web/CLAUDE.md).
//
// [본문은 글상자가 아니라 `contenteditable` 이다]
//
// 본문 편집기(`DxHtmlEditor`)에는 `value` 가 없고, 그 안의
// `[contenteditable="true"]` 칸이 곧 글이다. 그래서 **보이는 것을 그대로**
// 읽는다(`innerHTML`) — `request-editor.js` 의 `readMarkup` 과 같은 방식이고,
// 우리가 DOM 에 직접 꽂은 `<img>` 까지 함께 담긴다는 뜻이다.
//
// 글에 든 그림은 걱정하지 않아도 된다. 붙여넣는 순간 이미 파일이 되어 본문에는
// 주소만 남으므로(`request-editor.js`), 되살린 글에서도 그대로 보인다. 적어 두는
// 것은 그 주소 한 줄이지 그림 자체가 아니다.
//
// 사건은 편집기 **껍데기**에서 받는다. `input` 은 거품처럼 올라오므로 안쪽
// 칸을 짚지 않아도 되고, 편집기가 그 칸을 다시 만들어도 처리기가 살아 있다.

/// 입력이 이만큼(ms) 멈추면 적는다. 회로를 타지 않으므로 짧게 잡아도 된다 —
/// 잃는 것을 마지막 한 문장 밑으로 줄이는 것이 요점이다.
const DELAY_MS = 400;

/// 이만큼 지난 것은 읽을 때 버린다. 지난주에 쓰다 만 글이 오늘 열린 빈 화면에
/// 얹히면 그건 도움이 아니라 사고다.
const KEEP_DAYS = 7;

/// 한 벌이 이 글자 수를 넘으면 **본문은 빼고 제목만** 적는다.
///
/// `localStorage` 는 도메인마다 5MB 안팎이고 그 자리를 탭 고정·공지 표시와
/// 나눠 쓴다. 넘치면 브라우저가 쓰기를 통째로 거절하므로 그 앞에서 우리가
/// 줄인다. 본문이 이만큼 커지는 것은 **다른 글에서 서식째 복사해** data URI
/// 가 박힌 경우뿐이다(붙여넣은 그림은 곧바로 파일이 되어 주소만 남는다).
const MAX_CHARS = 512 * 1024;

/// 지금 지켜보고 있는 화면들. 화면 하나에 하나다.
const watchers = new Set();

/// 편집기의 글 쓰는 칸. 찾는 시점을 미루는 까닭은 `request-editor.js` 와 같다.
function areaIn(box) {
    return box ? box.querySelector('[contenteditable="true"]') : null;
}

function areaOf(selector) {
    return areaIn(document.querySelector(selector));
}

/// 제목 칸. `DxTextBox` 가 그리는 `<input>` 을 껍데기 안에서 찾는다 —
/// DevExpress 안쪽 이름을 짚지 않는다(판올림마다 바뀔 자리다).
function inputIn(box) {
    return box ? box.querySelector('input') : null;
}

/// **빈 편집기인가.** 브라우저마다 `<p><br></p>` 같은 뼈대를 남기므로 글자
/// 길이로 본다. 그림만 있고 글자가 없는 것은 빈 글이 아니다.
/// `request-editor.js` 의 `readMarkup` 과 같은 잣대여야 한다 — 다르면 등록은
/// 되는데 임시 보관은 안 되는(또는 그 반대) 글이 생긴다.
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

    return { title, html, savedAt: at };
}

// [**되살리는 쪽은 여기 없다** — 편집기가 받아 준다]
//
// 처음에는 여기서 `innerHTML` 로 직접 얹었다. 편집기가 첫 그리기 뒤의
// `Markup` 변화를 흘릴까 봐 둔 길이었는데, **재어 보니 그러지 않는다** —
// 헤드리스로 띄워 되살리기와 「지우고 새로 쓰기」를 몰아 보니 둘 다
// 화면(`Markup`) 쪽이 먼저 끝내 놓아 여기가 할 일이 없었다
// (DevExpress 26.1.4). 그래서 걷었다. 돌지 않는 길은 언젠가 썩는다.
//
// 읽어 가는 쪽만 편집기에게 못 맡긴다(`request-editor.js` 의 `readMarkup`) —
// 그쪽은 값을 늦게 알려 주기 때문이고, 넣는 쪽과는 사정이 다르다.

/// 적어 둔 것을 버린다. **지켜보기는 이어 간다** — 사람이 「지우고 새로 쓰기」를
/// 누른 뒤 다시 적는 글은 또 적어 두어야 한다.
export function forget(key) {
    for (const w of watchers) {
        if (w.key !== key) continue;

        if (w.timer) {
            clearTimeout(w.timer);
            w.timer = null;
        }
    }

    drop(key);
}

/// 지켜보기를 거둔다. `alsoForget` 이면 적어 둔 것도 함께 버린다.
///
/// 등록에 성공한 뒤가 그 경우다 — 그 글은 이미 서버에 들어갔으므로, 남겨 두면
/// 다음에 이 화면을 열 때 **이미 등록한 글이 되살아나** 같은 요청을 두 번 넣게 된다.
export function stop(key, alsoForget) {
    for (const w of [...watchers]) {
        if (w.key === key) detach(w);
    }

    if (alsoForget) drop(key);
}

function detach(w) {
    if (w.timer) {
        clearTimeout(w.timer);
        w.timer = null;
    }

    w.box.removeEventListener('input', w.onInput);
    w.title.removeEventListener('input', w.onInput);
    document.removeEventListener('visibilitychange', w.onHide);
    window.removeEventListener('pagehide', w.onHide);

    watchers.delete(w);
}

/// 지금 화면에 있는 것을 적는다.
function write(w) {
    w.timer = null;

    const area = areaIn(w.box);
    const title = inputIn(w.title);

    // 편집기가 화면에서 사라졌다. **적어 둔 것을 건드리지 않는다** — 여기서
    // 빈 글로 덮으면 화면을 떠나는 순간 임시본이 지워진다.
    if (!area || !title) return;

    const text = title.value || '';
    const html = blank(area) ? '' : area.innerHTML;

    if (text.trim() === '' && html === '') {
        drop(w.key);
        return;
    }

    let payload = JSON.stringify({ t: text, h: html, at: Date.now() });
    let short = false;

    if (payload.length > MAX_CHARS) {
        payload = JSON.stringify({ t: text, h: '', at: Date.now() });
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

/// **임시 보관이 안 되고 있다**고 화면에 알린다. 한 번만 알린다.
///
/// 이것이 없으면 사람은 적히고 있는 줄 알고 계속 쓴다. 아예 없는 것보다
/// 나쁜 상태다.
function tell(w) {
    if (w.told || !w.owner) return;

    w.told = true;

    try {
        w.owner.invokeMethodAsync('OnDraftBlocked');
    } catch (e) {
        // 회로가 닫히는 중이다. 알릴 화면이 없다.
    }
}

/// 제목 칸과 편집기를 지켜보기 시작한다. 아직 화면에 없으면 거짓 —
/// 부르는 쪽이 다음 그리기에 다시 본다(`request-editor.js` 의 `bind` 와 같다).
export function attach(key, titleSelector, editorSelector, owner) {
    const box = document.querySelector(editorSelector);
    const title = document.querySelector(titleSelector);

    if (!box || !areaIn(box) || !title || !inputIn(title)) return false;

    for (const w of watchers) {
        if (w.key === key && w.box === box) return true;
    }

    const w = { key, box, title, owner, timer: null, told: false };

    w.onInput = () => {
        // 화면이 사라졌다. 손을 뗀다 — 화면을 옮길 때마다 하나씩 쌓인다.
        if (!box.isConnected) {
            detach(w);
            return;
        }

        if (w.timer) clearTimeout(w.timer);
        w.timer = setTimeout(() => write(w), DELAY_MS);
    };

    // 탭을 닫거나 다른 탭으로 옮기면 기다리던 것을 바로 적는다. 휴대폰에서는
    // 화면을 가리는 것이 곧 **앱이 접히는 것**이라, 여기서 안 적으면 다시
    // 돌아왔을 때 마지막 몇 자가 비어 있다. (다시 보일 때도 불리지만 그때는
    // 기다리던 것이 없어 아무 일도 하지 않는다.)
    w.onHide = () => flush(w);

    watchers.add(w);

    box.addEventListener('input', w.onInput);
    title.addEventListener('input', w.onInput);
    document.addEventListener('visibilitychange', w.onHide);
    window.addEventListener('pagehide', w.onHide);

    return true;
}
