/*
    아바타 카드(`UserCardHost`)를 누른 아바타 곁에 말풍선으로 붙인다.

    자리는 브라우저가 잰다 — 아바타가 격자 칸·카드 안에 있어 서버는 좌표를 모른다.
    카드는 `position: fixed` 라 굴리거나 창 크기를 바꾸면 다시 잰다. 카드 키가
    자료가 온 뒤에 바뀌므로(연락처 줄) 그것도 `ResizeObserver` 로 다시 잰다.

    휴대폰(`jsini-ucard--center`)은 CSS 가 가운데에 세우므로 여기서는 초점만 옮긴다.
*/

const GAP = 10;
const MARGIN = 8;

let current = null;

function position(card, anchorId) {
    if (card.classList.contains('jsini-ucard--center')) {
        card.dataset.placed = '1';
        return;
    }

    const anchor = document.getElementById(anchorId);
    if (!anchor) {
        // 목록이 다시 그려져 아바타가 사라졌다. 마지막 자리를 지킨다.
        card.dataset.placed = '1';
        return;
    }

    const r = anchor.getBoundingClientRect();
    const w = card.offsetWidth;
    const h = card.offsetHeight;
    const vw = document.documentElement.clientWidth;
    const vh = window.innerHeight;

    // 아래가 기본이고, 아래에 자리가 없고 위에 있으면 위로 올린다.
    const fitsBelow = r.bottom + GAP + h <= vh - MARGIN;
    const fitsAbove = r.top - GAP - h >= MARGIN;
    const below = fitsBelow || !fitsAbove;

    const top = below ? r.bottom + GAP : r.top - GAP - h;
    const center = r.left + r.width / 2;
    const left = Math.max(MARGIN, Math.min(center - w / 2, vw - w - MARGIN));
    const arrow = Math.max(16, Math.min(center - left, w - 16));

    card.style.top = `${Math.max(MARGIN, top)}px`;
    card.style.left = `${left}px`;
    card.style.setProperty('--jsini-ucard-arrow', `${arrow}px`);
    card.dataset.side = below ? 'bottom' : 'top';
    card.dataset.placed = '1';
}

export function place(card, anchorId) {
    if (!card) {
        return;
    }

    if (current?.card !== card || current.anchorId !== anchorId) {
        release();

        const handler = () => position(card, anchorId);
        const observer = new ResizeObserver(handler);
        observer.observe(card);
        window.addEventListener('resize', handler);
        window.addEventListener('scroll', handler, true);

        current = { card, anchorId, handler, observer };
        position(card, anchorId);

        // 자리를 잡은 뒤에 옮긴다 — 감춰 둔(visibility: hidden) 동안에는 초점이 안 간다.
        card.focus({ preventScroll: true });
        return;
    }

    position(card, anchorId);
}

export function release() {
    if (!current) {
        return;
    }

    window.removeEventListener('resize', current.handler);
    window.removeEventListener('scroll', current.handler, true);
    current.observer.disconnect();
    current = null;
}
