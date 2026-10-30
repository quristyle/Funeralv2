/**
 * 카카오톡 안에서 연 링크를 기기의 기본 브라우저로 넘긴다.
 *
 * 카카오톡 인앱 브라우저에서는 PWA 설치 · 웹푸시 · 패스키가 안 되고 로그인 쿠키도
 * 따로 놀아서, 링크를 누른 사람이 포털을 반쯤 깨진 채로 쓰게 된다.
 * `kakaotalk://web/openExternal` 은 카카오톡이 아이폰·안드로이드 모두에서 받는
 * 스킴이라 기본 브라우저(사파리 · 크롬 등)로 같은 주소를 연다.
 *
 * 탭마다 한 번만 넘긴다. 넘기기가 실패하거나 사람이 카카오톡으로 돌아와
 * 그대로 쓰기로 했을 때, 로그인 뒤 새 문서가 뜰 때마다 또 튕겨 내지 않으려는 것이다.
 */
(function () {
    var ua = navigator.userAgent || '';
    if (!/KAKAOTALK/i.test(ua)) return;

    var KEY = 'jsini-kakao-external';
    try {
        if (sessionStorage.getItem(KEY)) return;
        sessionStorage.setItem(KEY, '1');
    } catch (e) { /* 저장소가 막혀 있어도 한 번은 넘긴다 */ }

    location.href = 'kakaotalk://web/openExternal?url=' + encodeURIComponent(location.href);
})();
