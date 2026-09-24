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
    async function locate() {
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
                },
            );
        });
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

    window.jsiniGeo = { locate };
})();
