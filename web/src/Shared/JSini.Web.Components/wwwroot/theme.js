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
   * 고를 수 있는 크기 — 다섯 단계.
   *
   * [DevExpress 모드는 여전히 셋이다]
   *
   * `DevExpress.Blazor.SizeMode` 가 주는 것은 Small · Medium · Large 뿐이고
   * **우리가 네 번째를 만들지 않는다.** 없는 값을 흘리면 그 크기에서만
   * 그리드·달력·팝업이 따라오지 않아 한 화면에 두 크기가 된다.
   *
   * 그런데 사람이 고치고 싶어 하는 것은 대개 **글자 크기**이고, 그것은
   * 우리 사다리(`--jsini-fs-*`)가 따로 갖고 있다. 그래서 단계를 다섯으로
   * 늘리고 **부품 크기는 가까운 DevExpress 모드로 접는다.**
   *
   *   xxsmall  0.625rem   Small     ← 우리가 넣은 것
   *   xsmall   0.6875rem  Small     ← 우리가 넣은 것
   *   small    0.75rem    Small
   *   compact  0.8125rem  Small     ← 우리가 넣은 것
   *   medium   0.875rem   Medium
   *   large    1rem       Large
   *
   * 접는 표는 **서버 쪽(`ThemeSize.Steps`)에 있다.** 여기 두면 두 곳에
   * 같은 표가 생기고, 어긋나도 예외가 안 나서 「글자는 바뀌는데 부품이
   * 안 따라온다」로만 보인다. 이 파일이 아는 것은 아이디와 이름뿐이다.
   *
   * `--jsini-fs-base` 를 실제로 정하는 것은 app.css 의
   * `:root[data-dx-size='…']` 이고, 그 속성은 아래 apply() 가 세운다.
   *
   * 이름은 한국어다. 셋일 때는 DevExpress 이름(Small·Medium·Large)을 그대로
   * 썼지만, 다섯 중 둘은 DevExpress 에 없는 단계라 그 이름을 이어 쓸 수 없다.
   * 순서대로 읽히는 것이 중요해서
   * 「가장작게 < 아주작게 < 작게 < 조금작게 < 보통 < 크게」로 둔다.
   */
  var SIZES = [
    { id: 'xxsmall', name: '가장작게' },
    { id: 'xsmall', name: '아주작게' },
    { id: 'small', name: '작게' },
    { id: 'compact', name: '조금작게' },
    { id: 'medium', name: '보통' },
    { id: 'large', name: '크게' },
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

    // ── 우리가 더한 것 ─────────────────────────────────────
    //
    // DevExpress 가 주는 강조색 파일은 위 열한 개뿐이다. 그래서 이 둘은
    // **파일이 아니라 색**이다 — `base` 의 파일을 싣고 그 위에 `custom` 의
    // 색으로 `--dxbl-accent-color-*` 를 채운다. Custom Color 칸이 하는 일과
    // 같은 길이고, 다른 것은 색이 이름을 갖는다는 점뿐이다.
    //
    // `base` 를 **색이 가까운 것**으로 고른다. 16단계를 우리가 만들어 덮지만
    // 강조색 파일에는 그 변수를 쓰지 않는 자리가 남아 있어(그림자·테두리
    // 일부), 먼 색을 밑에 깔면 그 자리만 딴 색으로 뜬다.
    { id: 'new-berry', name: 'new berry', swatch: '#5c3d85', base: 'purple', custom: '#5c3d85' },

    // 우분투의 그 주황. 공식 브랜드 색이 #E95420 이다.
    { id: 'ubuntu', name: 'Ubuntu', swatch: '#e95420', base: 'rust', custom: '#e95420' },
  ];

  /** 프리셋 하나. 없는 이름이면 `null`. */
  function fluentAccent(id) {
    for (var i = 0; i < FLUENT_ACCENTS.length; i++) {
      if (FLUENT_ACCENTS[i].id === id) return FLUENT_ACCENTS[i];
    }
    return null;
  }

  /**
   * 그 프리셋이 실제로 실을 강조색 **파일** 이름.
   *
   * 우리가 더한 프리셋은 자기 파일이 없으므로 밑에 깔 것(`base`)을 준다.
   */
  function accentFile(id) {
    var accent = fluentAccent(id);
    return accent && accent.base ? accent.base : id;
  }

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
      FLUENT + 'accents/' + accentFile(spec.accent) + '.min.css',
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
  /** 우리가 더한 프리셋이 들고 있는 색. DevExpress 것에는 없어서 `null`. */
  function presetColor(id) {
    var accent = fluentAccent(id);
    return accent && accent.custom ? accent.custom : null;
  }

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

      // 사용자 지정 색이 있으면 그것, 없으면 프리셋이 들고 있는 색(우리가 더한
      // 둘). 둘 다 없으면 DevExpress 파일의 색이 그대로 쓰인다.
      applyCustomAccent(spec.family === 'fluent' ? (spec.custom || presetColor(spec.accent)) : null);

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
    var accent = fluentAccent(spec.accent) ? spec.accent : DEFAULT.accent;

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

  /**
   * 감춰 둔 폼을 제출한다.
   *
   * [이것이 없어서 로그아웃이 조용히 안 되고 있었다]
   *
   * 세 곳이 이렇게 부르고 있었다.
   *
   *     Js.InvokeVoidAsync("document.getElementById('jsini-logout-form').submit")
   *
   * **Blazor 는 그 글자를 `.` 으로 쪼개 이름을 하나씩 찾는다.** 그래서
   * `document` 에서 `getElementById('jsini-logout-form')` 라는 **이름의 속성**을
   * 찾고, 없으니 던진다 —
   * `Could not find '…' ('getElementById('…')' was undefined)`.
   *
   * 증상이 나쁜 이유는 **예외가 화면에 안 보인다**는 것이다. 브라우저 콘솔에만
   * 찍히고 화면은 아무 일도 일어나지 않는다. 그래서 헤더의 「로그아웃」과
   * 비밀번호를 바꾼 뒤의 로그아웃이 **누르면 아무 반응이 없는 상태**로
   * 남아 있었다.
   *
   * 인자를 받는 함수로 두면 그 쪼개기에 걸릴 것이 없다.
   *
   * [왜 폼이어야 하나]
   *
   * 쿠키를 지우는 것은 서버 일이라 회로 안에서 주소를 옮기는 것으로는 안 되고,
   * 위조방지 토큰도 함께 가야 한다. 그리고 셸은 로그아웃을 GET 으로 받지
   * 않는다 — GET 이면 이미지 태그 하나로 남을 로그아웃시킬 수 있다.
   */
  window.jsiniForm = {
    submit: function (id) {
      var form = document.getElementById(id);

      if (!form) {
        // 던지지 않는다. 회로가 죽을 뿐 사용자는 아무것도 알 수 없다.
        // 콘솔에 남기면 다음 사람이 찾을 수 있다.
        if (window.console) console.warn('[jsini] 제출할 폼을 찾지 못했다: ' + id);
        return false;
      }

      form.submit();
      return true;
    },
  };

  /**
   * 회로가 붙을 때 브라우저에서 읽어 올 것을 **한 번에** 읽는다.
   *
   * [무엇을 고친 것인가]
   *
   * 레이아웃이 뜰 때 부품 다섯이 저마다 저장소를 읽었다 — 잠금 표시,
   * 공지 닫힘 표시, 「오늘 하루 보지 않기」, 고정 탭, 지금 테마. 게다가
   * 워터마크를 거는 호출이 하나 더 있었다.
   *
   * Blazor Server 에서 이 호출 하나하나가 **브라우저까지 갔다 오는 왕복
   * 하나**다. 여섯이 직렬로 나가면 왕복 50ms 환경에서 300ms 가 그냥 붙는다.
   * 그리고 포털은 **업무를 넘나들 때마다 레이아웃을 새로 만들기 때문에**
   * (Piral 모듈 컨테이너가 갈린다) 그 여섯이 화면 전환마다 다시 났다.
   *
   * [왜 값마다 함수를 두지 않았나]
   *
   * `locked()` `pinnedTabs()` 처럼 이름 있는 함수를 두면 읽는 자리가 는다.
   * 그러면 부품이 늘 때마다 왕복도 함께 늘고, 지금 고친 것이 되돌아온다.
   * 열쇠 목록을 받아 **사전 하나로** 돌려주면 부품이 몇이든 왕복은 하나다.
   *
   * [열쇠를 여기 적어 두지 않는다]
   *
   * 열쇠는 서버 쪽(`PortalBoot`)이 들고 있고 이 함수는 받은 것만 읽는다.
   * 양쪽에 적어 두면 한쪽만 고치는 날이 오고, 그때 증상은 「그 표시가
   * 조용히 안 읽힌다」다 — 오류가 아니라 기능이 하나 사라지는 쪽이다.
   *
   * @param {{watermark?: string|null, session?: string[], local?: string[]}} req
   * @returns {{session: Object, local: Object, theme: Object}}
   */
  window.jsiniBoot = {
    read: function (req) {
      req = req || {};

      // 워터마크. **읽기와 같은 왕복에 태우려고 여기 있다.** 이 함수가 하는
      // 일 중 유일하게 화면을 바꾸는 것이라, 이름에 담지 못한 대신 적어 둔다.
      //
      // **거는 것만 한다.** 걷는 일(`jsiniWatermark.hide`)을 여기 태우면
      // 이름이 안 실려 온 왕복 하나가 방금 걸어 둔 워터마크를 지운다 —
      // 읽는 순서를 우리가 정하지 못하므로 그 왕복은 실제로 생긴다.
      if (req.watermark) {
        window.jsiniWatermark.show(req.watermark);
      }

      // 한동안 이 왕복에 「지울 열쇠」도 태웠다(`req.forget`). 로그인 화면이
      // 로그인 뒤 공지의 닫힘 표시를 지우던 자리인데, 그 화면이 회로를 쓰지
      // 않게 되면서 `jsiniNotice` 가 직접 지운다 — 태울 것이 없어졌다.

      return {
        session: readAll(readSession, req.session),
        local: readAll(readLocal, req.local),

        // 테마는 저장소가 아니라 위쪽 `jsiniTheme` 가 좁혀 둔 값이다.
        // 함께 실어 주는 이유는 그것 하나 때문에 왕복을 또 하지 않으려는 것뿐이다.
        theme: window.jsiniTheme.current(),
      };
    },
  };

  // ── 로그인 화면의 공개 공지 팝업 ────────────────────────────
  //
  // [왜 여기 순수 JS 로 있나]
  //
  // 이 팝업은 **회로를 기다리지 않는다.** 로그인 화면은 정적 SSR 이라
  // (쿠키를 구워야 한다) 팝업을 대화형 섬으로 올려 두었더니, 뜨기까지
  // blazor.web.js → DevExpress 모듈 1.4MB 해석 → initializers → negotiate →
  // 웹소켓 → 회로 시작 → 저장소 왕복 → 공지 조회를 모두 지나야 했다.
  // 캐시가 다 찬 개발 장비에서도 로그인 폼보다 460ms 늦었다.
  //
  // 지금은 서버가 공지를 첫 HTML 에 함께 실어 보내고(`PublicNoticePopup`),
  // 넘기기·닫기·「오늘 하루 보지 않기」만 이 코드가 받는다. theme.js 는
  // <head> 에서 동기로 도므로 그 마크업이 파싱될 때 이미 여기 있다.
  //
  // [열쇠와 날짜를 여기 적지 않는다]
  //
  // 저장소 열쇠의 정본은 서버(`PortalBoot`)이고, 「오늘」의 기준도 서버다
  // (로그인 뒤 팝업이 같은 값을 서버 날짜로 읽고 쓴다). 둘 다 마크업의
  // data- 속성으로 받는다 — 양쪽에 적으면 한쪽만 고치는 날이 오고, 그때
  // 증상은 「그 표시가 조용히 안 읽힌다」다.
  window.jsiniNotice = {
    /**
     * 팝업 하나를 살린다. **두 번 불러도 한 번만 듣는다** — 인라인
     * <script> 와 아래 훑기가 같은 요소를 함께 가리킬 수 있다.
     *
     * @param {HTMLElement} root `[data-jsini-notice]`
     */
    init: function (root) {
      if (!root || root.dataset.jsiniNoticeReady) return;
      root.dataset.jsiniNoticeReady = '1';

      // **공지가 없어도 이것은 한다.** 로그인 화면을 본다는 것은 곧
      // 로그인한다는 뜻이라, 로그인 뒤 공지를 닫아 둔 표시를 지워야
      // 다시 로그인할 때 그 공지가 또 뜬다.
      if (root.dataset.forgetKey) forget([root.dataset.forgetKey]);

      var pages = toArray(root.querySelectorAll('[data-notice-id]'));

      if (!pages.length) return;

      // 이 탭에서 방금 닫았으면 아무것도 하지 않는다. 마크업은 `hidden`
      // 으로 나갔으므로 그대로 두면 된다.
      if (closedRecently(root.dataset.closedKey)) return;

      var today = root.dataset.today || '';
      var dismissed = readDismissed(root.dataset.dismissedKey, today);

      // 오늘 안 보기로 해 둔 것을 뺀다. 지운 것이 아니라 감춘 것이다 —
      // 점과 「공지 2 / 3」이 산 것만 세도록 아래에서 다시 매긴다.
      var live = [];

      for (var i = 0; i < pages.length; i++) {
        if (dismissed[pages[i].getAttribute('data-notice-id')]) continue;
        live.push(pages[i]);
      }

      if (!live.length) return;

      show(root, pages, live, today);
    },
  };

  /**
   * 팝업을 띄우고 단추를 잇는다.
   *
   * 자리(`at`)와 체크해 둔 것(`picked`)만이 이 창의 상태다. DOM 을 상태로
   * 쓰지 않는다 — 「지금 몇 번째인가」를 보이는 요소로 되짚기 시작하면
   * 감춰 둔 공지가 그 계산에 섞인다.
   */
  function show(root, pages, live, today) {
    var at = 0;
    var picked = {};
    var dots = toArray(root.querySelectorAll('[data-act="goto"]'));
    var dotBox = root.querySelector('[data-dots]');
    var prev = root.querySelector('[data-act="prev"]');
    var next = root.querySelector('[data-act="next"]');
    var check = root.querySelector('[data-act="dismiss"]');
    var card = root.querySelector('.jsini-snotice__card') || root;

    // 점은 **공지 수만큼** 나와 있다(서버는 무엇이 걸러질지 모른다).
    // 점 i 는 공지 i 의 것이라, 산 것만 남기고 번호를 다시 매긴다.
    var liveDots = [];

    for (var i = 0; i < dots.length; i++) {
      var alive = i < pages.length && live.indexOf(pages[i]) >= 0;

      dots[i].hidden = !alive;

      if (!alive) continue;

      liveDots.push(dots[i]);
      dots[i].setAttribute('aria-label', liveDots.length + '번째 공지');
      dots[i].dataset.at = String(liveDots.length - 1);
    }

    // 한 건만 남았으면 넘길 것이 없다.
    if (live.length < 2) {
      if (dotBox) dotBox.hidden = true;
      if (prev) prev.hidden = true;
    }

    root.addEventListener('click', function (e) {
      var act = e.target.closest ? e.target.closest('[data-act]') : null;

      if (!act || act === check) return;

      var name = act.getAttribute('data-act');

      if (name === 'close') close();
      else if (name === 'prev') go(at - 1);
      else if (name === 'goto') go(Number(act.dataset.at));
      else if (name === 'next') at < live.length - 1 ? go(at + 1) : close();
    });

    if (check) {
      check.addEventListener('change', function () {
        picked[live[at].getAttribute('data-notice-id')] = check.checked;
      });
    }

    // Esc 로 닫고, Tab 은 창 안에서만 돈다.
    //
    // [초점을 묶는 이유 — DevExpress 팝업이 하던 일이다]
    //
    // 그 부품은 창 앞뒤에 초점 울타리(`dxbl-focus-guard`)를 세워 둔다.
    // 골격을 다시 세우면서 그것까지 따라오지 않아서, **팝업이 떠 있는데
    // Tab 이 뒤의 로그인 폼으로 새어 나갔다.** 화면을 못 보는 사람에게는
    // 「공지가 떴다」가 아니라 「아이디 칸이 사라졌다」로 읽힌다.
    root.__keys = function (e) {
      if (e.key === 'Escape') { close(); return; }
      if (e.key !== 'Tab') return;

      var stops = focusables(card);

      if (!stops.length) return;

      var edge = e.shiftKey ? stops[0] : stops[stops.length - 1];

      // 울타리 안이 아니면(뒤의 폼에 있다) 무조건 끌어온다.
      if (!card.contains(document.activeElement)) {
        e.preventDefault();
        stops[0].focus();
        return;
      }

      if (document.activeElement !== edge) return;

      e.preventDefault();
      (e.shiftKey ? stops[stops.length - 1] : stops[0]).focus();
    };

    document.addEventListener('keydown', root.__keys);

    dragByHead(root, card);

    go(0);
    root.hidden = false;

    // 창이 떴으니 초점도 창에 있어야 한다. **단추가 아니라 창에 둔다.**
    //
    // 한동안 「다음/닫기」에 두었다 — 공지를 읽고 나서 가장 먼저 누를
    // 것이라 생각해서다. 그런데 로그인 화면에서 **Enter 한 번이 공지를
    // 닫아 버렸다.** 아이디를 치려고 온 사람이 습관으로 Enter 를 누르거나
    // 비밀번호 관리자가 채운 뒤 Enter 가 들어가면, 읽지도 않은 공지가
    // 닫히고 **그 탭에서는 다시 뜨지 않았다.**
    //
    // 창에 초점을 두면 Enter 가 아무것도 누르지 않는다. 읽는 프로그램에는
    // 여전히 「대화창 안」으로 들리고, Tab 을 누르면 아래 울타리가 받는다.
    card.setAttribute('tabindex', '-1');
    card.focus();

    /** 다른 공지로. 체크는 공지마다 따로라 옮길 때 지금 것을 기억해 둔다. */
    function go(target) {
      if (target < 0 || target >= live.length) return;

      if (check) picked[live[at].getAttribute('data-notice-id')] = check.checked;

      at = target;

      for (var i = 0; i < live.length; i++) {
        live[i].hidden = i !== at;

        var count = live[i].querySelector('[data-count]');

        if (count) {
          count.textContent = live.length > 1
            ? ' · 공지 ' + (i + 1) + ' / ' + live.length
            : '';
        }
      }

      for (var d = 0; d < liveDots.length; d++) {
        liveDots[d].className = 'jsini-notice-popup__dot' + (d === at ? ' is-on' : '');
      }

      if (prev) prev.disabled = at === 0;
      if (next) next.textContent = at < live.length - 1 ? '다음' : '닫기';
      if (check) check.checked = !!picked[live[at].getAttribute('data-notice-id')];
    }

    /**
     * 닫는다. **× 로 닫아도 체크해 둔 것은 기억한다** — × 는 「더 보지
     * 않겠다」는 뜻이라 무시하면 다음에 또 뜬다.
     */
    function close() {
      if (check) picked[live[at].getAttribute('data-notice-id')] = check.checked;

      root.hidden = true;
      document.removeEventListener('keydown', root.__keys);

      // 초점을 로그인 폼으로 넘긴다. 창을 닫은 사람이 다음에 할 일이
      // 아이디를 치는 것이라, 그대로 두면 초점이 사라진 단추에 남는다.
      var back = document.getElementById('username');

      if (back) back.focus();

      // 닫은 **시각**을 적어 둔다. `'1'` 이 아닌 이유는 `closedRecently` 에.
      try {
        window.sessionStorage.setItem(root.dataset.closedKey, String(Date.now()));
      } catch (e) {
        // 못 적어도 이번 탭에서 한 번 더 뜨는 것이 전부다.
      }

      saveDismissed(root.dataset.dismissedKey, today, picked);
    }
  }

  /**
   * 머리를 잡아 창을 옮긴다. `CommPopup` 의
   * `AllowDrag` + `AllowDragByHeaderOnly` 를 회로 없이 한 것이다.
   *
   * [머리로만 잡는다]
   *
   * 본문까지 잡히면 글을 끌어 고르려는 동작이 창 옮기기로 먹혀서 **공지를
   * 복사할 수 없다.** 그래서 `.jsini-drag-head` 안에서 시작한 것만 받는다 —
   * 그 클래스는 `CommPopup` 이 붙이는 것과 같은 것이고, 마우스 모양과 글자
   * 선택 막기는 app.css 가 정한다.
   *
   * [가운데 정렬을 기준으로 얹는다]
   *
   * 창은 flex 가운데 정렬로 앉아 있다. `left`/`top` 을 주면 그 정렬을 걷어내야
   * 해서 창 크기가 바뀔 때(공지를 넘겨 본문 길이가 달라질 때) 자리가 튄다.
   * `transform` 으로 **옮긴 거리만** 얹으면 정렬은 그대로 남는다.
   *
   * [화면 밖으로 내보내지 않는다]
   *
   * 머리를 화면 위로 끌어 올려 버리면 다시 잡을 수 없다 — 닫을 X 도 함께
   * 나간다. 창이 사방으로 조금씩은 남게 묶는다(`EDGE`).
   *
   * 포인터 이벤트로 받으므로 마우스와 손가락이 같은 길을 탄다.
   * `setPointerCapture` 를 걸어 두면 포인터가 창을 벗어나도 계속 따라온다 —
   * 걸지 않으면 빠르게 끌 때 창이 손에서 떨어진다.
   */
  function dragByHead(root, card) {
    var EDGE = 40;
    var dx = 0;
    var dy = 0;
    var fromX = 0;
    var fromY = 0;
    var moving = false;

    card.addEventListener('pointerdown', function (e) {
      // 단추는 누르는 자리다. 머리 안이라도 끌기로 먹지 않는다.
      if (e.button !== 0 || !e.target.closest) return;
      if (!e.target.closest('.jsini-drag-head')) return;
      if (e.target.closest('button, a, input, label')) return;

      moving = true;
      fromX = e.clientX - dx;
      fromY = e.clientY - dy;

      root.classList.add('jsini-snotice--dragging');

      try {
        card.setPointerCapture(e.pointerId);
      } catch (err) {
        // 못 걸어도 아래 pointermove 가 온다. 빠르게 끌 때만 놓친다.
      }

      e.preventDefault();
    });

    card.addEventListener('pointermove', function (e) {
      if (!moving) return;

      var box = card.getBoundingClientRect();

      // 지금 놓인 자리에서 얼마나 더 갈 수 있는지로 묶는다. 창 크기가
      // 공지마다 달라서 고정 한계로는 잡을 수 없다.
      var minX = dx - (box.right - EDGE);
      var maxX = dx + (window.innerWidth - box.left - EDGE);
      var minY = dy - box.top;
      var maxY = dy + (window.innerHeight - box.top - EDGE);

      dx = clamp(e.clientX - fromX, minX, maxX);
      dy = clamp(e.clientY - fromY, minY, maxY);

      card.style.transform = 'translate(' + dx + 'px, ' + dy + 'px)';
    });

    card.addEventListener('pointerup', stop);
    card.addEventListener('pointercancel', stop);

    function stop() {
      moving = false;
      root.classList.remove('jsini-snotice--dragging');
    }
  }

  function clamp(v, lo, hi) {
    return v < lo ? lo : (v > hi ? hi : v);
  }

  /**
   * 이 탭에서 **방금** 닫았는가.
   *
   * [왜 「닫았다/안 닫았다」가 아니라 시한인가]
   *
   * 이 표시가 막으려는 것은 하나뿐이다 — 비밀번호를 틀려 폼이 다시 올라올
   * 때(정적 SSR 이라 문서가 새로 로드된다) 방금 닫은 공지가 또 뜨는 것.
   * 그런데 값을 `'1'` 로 두었더니 **탭을 닫을 때까지** 안 떴다. 브라우저를
   * 새로 열면 뜨고 그 탭에서는 안 뜨니, 증상이 「공지가 안 나온다」로만
   * 보인다 — 실제로 그렇게 신고를 받았다.
   *
   * 그래서 닫은 시각을 적고 <b>몇 분만</b> 인정한다. 폼을 다시 올리는 것은
   * 초 단위 안에 일어나므로 막으려던 것은 그대로 막고, 로그아웃하고 한참
   * 뒤에 돌아온 사람은 공지를 다시 본다(로그인 뒤 공지도 그렇게 다시
   * 뜬다 — `:user` 를 로그인 화면이 지운다).
   *
   * 옛 `'1'` 이 남아 있는 탭은 <b>저절로 풀린다</b> — 1 은 1970년이라
   * 언제 보아도 시한이 지난 값이다.
   */
  function closedRecently(key) {
    var at = Number(readSession(key));

    return at > 0 && Date.now() - at < CLOSED_FOR;
  }

  /** 닫은 것을 인정하는 시간. 위 주석 참고. */
  var CLOSED_FOR = 5 * 60 * 1000;

  /**
   * 「오늘 하루 보지 않기」로 적어 둔 것. 날짜가 지났으면 없던 일로 본다.
   *
   * 값이 깨져 있으면 없는 것으로 본다 — 공지를 한 번 더 보는 쪽이 안 보이는
   * 쪽보다 낫다.
   */
  function readDismissed(key, today) {
    var out = {};

    if (!key) return out;

    try {
      var raw = readLocal(key);

      if (!raw) return out;

      var rec = JSON.parse(raw);

      // 칸 이름은 서버(`NoticeAutoPopup.DismissRecord`)가 정한다. 옛 값이
      // 소문자로 남아 있을 수 있어 둘 다 본다.
      if ((rec.Until || rec.until) !== today) return out;

      var ids = rec.Ids || rec.ids || [];

      for (var i = 0; i < ids.length; i++) out[ids[i]] = true;
    } catch (e) {
      /* 위 주석과 같다 */
    }

    return out;
  }

  /**
   * 체크해 둔 것을 적어 둔다. **이미 적혀 있던 것과 합친다** — 덮어쓰면
   * 이 탭에서 안 본 공지가 오늘 다시 뜬다.
   *
   * 칸 이름은 대문자로 적는다. 로그인 뒤 팝업이 그 이름으로 읽기 때문이다.
   */
  function saveDismissed(key, today, picked) {
    if (!key) return;

    var ids = readDismissed(key, today);
    var any = false;

    for (var id in picked) {
      if (picked[id]) { ids[id] = true; any = true; }
    }

    if (!any) return;

    try {
      window.localStorage.setItem(key, JSON.stringify({
        Until: today,
        Ids: Object.keys(ids),
      }));
    } catch (e) {
      // 사생활 보호 모드에서는 setItem 이 던진다. 이번 창에서만 안 보이고
      // 끝날 뿐이라 사용자에게 말할 일이 아니다.
    }
  }

  /**
   * 문서에 있는 공지 팝업을 모두 살린다.
   *
   * [인라인 <script> 가 있는데 왜 또 훑나]
   *
   * 그 스크립트는 **문서가 파싱될 때만** 돈다. Blazor 의 향상된 이동
   * (enhanced navigation)으로 로그인 화면에 들어오면 문서가 새로 파싱되지
   * 않아 그 길이 없다. 그때는 이 훑기가 받는다. `init` 이 두 번 불려도
   * 한 번만 듣는다.
   */
  function scanNotices() {
    var nodes = document.querySelectorAll('[data-jsini-notice]');

    for (var i = 0; i < nodes.length; i++) window.jsiniNotice.init(nodes[i]);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', hookNotices);
  } else {
    hookNotices();
  }

  function hookNotices() {
    scanNotices();

    // `Blazor` 는 blazor.web.js 가 실린 뒤에 생긴다 — 이 파일은 <head> 에서
    // 그보다 먼저 도므로 여기서 붙인다.
    if (window.Blazor && window.Blazor.addEventListener) {
      window.Blazor.addEventListener('enhancedload', scanNotices);
    }
  }

  /**
   * 창 안에서 초점이 갈 수 있는 것들. 감춰 둔 공지의 첨부 링크는 뺀다 —
   * 넣으면 Tab 이 보이지 않는 링크를 지나간다.
   */
  function focusables(card) {
    var all = card.querySelectorAll(
      'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])');
    var out = [];

    for (var i = 0; i < all.length; i++) {
      if (all[i].hidden || all[i].offsetParent === null) continue;

      out.push(all[i]);
    }

    return out;
  }

  /** `NodeList` 를 배열로. `Array.from` 은 옛 브라우저에 없다. */
  function toArray(nodes) {
    var out = [];

    for (var i = 0; i < nodes.length; i++) out.push(nodes[i]);

    return out;
  }

  /**
   * 열쇠 목록을 사전으로 읽는다. 없는 열쇠는 `null` 로 담는다 —
   * 칸을 빼면 받는 쪽이 「없다」와 「안 읽었다」를 구분할 수 없다.
   */
  function readAll(read, keys) {
    var out = {};

    if (!keys) return out;

    for (var i = 0; i < keys.length; i++) {
      out[keys[i]] = read(keys[i]);
    }

    return out;
  }

  /**
   * 저장소를 읽는다. **못 읽어도 던지지 않는다.**
   *
   * 사생활 보호 모드나 서드파티 차단 설정에서는 `sessionStorage` 에
   * 손대는 것만으로 던진다. 한 열쇠 때문에 나머지 넷까지 잃으면 안 되므로
   * 열쇠마다 각자 막는다. 못 읽은 것은 `null` 이고, 그 뜻은 받는 쪽이 정한다.
   */
  function readSession(key) {
    try { return window.sessionStorage.getItem(key); } catch (e) { return null; }
  }

  function readLocal(key) {
    try { return window.localStorage.getItem(key); } catch (e) { return null; }
  }

  /**
   * 세션 저장소에서 지운다. 못 지워도 넘어간다 — 위와 같은 이유다.
   * 쓰는 곳은 `jsiniNotice.init`(로그인 뒤 공지의 닫힘 표시 지우기) 하나다.
   */
  function forget(keys) {
    if (!keys) return;

    for (var i = 0; i < keys.length; i++) {
      try { window.sessionStorage.removeItem(keys[i]); } catch (e) { /* 위와 같다 */ }
    }
  }
})();
