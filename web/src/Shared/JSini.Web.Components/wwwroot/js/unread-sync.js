/**
 * 상단 띠의 **안 읽은 수**를 바깥 사정과 맞춘다.
 *
 * [무엇이 어긋나 있었나]
 *
 * 종에 붙는 숫자는 **회로가 붙는 순간과 화면을 옮길 때만** 다시 셌다.
 * 그래서 한 사람이 장비를 둘 쓰면 이런 일이 생긴다 —
 *
 *   · 휴대폰에서 알림을 다 읽는다 → 책상 화면의 종은 여전히 3건이다.
 *   · 그 책상 화면에서 종을 눌러 목록을 본다 → 목록은 비어 있는데
 *     **숫자는 3 그대로**다(목록과 숫자가 서로 다른 길로 오므로).
 *   · 새 알림이 와서 휴대폰이 울려도, 열어 둔 책상 화면의 숫자는 안 는다.
 *
 * 화면을 옮기기 전에는 영영 안 맞으므로, 사람에게는 **숫자가 고장 난 것**으로
 * 보인다. 실제로 틀린 것은 자료가 아니라 「언제 다시 세나」뿐이다.
 *
 * [여기서 듣는 것]
 *
 *   visibilitychange · focus  탭·창으로 돌아왔다. **다른 장비에서 읽고 온
 *                             사람이 반드시 지나는 문**이라 이것 하나가
 *                             가장 크게 듣는다.
 *   online                    끊겼다 붙었다. 끊긴 동안의 변화를 받는다.
 *   서비스워커의 알림           이 기기에 푸시가 **막 도착했다**(push-sw.js 가
 *                             `postMessage` 로 알린다). 알림이 울리는 그 순간
 *                             열려 있는 화면의 숫자도 함께 오른다.
 *
 * 나머지 한 자리(주기적으로 다시 세기)는 C# 쪽 시계가 맡는다 — 여기서 하면
 * 회로가 끊긴 뒤에도 브라우저가 계속 두드린다.
 *
 * [왜 한 번에 몰아서 부르나]
 *
 * 창으로 돌아오면 `visibilitychange` 와 `focus` 가 **거의 동시에** 온다.
 * 그대로 흘리면 게이트웨이를 두 번 탄다 — 짧게 모아 한 번만 보낸다.
 */

/** 몰아 보내는 시간(ms). 사람이 못 느끼면서 겹침은 다 걷어내는 길이다. */
const COALESCE_MS = 250;

/**
 * 지금 듣고 있는 것. **한 페이지에 종은 하나**라 하나만 들고 있으면 된다.
 *
 * 모듈은 브라우저가 한 번만 싣고 계속 돌려쓰므로(import 는 캐시된다) 회로가
 * 끊겼다 다시 붙으면 새 손잡이로 다시 걸러 온다 — 그때 **옛것을 반드시
 * 떼어야 한다.** 안 떼면 죽은 회로로 계속 부르고, 브라우저 콘솔에 오류만
 * 쌓인다.
 */
let current = null;

export function attachUnreadSync(dotnet) {
  detachUnreadSync();

  let timer = 0;

  /** 모아서 한 번 부른다. 회로가 이미 끊겼으면 조용히 넘어간다. */
  const ping = () => {
    if (timer) return;
    timer = setTimeout(() => {
      timer = 0;
      // 눈에 안 보이는 탭은 세지 않는다 — 돌아올 때 어차피 한 번 센다.
      if (document.visibilityState === 'hidden') return;
      dotnet.invokeMethodAsync('SyncUnreadAsync').catch(() => { });
    }, COALESCE_MS);
  };

  /** 보이는지를 C# 에 알린다. 안 보이는 동안은 그쪽 시계도 쉰다. */
  const onVisibility = () => {
    const visible = document.visibilityState === 'visible';
    dotnet.invokeMethodAsync('SetVisibleAsync', visible).catch(() => { });
    if (visible) ping();
  };

  /** 서비스워커가 「푸시가 왔다」고 알려 온 것만 받는다. */
  const onMessage = (e) => {
    if (e.data && e.data.type === 'jsini-unread') ping();
  };

  document.addEventListener('visibilitychange', onVisibility);
  window.addEventListener('focus', ping);
  window.addEventListener('online', ping);

  const sw = navigator.serviceWorker;
  if (sw) sw.addEventListener('message', onMessage);

  current = {
    detach() {
      if (timer) clearTimeout(timer);
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('focus', ping);
      window.removeEventListener('online', ping);
      if (sw) sw.removeEventListener('message', onMessage);
    },
  };
}

export function detachUnreadSync() {
  if (!current) return;

  const previous = current;
  current = null;
  previous.detach();
}
