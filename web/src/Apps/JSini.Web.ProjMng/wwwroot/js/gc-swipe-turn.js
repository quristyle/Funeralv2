/**
 * 프로젝트 일정표를 휴대폰에서 좌우로 쓸어 넘길 때, 판이 쓴 쪽으로 밀려 나가고 반대쪽에서 들어온다.
 * 자료만 바뀌면 넘어간 것이 눈에 안 띈다. 서버는 「나가라」와 「들어와라」만 부르고 움직임은 브라우저가 그린다.
 * 클래스가 아니라 Web Animations 로 건다 — Blazor 가 class 를 갈아 치우면 붙여 둔 클래스가 지워진다.
 */

const running = new WeakMap();

/** 들어오라는 말이 끝내 안 오면(회로가 끊김) 판이 흐린 채로 굳지 않게 되돌린다. */
const OUT_GUARD_MS = 4000;

const still = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

function play(el, frames, options) {
  running.get(el)?.cancel();

  const a = el.animate(frames, options);
  running.set(el, a);
  a.finished.then(() => { if (running.get(el) === a && options.fill !== 'forwards') running.delete(el); }, () => {});

  return a;
}

/** 다음(+1)이면 왼쪽으로, 이전(-1)이면 오른쪽으로 밀려 나간다 — 손가락이 간 쪽이다. */
export function turnOut(el, dir) {
  if (!el?.animate) return;

  const dx = still() ? 0 : -dir * 32;
  const a = play(el, [
    { transform: 'translateX(0)', opacity: 1 },
    { transform: `translateX(${dx}px)`, opacity: 0.2 },
  ], { duration: 130, easing: 'ease-in', fill: 'forwards' });

  setTimeout(() => { if (running.get(el) === a) { a.cancel(); running.delete(el); } }, OUT_GUARD_MS);
}

/** 새 날·새 달이 반대쪽에서 들어온다. 나가던 움직임은 여기서 걷힌다. */
export function turnIn(el, dir) {
  if (!el?.animate) return;

  const dx = still() ? 0 : dir * 56;
  play(el, [
    { transform: `translateX(${dx}px)`, opacity: 0.2 },
    { transform: 'translateX(0)', opacity: 1 },
  ], { duration: 240, easing: 'cubic-bezier(.2, .8, .2, 1)' });
}
