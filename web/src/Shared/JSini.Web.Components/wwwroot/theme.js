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

  /** 강조색 프리셋. 파일 이름이 곧 식별자다. */
  var FLUENT_ACCENTS = [
    { id: 'blue', name: 'Blue', swatch: '#0f6cbd' },
    { id: 'cool-blue', name: 'Cool Blue', swatch: '#3b5b8c' },
    { id: 'desert', name: 'Desert', swatch: '#a67c52' },
    { id: 'mint', name: 'Mint', swatch: '#3a8f6f' },
    { id: 'moss', name: 'Moss', swatch: '#5c7a3a' },
    { id: 'orchid', name: 'Orchid', swatch: '#9a4f96' },
    { id: 'purple', name: 'Purple', swatch: '#6f42c1' },
    { id: 'rose', name: 'Rose', swatch: '#b8436b' },
    { id: 'rust', name: 'Rust', swatch: '#b1562a' },
    { id: 'steel', name: 'Steel', swatch: '#4a6572' },
    { id: 'storm', name: 'Storm', swatch: '#5a5f6e' },
  ];

  var CLASSIC_THEMES = [
    { id: 'blazing-berry', name: 'Blazing Berry', dark: false },
    { id: 'blazing-dark', name: 'Blazing Dark', dark: true },
    { id: 'purple', name: 'Purple', dark: false },
    { id: 'office-white', name: 'Office White', dark: false },
  ];

  /**
   * 기본값.
   *
   * DevExpress 데모의 기본과 같은 자리 — Fluent Light + Blue.
   * Classic 을 기본으로 두지 않는 이유는 파일이 2.8MB 라 첫 방문이 느려서다
   * (Fluent 은 core 1.6MB 에 밝기·강조색이 100KB 남짓이다).
   */
  var DEFAULT = { family: 'fluent', mode: 'light', accent: 'blue', custom: null };

  // ── 스타일시트 관리 ───────────────────────────────────────

  /** 이미 붙인 <link> 들. 키는 주소. */
  var links = {};

  /**
   * 스타일시트를 붙인다(이미 있으면 그대로 쓴다).
   * 다 실렸을 때 콜백을 부른다 — 실패해도 부른다(그 테마만 안 예뻐질 뿐이다).
   */
  function ensure(href, onReady) {
    var existing = links[href];

    if (existing) {
      if (existing.__loaded) {
        onReady();
      } else {
        existing.addEventListener('load', onReady, { once: true });
        existing.addEventListener('error', onReady, { once: true });
      }
      return existing;
    }

    var link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    link.disabled = true;
    link.setAttribute('data-jsini-theme', '');

    link.addEventListener('load', function () {
      link.__loaded = true;
      onReady();
    }, { once: true });

    link.addEventListener('error', function () {
      link.__loaded = true;
      onReady();
    }, { once: true });

    document.head.appendChild(link);
    links[href] = link;
    return link;
  }

  /** 지금 고른 것에 필요한 스타일시트 주소들. */
  function sheetsFor(spec) {
    if (spec.family === 'classic') {
      return [CLASSIC + spec.classic + '.bs5.min.css'];
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

  function isDark(spec) {
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

    function ready() {
      if (--pending > 0) return;

      // **다 실린 뒤에** 갈아 끼운다. 순서를 바꾸면 화면이 번쩍인다.
      for (var href in links) {
        links[href].disabled = wanted.indexOf(href) < 0;
      }

      applyCustomAccent(spec.family === 'fluent' ? spec.custom : null);

      // 우리 CSS 가 보는 표시. 사이드바·헤더 색이 DevExpress 테마와 함께 움직인다.
      var root = document.documentElement;
      root.setAttribute('data-theme', isDark(spec) ? 'dark' : 'light');
      root.setAttribute('data-dx-family', spec.family);

      // DevExpress Fluent 이 밝기별 클래스를 본다.
      root.classList.toggle('dxbl-theme-fluent-mode-light',
        spec.family === 'fluent' && spec.mode === 'light');
      root.classList.toggle('dxbl-theme-fluent-mode-dark',
        spec.family === 'fluent' && spec.mode === 'dark');

      current = spec;
      if (done) done();
    }

    for (var i = 0; i < wanted.length; i++) {
      ensure(wanted[i], ready);
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
  }

  /** 저장된 값을 지금 아는 모양으로 좁힌다. 모르는 값이면 기본값으로. */
  function normalize(spec) {
    if (!spec || typeof spec !== 'object') return DEFAULT;

    if (spec.family === 'classic') {
      for (var i = 0; i < CLASSIC_THEMES.length; i++) {
        if (CLASSIC_THEMES[i].id === spec.classic) {
          return { family: 'classic', classic: spec.classic };
        }
      }
      return DEFAULT;
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
    };
  }

  apply(normalize(stored()));

  window.jsiniTheme = {
    /** 고를 수 있는 것들. 테마 창이 이 목록을 그린다. */
    catalog: function () {
      return {
        modes: FLUENT_MODES,
        accents: FLUENT_ACCENTS,
        classic: CLASSIC_THEMES,
      };
    },

    /** 지금 고른 것. */
    current: function () {
      return current;
    },

    /** 지금 테마가 어두운가. */
    isDark: function () {
      return isDark(current);
    },

    /** Fluent 을 고른다. 밝기와 강조색을 함께 넘긴다. */
    setFluent: function (mode, accent, custom) {
      var spec = normalize({ family: 'fluent', mode: mode, accent: accent, custom: custom });
      apply(spec);
      save(spec);
      return spec;
    },

    /** Classic 한 장짜리 테마를 고른다. */
    setClassic: function (id) {
      var spec = normalize({ family: 'classic', classic: id });
      apply(spec);
      save(spec);
      return spec;
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
})();
