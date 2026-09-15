/*
    카카오(다음) 우편번호 서비스를 띄우고 고른 주소를 돌려준다.

    ────────────────────────────────────────────────────────────
    [스크립트를 <head> 에 적지 않는다]

    주소를 찾는 화면은 회사 관리 하나다. `<head>` 에 적으면 **모든 화면**이
    바깥 CDN 으로 한 번씩 나가고, 그 CDN 이 느린 날에는 주소와 아무 상관없는
    화면까지 늦게 뜬다. 여기서 **단추를 누른 그 순간** 받는다.

    받은 뒤에는 `window.daum` 이 남으므로 두 번째부터는 그냥 열린다.

    ────────────────────────────────────────────────────────────
    [새 창(`open`)이 아니라 덮개(`embed`)로 띄운다]

    `Postcode.open()` 은 `window.open` 을 부른다 — 팝업 차단기가 막으면
    **아무 일도 안 일어난다.** 사용자에게는 단추가 고장 난 것으로 보이고,
    막혔다는 것을 우리가 알 방법도 없다.

    덮개는 그냥 이 문서 안의 `<div>` 라 막힐 것이 없다.

    ────────────────────────────────────────────────────────────
    [회사 편집 창 **위에** 떠야 한다]

    주소 칸은 DevExpress 팝업 편집 폼 안에 있다. 그 팝업이 z-index 1000 대를
    쓰므로 덮개가 그보다 낮으면 **열리긴 하는데 편집 창 뒤에 가려** 보이지
    않는다. 치수와 z-index 는 admin.css 의 `.ad-addr` 가 갖고 있다.

    ────────────────────────────────────────────────────────────
    [결과를 약속(Promise)으로 돌려준다]

    `DotNetObjectReference` 로 되부르지 않는다. 그쪽은 화면이 먼저 사라졌을 때
    (편집 창을 닫았다거나) 죽은 참조로 되부르게 되고, 그 예외가 회로를 끊는다.
    Blazor 는 JS 가 돌려준 약속을 기다려 주므로, **부른 쪽이 살아 있을 때만**
    값이 도착한다.

    고르지 않고 닫으면 `null` 이다.
*/

const SCRIPT_URL = 'https://t1.daumcdn.net/mapjsapi/bundle/postcode/prod/postcode.v2.js';

/** 받는 중인 스크립트. 단추를 연달아 누를 때 두 번 받지 않게 들고 있는다. */
let loading = null;

function loadScript() {
    if (window.daum && window.daum.Postcode) {
        return Promise.resolve();
    }

    if (loading) {
        return loading;
    }

    loading = new Promise((resolve, reject) => {
        const tag = document.createElement('script');
        tag.src = SCRIPT_URL;
        tag.async = true;
        tag.onload = () => resolve();
        tag.onerror = () => {
            // **실패를 기억하지 않는다.** 그대로 두면 잠깐 끊겼던 망이
            // 돌아와도 이 화면이 살아 있는 동안에는 영영 안 열린다.
            loading = null;
            reject(new Error('주소 검색을 불러오지 못했습니다.'));
        };
        document.head.appendChild(tag);
    });

    return loading;
}

/**
 * 도로명 주소 뒤에 붙는 참고 항목. `(역삼동, 대우빌딩)` 꼴이다.
 *
 * 지번을 고른 경우에는 붙이지 않는다 — 지번 주소에는 이미 동 이름이 들어 있어
 * `역삼동 123-45 (역삼동)` 이 된다.
 */
function reference(data) {
    if (data.userSelectedType !== 'R') {
        return '';
    }

    const parts = [];

    // 법정동·법정리. 끝 글자로 가리는 것은 다음이 안내하는 방식 그대로다 —
    // 그것이 아닌 값(`산`, 번지 조각)이 섞여 들어온다.
    if (data.bname && /[동|로|가]$/g.test(data.bname)) {
        parts.push(data.bname);
    }

    // 건물 이름은 **공동주택일 때만** 쓸모가 있다. 그 밖에는 상호가 실려 와
    // 세든 회사의 주소에 남의 간판이 붙는다.
    if (data.buildingName && data.apartment === 'Y') {
        parts.push(data.buildingName);
    }

    return parts.length === 0 ? '' : ` (${parts.join(', ')})`;
}

/**
 * 주소 찾기 덮개를 띄운다.
 *
 * @returns {Promise<{zipCode: string, address: string}|null>}
 *          고른 주소. 고르지 않고 닫았으면 null.
 */
export async function pick() {
    await loadScript();

    return new Promise((resolve) => {
        const back = document.createElement('div');
        back.className = 'ad-addr';

        const box = document.createElement('div');
        box.className = 'ad-addr__box';

        const head = document.createElement('div');
        head.className = 'ad-addr__head';
        head.textContent = '주소 찾기';

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'ad-addr__close';
        close.setAttribute('aria-label', '닫기');
        close.textContent = '×';

        const frame = document.createElement('div');
        frame.className = 'ad-addr__frame';

        head.appendChild(close);
        box.appendChild(head);
        box.appendChild(frame);
        back.appendChild(box);
        document.body.appendChild(back);

        // **한 번만 끝낸다.** 아래 길이 셋(고름·닫개·Esc)인데 다음 위젯은
        // 고르고 나서 `onclose` 도 부른다 — 막지 않으면 고른 값을 돌려준
        // 직후 `null` 이 한 번 더 간다.
        let done = false;

        function finish(result) {
            if (done) return;
            done = true;

            document.removeEventListener('keydown', onKey, true);
            back.remove();
            resolve(result);
        }

        function onKey(e) {
            if (e.key === 'Escape') {
                // 편집 창까지 함께 닫히지 않게 여기서 멈춘다.
                e.preventDefault();
                e.stopPropagation();
                finish(null);
            }
        }

        // 캡처 단계로 듣는다. DevExpress 팝업이 Esc 를 먼저 받아 **뒤에 있는
        // 편집 창을 닫아 버리기** 때문이다.
        document.addEventListener('keydown', onKey, true);

        close.addEventListener('click', () => finish(null));

        // 바깥을 눌러도 닫는다. 덮개 자신을 누른 것만 센다 — 안쪽에서
        // 올라온 클릭(주소 목록 등)까지 받으면 고르는 중에 닫힌다.
        back.addEventListener('click', (e) => {
            if (e.target === back) {
                finish(null);
            }
        });

        new window.daum.Postcode({
            oncomplete: (data) => {
                const base = data.userSelectedType === 'R'
                    ? data.roadAddress
                    : data.jibunAddress;

                finish({
                    zipCode: data.zonecode ?? '',
                    address: (base ?? '') + reference(data),
                });
            },

            // 사용자가 위젯 안의 뒤로/닫기로 빠져나온 경우. 우리가 이미
            // 끝냈으면 `finish` 가 무시한다.
            onclose: () => finish(null),

            // 덮개 높이를 우리가 정하므로 위젯도 그 높이를 다 쓰게 한다.
            width: '100%',
            height: '100%',
        }).embed(frame, { autoClose: false });
    });
}
