# 서비스워커로 `geolocation` 을 처리하는 길이 있나 — 없다

> **물음** — 「내 위치」 수집은 포털을 열어 둔 동안만 돈다
> (`LocationAskPopup` · `GeoLocator`). 서비스워커는 창이 닫힌 뒤에도 살아
> 있으니, 거기서 위치를 읽어 두면 되지 않나?
>
> **답** — **안 된다.** 서비스워커에는 `navigator.geolocation` 이 **아예
> 없다.** 끌 수 있는 깃발이나 채워 넣을 폴리필이 있는 것이 아니라, 그
> 전역에 그 물건 자체가 실리지 않는다.
>
> 그래도 **할 수 있는 일 하나**(열린 창에 물어보는 중계)와, **이 포털에서는
> 그것으로 벌 것이 없는 까닭**, 그리고 **대신 늘린 것**을 아래에 적는다.

## 1. 규격이 그렇게 생겼다

Geolocation API 는 `Navigator` 에 달린다. 그런데 워커의 전역에 실리는 것은
`Navigator` 가 아니라 **`WorkerNavigator`** 라는 딴 물건이고, 거기에는 위치가
섞여 들어가 있지 않다. `Geolocation` 생성자조차 없다.

### 실측 (크롬 153, 2026-10-09)

localhost 에 빈 페이지와 서비스워커 하나를 세워 **서비스워커 안에서** 재 본 것이다.

| 본 것 | 값 |
|---|---|
| `self.constructor.name` | `ServiceWorkerGlobalScope` |
| `navigator.constructor.name` | **`WorkerNavigator`** |
| `'geolocation' in navigator` | **`false`** |
| `typeof navigator.geolocation` | **`undefined`** |
| `typeof self.Geolocation` | `undefined` (생성자도 없다) |
| `typeof self.GeolocationPosition` | `undefined` |
| `navigator.permissions` | **있다** (`object`) |
| `navigator.permissions.query({name:'geolocation'})` | **`granted`** ← 함정 |

마지막 줄이 사람을 속이는 자리다. **권한은 물어볼 수 있다.** 사람이 창에서
허용해 둔 상태가 서비스워커에서도 `granted` 로 보이므로 「되는구나」 싶지만,
정작 **쓸 물건이 없다.** 권한 상태만 보고 짠 코드는 조용히 `undefined` 를
건드리다 끝난다.

워커의 `navigator` 에 실제로 있는 것은 이것뿐이다 —
`appCodeName · appName · appVersion · clearAppBadge · connection · deviceMemory ·
gpu · hardwareConcurrency · language · languages · locks · mediaCapabilities ·
onLine · permissions · platform · product · setAppBadge · storage ·
storageBuckets · userAgent · userAgentData`.

### 깨우는 계기가 바뀌어도 같다

`push` 로 깨워도, `periodicsync` 로 깨워도 **그 안에서 본 `geolocation` 은
여전히 `undefined`** 다. 깨어나는 길이 문제가 아니라 그 전역에 물건이 없는 것이다.

```
푸시로 깨운 서비스워커가 본 것
  { woke: 'push', geolocationType: 'undefined', windowClients: 0,
    openWindow: 'throw:InvalidAccessError: Not allowed to open a window.' }

periodicsync 로 깨운 서비스워커가 본 것
  { woke: 'periodicsync', tag: 'geo', geolocationType: 'undefined' }
```

## 2. 서비스워커가 할 수 있는 일 하나 — 열린 창에 물어보는 중계

서비스워커는 못 읽지만 **창은 읽을 수 있다.** 그래서 깨어난 서비스워커가
`clients.matchAll` 로 창을 찾아 「지금 재서 알려 달라」고 보내고, 창이 재서
돌려주는 길은 **실제로 된다.** 위 실측에서 확인했다 — 심어 둔 좌표
(37.5665, 126.978)가 서비스워커까지 돌아왔다.

```js
// 서비스워커
const wins = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
for (const w of wins) w.postMessage({ type: 'geo-please' });   // 창에 부탁한다

// 창
navigator.serviceWorker.addEventListener('message', async (e) => {
  if (e.data.type !== 'geo-please') return;
  navigator.geolocation.getCurrentPosition(p => { /* 여기서 비로소 읽힌다 */ });
});
```

이 짜임 자체는 이 저장소에 이미 있다 — 알림이 왔을 때 상단 띠의 숫자를
다시 세게 하는 길이 같은 모양이다(`push-sw.js` 의 `notifyClients` ↔
`unread-sync.js`).

### 그런데 이 포털에서는 그것으로 벌 것이 없다

벽이 셋이고, **하나만으로도 끝난다.**

| 벽 | 무슨 일이 되나 |
|---|---|
| **창이 없으면 창을 열 수도 없다** | `clients.openWindow` 는 사람의 몸짓이 이어진 이벤트(`notificationclick`)에서만 열린다. `push` 안에서 부르면 `InvalidAccessError: Not allowed to open a window.` 다 — 위 실측에 그대로 찍혀 있다 |
| **창이 있으면 이미 잰다** | 이 포털은 Blazor **Server** 다. 재는 시계는 브라우저가 아니라 **서버의 회로**에서 돌아서(`LocationAskPopup` 의 `RunClockAsync`) 탭이 뒤에 가 있어도 브라우저의 타이머 조이기와 무관하게 깬다. 중계로 더 벌 틈이 없다 |
| **좌표를 저장할 열쇠가 브라우저에 없다** | 게이트웨이를 부를 토큰은 **서버가 들고 있다**(BFF — `push-sw.js` 의 `withReadMark` 머리말과 같은 사정). 창이 좌표를 읽어 와도 회로가 끊겨 있으면 **보낼 곳이 없다** |

## 3. `periodicsync` 는 왜 답이 아닌가

이름만 보면 「닫힌 뒤에도 주기적으로」로 읽히지만, 네 가지가 걸린다.

1. **그 안에도 `geolocation` 이 없다** (위 실측). 결국 2번의 중계가 필요하고,
   중계는 창을 요구한다.
2. **크로미움 전용**이고 **설치된 PWA** 여야 한다. 사파리·파이어폭스에는 없다.
3. `minInterval` 은 **부탁이지 약속이 아니다.** 실제 간격은 브라우저가 사이트
   사용도(site engagement)로 정하고, 크롬은 **하루 한 번꼴**까지 벌린다.
   우리 간격은 **30분**이다(`GeoLocator.SyncInterval`) — 자리가 틀리다.
4. 푸시로 깨우는 길도 같은 벽에 더해 **알림을 띄워야 한다.** 깨어나서 알림을
   하나도 안 띄우면 크롬이 대신 「백그라운드에서 갱신되었습니다」를 띄우고,
   그것이 반복되면 **구독 자체를 끊는다**(`push-sw.js` 머리말). 위치를 모으려고
   30분마다 알림을 띄울 수는 없다.

## 4. 그래서 대신 늘린 것 — 끊긴 회로에서 시계를 버리지 않는다

서비스워커로 벌 수 없는 대신, **열려 있는데도 안 재던 자리**를 하나 메웠다.

시계는 서버의 회로에서 돈다. 그 시계가 깰 때 하는 일은 브라우저를 한 번
건드리는 것인데(`localStorage` 를 다시 읽어 옆 탭과 겹치지 않게 한다),
**회로가 끊긴 동안 그 호출은 `JSDisconnectedException` 으로 터진다.**
그런데 그 예외를 받는 자리가 `while` **밖**에 있어서, 한 번 터지면 시계가
그대로 멈췄다.

```
휴대폰을 덮는다 ─▶ 웹소켓이 끊긴다 ─▶ 시계가 깬다 ─▶ JS 호출이 터진다
                                                        └─▶ 시계가 죽는다
다시 연다 ─▶ 회로는 되살아난다(15분 안이면) ─▶ 그런데 시계는 이미 없다
                                                 └─▶ 영영 안 잰다
```

`JSDisconnectedException` 은 `JSException` 이 **아니다** — 둘 다
`System.Exception` 을 바로 물려받은 남남이라(실측으로 확인), 중간에 깔아 둔
`catch (JSException …)` 들에 걸리지 않고 끝까지 올라온다.

화면을 새로 열면(`firstRender`) 처음부터 다시 살피므로 **새 회로**에서는
문제가 없었다. 드러나지 않았던 것은 **되살아난 회로**다 — 재연결은
`firstRender` 를 다시 일으키지 않는다(`reconnect.js`: 끊긴 회로를 서버가
**15분** 붙들고, 되살아나면 화면이 끊기기 전 그대로다).

고친 뒤에는 끊김을 **기다리는 일**로 다룬다. 시계를 버리지 않고 30초 뒤에
다시 해 보므로, 휴대폰을 다시 열어 회로가 이어지면 **그 30초 안에** 잰다.

## 5. 정말로 「닫은 뒤에도」 모으려면

| 길 | 되나 |
|---|---|
| 서비스워커에서 직접 | **안 된다**. 그 전역에 `geolocation` 이 없다 |
| 서비스워커 → 열린 창 중계 | 창이 살아 있는 동안만. 그 동안은 회로의 시계가 이미 잰다 |
| `periodicsync` | 간격을 우리가 못 정한다(하루 한 번꼴). 그 안에도 위치가 없다 |
| 푸시로 깨우기 | 같은 벽 + 깰 때마다 알림을 띄워야 한다 |
| **네이티브 껍데기** (안드로이드 서비스 · Capacitor 플러그인 · Flutter) | **된다.** 웹에서 이것을 흉내 내는 길은 없다 |

웹으로 할 수 있는 것은 **열어 둔 동안 성실히 재는 것**까지다. 설정 화면이
「확인한 때」를 보여 주는 까닭이 그것이고, 지도의 점 사이가 벌어져 있으면
그 사이에 포털이 닫혀 있었다는 뜻이다(`/admin/location/my-track`).

## 다시 재 보려면

위 표를 다시 뽑는 길이다. 운영에 손대지 않고 **빈 페이지 하나**로 끝난다.

1. `/tmp` 에 `index.html` 과 `sw.js` 를 둔다. 서비스워커는 `install` 에서
   `skipWaiting`, `activate` 에서 `clients.claim`, 그리고 `message` ·
   `push` · `periodicsync` 에서 본 것을 **캐시에 적어 둔다**
   (전역 변수에 들면 깨어날 때마다 0으로 돌아간다).
2. `python3 -m http.server 58123 --bind 127.0.0.1` — **`localhost` 는 보안
   컨텍스트로 쳐 준다.** 사설 IP 로 열면 서비스워커가 등록되지 않는다.
3. 크롬을 `--remote-debugging-port` 로 띄우고 CDP 로 몬다
   (`Browser.grantPermissions` 로 위치·알림을 미리 허용하고,
   `Emulation.setGeolocationOverride` 로 좌표를 심는다).
4. **창이 없을 때**를 보려면 우리 출처의 탭을 모두 닫고,
   `about:blank` 탭의 세션에서 `ServiceWorker.deliverPushMessage` ·
   `ServiceWorker.dispatchPeriodicSyncEvent` 를 부른다. 두 명령은 **페이지
   세션에만** 있다 — 브라우저 세션에 보내면 `wasn't found` 가 돌아온다.
5. 적어 둔 것은 다시 탭을 열어 서비스워커에게 물어 읽는다.

## 같이 볼 것

- `web/src/Shell/JSini.Web.Shell/wwwroot/js/geo.js` — 창에서 위치를 묻는 자리
- `web/src/Shared/JSini.Web.Components/Settings/GeoLocator.cs` — 잡아서 저장하는 절차
- `web/src/Shared/JSini.Web.Components/Layout/LocationAskPopup.razor` — 권유 창과 시계
- `web/src/Shell/JSini.Web.Shell/wwwroot/push-sw.js` — 서비스워커 본체
- [docs/push-delivery.md](push-delivery.md) — 푸시가 깨우는 시점이 왜 우리 뜻대로가 아닌가
