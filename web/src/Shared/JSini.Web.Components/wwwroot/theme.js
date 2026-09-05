/**
 * 테마 · 화면 상태를 다루는 작은 스크립트.
 *
 * [왜 Blazor 가 아니라 순수 JS 인가]
 *
 * 테마는 **첫 그림이 그려지기 전에** 정해져야 한다. Blazor Server 회로는 첫
 * HTML 이 나간 뒤에 붙으므로, 회로에서 테마를 정하면 사용자가 기본 테마로
 * 한 번 그려진 화면을 보고 나서 바뀌는 것을 본다(FOUC). 그래서 이 파일은
 * <head> 바로 다음에서 동기로 돈다.
 *
 * 저장은 localStorage 다. 서버에 저장하는 방법도 있지만(계정 환경설정 API 가
 * 있다) 그러면 로그인 전 화면(로그인·오류)이 테마를 알 수 없다.
 * 서버 저장은 **덮어쓰기**로 나중에 붙인다 — 기기별 값이 먼저고 계정 값이 그 위다.
 */
(function () {
  'use strict';

  var STORAGE_KEY = 'jsini.theme';
  var DEFAULT_THEME = 'office-white';

  /**
   * 고를 수 있는 테마. **순서가 곧 전환 화면의 순서다.**
   *
   * `dark` 는 어두운 테마인지 — 우리 스타일(app.css)이 색을 맞추는 데 쓴다.
   * DevExpress 테마마다 밝기가 다른데 우리 사이드바·헤더는 우리 CSS 라서,
   * 그 둘이 어긋나면 "사이드바만 하얗다" 가 된다.
   */
  var THEMES = [
    { id: 'office-white', name: 'Office White', dark: false },
    { id: 'blazing-berry', name: 'Blazing Berry', dark: false },
    { id: 'purple', name: 'Purple', dark: false },
    { id: 'blazing-dark', name: 'Blazing Dark', dark: true },
  ];

  function find(id) {
    for (var i = 0; i < THEMES.length; i++) {
      if (THEMES[i].id === id) return THEMES[i];
    }
    return null;
  }

  function stored() {
    try {
      return window.localStorage.getItem(STORAGE_KEY);
    } catch (e) {
      // 사생활 보호 창에서는 localStorage 접근 자체가 던진다.
      // 그때는 기본 테마로 돌면 된다 — 화면이 안 뜨는 것보다 낫다.
      return null;
    }
  }

  function save(id) {
    try {
      window.localStorage.setItem(STORAGE_KEY, id);
    } catch (e) {
      /* 저장 못 해도 이번 화면은 정상 동작한다. */
    }
  }

  /**
   * 테마 스타일시트를 껐다 켠다.
   *
   * **새로 내려받지 않는다.** 모두 <link> 로 미리 실어 두고 `disabled` 만
   * 바꾼다. 새로 받으면 전환할 때 화면이 한 번 하얗게 번쩍인다.
   */
  function apply(id) {
    var theme = find(id) || find(DEFAULT_THEME);
    var links = document.querySelectorAll('link[data-jsini-theme]');

    for (var i = 0; i < links.length; i++) {
      links[i].disabled = links[i].getAttribute('data-jsini-theme') !== theme.id;
    }

    // 우리 CSS 가 보는 표시. 사이드바·헤더 색이 DevExpress 테마와 함께 움직인다.
    document.documentElement.setAttribute('data-theme', theme.dark ? 'dark' : 'light');
    document.documentElement.setAttribute('data-dx-theme', theme.id);

    return theme;
  }

  var current = apply(stored() || DEFAULT_THEME);

  window.jsiniTheme = {
    /** 고를 수 있는 테마들. 전환 화면이 이 목록을 그린다. */
    list: function () {
      return THEMES;
    },

    /** 지금 테마 식별자. */
    current: function () {
      return current.id;
    },

    /** 지금 테마가 어두운가. */
    isDark: function () {
      return current.dark;
    },

    /** 테마를 고른다. 모르는 값이면 기본 테마로 떨어진다. */
    set: function (id) {
      current = apply(id);
      save(current.id);
      return current.id;
    },
  };

  /**
   * 전체화면 토글.
   *
   * vben 헤더에 있던 기능이다. 상황판에 띄워 두는 화면(빈소현황·SM 모니터링)에서
   * 실제로 쓰인다 — 브라우저 주소줄까지 지워야 글자가 커진다.
   */
  window.jsiniScreen = {
    isFull: function () {
      return document.fullscreenElement !== null;
    },

    toggle: function () {
      if (document.fullscreenElement) {
        document.exitFullscreen();
        return false;
      }

      // 실패해도 화면은 그대로다. 권한이 없거나 iframe 안이면 브라우저가 거절한다.
      var request = document.documentElement.requestFullscreen();
      if (request && request.catch) request.catch(function () {});
      return true;
    },
  };

  /**
   * 워터마크. 화면 위에 로그인 아이디를 옅게 반복해 깐다.
   *
   * [왜 있나 — 화면 촬영을 막으려는 것이 아니다]
   *
   * 막을 수는 없다. 목적은 **찍힌 사진에서 누구 화면인지 드러나게** 하는 것이다.
   * 고인·상주 정보가 나오는 화면이 있어 옛 포털도 같은 이유로 켜 두었다.
   *
   * `pointer-events: none` 이라 클릭을 가로채지 않는다. 지우려면 개발자 도구를
   * 열면 되지만, 그렇게까지 한 사람은 이미 고의라 다른 문제다.
   */
  window.jsiniWatermark = {
    show: function (text) {
      var id = 'jsini-watermark';
      var existing = document.getElementById(id);
      if (existing) existing.remove();

      if (!text) return;

      var svg =
        '<svg xmlns="http://www.w3.org/2000/svg" width="240" height="140">' +
        '<text x="0" y="80" transform="rotate(-24 0 80)" ' +
        'fill="rgba(128,128,128,0.14)" font-size="15" font-family="sans-serif">' +
        text.replace(/[<>&"]/g, '') +
        '</text></svg>';

      var layer = document.createElement('div');
      layer.id = id;
      layer.style.cssText =
        'position:fixed;inset:0;z-index:9999;pointer-events:none;' +
        'background-repeat:repeat;background-image:url("data:image/svg+xml;base64,' +
        window.btoa(unescape(encodeURIComponent(svg))) +
        '")';

      document.body.appendChild(layer);
    },

    hide: function () {
      var el = document.getElementById('jsini-watermark');
      if (el) el.remove();
    },
  };
})();
