/**
 * 히어로 배경 모션 — '상승 조각' 모티프를 캔버스에 아주 느리게 흘린다.
 *
 * 원본: fronts/apps/jsini-site/src/components/shard-motion.vue.
 * Vue 컴포넌트였던 것을 평범한 스크립트로 옮겼다 — 이 앱에는 회로가 없으므로
 * Blazor 와 무관하게 도는 편이 맞다. `[data-shard-motion]` 을 붙인 캔버스를
 * 찾아 스스로 붙는다.
 *
 * [영상을 쓰지 않는 이유가 셋이다]
 *   1) 3MB 비디오 대 2KB 스크립트. 소개 사이트에서 첫 화면 속도가 곧 이탈률이다.
 *   2) 해상도가 무한하다. 4K 화면에서도 사선이 깨지지 않는다.
 *   3) **흑백 그라디언트는 비디오 압축에서 밴딩이 가장 잘 생기는 소재다.**
 *      우리 팔레트가 정확히 그것이라, 영상으로 만들면 계단이 보인다.
 *
 * 모티프의 규칙은 docs/brand/README.md 6절을 따른다 —
 * 기울기(사선 22 : 높이 100)와 명도 4단계를 유지하고, 조각을 늘리지 않는다.
 *
 * 지키는 것
 *   · `prefers-reduced-motion: reduce` 면 **한 프레임만 그리고 멈춘다.**
 *   · 히어로 문구는 이 위에 얹지 않는다. 배경은 오른쪽, 문구는 왼쪽이다(CSS 가 배치).
 *   · 탭이 보이지 않으면 멈춘다. 배경 탭에서 CPU 를 쓰지 않는다.
 */
(function () {
  'use strict';

  /** 조각의 명도 4단계. 어두운 배경 위라 Graphite → Paper 로 올라간다. */
  var TONES = ['#1c1c1e', '#3a3a3c', '#6e6e73', '#d2d2d7'];

  /** 기울기. 위로 갈수록 오른쪽으로 밀린다 (상승 조각과 같은 각도). */
  var SHEAR = 0.22;

  var canvas = document.querySelector('[data-shard-motion]');
  if (!canvas) return;

  var ctx = canvas.getContext('2d');
  if (!ctx) return;

  var raf = 0;

  function draw() {
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    var w = canvas.clientWidth;
    var h = canvas.clientHeight;

    if (w === 0 || h === 0) return;

    canvas.width = w * dpr;
    canvas.height = h * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // 조각 폭과 간격을 화면 폭에 맞춘다. 좁은 화면에서는 개수를 줄인다(늘리지는 않는다).
    var count = w < 640 ? 3 : 4;
    var band = w / (count + 1);
    var barW = band * 0.34;

    function render(time) {
      ctx.clearRect(0, 0, w, h);

      for (var i = 0; i < count; i += 1) {
        // 아주 느리게 오르내린다. 주기를 조각마다 어긋나게 해서 함께 움직이지 않게 한다.
        var phase = reduce ? 0 : Math.sin(time / 9000 + i * 1.7) * 0.5 + 0.5;
        var height = h * (0.28 + 0.16 * i + 0.1 * phase);
        var x = band * (i + 0.4);
        var top = h - height;

        ctx.fillStyle = TONES[i % TONES.length];
        ctx.beginPath();
        ctx.moveTo(x, h);
        ctx.lineTo(x + barW, h);
        ctx.lineTo(x + barW + height * SHEAR, top);
        ctx.lineTo(x + height * SHEAR, top);
        ctx.closePath();
        ctx.fill();
      }

      if (!reduce && !document.hidden) {
        raf = requestAnimationFrame(render);
      }
    }

    cancelAnimationFrame(raf);
    render(reduce ? 0 : performance.now());
  }

  draw();
  window.addEventListener('resize', draw);

  document.addEventListener('visibilitychange', function () {
    if (document.hidden) {
      cancelAnimationFrame(raf);
    } else {
      draw();
    }
  });
})();
