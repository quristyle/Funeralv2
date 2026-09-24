/**
 * 위치 배관 — 브라우저에게 「지금 어디인가」를 묻는다.
 *
 * ── 왜 pwa.js 와 나눠 두나 ─────────────────────────────────────
 *
 * 저쪽은 **자기 자신을 즉시 실행한다**(서비스워커를 등록한다). 이쪽은
 * 사람이 단추를 누를 때만 도는 함수 하나뿐이라, 실려 있는 것만으로는
 * 아무 일도 하지 않는다. 섞어 두면 「등록 배관」을 읽으러 온 사람이
 * 위치 코드까지 지나가게 된다.
 *
 * ── 웹은 뒤에서 위치를 못 읽는다 ───────────────────────────────
 *
 * **이 한 가지가 「내 위치 날씨」의 구조를 전부 정한다.** 서비스워커는
 * geolocation 을 쓸 수 없고, 탭이 닫힌 뒤에 위치를 묻는 길도 없다.
 * 그래서 화면이 열려 있을 때 한 번 받아 **서버에 저장해 두고**, 알림은
 * 서버가 그 저장된 좌표로 만들어 보낸다. 이사나 출장으로 자리가 바뀌면
 * 사람이 다시 눌러 줘야 한다 — 설정 화면이 마지막으로 잡은 날짜를
 * 보여 주는 것이 그 때문이다.
 *
 * ── 그래도 다시 잡는 일은 저절로 된다 ─────────────────────────
 *
 * **화면이 열려 있는 동안에는** 다시 잴 수 있다. 권한을 이미 허용해 둔
 * 브라우저는 `getCurrentPosition` 을 물음창 없이 그대로 돌려주므로,
 * 포털을 열어 두기만 하면 사람이 단추를 누르지 않아도 좌표가 따라온다
 * (`quiet`). 그 판단을 브라우저에게 물어 두는 것이 `permission` 이다 —
 * 상태를 모르는 채로 재면 **아무도 부르지 않은 물음창**이 튀어나온다.
 *
 * 되는 곳과 안 되는 곳이 갈린다. Permissions API 가 없는 브라우저
 * (옛 iOS 사파리)에서는 `state` 가 `unknown` 이고, 그때는 조용히 재지
 * 않는다 — 물음창이 뜰지 어떨지 알 수 없어서다. 그 브라우저의 사람은
 * 설정 화면의 단추로 다시 잡는다.
 *
 * ── 실패를 삼키지 않는다 ───────────────────────────────────────
 *
 * 사람이 단추를 눌러 부른 것이므로 까닭을 담아 돌려준다. 「위치를 받지
 * 못했습니다」만 띄우면, 권한을 거절한 것인지 기기가 못 잡은 것인지
 * HTTPS 가 아니라 막힌 것인지 알 길이 없다.
 */
(function () {
    'use strict';

    /**
     * 지금 위치. `{ ok, latitude, longitude, accuracy, error }` 를 돌려준다.
     *
     * **보안 컨텍스트가 아니면 브라우저가 아예 거절한다** — 개발 장비의
     * http://localhost 는 보안 컨텍스트로 쳐 주지만, 사설 IP(http://192.168.x.x)로
     * 열면 안 된다. 운영은 HTTPS 라 문제가 없고, 그 구분을 먼저 말해 준다.
     */
    async function locate(options) {
        if (!('geolocation' in navigator)) {
            return { ok: false, error: '이 브라우저는 위치 기능을 지원하지 않습니다.' };
        }

        if (!window.isSecureContext) {
            return { ok: false, error: '보안 연결(HTTPS)에서만 위치를 쓸 수 있습니다.' };
        }

        return new Promise((resolve) => {
            navigator.geolocation.getCurrentPosition(
                (position) => resolve({
                    ok: true,
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    accuracy: position.coords.accuracy,
                }),
                (error) => resolve({ ok: false, error: describe(error) }),
                {
                    // 동네 날씨에 미터 단위 정확도가 필요하지 않다. 기상청 격자가
                    // 5km 칸이라, 높은 정확도를 요구하면 휴대폰이 GPS 를 켜고
                    // 한참 걸리기만 한다.
                    enableHighAccuracy: false,
                    timeout: 10000,
                    // 10분 안에 잡아 둔 값이 있으면 그것을 쓴다.
                    maximumAge: 600000,
                    // 조용한 확인은 더 느슨하게 부른다(`quiet`).
                    ...(options || {}),
                },
            );
        });
    }

    /**
     * **물음창 없이** 지금 위치. 권한이 이미 허용된 경우에만 실제로 잰다.
     *
     * 사람이 시킨 일이 아니므로 <b>조건이 안 맞으면 조용히 물러난다</b> —
     * `{ ok: false, skipped: true }` 다. 실패(`skipped: false`)와 가르는
     * 이유는 부르는 쪽이 말을 할지 말지를 그것으로 정하기 때문이다.
     */
    async function quiet() {
        const state = await permission();

        if (state.state !== 'granted') {
            return { ok: false, skipped: true, error: state.state };
        }

        const got = await locate({
            // 이미 있는 값이면 그대로 쓴다. 조용한 확인 하나 때문에 휴대폰이
            // GPS 를 켜면 배터리가 눈에 띄게 준다 — 동네 날씨에 30분 전 좌표는
            // 충분하다(기상청 격자가 5km 칸이다).
            maximumAge: 1800000,
            timeout: 8000,
        });

        return got.ok ? got : { ...got, skipped: false };
    }

    /**
     * 브라우저가 위치를 내줄 사정인가. `{ supported, secure, state }` 이고
     * `state` 는 `granted` · `denied` · `prompt` · `unknown` 중 하나다.
     *
     * **`unknown` 은 「모른다」이지 「안 된다」가 아니다.** Permissions API 가
     * 없는 브라우저라, 물어보면 될 수도 있다 — 권유 창은 띄우되 조용한
     * 재측정만 건너뛴다.
     */
    async function permission() {
        if (!('geolocation' in navigator)) {
            return { supported: false, secure: false, state: 'unsupported' };
        }

        if (!window.isSecureContext) {
            return { supported: true, secure: false, state: 'insecure' };
        }

        if (!navigator.permissions || !navigator.permissions.query) {
            return { supported: true, secure: true, state: 'unknown' };
        }

        try {
            const status = await navigator.permissions.query({ name: 'geolocation' });
            return { supported: true, secure: true, state: status.state || 'unknown' };
        } catch {
            // 이름을 모르는 브라우저는 던진다. 모르는 것으로 본다.
            return { supported: true, secure: true, state: 'unknown' };
        }
    }

    /**
     * 브라우저가 준 오류를 사람 말로. **코드로 가른다** — `message` 는
     * 브라우저마다 다르고 영어인 경우가 많다.
     */
    function describe(error) {
        if (!error) return '위치를 받지 못했습니다.';

        switch (error.code) {
            case 1: // PERMISSION_DENIED
                return '위치 권한이 허용되지 않았습니다. 브라우저 주소창의 자물쇠에서 위치를 허용하십시오.';
            case 2: // POSITION_UNAVAILABLE
                return '기기가 위치를 잡지 못했습니다. 잠시 뒤 다시 시도하십시오.';
            case 3: // TIMEOUT
                return '위치를 잡는 데 너무 오래 걸립니다. 실외에서 다시 시도하십시오.';
            default:
                return error.message || '위치를 받지 못했습니다.';
        }
    }

    // **`locate` 는 인자 없이 부른다.** C# 쪽은 `jsiniGeo.locate` 를 그대로
    // 부르고(인자를 안 넘긴다), 옵션은 이 파일 안의 `quiet` 만 쓴다.
    window.jsiniGeo = { locate, quiet, permission };
})();
