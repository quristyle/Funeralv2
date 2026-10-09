const attachments = new WeakMap();

export function attachMoreObserver(sentinel, dotnet) {
  if (!sentinel || attachments.has(sentinel)) return;

  let intersecting = false;
  let scrollingDown = false;
  let triggered = false;
  const positions = new WeakMap();

  const scrollTop = (target) => target === document
    || target === document.documentElement
    || target === document.body
    ? document.scrollingElement?.scrollTop ?? window.scrollY
    : target.scrollTop;

  const requestMore = () => {
    if (!intersecting || !scrollingDown || triggered) return;

    triggered = true;
    dotnet.invokeMethodAsync("ShowMoreFromScrollAsync").catch((error) => {
      triggered = false;
      console.error("최근 지시 목록을 더 펼치지 못했습니다.", error);
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
    if (!scrollingDown) triggered = false;
    requestMore();
  };

  const observer = new IntersectionObserver((entries) => {
    intersecting = entries[entries.length - 1].isIntersecting;
    if (!intersecting) triggered = false;
    requestMore();
  }, { rootMargin: "0px 0px 96px 0px" });

  for (let parent = sentinel.parentElement; parent; parent = parent.parentElement) {
    positions.set(parent, scrollTop(parent));
  }
  positions.set(document, scrollTop(document));
  document.addEventListener("scroll", onScroll, { capture: true, passive: true });
  observer.observe(sentinel);
  attachments.set(sentinel, { observer, onScroll });
}

export function detachMoreObserver(sentinel) {
  const attachment = attachments.get(sentinel);
  if (!attachment) return;

  attachment.observer.disconnect();
  document.removeEventListener("scroll", attachment.onScroll, true);
  attachments.delete(sentinel);
}
