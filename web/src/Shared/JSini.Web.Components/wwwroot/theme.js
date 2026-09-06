/**
 * 테마 · 화면 상태를 다루는 작은 스크립트.
 *
 * [왜 Blazor 가 아니라 순수 JS 인가]
 *
 * 테마는 **첫 그림이 그려지기 전에** 정해져야 한다. Blazor Server 회로는 첫
 * HTML 이 나간 뒤에 붙으므로, 회로에서 테마를 정하면 사용자가 기본 테마로
 * 한 번 그려진 화면을 보고 나서 바뀌는 것을 본다(FOUC). 그래서 이 파일은
 * <head> 안에서 동기로 돈다.
 *
 * [스타일시트를 미리 싣지 않는다]
 *
 * DevExpress Classic 테마가 **하나에 2.8MB** 다. 넷을 미리 실으면 11MB 를
 * 내려받는다. 그래서 필요한 것만 그때그때 <link> 로 붙이고, 한 번 붙인 것은
 * 지우지 않는다(`disabled` 로만 끈다) — 되돌아갈 때 다시 받지 않게.
 *
 * [바꿀 때 번쩍이지 않게 하는 법]
 *
 * 새 스타일시트가 **다 실린 뒤에** 옛 것을 끈다. 순서를 바꾸면 그 사이에
 * 스타일이 하나도 없는 순간이 생겨 화면이 하얗게 번쩍인다.
 *
 * 저장은 localStorage 다. 계정에 저장하는 방법도 있지만(환경설정 API 가 있다)
 * 그러면 로그인 전 화면이 테마를 알 수 없다.
 */
(function () {
  'use strict';

  var STORAGE_KEY = 'jsini.theme';
  var FLUENT = '_content/DevExpress.Blazor.Themes.Fluent/';
  var CLASSIC = '_content/DevExpress.Blazor.Themes/';
  var BOOTSTRAP = '_content/JSini.Web.Components/bootstrap/';

  /**
   * 크기 모드 쿠키.
   *
   * [왜 테마와 달리 쿠키까지 굽나]
   *
   * 테마는 스타일시트라 브라우저에서 갈아 끼우면 끝이다. 크기는 다르다 —
   * DevExpress 는 부품 뿌리에 `dxbl-sm`/`dxbl-lg` 클래스를 **서버가 그릴 때**
   * 붙인다(SizeMode). 그래서 서버가 첫 그림을 그리는 순간 이미 알고 있어야
   * 하고, 그때 읽을 수 있는 것은 요청에 실려 온 쿠키뿐이다.
   *
   * 없으면 프리렌더는 Medium 으로 그려졌다가 회로가 붙으면서 고른 크기로
   * 다시 그려진다 — 화면이 한 번 출렁인다.
   *
   * HttpOnly 가 아니다. 여기서 써야 하기 때문이고, 담기는 값은 'small' 셋 중
   * 하나뿐이라 새어도 잃을 것이 없다.
   */
  var SIZE_COOKIE = 'jsini.size';

  /**
   * 고를 수 있는 크기. **DevExpress 가 주는 것이 이 셋뿐이다**
   * (`DevExpress.Blazor.SizeMode` — Small · Medium · Large).
   *
   * 우리가 네 번째를 만들지 않는다. 만들면 그 크기에서만 그리드·달력·팝업이
   * 따라오지 않아 한 화면에 두 크기가 된다.
   */
  var SIZES = [
    { id: 'small', name: 'Small' },
    { id: 'medium', name: 'Medium' },
    { id: 'large', name: 'Large' },
  ];

  /**
   * Bootstrap 을 고르면 스타일시트가 **두 장**이다.
   *
   *   1) Bootstrap(또는 Bootswatch) 본체 — 색과 변수를 정한다
   *   2) DevExpress 의 bootstrap-external.bs5.min.css — 그 변수를 읽어
   *      그리드 · 달력 · 팝업에 옮긴다
   *
   * **순서가 뒤집히면 안 된다.** 2) 가 1) 의 --bs-* 를 읽는 쪽이라,
   * 1) 이 뒤에 오면 DevExpress 부품만 옛 색으로 남는다.
   * 그 순서는 priorityOf 가 지킨다.
   */
  var BOOTSTRAP_THEMES = [
    { id: 'default', name: 'Default', file: 'bootstrap.min.css', dark: false, swatch: '#027bff' },
    { id: 'default-dark', name: 'Default Dark', file: 'bootstrap.min.css', dark: true, swatch: '#212529' },
    { id: 'cerulean', name: 'Cerulean', file: 'cerulean.min.css', dark: false, swatch: '#2ea4e7' },
    { id: 'flatly', name: 'Flatly', file: 'flatly.min.css', dark: false, swatch: '#dbe4ec' },
    { id: 'journal', name: 'Journal', file: 'journal.min.css', dark: false, swatch: '#eb6864' },
    { id: 'lumen', name: 'Lumen', file: 'lumen.min.css', dark: false, swatch: '#158cba' },
  ];

  function bootstrapTheme(id) {
    for (var i = 0; i < BOOTSTRAP_THEMES.length; i++) {
      if (BOOTSTRAP_THEMES[i].id === id) return BOOTSTRAP_THEMES[i];
    }
    return null;
  }

  /**
   * 고를 수 있는 것들. DevExpress 데모의 테마 창과 같은 구성이다.
   *
   * Fluent 은 **조각을 합쳐** 만든다 — 공통(core+global) + 밝기(mode) + 강조색(accent).
   * Classic 은 한 장짜리다.
   */
  var FLUENT_MODES = [
    { id: 'light', name: 'Light', dark: false },
    { id: 'dark', name: 'Dark', dark: true },
  ];

  /**
   * 강조색 프리셋. 파일 이름이 곧 식별자다.
   *
   * **`swatch` 는 눈대중이 아니다.** 데모 테마 창 캡처(docs/테마캡쳐.png)에서
   * 색 네모의 픽셀을 그대로 읽었다. 한동안 비슷해 보이는 색을 손으로 적어
   * 두었는데, 고르기 전과 고른 뒤의 화면 색이 서로 달라 **어느 것을 골랐는지
   * 네모만 보고는 알 수 없었다.**
   */
  var FLUENT_ACCENTS = [
    { id: 'blue', name: 'Blue', swatch: '#0f6cbd' },
    { id: 'cool-blue', name: 'Cool Blue', swatch: '#2d7d9a' },
    { id: 'desert', name: 'Desert', swatch: '#847545' },
    { id: 'mint', name: 'Mint', swatch: '#018574' },
    { id: 'moss', name: 'Moss', swatch: '#486860' },
    { id: 'orchid', name: 'Orchid', swatch: '#c239b3' },
    { id: 'purple', name: 'Purple', swatch: '#5b5fc7' },
    { id: 'rose', name: 'Rose', swatch: '#ea005e' },
    { id: 'rust', name: 'Rust', swatch: '#da3b01' },
    { id: 'steel', name: 'Steel', swatch: '#68768a' },
    { id: 'storm', name: 'Storm', swatch: '#6d6a68' },
  ];

  var CLASSIC_THEMES = [
    { id: 'blazing-berry', name: 'Blazing Berry', dark: false, swatch: '#5c2d91' },
    { id: 'blazing-dark', name: 'Blazing Dark', dark: true, swatch: '#46444a' },
    { id: 'purple', name: 'Purple', dark: false, swatch: '#7989ff' },
    { id: 'office-white', name: 'Office White', dark: false, swatch: '#fe7109' },
  ];

  /**
   * 기본값.
   *
   * DevExpress 데모의 기본과 같은 자리 — Fluent Light + Blue.
   * Classic 을 기본으로 두지 않는 이유는 파일이 2.8MB 라 첫 방문이 느려서다
   * (Fluent 은 core 1.6MB 에 밝기·강조색이 100KB 남짓이다).
   */
  var DEFAULT = { family: 'fluent', mode: 'light', accent: 'blue', custom: null, size: 'medium' };

  // ── 스타일시트 관리 ───────────────────────────────────────

  /** 이미 붙인 <link> 들. 키는 주소. */
  var links = {};

  /**
   * 스타일시트를 꽂을 자리.
   *
   * [왜 순서를 우리가 정해야 하나]
   *
   * 붙이는 차례대로 <head> 뒤에 쌓으면 **고르는 차례에 따라 순서가 달라진다.**
   * Cerulean 을 먼저 고르면 [cerulean][dx-external] 이 되지만, 그 뒤에 Flatly 로
   * 바꾸면 dx-external 은 이미 있으므로 flatly 만 뒤에 붙어
   * [cerulean][dx-external][flatly] 가 된다 — DevExpress 부품이 flatly 색을 못
   * 읽는다. 같은 화면인데 **어떤 차례로 눌렀느냐에 따라** 달라지는 것이라
   * 재현하기도 나쁘다.
   *
   * 그래서 주소마다 자리 번호를 주고 그 자리에 꽂는다. 다 실린 뒤에 옮기는
   * 방법도 있지만, <link> 를 옮기면 잠깐 스타일이 빠져 번쩍인다.
   */
  function priorityOf(href) {
    if (href.indexOf(FLUENT) === 0) {
      if (href.indexOf('core') >= 0) return 10;
      if (href.indexOf('global') >= 0) return 11;
      if (href.indexOf('modes/') >= 0) return 12;
      return 13;                                   // accents
    }
    if (href.indexOf(BOOTSTRAP) === 0) return 30;            // Bootstrap 본체가 먼저
    if (href.indexOf('bootstrap-external') >= 0) return 31;  // DevExpress 다리
    return 20;                                               // Classic 한 장짜리
  }

  /**
   * 테마 <link> 는 여기까지다. 이 자리표 뒤는 우리 CSS(app.css · 모듈)다.
   *
   * 이 스크립트는 <head> 안에서 동기로 돌므로, 지금 이 자리가 곧
   * "DevExpress 테마 다음, 우리 CSS 앞" 이다.
   */
  var boundary = document.createElement('meta');
  boundary.setAttribute('name', 'jsini-theme-boundary');

  (function () {
    var script = document.currentScript;

    if (script && script.parentNode) {
      script.parentNode.insertBefore(boundary, script);
    } else {
      document.head.appendChild(boundary);
    }
  })();

  /** 자리 번호에 맞는 곳에 꽂는다. 뒤에 올 것이 없으면 자리표 바로 앞. */
  function insertOrdered(link, href) {
    var priority = priorityOf(href);
    var existing = document.head.querySelectorAll('link[data-jsini-theme]');

    for (var i = 0; i < existing.length; i++) {
      if (priorityOf(existing[i].getAttribute('href')) > priority) {
        document.head.insertBefore(link, existing[i]);
        return;
      }
    }

    document.head.insertBefore(link, boundary);
  }

  /**
   * 한 번 실린 스타일시트를 켜고 끈다.
   *
   * 이미 받아 둔 것이라 `disabled` 로 즉시 바뀐다. `media` 를 함께 되돌리는
   * 것은 처음에 `not all` 로 만들어졌을 수 있기 때문이다 — 그대로 두면
   * `disabled = false` 로 켜도 적용되지 않는다.
   */
  function setActive(link, on) {
    link.media = 'all';
    link.disabled = !on;
  }

  /** 다 실렸을 때 부를 것을 걸어 둔다. 이미 실렸으면 바로 부른다. */
  function whenReady(link, callback) {
    if (link.__loaded) {
      callback();
      return;
    }

    link.__waiters = link.__waiters || [];
    link.__waiters.push(callback);
  }

  /** 다 실렸다고 알린다. 두 번 불러도 한 번만 통한다. */
  function settle(link) {
    if (link.__loaded) return;

    link.__loaded = true;

    var waiters = link.__waiters || [];
    link.__waiters = [];

    for (var i = 0; i < waiters.length; i++) {
      waiters[i]();
    }
  }

  /**
   * 스타일시트를 붙인다(이미 있으면 그대로 쓴다).
   * 다 실렸을 때 콜백을 부른다 — 실패해도 부른다(그 테마만 안 예뻐질 뿐이다).
   */
  function ensure(href, onReady, startEnabled) {
    var link = links[href];

    if (!link) {
      link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = href;
      link.setAttribute('data-jsini-theme', '');

      // **첫 적용만 켠 채로 만든다.**
      //
      // 처음에는 갈아 끼울 옛 테마가 없으니 켠 채로 만들어도 번쩍일 일이 없고,
      // 그러면 브라우저가 이 파일을 기다렸다가 그린다(맨몸 화면이 안 스친다).
      //
      // 두 번째부터는 꺼서 만든다 — 다 실린 뒤에 옛 것과 한꺼번에 바꿔야
      // 그 사이에 두 테마가 겹쳐 보이지 않는다.
      //
      // [끄는 방법이 `disabled` 면 안 된다 — 실제로 밟았다]
      //
      // `link.disabled = true` 로 만든 <link> 는 브라우저가 **아예 내려받지
      // 않는다.** 요청도, `load` 도, `sheet` 도 없다. 그래서 여기서 기다리는
      // 콜백이 영영 안 불리고 테마가 바뀌지 않았다. 새로고침하면 그때는
      // 첫 적용이라 켠 채로 만들어져 적용된 것처럼 보였다 —
      // **"고르면 아무 일도 없는데 F5 하면 바뀐다"** 가 그 증상이다.
      //
      // `media = 'not all'` 은 다르다. 정상으로 받아 오고 `load` 도 오는데
      // 적용만 안 된다. 다 받은 뒤 `media` 를 되돌리면서 켠다(setActive).
      link.media = startEnabled ? 'all' : 'not all';

      link.addEventListener('load', function () { settle(link); }, { once: true });
      link.addEventListener('error', function () { settle(link); }, { once: true });

      // 못 받아도 넘어간다. 안 그러면 한 장이 막혔을 때 그 테마에서
      // 영영 못 벗어난다 — 안 예쁜 것보다 안 바뀌는 것이 나쁘다.
      window.setTimeout(function () { settle(link); }, 4000);

      insertOrdered(link, href);
      links[href] = link;
    }

    whenReady(link, onReady);
    return link;
  }

  /** 지금 고른 것에 필요한 스타일시트 주소들. */
  function sheetsFor(spec) {
    if (spec.family === 'classic') {
      return [CLASSIC + spec.classic + '.bs5.min.css'];
    }

    if (spec.family === 'bootstrap') {
      var theme = bootstrapTheme(spec.bootstrap) || BOOTSTRAP_THEMES[0];

      // Default 와 Default Dark 는 **같은 파일**이다. 5.3 부터 어두운 쪽이
      // 별도 파일이 아니라 data-bs-theme="dark" 로 켜지기 때문이다.
      return [BOOTSTRAP + theme.file, CLASSIC + 'bootstrap-external.bs5.min.css'];
    }

    return [
      FLUENT + 'core.min.css',
      FLUENT + 'global.min.css',
      FLUENT + 'modes/' + spec.mode + '.min.css',
      FLUENT + 'accents/' + spec.accent + '.min.css',
    ];
  }

  /**
   * 사용자가 고른 색을 강조색으로 쓴다.
   *
   * DevExpress 의 강조색 파일은 `var(--dxbl-accent-color-90, #0f6cbd)` 처럼
   * **덮어쓸 수 있게** 되어 있다. 그래서 파일을 새로 만들 필요 없이 그 변수만
   * 채워 주면 된다 — 데모의 Custom Color 도 같은 방식이다.
   *
   * 16단계를 고른 색 하나에서 만든다. 90 을 기준으로 잡고 위로는 흰색,
   * 아래로는 검정 쪽으로 섞는다. DevExpress 가 손으로 고른 값만큼 곱지는
   * 않지만, 한 색에서 만드는 이상 이보다 나은 방법이 없다.
   */
  function applyCustomAccent(hex) {
    var id = 'jsini-accent';
    var style = document.getElementById(id);

    if (!hex) {
      if (style) style.remove();
      return;
    }

    var rgb = parseHex(hex);
    if (!rgb) return;

    var steps = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160];
    var rules = [];

    for (var i = 0; i < steps.length; i++) {
      var step = steps[i];
      var mixed;

      if (step < 90) {
        // 90 에서 10 으로 갈수록 흰색에 가깝게. 0.92 까지 섞는다.
        mixed = mix(rgb, [255, 255, 255], ((90 - step) / 80) * 0.92);
      } else if (step > 90) {
        // 90 에서 160 으로 갈수록 검정에 가깝게.
        mixed = mix(rgb, [0, 0, 0], ((step - 90) / 70) * 0.88);
      } else {
        mixed = rgb;
      }

      rules.push('--dxbl-accent-color-' + step + ':' + toHex(mixed));
    }

    if (!style) {
      style = document.createElement('style');
      style.id = id;
      document.head.appendChild(style);
    }

    style.textContent = ':root{' + rules.join(';') + '}';
  }

  function parseHex(hex) {
    var m = /^#?([0-9a-f]{6})$/i.exec(String(hex).trim());
    if (!m) return null;

    var n = parseInt(m[1], 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }

  function mix(a, b, ratio) {
    return [
      Math.round(a[0] + (b[0] - a[0]) * ratio),
      Math.round(a[1] + (b[1] - a[1]) * ratio),
      Math.round(a[2] + (b[2] - a[2]) * ratio),
    ];
  }

  function toHex(rgb) {
    return '#' + rgb.map(function (v) {
      return ('0' + Math.max(0, Math.min(255, v)).toString(16)).slice(-2);
    }).join('');
  }

  // ── 적용 ──────────────────────────────────────────────────

  var current = null;

  /**
   * 방금 **고른** 것. `current` 와 다르다.
   *
   * `current` 는 스타일시트가 다 실린 뒤에야 채워진다(그 전에는 아직 옛 테마가
   * 화면에 있으니 그게 맞다). 그런데 크기는 스타일시트를 기다리지 않으므로,
   * 그 사이에 크기를 물으면 `current` 는 아직 null 이거나 옛 값이다.
   * 실제로 첫 로드 직후 쿠키를 구울 때 여기에 걸렸다.
   */
  var chosen = null;

  function isDark(spec) {
    if (spec.family === 'bootstrap') {
      var bs = bootstrapTheme(spec.bootstrap);
      return bs ? bs.dark : false;
    }

    if (spec.family === 'classic') {
      for (var i = 0; i < CLASSIC_THEMES.length; i++) {
        if (CLASSIC_THEMES[i].id === spec.classic) return CLASSIC_THEMES[i].dark;
      }
      return false;
    }
    return spec.mode === 'dark';
  }

  function apply(spec, done) {
    var wanted = sheetsFor(spec);
    var pending = wanted.length;
    var initial = current === null;

    chosen = spec;

    // **크기는 스타일시트를 기다리지 않는다.**
    //
    // 아래 ready() 안에 두면 테마 CSS 를 다 받은 뒤에야 글자 크기가 잡혀
    // 본문이 한 번 출렁인다. 크기는 우리 CSS 변수(app.css 의 --jsini-fs-*)
    // 하나로 끝나고 어느 테마를 골랐는지와 무관하므로 지금 바로 세운다.
    document.documentElement.setAttribute('data-dx-size', spec.size);

    function ready() {
      if (--pending > 0) return;

      // **다 실린 뒤에** 갈아 끼운다. 순서를 바꾸면 화면이 번쩍인다.
      for (var href in links) {
        setActive(links[href], wanted.indexOf(href) >= 0);
      }

      applyCustomAccent(spec.family === 'fluent' ? spec.custom : null);

      // 우리 CSS 가 보는 표시. 사이드바·헤더 색이 DevExpress 테마와 함께 움직인다.
      var root = document.documentElement;
      root.setAttribute('data-theme', isDark(spec) ? 'dark' : 'light');
      root.setAttribute('data-dx-family', spec.family);

      // Bootstrap 5.3 의 어두운 쪽 스위치. 다른 테마에서는 붙어 있으면 안 된다 —
      // Bootstrap 이 안 실린 채로 이 표시만 남으면 아무 일도 안 하지만,
      // Bootstrap 으로 돌아왔을 때 Default 인데 어둡게 나온다.
      if (spec.family === 'bootstrap' && isDark(spec)) {
        root.setAttribute('data-bs-theme', 'dark');
      } else {
        root.removeAttribute('data-bs-theme');
      }

      // DevExpress Fluent 이 밝기별 클래스를 본다.
      root.classList.toggle('dxbl-theme-fluent-mode-light',
        spec.family === 'fluent' && spec.mode === 'light');
      root.classList.toggle('dxbl-theme-fluent-mode-dark',
        spec.family === 'fluent' && spec.mode === 'dark');

      current = spec;
      if (done) done();
    }

    for (var i = 0; i < wanted.length; i++) {
      ensure(wanted[i], ready, initial);
    }
  }

  function stored() {
    try {
      var raw = window.localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (e) {
      // 사생활 보호 창에서는 접근 자체가 던진다. 기본값으로 돌면 된다.
      return null;
    }
  }

  function save(spec) {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(spec));
    } catch (e) {
      /* 저장 못 해도 이번 화면은 정상 동작한다. */
    }

    saveSizeCookie(spec.size);
  }

  /**
   * 크기만 쿠키로도 남긴다 — 서버가 첫 그림을 그릴 때 읽는다(SIZE_COOKIE 주석).
   *
   * `SameSite=Lax` 는 인증 쿠키와 같은 값이다. 여기에 담기는 것은 화면 취향뿐이라
   * 더 조일 이유도 없고, 느슨하게 할 이유는 더 없다.
   */
  function saveSizeCookie(size) {
    try {
      document.cookie =
        SIZE_COOKIE + '=' + size + ';path=/;max-age=31536000;samesite=lax';
    } catch (e) {
      /* 쿠키를 막아 둔 브라우저. 회로가 붙은 뒤에 맞춰진다 — 첫 그림만 기본 크기다. */
    }
  }

  /** 아는 크기면 그대로, 모르면 기본값. */
  function normalizeSize(value) {
    for (var i = 0; i < SIZES.length; i++) {
      if (SIZES[i].id === value) return value;
    }
    return DEFAULT.size;
  }

  /**
   * 저장된 값을 지금 아는 모양으로 좁힌다. 모르는 값이면 기본값으로.
   *
   * **크기는 테마 묶음과 따로 논다.** 테마를 Classic 으로 바꿔도 고른 크기는
   * 그대로여야 하므로, 어느 갈래로 빠지든 크기는 따로 실어 준다.
   */
  function normalize(spec) {
    if (!spec || typeof spec !== 'object') return DEFAULT;

    var size = normalizeSize(spec.size);

    if (spec.family === 'classic') {
      for (var i = 0; i < CLASSIC_THEMES.length; i++) {
        if (CLASSIC_THEMES[i].id === spec.classic) {
          return { family: 'classic', classic: spec.classic, size: size };
        }
      }
      return fallback(size);
    }

    if (spec.family === 'bootstrap') {
      return bootstrapTheme(spec.bootstrap)
        ? { family: 'bootstrap', bootstrap: spec.bootstrap, size: size }
        : fallback(size);
    }

    var mode = spec.mode === 'dark' ? 'dark' : 'light';
    var accent = DEFAULT.accent;

    for (var j = 0; j < FLUENT_ACCENTS.length; j++) {
      if (FLUENT_ACCENTS[j].id === spec.accent) accent = spec.accent;
    }

    return {
      family: 'fluent',
      mode: mode,
      accent: accent,
      custom: parseHex(spec.custom) ? spec.custom : null,
      size: size,
    };
  }

  /** 테마는 기본값으로 돌리되 크기는 살린다. */
  function fallback(size) {
    return {
      family: DEFAULT.family,
      mode: DEFAULT.mode,
      accent: DEFAULT.accent,
      custom: DEFAULT.custom,
      size: size,
    };
  }

  apply(normalize(stored()));

  // 저장은 했는데 쿠키가 없는 사람이 있다 — 크기를 넣기 전부터 쓰던 사람이다.
  // 서랍을 열어 보지 않아도 다음 새로고침부터는 서버가 알도록 지금 구워 둔다.
  saveSizeCookie(chosen.size);

  window.jsiniTheme = {
    /** 고를 수 있는 것들. 테마 창이 이 목록을 그린다. */
    catalog: function () {
      return {
        modes: FLUENT_MODES,
        accents: FLUENT_ACCENTS,
        classic: CLASSIC_THEMES,
        bootstrap: BOOTSTRAP_THEMES,
        sizes: SIZES,
      };
    },

    /** 지금 고른 것. 스타일시트가 아직 오는 중이면 방금 고른 쪽을 준다. */
    current: function () {
      return current || chosen;
    },

    /** 지금 테마가 어두운가. */
    isDark: function () {
      return isDark(current);
    },

    /** Fluent 을 고른다. 밝기와 강조색을 함께 넘긴다. */
    setFluent: function (mode, accent, custom) {
      return commit({
        family: 'fluent', mode: mode, accent: accent, custom: custom, size: chosen.size,
      });
    },

    /** Classic 한 장짜리 테마를 고른다. */
    setClassic: function (id) {
      return commit({ family: 'classic', classic: id, size: chosen.size });
    },

    /** Bootstrap(또는 Bootswatch) 테마를 고른다. */
    setBootstrap: function (id) {
      return commit({ family: 'bootstrap', bootstrap: id, size: chosen.size });
    },

    /**
     * 크기를 고른다 (small · medium · large).
     *
     * 테마는 건드리지 않는다 — 지금 것을 그대로 두고 크기만 갈아 끼운다.
     * 스타일시트가 바뀌지 않으므로 다시 받는 것도 없다.
     *
     * 화면(ThemeToggle)이 이 뒤에 DevExpress 쪽 SizeMode 도 함께 바꾼다.
     * 여기서 하는 일은 우리 CSS 변수와 저장뿐이다.
     */
    setSize: function (id) {
      var spec = {};

      for (var key in chosen) {
        if (Object.prototype.hasOwnProperty.call(chosen, key)) spec[key] = chosen[key];
      }

      spec.size = id;
      return commit(spec);
    },
  };

  /** 좁히고 · 적용하고 · 저장한다. 네 setter 가 똑같이 하던 일이다. */
  function commit(raw) {
    var spec = normalize(raw);
    apply(spec);
    save(spec);
    return spec;
  }

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
   * `pointer-events: none` 이라 클릭을 가로채지 않는다.
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
        String(text).replace(/[<>&"]/g, '') +
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

  /**
   * AI 대화창의 스크롤 (D11).
   *
   * 답이 한 글자씩 붙는 동안 늘 맨 아래가 보여야 한다. Blazor Server 는
   * 브라우저의 스크롤 위치를 모르므로 이 한 줄만 JS 로 한다.
   *
   * **사람이 위로 올려 지난 대화를 읽고 있으면 끌어내리지 않는다.**
   * 끌어내리면 읽던 자리를 계속 빼앗긴다. 바닥 근처(48px 안)일 때만 따라간다.
   */
  window.jsiniChat = {
    toBottom: function (el) {
      if (!el) return;
      var gap = el.scrollHeight - el.scrollTop - el.clientHeight;
      if (gap > 48) return;
      el.scrollTop = el.scrollHeight;
    },
  };
})();
