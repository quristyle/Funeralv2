/*
    탭 줄 굴림 — 넘치는지 보고, 양끝 화살표로 밀고, 켜진 탭을 끌어온다.

    [왜 JS 가 필요한가]

    탭 줄(`.jsini-tabs__list`)은 `overflow-x: auto` 라 넘치면 가로로 굴러간다.
    그런데 **넘쳤는지와 지금 어디까지 굴러갔는지는 브라우저만 안다** —
    `scrollWidth`·`clientWidth`·`scrollLeft` 는 그려진 뒤에야 값이 생기고,
    C# 쪽에는 그 값을 받아 볼 길이 없다. 양끝 화살표를 「끝에 닿으면 꺼진
    낯」으로 두려면 그 셋을 재서 되알려 주어야 한다.

    [되알림은 바뀔 때만 보낸다]

    굴릴 때마다 보내면 손가락 한 번에 수십 번이 올라가고, 그때마다 회로가
    탭 줄을 다시 그린다. 그래서 **앞서 보낸 것과 다를 때만** 보낸다.

    [무엇을 듣는가]

    - `scroll` — 사람이 굴렸다.
    - `ResizeObserver` — 창이나 사이드바 폭이 바뀌어 **담을 칸**이 바뀌었다.
    - `MutationObserver` — 탭이 늘거나 줄거나 **이름이 바뀌어** 담길 것이
      바뀌었다. 상자 크기는 그대로라 ResizeObserver 는 울지 않는다.

    [화살표가 늘었다 줄었다 해도 흔들리지 않는다]

    넘칠 때만 화살표를 그리므로 「화살표가 생기면 칸이 좁아져 또 넘친다」가
    된다. 넘침 → 화살표 → 더 좁아짐 → 그대로 넘침(멈춤). 안 넘침 → 화살표
    없음 → 더 넓음 → 그대로 안 넘침(멈춤). 한쪽으로만 가므로 깜박이지 않는다.
*/

/** 보고 있는 탭 줄들. 열쇠는 그 요소다 — 떼어 낼 때 같은 요소로 찾는다. */
const tracked = new Map();

/**
 * 소수점 폭 때문에 `scrollWidth` 가 `clientWidth` 보다 1px 크게 나오는 일이
 * 잦다. 그대로 믿으면 넘치지도 않았는데 화살표가 선다.
 */
const SLACK = 2;

function measure(list) {
    const over = list.scrollWidth - list.clientWidth > SLACK;

    return {
        over,
        left: over && list.scrollLeft > SLACK,
        right: over && list.scrollLeft + list.clientWidth < list.scrollWidth - SLACK,
    };
}

function report(entry, force) {
    const now = measure(entry.list);
    const was = entry.last;

    if (!force && was && was.over === now.over && was.left === now.left && was.right === now.right) {
        return;
    }

    entry.last = now;

    // 회로가 이미 끊겼을 수 있다(기기를 덮었다 열었을 때). 그때 던지는 예외를
    // 그대로 두면 관찰자가 울 때마다 콘솔이 빨개진다.
    entry.dotnet.invokeMethodAsync('OnOverflowChanged', now.over, now.left, now.right).catch(() => { });
}

export function attach(list, dotnet) {
    if (!list) {
        return;
    }

    // 업무를 옮기면 레이아웃이 통째로 다시 만들어져(`MainLayout` 머리말) 이 줄도
    // 새 요소로 선다. 떨어져 나간 옛 요소를 그대로 두면 관찰자와 .NET 손잡이가
    // 회로가 사는 내내 쌓인다 — C# 쪽에서 떼어 주기를 기다리지 않고 여기서 쓴다
    // (떠나는 쪽은 이미 사라진 요소를 가리킬 수 없다).
    for (const old of [...tracked.keys()]) {
        if (!old.isConnected) {
            detach(old);
        }
    }

    detach(list);

    const entry = { list, dotnet, last: null };

    entry.onScroll = () => report(entry, false);
    list.addEventListener('scroll', entry.onScroll, { passive: true });

    entry.ro = new ResizeObserver(() => report(entry, false));
    entry.ro.observe(list);

    entry.mo = new MutationObserver(() => report(entry, false));
    entry.mo.observe(list, { childList: true, subtree: true, characterData: true });

    tracked.set(list, entry);

    report(entry, true);
}

export function detach(list) {
    const entry = list && tracked.get(list);

    if (!entry) {
        return;
    }

    list.removeEventListener('scroll', entry.onScroll);
    entry.ro.disconnect();
    entry.mo.disconnect();
    tracked.delete(list);
}

/**
 * 한 칸 민다. `dir` 이 -1 이면 왼쪽, 1 이면 오른쪽이다.
 *
 * 보이는 폭을 **통째로** 밀지 않는다 — 그러면 방금 보던 탭이 하나도 안 남아
 * 어디서 어디로 간 것인지 눈이 못 따라간다. 8할만 밀어 한두 개를 겹쳐 남긴다.
 */
export function step(list, dir) {
    if (!list) {
        return;
    }

    const by = Math.max(120, Math.round(list.clientWidth * 0.8)) * dir;

    list.scrollBy({ left: by, behavior: 'smooth' });
}

/**
 * 켜진 탭이 가려져 있으면 보이는 자리까지 끌어온다.
 *
 * `scrollIntoView` 를 쓰지 않는다 — 그것은 **쪽 전체**도 함께 굴려서, 탭 줄
 * 하나 맞추려다 본문이 위아래로 튄다. 좌표를 직접 재어 이 상자만 굴린다.
 */
export function reveal(list, smooth) {
    if (!list) {
        return;
    }

    const tab = list.querySelector('.jsini-tab--active');

    if (!tab) {
        return;
    }

    const box = list.getBoundingClientRect();
    const mine = tab.getBoundingClientRect();

    // 끝에 딱 붙이지 않는다. 한 뼘 띄워야 「옆에 더 있다」가 보인다.
    const pad = 12;

    let delta = 0;

    if (mine.left < box.left + pad) {
        delta = mine.left - box.left - pad;
    } else if (mine.right > box.right - pad) {
        delta = mine.right - box.right + pad;
    }

    if (delta === 0) {
        return;
    }

    list.scrollBy({ left: delta, behavior: smooth ? 'smooth' : 'auto' });
}
