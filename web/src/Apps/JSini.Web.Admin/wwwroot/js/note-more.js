const attachments = new WeakMap();

export function attachMoreObserver(sentinel, dotnet) {
  if (!sentinel || attachments.has(sentinel)) return;

  let intersecting = false;
  let scrollingDown = false;
  let inFlight = false;
  let exhausted = false;
  const positions = new WeakMap();

  const scrollTop = (target) => target === document
    || target === document.documentElement
    || target === document.body
    ? document.scrollingElement?.scrollTop ?? window.scrollY
    : target.scrollTop;

  const requestMore = () => {
    if (!intersecting || !scrollingDown || inFlight || exhausted) return;

    inFlight = true;
    dotnet.invokeMethodAsync("ShowMoreFromScrollAsync")
      .then((hasMore) => {
        exhausted = !hasMore;
      })
      .catch((error) => {
        console.error("알림 목록을 더 펼치지 못했습니다.", error);
      })
      .finally(() => {
        inFlight = false;
      });
  };

  const onScroll = (event) => {
    const target = event.target;
    if (target !== document && !target.contains?.(sentinel)) return;

    const top = scrollTop(target);
    const previous = positions.get(target);
    positions.set(target, top);

    if (previous === undefined) return;
    if (top === previous) return;

    scrollingDown = top > previous;
    if (!scrollingDown) exhausted = false;
    requestMore();
  };

  const onWheel = (event) => {
    if (event.deltaY > 0) {
      scrollingDown = true;
      requestMore();
    } else if (event.deltaY < 0) {
      scrollingDown = false;
      exhausted = false;
    }
  };

  let touchStartY = 0;
  const onTouchStart = (event) => {
    if (event.touches.length === 1) {
      touchStartY = event.touches[0].clientY;
    }
  };

  const onTouchMove = (event) => {
    if (event.touches.length === 1) {
      const currentY = event.touches[0].clientY;
      if (touchStartY - currentY > 5) {
        scrollingDown = true;
        requestMore();
      } else if (currentY - touchStartY > 5) {
        scrollingDown = false;
        exhausted = false;
      }
    }
  };

  const observer = new IntersectionObserver((entries) => {
    intersecting = entries[entries.length - 1].isIntersecting;
    if (!intersecting) exhausted = false;
    requestMore();
  }, {
    rootMargin: `0px 0px ${Math.min(480, Math.max(240, Math.round(window.innerHeight * 0.6)))}px 0px`
  });

  for (let parent = sentinel.parentElement; parent; parent = parent.parentElement) {
    positions.set(parent, scrollTop(parent));
  }
  positions.set(document, scrollTop(document));
  document.addEventListener("scroll", onScroll, { capture: true, passive: true });
  document.addEventListener("wheel", onWheel, { passive: true });
  document.addEventListener("touchstart", onTouchStart, { passive: true });
  document.addEventListener("touchmove", onTouchMove, { passive: true });
  observer.observe(sentinel);
  attachments.set(sentinel, { observer, onScroll, onWheel, onTouchStart, onTouchMove });
}

export function detachMoreObserver(sentinel) {
  const attachment = attachments.get(sentinel);
  if (!attachment) return;

  attachment.observer.disconnect();
  document.removeEventListener("scroll", attachment.onScroll, true);
  document.removeEventListener("wheel", attachment.onWheel);
  document.removeEventListener("touchstart", attachment.onTouchStart);
  document.removeEventListener("touchmove", attachment.onTouchMove);
  attachments.delete(sentinel);
}
