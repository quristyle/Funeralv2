const attachments = new WeakMap();

const findScrollContainer = (element) => {
  let parent = element?.parentElement;
  while (parent && parent !== document.body && parent !== document.documentElement) {
    const style = window.getComputedStyle(parent);
    if (/(auto|scroll)/.test(style.overflowY)) {
      return parent;
    }
    parent = parent.parentElement;
  }
  return null;
};

const scrollTop = (target) => (!target || target === document || target === document.documentElement || target === document.body)
  ? (document.scrollingElement?.scrollTop ?? window.scrollY ?? 0)
  : target.scrollTop;

export function attachMoreObserver(sentinel, dotnet) {
  if (!sentinel || attachments.has(sentinel)) return;

  const container = findScrollContainer(sentinel);
  const panel = sentinel.closest(".pm-ask");

  let intersecting = false;
  let scrollingDown = false;
  let inFlight = false;
  let exhausted = false;
  const positions = new WeakMap();

  const isNearBottom = () => {
    if (!sentinel.isConnected) return false;

    if (container) {
      const remaining = container.scrollHeight - (container.scrollTop + container.clientHeight);
      if (remaining <= 240) return true;
    } else {
      const scrollEl = document.scrollingElement || document.documentElement;
      const remaining = scrollEl.scrollHeight - (scrollEl.scrollTop + window.innerHeight);
      if (remaining <= 240) return true;
    }

    const sRect = sentinel.getBoundingClientRect();
    if (sRect.width === 0 && sRect.height === 0) return false;
    const boundaryBottom = container ? container.getBoundingClientRect().bottom : window.innerHeight;
    return sRect.top <= boundaryBottom + 240;
  };

  const requestMore = () => {
    const atEnd = intersecting || isNearBottom();
    if (!atEnd || !scrollingDown || inFlight || exhausted) return;

    inFlight = true;
    dotnet.invokeMethodAsync("ShowMoreFromScrollAsync")
      .then((hasMore) => {
        exhausted = !hasMore;
      })
      .catch((error) => {
        console.error("최근 지시 목록을 더 펼치지 못했습니다.", error);
      })
      .finally(() => {
        inFlight = false;
      });
  };

  const onScroll = (event) => {
    const target = event.target;
    if (target !== document && target !== container && !target.contains?.(sentinel)) return;

    const top = scrollTop(target);
    const previous = positions.get(target);
    positions.set(target, top);

    if (previous === undefined) return;
    if (top === previous) return;

    scrollingDown = top > previous;
    if (!scrollingDown) exhausted = false;
    requestMore();
  };

  const isTargetInside = (target) => {
    if (!target) return false;
    if (container && container.contains(target)) return true;
    if (panel && panel.contains(target)) return true;
    if (!container && !panel) return true;
    return false;
  };

  const onWheel = (event) => {
    if (!isTargetInside(event.target)) return;

    if (event.deltaY > 0) {
      scrollingDown = true;
      requestMore();
    } else if (event.deltaY < 0) {
      scrollingDown = false;
      exhausted = false;
    }
  };

  const observer = new IntersectionObserver((entries) => {
    intersecting = entries[entries.length - 1].isIntersecting;
    if (!intersecting) exhausted = false;
    requestMore();
  }, {
    root: container,
    rootMargin: `0px 0px ${Math.min(480, Math.max(240, Math.round(window.innerHeight * 0.6)))}px 0px`
  });

  for (let parent = sentinel.parentElement; parent; parent = parent.parentElement) {
    positions.set(parent, scrollTop(parent));
  }
  positions.set(document, scrollTop(document));
  if (container) {
    positions.set(container, scrollTop(container));
  }

  document.addEventListener("scroll", onScroll, { capture: true, passive: true });
  document.addEventListener("wheel", onWheel, { passive: true });
  observer.observe(sentinel);

  attachments.set(sentinel, {
    observer,
    onScroll,
    onWheel,
    reset: () => {
      exhausted = false;
      inFlight = false;
      if (container) {
        container.scrollTop = 0;
      }
    }
  });
}

export function resetMoreObserver(sentinel) {
  const attachment = attachments.get(sentinel);
  if (!attachment) return;
  attachment.reset();
}

export function detachMoreObserver(sentinel) {
  const attachment = attachments.get(sentinel);
  if (!attachment) return;

  attachment.observer.disconnect();
  document.removeEventListener("scroll", attachment.onScroll, true);
  document.removeEventListener("wheel", attachment.onWheel);
  attachments.delete(sentinel);
}
