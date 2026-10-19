/**
 * 빠른 지시 최근 리스트의 쓸어내려 새로고침. 끄는 동안은 브라우저가 CSS 변수로 그리고,
 * 문턱을 넘어 손을 뗐을 때만 서버가 한 번 듣는다(pointermove 를 서버로 보내면 버벅인다).
 * 스크롤이 맨 위일 때만 시작하고, 가로 이동이나 위로 미는 손짓은 ask-swipe.js 나 브라우저에 넘긴다.
 */

const attached = new WeakSet();

/** 방향을 가리기 전에 지켜보는 거리. 탭과 끌기를 여기서 가른다. */
const SLOP = 6;

/** 새로고침을 일으키는 문턱 거리(px). */
const THRESHOLD = 48;

/** 최대 당김 거리(px). */
const MAX_PULL = 80;

/** 당김 저항 계수 (손가락 이동량 대비 인디케이터 당김 비율). */
const DAMPING = 0.42;

/** 복귀 애니메이션 시간(ms). */
const SETTLE_MS = 200;

export function attachPullRefresh(selector, dotnet) {
  const root = document.querySelector(selector);
  if (!root || attached.has(root)) return;

  attached.add(root);

  const scrollEl = root.querySelector('.pm-ask__scroll') || root;
  const pullEl = root.querySelector('.pm-ask__pull');
  const textEl = pullEl?.querySelector('.pm-ask__pull-text');

  if (!pullEl) return;

  let pointer = -1;
  let startX = 0;
  let startY = 0;
  let dy = 0;

  /** 방향을 가렸나 · 아래로 끄는 중인가. */
  let decided = false;
  let live = false;

  /** 지금 새로고침 호출 중인가. */
  let refreshing = false;

  const setStyle = (name, val) => pullEl.style.setProperty(name, val);

  const updateText = (ready) => {
    if (!textEl) return;
    textEl.textContent = ready ? '손을 놓아 새로고침' : '당겨서 새로고침';
  };

  const forget = () => {
    pointer = -1;
    decided = false;
    live = false;
    dy = 0;
  };

  const resetPull = () => {
    setStyle('--pm-ask-pull-ms', `${SETTLE_MS}ms`);
    setStyle('--pm-ask-pull-y', '0px');
    pullEl.classList.remove('pm-ask__pull--ready');

    window.setTimeout(() => {
      if (!refreshing) {
        pullEl.classList.remove('pm-ask__pull--active');
        pullEl.style.removeProperty('--pm-ask-pull-ms');
        pullEl.style.removeProperty('--pm-ask-pull-y');
        updateText(false);
      }
    }, SETTLE_MS);
  };

  scrollEl.addEventListener('pointerdown', (e) => {
    if (refreshing || live) return;
    if (e.pointerType === 'mouse' && e.button !== 0) return;

    // 스크롤이 맨 위에 있을 때만 쓸어내려 새로고침이 가능하다.
    if (scrollEl.scrollTop > 0) return;

    pointer = e.pointerId;
    startX = e.clientX;
    startY = e.clientY;
    decided = false;
    live = false;
    dy = 0;
  });

  scrollEl.addEventListener('pointermove', (e) => {
    if (e.pointerId !== pointer || refreshing) return;

    const mx = e.clientX - startX;
    const my = e.clientY - startY;

    if (!decided) {
      if (Math.abs(mx) < SLOP && Math.abs(my) < SLOP) return;

      decided = true;

      // 가로 이동이 더 크거나 위로 올리는 스크롤이면 취소하고 다른 동작에 넘긴다.
      if (Math.abs(mx) >= Math.abs(my) || my <= 0 || scrollEl.scrollTop > 0) {
        forget();
        return;
      }

      live = true;
      scrollEl.setPointerCapture?.(pointer);
      pullEl.classList.add('pm-ask__pull--active');
      setStyle('--pm-ask-pull-ms', '0ms');
    }

    if (!live) return;

    // 세로 아래로 끄는 것이 확정되면 브라우저 기본 스크롤/바운스를 막는다.
    e.preventDefault();

    dy = Math.max(0, Math.min(MAX_PULL, my * DAMPING));
    setStyle('--pm-ask-pull-y', `${dy}px`);

    const ready = dy >= THRESHOLD;
    pullEl.classList.toggle('pm-ask__pull--ready', ready);
    updateText(ready);
  }, { passive: false });

  const release = (e) => {
    if (e.pointerId !== pointer) return;

    const wasLive = live;
    const ready = dy >= THRESHOLD && e.type === 'pointerup';

    forget();

    if (!wasLive) return;

    if (!ready) {
      resetPull();
      return;
    }

    // 문턱을 넘겨 새로고침 트리거
    refreshing = true;
    pullEl.classList.remove('pm-ask__pull--ready');
    pullEl.classList.add('pm-ask__pull--refreshing');
    setStyle('--pm-ask-pull-ms', `${SETTLE_MS}ms`);
    setStyle('--pm-ask-pull-y', '48px');
    if (textEl) textEl.textContent = '새로고침 중...';

    dotnet.invokeMethodAsync('RefreshRecentFromSwipeDownAsync')
      .catch((err) => {
        console.error('최근 지시 목록 새로고침 실패:', err);
      })
      .finally(() => {
        refreshing = false;
        pullEl.classList.remove('pm-ask__pull--refreshing');
        resetPull();
      });
  };

  scrollEl.addEventListener('pointerup', release);
  scrollEl.addEventListener('pointercancel', release);
}
