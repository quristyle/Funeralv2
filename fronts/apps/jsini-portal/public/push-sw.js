/**
 * 웹푸시 처리 — 워크박스가 만든 서비스워커(sw.js)가 importScripts 로 실어 온다
 * (vite.config.mts 의 pwaOptions.workbox.importScripts).
 *
 * 페이로드의 키 이름(title · body · url · icon · tag)은 NotificationServer 의
 * PushSender.BuildPayload 와 맞춘 것이다 — **바꾸면 알림이 빈 채로 뜬다.**
 */

self.addEventListener('push', (event) => {
  // 페이로드가 JSON 이 아니거나 비어 있어도 알림 자체는 띄운다 —
  // 조용히 버리면 푸시 권한이 있는데 아무 일도 없는 것처럼 보인다.
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    data = { body: event.data ? event.data.text() : '' };
  }

  const title = data.title || 'JSini 포털';
  const options = {
    body: data.body || '',
    icon: data.icon || '/pwa-icon-192.png',
    badge: '/pwa-icon-192.png',
    tag: data.tag || undefined,
    data: { url: data.url || '/' },
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const url = event.notification.data?.url || '/';

  // 이미 열린 창이 있으면 그 창을 앞으로 가져와 이동하고, 없으면 새로 연다.
  event.waitUntil(
    self.clients
      .matchAll({ type: 'window', includeUncontrolled: true })
      .then((windows) => {
        for (const win of windows) {
          if ('focus' in win) {
            win.focus();
            if ('navigate' in win && url !== '/') {
              win.navigate(url).catch(() => {});
            }
            return;
          }
        }
        return self.clients.openWindow(url);
      }),
  );
});
