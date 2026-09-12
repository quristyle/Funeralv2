/**
 * 다이어그램 JS interop — Vue 의 `erd-diagram.vue`(@maxgraph/core)를 잇는다.
 *
 * [로컬 정적 자산이다]
 *
 * maxgraph 는 CDN 이 아니라 ../lib/maxgraph/esm (@maxgraph/core 0.24.0 의 ESM
 * 배포본 복사)에서 온다. 운영에서 외부 CDN 의존을 두지 않는다. 아래 import 는
 * 이 모듈이 처음 로드될 때(=다이어그램 화면이 처음 열릴 때) 걸리므로,
 * ERD·플로우·유스케이스를 열지 않는 사용자는 이 무게를 지불하지 않는다.
 *
 * [편집기다, 뷰어가 아니다]
 *
 * 도형을 끌어 옮기고, 크기를 바꾸고, 도형끼리 선으로 잇고, **잘못 그은 선을
 * 지우고, 선에 이름을 붙인다.**
 *
 * 도형의 이름·설명은 여기서 고치지 않는다 — 그 값은 DB 메타가 정본이고
 * (테이블 이름·코멘트), 다이어그램이 저장하는 것은 **배치와 관계**뿐이다.
 * 그래서 편집을 **선에만** 연다(`isCellEditable`).
 */
import {
  CircleLayout,
  FastOrganicLayout,
  Graph,
  HierarchicalLayout,
  InternalEvent,
  RubberBandHandler,
  Shape,
  ShapeRegistry,
  StencilShape,
  StencilShapeRegistry,
} from '../lib/maxgraph/esm/index.js';

const DEFAULT_W = 180;
const DEFAULT_H = 60;

/**
 * 크기가 적혀 있는 도형의 하한. **기본값과 다른 것이다** — 기본값은 크기가
 * 안 적힌 새 도형에 주는 값이고, 이쪽은 「적혀 있으니 그대로 쓰되 집을 수는
 * 있어야 한다」는 선이다.
 */
const MIN_W = 24;
const MIN_H = 20;

/** 칸 한 줄의 높이. 상자를 얼마나 키울지 재는 데만 쓴다(CSS 와 맞춘다). */
const FIELD_LINE_H = 18;

/** 머리(이름 + 설명)가 차지하는 높이. 위와 같은 이유로 CSS 와 맞춘다. */
const HEAD_H = 42;

/** 표 상자의 기본 너비. 칸 이름과 자료형이 한 줄에 들어갈 만큼. */
const TABLE_W = 230;

/**
 * 표 상자의 속. **모양은 CSS 가 맡는다**(`.pm-erd-*`).
 *
 * 글자를 그대로 끼우지 않는다 — 표·칸 이름은 대상 DB 에서 온 값이라
 * `<`·`&` 가 들어 있으면 라벨이 깨진다.
 */
function erdLabel(entity, fields) {
  const rows = fields.map((field) => {
    // `이름 (PK) 자료형` 으로 온다. 뒤쪽 자료형을 오른쪽에 붙인다.
    const pk = / \(PK\) /.test(field);
    const [name, ...rest] = field.replace(' (PK) ', ' ').split(' ');
    const type = rest.join(' ');

    return `<div class="pm-erd__row${pk ? ' pm-erd__row--pk' : ''}">`
      + `<span class="pm-erd__key">${pk ? '●' : ''}</span>`
      + `<span class="pm-erd__name">${esc(name)}</span>`
      + `<span class="pm-erd__type">${esc(type)}</span>`
      + '</div>';
  }).join('');

  const desc = entity.desc ? `<div class="pm-erd__desc">${esc(entity.desc)}</div>` : '';

  return '<div class="pm-erd">'
    + `<div class="pm-erd__head"><div class="pm-erd__title">${esc(entity.name || entity.id)}</div>${desc}</div>`
    + (rows ? `<div class="pm-erd__body">${rows}</div>` : '')
    + '</div>';
}

/**
 * 격자로 떨어뜨린다. 관계선이 없으면 maxgraph 의 배치는 할 일이 없다 —
 * 그때도 **겹쳐 쌓인 것만은 풀어 줘야** 그림을 손볼 수 있다.
 *
 * 키가 제각각(칸이 많은 표는 길다)이라 **열 단위로 쌓는다** — 줄 단위로
 * 놓으면 제일 긴 상자가 그 줄의 높이를 정해 사이가 휑해진다.
 */
function gridArrange(graph, parent) {
  const cells = graph.getChildCells(parent, true, false);
  if (cells.length === 0) return;

  const GAP_X = 40;
  const GAP_Y = 30;
  const columns = Math.max(1, Math.ceil(Math.sqrt(cells.length)));
  const heights = new Array(columns).fill(0);
  const widths = new Array(columns).fill(0);

  cells.forEach((cell, i) => {
    const column = i % columns;
    const geo = cell.getGeometry?.();
    if (!geo) return;

    widths[column] = Math.max(widths[column], geo.width);
  });

  // 열의 왼쪽 자리는 앞 열들의 폭이 정한다.
  const lefts = widths.reduce((acc, w, i) => {
    acc.push(i === 0 ? 20 : acc[i - 1] + widths[i - 1] + GAP_X);
    return acc;
  }, []);

  cells.forEach((cell, i) => {
    const column = i % columns;
    const geo = cell.getGeometry?.()?.clone();
    if (!geo) return;

    geo.x = lefts[column];
    geo.y = 20 + heights[column];
    heights[column] += geo.height + GAP_Y;

    graph.getDataModel().setGeometry(cell, geo);
  });
}

/* ── 도구상자 미리보기 ───────────────────────────────────────

   도구를 글자 이름으로만 늘어놓으면 「Or」·「Card」 가 무슨 모양인지 알 수
   없다. **그 도형을 실제로 그려** 작은 그림으로 보여 준다.

   그리는 사람은 maxgraph 자신이다 — 우리가 SVG 를 따로 그리면 캔버스의
   모양과 미리보기가 언젠가 어긋난다(특히 stencil 135개).

   숨긴 그래프에 도형을 하나씩 넣었다 빼면서 그려진 `<g>` 를 가져온다.
   **한 번만 한다** — 도구상자를 처음 열 때다.
   ---------------------------------------------------------- */

/**
 * 끌어다 놓을 때 싣는 꼬리표. **브라우저가 정한 이름이 아니라 우리 것**이라
 * 다른 데서 끌어온 것(파일·글자)과 섞이지 않는다.
 */
const DRAG_TYPE = 'text/pm-shape';

/** 미리보기 상자. 모두 같은 크기라야 도구상자가 깔끔하다. */
const PREVIEW_BOX = 40;

/* ----------------------------------------------------------
   붙여넣은 그림

   클립보드의 그림은 **원본 그대로 두지 않는다.** 두 군데서 막힌다.

   1. 저장은 캔버스 → 회로(SignalR) → 서버로 간다. Blazor 서버의 수신 한도가
      있어서(셸의 `MaximumReceiveMessageSize`) 큰 그림 하나로 **회로가 끊긴다.**
      화면 캡처 한 장이 base64 로 3~8MB 가 되는 것은 예사다.
   2. 저장본은 `projmng.dev_db_prop.db_pvalue` 한 칸에 들어간다. 그림 여러 장을
      원본으로 담으면 그 칸 하나가 수십 MB 가 된다.

   그래서 **긴 변 기준으로 줄이고 다시 압축해** 넣는다. 다이어그램에 붙이는
   그림은 캡처·아이콘·도식이라 이 정도면 읽는 데 지장이 없다.
   ---------------------------------------------------------- */

/** 긴 변의 상한(픽셀). 이보다 크면 비율을 지켜 줄인다. */
const IMAGE_MAX_EDGE = 1100;

/** 한 장의 상한(문자 수 ≈ 바이트). 넘으면 더 줄이거나 더 눌러 담는다. */
const IMAGE_MAX_CHARS = 420 * 1024;

/** 캔버스에 처음 놓을 때의 긴 변. 원본이 커도 화면을 뒤덮지 않게. */
const IMAGE_PLACE_EDGE = 420;

/** 만들어 둔 미리보기. `kind` → `{ svg, w, h }`. */
let previews = null;

function buildPreviews() {
  if (previews) { return previews; }

  previews = {};

  const holder = document.createElement('div');
  holder.style.cssText = 'position:absolute;left:-10000px;top:0;width:200px;height:200px;overflow:hidden';
  document.body.appendChild(holder);

  try {
    const graph = new Graph(holder);
    graph.setEnabled(false);

    const all = [
      ...Object.entries(SHAPES).map(([kind, v]) => ({ kind, style: v.style, size: v.size })),
      ...stencils.map((v) => ({ kind: v.kind, style: { shape: v.kind }, size: v.size })),
    ];

    for (const item of all) {
      const [w, h] = item.size ?? [DEFAULT_W, DEFAULT_H];

      // **비율을 지킨 채** 같은 상자에 눕힌다. 긴 쪽이 상자에 딱 맞는다.
      const scale = PREVIEW_BOX / Math.max(w, h);
      const pw = Math.max(6, Math.round(w * scale));
      const ph = Math.max(6, Math.round(h * scale));

      // 가운데로 — 납작한 모양(화살표)이 상자 위쪽에 붙지 않게.
      const x = Math.round((PREVIEW_BOX - pw) / 2) + 1;
      const y = Math.round((PREVIEW_BOX - ph) / 2) + 1;

      let cell = null;

      try {
        cell = graph.insertVertex({
          parent: graph.getDefaultParent(),
          position: [x, y],
          size: [pw, ph],

          // 미리보기는 **테두리와 면만** 본다. 우리 표 상자의 HTML 라벨이나
          // 그림자가 끼면 작은 그림이 지저분해진다.
          style: { ...item.style, shadow: false, html: false },
          value: '',
        });

        const node = graph.getView().getState(cell)?.shape?.node;

        if (node) {
          previews[item.kind] = {
            svg: node.outerHTML,
            w: PREVIEW_BOX + 2,
            h: PREVIEW_BOX + 2,
          };
        }
      }
      catch {
        // 모양 하나가 안 그려져도 나머지는 보여 준다. 그 칸은 글자만 남는다.
      }
      finally {
        if (cell) { graph.removeCells([cell]); }
      }
    }

    graph.destroy?.();
  }
  finally {
    holder.remove();
  }

  return previews;
}

/** 라벨에 끼울 글자를 안전하게. */
function esc(text) {
  return String(text ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}

/** 관계선 모양. 화살표가 있어야 어느 쪽이 어느 쪽을 가리키는지 보인다. */
const EDGE_STYLE = {
  strokeColor: '#5b7fa6',
  strokeWidth: 1.5,
  endArrow: 'block',
  rounded: true,
  labelBackgroundColor: '#ffffff',
  fontColor: '#3f5871',
};

/**
 * **외래키에서 저절로 그어진 선**의 모양. 사람이 끈 선과 갈라 보여야 한다 —
 * 이쪽은 대상 DB 의 제약이라 지워도 다음 불러오기에 되살아난다.
 */
const AUTO_EDGE_STYLE = {
  ...EDGE_STYLE,
  strokeColor: '#3f8f5f',
  fontColor: '#2f6b47',
  dashed: true,
};

/**
 * 표 상자. **HTML 라벨로 그린다.**
 *
 * 도형 하나에 머리(이름·설명)와 몸(칸 목록)이 함께 들어가야 ERD 로 읽힌다.
 * 자식 셀을 넣는 길도 있지만 그러면 끌어 옮길 때 따로 놀고 저장 형식에도
 * 담을 자리가 없다. 글자 하나로 두고 **모양은 CSS 가 맡는다**
 * (`projmng.css` 의 `.pm-erd-*`) — 테마 색을 그대로 따라간다.
 */
const VERTEX_STYLE = {
  fillColor: '#ffffff',
  strokeColor: '#d9d9d9',
  fontColor: '#262626',
  rounded: true,
  arcSize: 8,
  shadow: true,
  whiteSpace: 'wrap',
  overflow: 'fill',      // 라벨이 상자를 꽉 채운다 — 머리띠가 위에 붙는다
  verticalAlign: 'top',
  align: 'left',
  spacing: 0,
};

/**
 * **손으로 만든 도형**의 모양. 표에서 온 것과 한눈에 갈라야 한다 —
 * 이름을 고칠 수 있는 것도, 다시 불러올 때 DB 가 덮어쓰지 않는 것도 이쪽뿐이다.
 */
const MANUAL_STYLE = {
  ...VERTEX_STYLE,
  fillColor: '#f0f7ff',
  strokeColor: '#5b7fa6',

  // 손으로 만든 도형은 **글자를 그대로** 쓴다 — 더블클릭해 고치는 대상이라
  // HTML 을 넣으면 편집기에 태그가 보인다.
  //
  // [긴 글자는 저절로 접히지 않는다]
  //
  // 여기 `whiteSpace: 'wrap'` 이 있어도 **글자 라벨은 접히지 않는다.** 접는
  // 일은 maxgraph 가 라벨을 HTML 로 그릴 때만 하고, 그 판정은 값이 DOM 노드나
  // `strictHtml` 일 때만 참이다. 우리 값은 그냥 글자라 한 줄로 뻗는다.
  //
  // 억지로 HTML 로 돌리지 않는다 — 그러면 더블클릭했을 때 편집기에 태그가
  // 보이고, 이 도형은 이름을 고치라고 있는 것이다. 대신 **줄바꿈은 들어
  // 있으면 그대로 그려진다.** 옛 도구에서 옮겨 온 그림은 옮기는 쪽에서
  // 상자 폭에 맞춰 줄을 끊어 둔다(`scripts/projmng-drawio-to-diagram.py`).
  //
  // `visible` 을 그대로 둔 이유: `width` 로 두면 상자 높이를 넘는 줄이
  // **잘린다**(`max-height` + `overflow: hidden`). 넘쳐 보이는 쪽이 안 보이는
  // 쪽보다 낫다.
  overflow: 'visible',
  verticalAlign: 'middle',
  align: 'center',
};

/**
 * **붙여넣은 그림**의 모양. 테두리도 바탕도 없다 — 그림 자체가 내용이라
 * 상자를 두르면 두 겹으로 보인다.
 *
 * 이름은 그림 **아래**에 붙인다. 가운데에 얹으면 그림 위에 글자가 겹친다.
 * (이름은 비어 있는 채로 만든다. 필요하면 더블클릭해 붙인다.)
 */
const IMAGE_STYLE = {
  shape: 'image',
  imageAspect: true,
  imageBorder: 'none',
  imageBackground: 'none',
  verticalLabelPosition: 'bottom',
  verticalAlign: 'top',
  labelBackgroundColor: 'none',
  fontColor: '#595959',
};

/**
 * 클립보드·파일에서 온 그림을 **줄여서** data URL 로 만든다.
 *
 * 크기를 줄이는 이유는 위 상수 머리말에 적었다. 형식은 두 갈래로 고른다 —
 * 도식·아이콘은 PNG 가 작고 투명이 살아 있고, 사진·화면 캡처는 PNG 로 거의
 * 안 눌린다. 그래서 **PNG 를 먼저 재 보고, 한도를 넘으면 흰 바탕을 깔아
 * JPEG 로** 바꾼다. 그래도 넘으면 더 줄인다.
 *
 * @returns {Promise<{url: string, w: number, h: number} | null>}
 *   여섯 번을 줄여도 한도 안에 못 들어오면 `null`. 화면이 그것으로 말한다.
 */
async function shrinkToDataUrl(blob) {
  let bitmap;
  try {
    bitmap = await createImageBitmap(blob);
  }
  catch {
    // 브라우저가 못 읽는 형식(일부 SVG·HEIC 등)이다.
    return null;
  }

  try {
    let scale = Math.min(1, IMAGE_MAX_EDGE / Math.max(bitmap.width, bitmap.height));

    for (let attempt = 0; attempt < 6; attempt++) {
      const w = Math.max(1, Math.round(bitmap.width * scale));
      const h = Math.max(1, Math.round(bitmap.height * scale));

      const canvas = document.createElement('canvas');
      canvas.width = w;
      canvas.height = h;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingQuality = 'high';
      ctx.drawImage(bitmap, 0, 0, w, h);

      // 첫 판만 PNG 를 재 본다. 두 번째부터는 이미 PNG 로는 안 되는 그림이다.
      if (attempt === 0) {
        const png = canvas.toDataURL('image/png');
        if (png.length <= IMAGE_MAX_CHARS) { return { url: png, w, h }; }
      }

      // JPEG 는 투명을 모른다. 바탕을 깔지 않으면 투명한 곳이 **검게** 나온다.
      const flat = document.createElement('canvas');
      flat.width = w;
      flat.height = h;
      const flatCtx = flat.getContext('2d');
      flatCtx.fillStyle = '#ffffff';
      flatCtx.fillRect(0, 0, w, h);
      flatCtx.drawImage(canvas, 0, 0);

      const jpeg = flat.toDataURL('image/jpeg', attempt === 0 ? 0.86 : 0.74);
      if (jpeg.length <= IMAGE_MAX_CHARS) { return { url: jpeg, w, h }; }

      scale *= 0.7;
    }

    return null;
  }
  finally {
    bitmap.close?.();
  }
}

/** 끌어온 것·붙여넣은 것 중에서 **그림 한 장**을 골라낸다. */
function pickImageFile(source) {
  const items = [...(source?.items ?? [])];
  const item = items.find((x) => x.kind === 'file' && (x.type || '').startsWith('image/'));
  if (item) { return item.getAsFile(); }

  return [...(source?.files ?? [])].find((f) => (f.type || '').startsWith('image/')) ?? null;
}

/**
 * **DB 에서 사라진 표**의 모양(업무 흐름 화면이 `gone` 으로 표시한다).
 *
 * 글자로만(`(삭제됨)`) 알리면 도형 스물몇 개 사이에서 눈에 안 띈다. 지우지는
 * 않는다 — 사라졌다는 사실 자체가 이 화면에서 보려는 것이기 때문이다.
 */
const GONE_STYLE = {
  ...VERTEX_STYLE,
  fillColor: '#fafafa',
  strokeColor: '#d0d0d0',
  fontColor: '#8c8c8c',
  dashed: true,
  shadow: false,
};


/* ── 우리가 더 그리는 모양 ───────────────────────────────────

   maxgraph 가 기본으로 주는 모양은 열넷뿐이다(사각형·타원·마름모·삼각형·
   육각형·원통·구름·액터 …). 순서도에서 흔히 쓰는 평행사변형·사다리꼴·문서
   같은 것은 **그림을 직접 그려 등록해야** 한다 — draw.io 도 같은 방식이다
   (거기는 그것을 stencil 로 잔뜩 싣는다).

   여기서는 **경로로 그리는 몇 가지**만 더한다. 파일 하나로 끝나고, 바깥에서
   받아 오는 것이 없다.
   ---------------------------------------------------------- */

/** 경로 하나로 끝나는 모양을 짧게 만든다. */
function pathShape(draw) {
  return class extends Shape {
    constructor(bounds, fill, stroke, strokewidth = 1) {
      super();
      this.bounds = bounds;
      this.fill = fill;
      this.stroke = stroke;
      this.strokeWidth = strokewidth;
    }

    paintVertexShape(c, x, y, w, h) {
      c.begin();
      draw(c, x, y, w, h);
      c.close();
      c.fillAndStroke();
    }
  };
}

/** 기울기(평행사변형·사다리꼴)는 폭의 이만큼을 쓴다. */
const SLANT = 0.22;

const CUSTOM_SHAPES = {
  // 입출력
  pmParallelogram: pathShape((c, x, y, w, h) => {
    const d = w * SLANT;
    c.moveTo(x + d, y);
    c.lineTo(x + w, y);
    c.lineTo(x + w - d, y + h);
    c.lineTo(x, y + h);
  }),

  // 수동 조작
  pmTrapezoid: pathShape((c, x, y, w, h) => {
    const d = w * SLANT;
    c.moveTo(x, y + h);
    c.lineTo(x + d, y);
    c.lineTo(x + w - d, y);
    c.lineTo(x + w, y + h);
  }),

  // 문서 — 아래가 물결
  pmDocument: pathShape((c, x, y, w, h) => {
    const dy = h * 0.18;
    c.moveTo(x, y);
    c.lineTo(x + w, y);
    c.lineTo(x + w, y + h - dy);
    c.quadTo(x + w * 0.75, y + h - dy * 2.2, x + w * 0.5, y + h - dy);
    c.quadTo(x + w * 0.25, y + h, x, y + h - dy);
  }),

  // 단계(화살표 띠)
  pmStep: pathShape((c, x, y, w, h) => {
    const d = Math.min(w * 0.2, h / 2);
    c.moveTo(x, y);
    c.lineTo(x + w - d, y);
    c.lineTo(x + w, y + h / 2);
    c.lineTo(x + w - d, y + h);
    c.lineTo(x, y + h);
    c.lineTo(x + d, y + h / 2);
  }),

  // 카드 — 왼쪽 위 모서리를 자른다
  pmCard: pathShape((c, x, y, w, h) => {
    const d = Math.min(w, h) * 0.2;
    c.moveTo(x + d, y);
    c.lineTo(x + w, y);
    c.lineTo(x + w, y + h);
    c.lineTo(x, y + h);
    c.lineTo(x, y + d);
  }),

  // 지연 — 오른쪽이 반원
  pmDelay: pathShape((c, x, y, w, h) => {
    const r = h / 2;
    c.moveTo(x, y);
    c.lineTo(x + w - r, y);
    c.quadTo(x + w + r * 0.33, y + r, x + w - r, y + h);
    c.lineTo(x, y + h);
  }),

  // 메모 — 오른쪽 아래가 접힌다
  pmNote: pathShape((c, x, y, w, h) => {
    const d = Math.min(w, h) * 0.25;
    c.moveTo(x, y);
    c.lineTo(x + w, y);
    c.lineTo(x + w, y + h - d);
    c.lineTo(x + w - d, y + h);
    c.lineTo(x, y + h);
  }),

  // 십자(합류·분기)
  pmCross: pathShape((c, x, y, w, h) => {
    const tx = w / 3;
    const ty = h / 3;
    c.moveTo(x + tx, y);
    c.lineTo(x + tx * 2, y);
    c.lineTo(x + tx * 2, y + ty);
    c.lineTo(x + w, y + ty);
    c.lineTo(x + w, y + ty * 2);
    c.lineTo(x + tx * 2, y + ty * 2);
    c.lineTo(x + tx * 2, y + h);
    c.lineTo(x + tx, y + h);
    c.lineTo(x + tx, y + ty * 2);
    c.lineTo(x, y + ty * 2);
    c.lineTo(x, y + ty);
    c.lineTo(x + tx, y + ty);
  }),
};

// **한 번만 등록한다.** 모듈은 한 번 실리고, 그 뒤에 만들어지는 그래프가
// 모두 이 이름들을 알게 된다.
for (const [name, shape] of Object.entries(CUSTOM_SHAPES)) {
  ShapeRegistry.add(name, shape);
}

/* ── stencil 도형 묶음 ───────────────────────────────────────

   mxGraph 가 배포하는 도형 묶음(XML)을 그대로 싣는다 — 순서도·화살표·BPMN·
   기본 도형 135개다. draw.io 가 쓰는 그 파일이고 라이선스도 같다(Apache 2.0,
   `../lib/stencils/LICENSE`).

   **운영에서 외부 CDN 을 쓰지 않는다.** maxgraph 본체와 같은 판단이라 파일을
   저장소에 실어 두고 여기서 읽는다.

   이름은 draw.io 와 같은 규칙으로 짓는다 — 묶음 이름 + 모양 이름(소문자·밑줄).
   `mxgraph.flowchart.document` 처럼 되고, 그 글자가 그대로 `shape` 값이 된다.
   ---------------------------------------------------------- */

/** 실을 묶음. 화면의 도구상자에 이 이름으로 칸이 생긴다. */
const STENCIL_FILES = [
  { file: 'basic.xml',     group: '도형 묶음' },
  { file: 'flowchart.xml', group: '순서도 묶음' },
  { file: 'arrows.xml',    group: '화살표' },
  { file: 'bpmn.xml',      group: 'BPMN' },
];

/** 등록이 끝난 stencil 들. 도구상자가 이 목록을 받아 간다. */
const stencils = [];

/** 한 번만 읽는다. 여러 그림 화면을 오가도 다시 받지 않는다. */
let stencilsLoading = null;

function ensureStencils() {
  stencilsLoading ??= (async () => {
    for (const { file, group } of STENCIL_FILES) {
      try {
        const url = new URL(`../lib/stencils/${file}`, import.meta.url);
        const xml = await (await fetch(url)).text();
        const root = new DOMParser().parseFromString(xml, 'text/xml').documentElement;
        const pack = (root.getAttribute('name') || 'stencil').toLowerCase();

        for (const node of root.querySelectorAll(':scope > shape')) {
          const label = node.getAttribute('name') || '';
          if (!label) continue;

          const kind = `${pack}.${label.toLowerCase().replaceAll(' ', '_')}`;

          StencilShapeRegistry.add(kind, new StencilShape(node));

          // 묶음마다 제 비율이 있다(화살표는 납작하고 게이트웨이는 정사각).
          // 그 비율로 넣어 줘야 처음부터 제 모양이다.
          const w = Number(node.getAttribute('w')) || 100;
          const h = Number(node.getAttribute('h')) || 80;
          const scale = 90 / Math.max(w, h);

          stencils.push({
            kind,
            group,
            label,
            size: [Math.round(w * scale), Math.round(h * scale)],
          });
        }
      }
      catch {
        // 묶음 하나를 못 읽었다고 그림 화면이 죽지는 않는다. 그 칸만 빈다.
      }
    }
  })();

  return stencilsLoading;
}

/**
 * **도구상자에 놓는 도형들.** maxgraph 가 기본으로 등록해 둔 모양을 쓴다
 * (`registerDefaultShapes`) — 우리가 그림을 그리는 것이 아니라 이름만 고른다.
 *
 * 여기 없는 모양이 필요해지면 한 줄 더하면 된다. 이름(`kind`)은 화면의
 * 도구상자가 그대로 넘긴다.
 */
const SHAPES = {
  // ── 기본 ─────────────────────────────────────────────────
  box:       { group: '기본', label: '사각형',   style: {} },
  round:     { group: '기본', label: '둥근상자', style: { rounded: true, arcSize: 12 } },
  ellipse:   { group: '기본', label: '타원',     style: { shape: 'ellipse' } },
  circle:    { group: '기본', label: '원',       style: { shape: 'ellipse', aspect: 'fixed' }, size: [90, 90] },
  rhombus:   { group: '기본', label: '마름모',   style: { shape: 'rhombus' } },
  triangle:  { group: '기본', label: '삼각형',   style: { shape: 'triangle' } },
  hexagon:   { group: '기본', label: '육각형',   style: { shape: 'hexagon' } },
  cross:     { group: '기본', label: '십자',     style: { shape: 'pmCross' } },
  text:      { group: '기본', label: '글자만',   style: { fillColor: 'none', strokeColor: 'none' } },

  // ── 순서도 ───────────────────────────────────────────────
  fcProcess: { group: '순서도', label: '처리',     style: {} },
  fcStart:   { group: '순서도', label: '시작·끝',  style: { rounded: true, arcSize: 40, fillColor: '#f6ffed', strokeColor: '#52a746' } },
  fcDecide:  { group: '순서도', label: '판단',     style: { shape: 'rhombus', fillColor: '#fffbe6', strokeColor: '#d4b106' } },
  fcData:    { group: '순서도', label: '입·출력',  style: { shape: 'pmParallelogram' } },
  fcManual:  { group: '순서도', label: '수동 조작', style: { shape: 'pmTrapezoid' } },
  fcDoc:     { group: '순서도', label: '문서',     style: { shape: 'pmDocument' } },
  fcPrep:    { group: '순서도', label: '준비',     style: { shape: 'hexagon' } },
  fcStep:    { group: '순서도', label: '단계',     style: { shape: 'pmStep' } },
  fcDelay:   { group: '순서도', label: '지연',     style: { shape: 'pmDelay' } },
  fcCard:    { group: '순서도', label: '카드',     style: { shape: 'pmCard' } },
  fcConn:    { group: '순서도', label: '연결자',   style: { shape: 'ellipse', aspect: 'fixed' }, size: [60, 60] },

  // ── UML ──────────────────────────────────────────────────
  umlClass:  { group: 'UML', label: '클래스',   style: { shape: 'swimlane', startSize: 26, verticalAlign: 'top', fillColor: '#ffffff', strokeColor: '#8c8c8c' }, size: [200, 120] },
  umlCase:   { group: 'UML', label: '유스케이스', style: { shape: 'ellipse' } },
  umlActor:  { group: 'UML', label: '액터',     style: { shape: 'actor', fillColor: '#ffffff' }, size: [46, 80] },
  umlIface:  { group: 'UML', label: '인터페이스', style: { shape: 'ellipse', aspect: 'fixed' }, size: [70, 70] },
  umlPkg:    { group: 'UML', label: '패키지',   style: { shape: 'swimlane', startSize: 22, fillColor: '#fafafa', verticalAlign: 'top' }, size: [200, 120] },
  umlNote:   { group: 'UML', label: '노트',     style: { shape: 'pmNote', fillColor: '#fffbe6', strokeColor: '#d4b106', align: 'left', verticalAlign: 'top', spacing: 6 } },

  // ── ERD ──────────────────────────────────────────────────
  erdEntity: { group: 'ERD', label: '엔터티',     style: { fillColor: '#ffffff', strokeColor: '#5b7fa6' } },
  erdWeak:   { group: 'ERD', label: '약한 엔터티', style: { fillColor: '#ffffff', strokeColor: '#5b7fa6', strokeWidth: 3 } },
  erdRel:    { group: 'ERD', label: '관계',       style: { shape: 'rhombus', fillColor: '#ffffff', strokeColor: '#5b7fa6' } },
  erdAttr:   { group: 'ERD', label: '속성',       style: { shape: 'ellipse', fillColor: '#ffffff', strokeColor: '#5b7fa6' }, size: [120, 50] },
  erdMulti:  { group: 'ERD', label: '다중값 속성', style: { shape: 'doubleEllipse', fillColor: '#ffffff', strokeColor: '#5b7fa6' }, size: [130, 56] },

  // ── 구성도 ───────────────────────────────────────────────
  infraDb:    { group: '구성도', label: 'DB',     style: { shape: 'cylinder', fillColor: '#ffffff' }, size: [110, 80] },
  infraCloud: { group: '구성도', label: '클라우드', style: { shape: 'cloud', fillColor: '#f5f9ff', strokeColor: '#5b7fa6' }, size: [160, 100] },
  infraSrv:   { group: '구성도', label: '서버',   style: { rounded: true, arcSize: 6, fillColor: '#f0f5ff', strokeColor: '#5b7fa6' } },
  infraUser:  { group: '구성도', label: '사용자', style: { shape: 'actor', fillColor: '#ffffff' }, size: [46, 80] },
  infraZone:  { group: '구성도', label: '영역',   style: { fillColor: 'none', strokeColor: '#8c8c8c', dashed: true, verticalAlign: 'top', align: 'left', spacing: 6 }, size: [280, 180] },
};

/**
 * **고른 선의 모양.** 도형과 달리 선은 만들기보다 **고쳐 쓰는** 일이 많다 —
 * 끌어서 그은 다음 「점선으로」·「양쪽 화살표로」 가 필요해진다.
 */
const EDGE_STYLES = {
  solid:  { label: '실선',     style: { dashed: false } },
  dashed: { label: '점선',     style: { dashed: true } },
  thick:  { label: '굵게',     style: { strokeWidth: 3 } },
  thin:   { label: '얇게',     style: { strokeWidth: 1 } },
  both:   { label: '양쪽 화살표', style: { startArrow: 'block', endArrow: 'block' } },
  none:   { label: '화살표 없음', style: { startArrow: 'none', endArrow: 'none' } },
  ortho:  { label: '직각',     style: { edgeStyle: 'orthogonalEdgeStyle', rounded: true } },
  curve:  { label: '곡선',     style: { curved: true } },
};

/**
 * 컨테이너에 그래프를 만든다. Blazor 가 돌려받은 객체 참조로
 * load / save / zoomIn / zoomOut / fit / deleteSelection / destroy 를 부른다.
 *
 * @param {HTMLElement} container 그림이 들어갈 자리
 * @param {object} [dotnet] 바뀔 때 알려 줄 .NET 객체 참조(`NotifyChanged`).
 *   **안 주면 알리지 않는다** — 보기만 하는 자리도 있기 때문이다.
 */
export async function create(container, dotnet) {
  // **도형 묶음을 먼저 등록한다.** 그림을 그리고 나서 등록하면 그 사이에
  // 그려진 도형이 빈 네모로 남는다.
  await ensureStencils();

  InternalEvent.disableContextMenu(container);

  const graph = new Graph(container);
  graph.setPanning(true);
  graph.setConnectable(true);
  graph.setCellsResizable(true);
  graph.setAllowDanglingEdges(false);
  graph.setHtmlLabels(true);
  graph.setDropEnabled(false);

  // **선과 「손으로 만든 도형」만 고칠 수 있다**(머리말).
  //
  // 표에서 온 도형은 이름·설명의 정본이 DB 라 여기서 고칠 것이 아니다 —
  // 고쳐 봐야 다음 불러오기에서 DB 값으로 되돌아간다. 그쪽은
  // [테이블·컬럼 설명 관리] 화면에서 코멘트를 고친다.
  graph.setCellsEditable(true);
  graph.isCellEditable = (cell) => {
    if (cell?.isEdge?.()) { return true; }

    return Boolean(cellToEntity.get(cell)?.manual);
  };

  graph.getStylesheet().putDefaultEdgeStyle({
    ...graph.getStylesheet().getDefaultEdgeStyle(),
    ...EDGE_STYLE,
  });

  new RubberBandHandler(graph);

  /** 셀 → 엔터티. 저장할 때 좌표를 되돌려 담기 위해 들고 있는다. */
  const cellToEntity = new Map();

  /** 외래키에서 그어진 선들. 저장에서 뺀다(머리말). */
  const autoEdges = new Set();

  /**
   * **칸 목록 때문에 늘려 준 크기.** 저장에서 되돌리려고 들고 있는다.
   *
   * 칸을 펼치면 상자가 커지는데, 그것은 **보여 주려고 우리가 늘린 것**이지
   * 사람이 정한 크기가 아니다. 그대로 저장하면 다음에 「컬럼 보기」를 끄고
   * 열었을 때 **속이 빈 커다란 상자**만 남는다.
   *
   * 사람이 직접 끌어 크기를 바꾼 경우는 지금 크기가 늘려 준 값과 다르므로
   * 그때는 새 값을 저장한다.
   */
  const autoSize = new Map();

  /** 그리는 중인가. 불러오는 동안의 변경은 「사람이 고친 것」이 아니다. */
  let loading = false;

  /**
   * 바뀐 것을 화면에 알린다.
   *
   * 저장하지 않고 나가면 배치가 사라지는데, 그 사실을 **화면이 말해 줄 수
   * 있어야** 한다. 알림은 한 번만 보낸다 — 도형을 끌면 변경 이벤트가 수십 번
   * 나므로, 화면이 이미 「고쳤다」를 알고 있으면 더 보낼 이유가 없다.
   */
  let notified = false;

  function notifyChanged() {
    if (loading || notified || !dotnet) { return; }

    notified = true;
    dotnet.invokeMethodAsync('NotifyChanged').catch(() => {
      // 회로가 끊겼다. 알릴 곳이 없을 뿐 그림은 그대로 쓸 수 있다.
    });
  }

  graph.getDataModel().addListener(InternalEvent.CHANGE, notifyChanged);

  /**
   * 전체가 보이게 맞추고 가운데로 둔다.
   *
   * **`graph.fit()` 이 아니다.** 0.24 에서 그 일이 플러그인으로 옮겨 갔다
   * (`FitPlugin`, 이름은 `fit`). 옛 이름으로 부르면 `graph.fit is not a
   * function` 으로 던지고 **회로가 끊긴다** — 「맞춤」 단추가 그랬다.
   *
   * [배율 상한은 **인자가 아니라 플러그인의 값**이다]
   *
   * `fitCenter()` 가 읽는 것은 `margin` 하나뿐이라, `{ maxScale: 1.2 }` 를
   * 넘기면 **조용히 무시되고** 기본 상한(`maxFitScale = 8`)까지 확대된다.
   * 도형이 두세 개뿐인 ERD·업무 흐름·유즈케이스에서 바로 드러난다 —
   * 「맞춤」을 누르면 도형 하나가 화면을 가득 채운다. 값은 플러그인에 얹는다.
   * 마인드맵(`mind-map.js`)도 같은 방식으로 막는다.
   */
  const fitPlugin = graph.getPlugin('fit');

  if (fitPlugin) {
    // 줄이는 데는 제한이 없다(큰 그림은 얼마든지 작아져야 한다).
    // 늘리는 쪽만 막는다 — 도형 두세 개짜리 그림을 8배로 키울 이유가 없다.
    fitPlugin.maxFitScale = 1.2;
  }

  function fitAll() {
    fitPlugin?.fitCenter?.();
  }

  /**
   * 도형 하나를 **화면의 그 자리에** 넣는다.
   *
   * 자리는 브라우저 좌표(`clientX/Y`)로 받는다 — 도구상자에서 끌어다 놓을 때
   * 마우스가 있는 곳이 그 값이라, 그래프 좌표로 옮기는 일을 한 곳에서만 한다.
   */
  /**
   * 브라우저 좌표(`clientX/Y`)를 **그래프 좌표**로 옮긴다.
   *
   * **재는 때가 중요하다.** 이 셈은 캔버스가 화면에서 어디 있는지
   * (`getBoundingClientRect`)에 기댄다. 그런데 캔버스 위쪽에는 알림 자리가
   * 있어서, 한마디 하는 순간 **캔버스가 그만큼 아래로 밀린다.**
   * 그림을 붙여넣을 때 「넣는 중입니다」를 띄우고 나서 재면 놓은 자리보다
   * 50px 쯤 아래에 생긴다. 그래서 **마우스가 있던 그때** 재어 둔다.
   */
  function graphPointAt(clientX, clientY) {
    const view = graph.getView();
    const scale = view.getScale() || 1;
    const translate = view.getTranslate();
    const box = container.getBoundingClientRect();

    return {
      x: (clientX - box.left) / scale - translate.x,
      y: (clientY - box.top) / scale - translate.y,
    };
  }

  /** 그래프 좌표의 한 점을 도형의 **가운데**로 삼아 왼쪽 위 모서리를 구한다. */
  function cornerOf(point, w, h) {
    return [Math.round(point.x - w / 2), Math.round(point.y - h / 2)];
  }

  /**
   * 저장본의 엔터티 하나를 **어떤 모양으로 그릴지** 정한다.
   *
   * 손으로 만든 도형은 `kind`(도형 종류)와 `image`(붙여넣은 그림)를 저장본에
   * 들고 있다. 그것을 안 보고 `MANUAL_STYLE` 하나로만 그리면, 저장하고 다시
   * 여는 순간 **골라 놓은 도형이 전부 네모가 되고 그림은 사라진다.**
   */
  function styleOf(entity) {
    if (entity.gone) { return GONE_STYLE; }
    if (!entity.manual) { return VERTEX_STYLE; }
    if (entity.image) { return { ...IMAGE_STYLE, image: entity.image }; }

    const stencil = stencils.find((x) => x.kind === entity.kind);
    const shape = SHAPES[entity.kind] ?? (stencil ? { style: { shape: entity.kind } } : null);

    return shape ? { ...MANUAL_STYLE, ...shape.style } : MANUAL_STYLE;
  }

  /** 손으로 만든 도형의 id. 표 이름과 겹치지 않게 접두어를 붙인다. */
  function manualId() {
    // 관계선이 이 id 를 가리키므로 **한 번 정하면 바꾸지 않는다** —
    // 이름을 고쳐도 id 는 그대로다.
    return `m-${Math.random().toString(36).slice(2, 10)}`;
  }

  function insertShape(kind, name, clientX, clientY) {
    const stencil = stencils.find((x) => x.kind === kind);

    const shape = SHAPES[kind]
      ?? (stencil
        ? { label: stencil.label, size: stencil.size, style: { shape: kind } }
        : SHAPES.round);

    const entity = {
      id: manualId(),
      name: name || shape.label,
      desc: '',
      manual: true,

      // **어떤 도형인지 저장본에 남긴다.**
      //
      // 한동안 안 남겼다. 그래서 육각형을 놓고 저장한 뒤 다시 열면 **모두
      // 네모**였다 — 불러오기가 손으로 만든 도형을 전부 `MANUAL_STYLE` 로만
      // 그렸기 때문이다. 도형을 고르는 일이 저장 한 번으로 사라졌다.
      kind: SHAPES[kind] || stencil ? kind : undefined,
    };

    const [w, h] = shape.size ?? [DEFAULT_W, DEFAULT_H];
    const [x, y] = cornerOf(graphPointAt(clientX, clientY), w, h);

    const cell = graph.insertVertex({
      parent: graph.getDefaultParent(),
      position: [x, y],
      size: [w, h],
      style: { ...MANUAL_STYLE, ...shape.style },
      value: entity.name,
    });

    cellToEntity.set(cell, entity);

    // **고르기만 한다.** 만들자마자 이름 편집으로 들어가면 그 편집기가
    // 포커스를 가져가며 **페이지가 굴러간다** — 그림 아래쪽에 놓았을 때
    // 화면이 훌쩍 뛴다. 이름은 더블클릭으로 고친다(화면 안내에 적혀 있다).
    graph.setSelectionCell(cell);

    return entity.id;
  }

  /**
   * **그림 한 장을 그 자리에 넣는다.**
   *
   * 처음 크기는 원본이 아니라 `IMAGE_PLACE_EDGE` 로 맞춘다 — 화면 캡처를
   * 붙이면 그림 하나가 캔버스를 통째로 덮어, 그 아래 있던 것을 못 찾는다.
   * 비율은 그대로라 모서리를 끌어 원하는 만큼 키우면 된다.
   */
  function insertImage(dataUrl, naturalW, naturalH, at) {
    const shrink = Math.min(1, IMAGE_PLACE_EDGE / Math.max(naturalW, naturalH));
    const w = Math.max(MIN_W, Math.round(naturalW * shrink));
    const h = Math.max(MIN_H, Math.round(naturalH * shrink));

    const entity = {
      id: manualId(),

      // 이름은 비워 둔다. 그림 아래에 글자가 붙는 자리라, 기본 이름을 넣으면
      // 붙이는 족족 「그림」이라는 글자가 따라다닌다. 필요하면 더블클릭한다.
      name: '',
      desc: '',
      manual: true,
      kind: 'image',
      image: dataUrl,
    };

    const [x, y] = cornerOf(at, w, h);

    const cell = graph.insertVertex({
      parent: graph.getDefaultParent(),
      position: [x, y],
      size: [w, h],
      style: { ...IMAGE_STYLE, image: dataUrl },
      value: '',
    });

    cellToEntity.set(cell, entity);
    graph.setSelectionCell(cell);

    return entity.id;
  }

  /** 화면에 한마디 한다(없으면 조용히 넘어간다). */
  function say(text) {
    dotnet?.invokeMethodAsync('NotifyNotice', text).catch(() => { });
  }

  /**
   * 그림 덩어리를 줄여서 캔버스에 놓는다. 붙여넣기와 파일 끌어놓기가 함께 쓴다.
   *
   * **줄이는 동안 기다린다.** 화면 캡처 한 장이 수 MB 라 몇백 밀리초가 걸리고,
   * 그동안 아무 말이 없으면 안 된 줄 알고 또 붙인다.
   */
  async function placeImage(blob, clientX, clientY) {
    if (!blob) { return; }

    // **먼저 자리를 잡아 둔다.** 아래 한마디가 캔버스를 밀어내기 때문이다
    // (`graphPointAt` 머리말).
    const at = graphPointAt(clientX, clientY);

    say('그림을 넣는 중입니다…');

    const shrunk = await shrinkToDataUrl(blob);
    if (!shrunk) {
      say('이 그림은 넣을 수 없습니다. 너무 크거나 브라우저가 읽지 못하는 형식입니다.');
      return;
    }

    insertImage(shrunk.url, shrunk.w, shrunk.h, at);
    say('');
  }

  /**
   * **마지막으로 마우스가 있던 자리.** 붙여넣기는 좌표를 들고 오지 않아서
   * (`Ctrl+V` 에는 마우스가 없다) 여기에 놓는다. 한 번도 안 움직였으면
   * 캔버스 가운데다.
   */
  let lastPoint = null;

  const onPointerMove = (event) => {
    lastPoint = { x: event.clientX, y: event.clientY };
  };

  container.addEventListener('pointermove', onPointerMove);

  function pointOrCenter() {
    const box = container.getBoundingClientRect();
    if (!lastPoint) {
      return [box.left + box.width / 2, box.top + box.height / 2];
    }

    // 캔버스 밖으로 나간 뒤의 자리는 쓰지 않는다 — 그림이 화면 밖에서 생긴다.
    const inside = lastPoint.x >= box.left && lastPoint.x <= box.right
      && lastPoint.y >= box.top && lastPoint.y <= box.bottom;

    return inside
      ? [lastPoint.x, lastPoint.y]
      : [box.left + box.width / 2, box.top + box.height / 2];
  }

  /* 클립보드의 그림 붙여넣기.

     **문서에 건다, 캔버스가 아니라.** `paste` 는 포커스가 있는 곳으로만 가는데
     캔버스를 한 번도 클릭하지 않았으면 포커스가 없어서 아무 일도 안 일어난다.
     그러면 「붙여넣기가 안 된다」로 끝난다. 그래서 문서에서 받고, **캔버스
     안에 포커스가 있거나 마우스가 캔버스 위에 있을 때만** 처리한다. */
  let hovering = false;
  const onEnter = () => { hovering = true; };
  const onLeave = () => { hovering = false; };

  container.addEventListener('pointerenter', onEnter);
  container.addEventListener('pointerleave', onLeave);

  const onPaste = (event) => {
    if (!container.isConnected) { return; }

    const active = document.activeElement;
    const focused = active === container || container.contains(active);
    if (!focused && !hovering) { return; }

    const blob = pickImageFile(event.clipboardData);
    if (!blob) { return; }

    // 그림이 있을 때만 가로챈다. 글자 붙여넣기(라벨 편집)는 그대로 둔다.
    event.preventDefault();

    const [x, y] = pointOrCenter();
    placeImage(blob, x, y);
  };

  document.addEventListener('paste', onPaste);

  /* 도구상자에서 끌어다 놓기.

     끌기 쪽(도구상자 단추)은 `paintPreviews` 가 건다 — 그 칸을 만드는 것도
     거기라서 한 곳에 모인다. 받는 쪽이 여기다.

     `dragover` 에서 기본 동작을 막지 않으면 **`drop` 이 아예 안 온다.** */
  const onDragOver = (event) => {
    const types = event.dataTransfer?.types ?? [];
    if (types.includes(DRAG_TYPE) || types.includes('Files')) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    }
  };

  const onDrop = (event) => {
    const kind = event.dataTransfer?.getData(DRAG_TYPE);
    if (kind) {
      event.preventDefault();
      insertShape(kind, null, event.clientX, event.clientY);
      return;
    }

    // 바탕화면에서 끌어온 그림 파일. 붙여넣기와 같은 길로 보낸다.
    const blob = pickImageFile(event.dataTransfer);
    if (!blob) { return; }

    event.preventDefault();
    placeImage(blob, event.clientX, event.clientY);
  };

  container.addEventListener('dragover', onDragOver);
  container.addEventListener('drop', onDrop);

  /** 고른 것을 지운다. 선이 대부분이고, 도형은 다시 불러오면 돌아온다. */
  function deleteSelection() {
    const cells = graph.getSelectionCells();
    if (!cells || cells.length === 0) { return 0; }

    graph.removeCells(cells, true);

    // 지운 도형은 저장 대상에서도 빠져야 한다.
    for (const cell of cells) {
      cellToEntity.delete(cell);
    }

    return cells.length;
  }

  // **Delete 로 지운다.** 도구 단추만 두면 그림을 손보는 내내 손이 캔버스와
  // 띠 사이를 오간다. 글자를 고치는 중일 때는 건드리지 않는다 — 그때 Delete 는
  // 글자를 지우는 키다.
  const onKeyDown = (event) => {
    if (event.key !== 'Delete') { return; }
    if (graph.isEditing()) { return; }

    if (deleteSelection() > 0) {
      event.preventDefault();
    }
  };

  container.setAttribute('tabindex', '0');
  container.addEventListener('keydown', onKeyDown);

  return {
    /** 모델을 캔버스에 그린다. 기존 도형은 모두 지운다. */
    load(model) {
      loading = true;
      notified = false;
      cellToEntity.clear();
      autoEdges.clear();
      autoSize.clear();
      const byId = new Map();

      graph.getDataModel().beginUpdate();
      try {
        graph.removeCells(
          graph.getChildCells(graph.getDefaultParent(), true, true),
        );

        // 좌표가 없는 엔터티는 격자로 흩뿌린다. 겹쳐 쌓이면 아무것도 알아볼 수 없다.
        let autoIndex = 0;

        (model?.entities ?? []).forEach((entity) => {
          const hasPos = Boolean(entity.x || entity.y);
          const x = hasPos
            ? (entity.x ?? 0)
            : 40 + (autoIndex % 5) * (DEFAULT_W + 30);
          const y = hasPos
            ? (entity.y ?? 0)
            : 40 + Math.floor(autoIndex / 5) * (DEFAULT_H + 40);
          if (!hasPos) autoIndex += 1;

          // **표 상자는 HTML 로 그린다**(머리말). 손으로 만든 도형은 글자
          // 그대로 둔다 — 더블클릭해 고치는 대상이다.
          const fields = entity.fields ?? [];

          // 그림은 이름이 비어 있는 것이 보통이다. 다른 손그림처럼 id 로
          // 메우면 그림 아래에 `m-3f2a9c11` 같은 글자가 붙는다.
          const label = entity.manual
            ? (entity.image ? (entity.name ?? '') : (entity.name || entity.id))
            : erdLabel(entity, fields);

          // 칸이 많으면 상자가 그만큼 커야 한다. 사람이 늘려 둔 크기가 더
          // 크면 그것을 쓴다 — 배치는 사람 것이 먼저다.
          const needed = HEAD_H + fields.length * FIELD_LINE_H + 10;

          // **칸이 없는 도형은 적힌 크기를 그대로 쓴다.**
          //
          // 한동안 여기도 `Math.max(entity.w, DEFAULT_W)` 였다. 기본값은
          // 「크기가 안 적힌 새 도형」을 위한 것인데, 적혀 있는 것까지 그
          // 바닥으로 밀어 올리면 **작게 그려 둔 도형이 전부 180x60 이 된다.**
          //
          // 옛 도구(draw.io)에서 옮겨 온 유즈케이스에서 그대로 드러났다 —
          // 도형 281개 중 247개가 그 바닥보다 작아서, 배치를 그대로 옮겨도
          // 상자들이 서로 겹쳐 덩어리로 보였다. 화살표·말풍선·글자 조각처럼
          // 작아야 뜻이 통하는 도형이 많다.
          //
          // 칸이 있는 상자는 예전 그대로다. 그쪽은 줄이 늘면 상자가 그만큼
          // 커져야 해서 바닥이 뜻을 가진다.
          const sized = entity.w > 0 && entity.h > 0;
          const size = fields.length > 0
            ? [Math.max(entity.w || 0, TABLE_W), Math.max(entity.h || 0, needed)]
            : sized
              // 너무 작으면 집을 수가 없다. 끌거나 지우려면 과녁이 있어야 한다.
              ? [Math.max(entity.w, MIN_W), Math.max(entity.h, MIN_H)]
              : [DEFAULT_W, DEFAULT_H];

          const cell = graph.insertVertex({
            parent: graph.getDefaultParent(),
            position: [x, y],
            size,
            style: styleOf(entity),
            value: label,
          });

          byId.set(entity.id, cell);
          cellToEntity.set(cell, entity);

          if (fields.length > 0) {
            const geo = cell.getGeometry?.();

            autoSize.set(cell, {
              w: geo?.width,
              h: geo?.height,
              originalW: entity.w,
              originalH: entity.h,
            });
          }
        });

        (model?.relations ?? []).forEach((relation) => {
          const source = byId.get(relation.from);
          const target = byId.get(relation.to);
          if (!source || !target) return;

          const edge = graph.insertEdge({
            parent: graph.getDefaultParent(),
            source,
            target,
            value: relation.label ?? '',
            style: relation.auto ? AUTO_EDGE_STYLE : undefined,
          });

          // 저장할 때 「저절로 그어진 선」을 가려내려고 표시해 둔다.
          if (relation.auto) { autoEdges.add(edge); }
        });
      } finally {
        graph.getDataModel().endUpdate();
        loading = false;
      }
    },

    /**
     * 캔버스의 현재 상태를 모델로 되돌린다.
     * 엔터티의 좌표·크기만 갱신한다 — 이름·설명은 DB 메타에서 오는 값이다.
     */
    save() {
      const entities = [];
      const relations = [];
      const idOfCell = new Map();

      cellToEntity.forEach((entity, cell) => {
        // **`graph.getCellGeometry(cell)` 이 아니다.** 0.24 에는 그 이름이
        // 없어서 저장이 통째로 터졌고(회로까지 끊겼다), 화면에는 「저장하지
        // 않은 변경」만 남았다. 좌표는 셀이 들고 있다.
        const geo = cell.getGeometry?.();

        // **칸 목록 때문에 우리가 늘려 준 크기는 되돌려 저장한다**(`autoSize`).
        //
        // 지금 크기가 「늘려 준 그 값」 그대로면 사람이 손대지 않은 것이므로
        // 원래 적혀 있던 값을 남긴다. 다르면 사람이 끌어 바꾼 것이라 지금
        // 값을 남긴다. **원래 값이 없던(새) 도형은 지금 값을 쓴다** —
        // `undefined` 를 저장하면 다음에 열 때 기본 크기로 떨어진다.
        const grown = autoSize.get(cell);
        const keptW = grown?.originalW > 0 && Math.round(geo?.width ?? 0) === Math.round(grown.w ?? 0);
        const keptH = grown?.originalH > 0 && Math.round(geo?.height ?? 0) === Math.round(grown.h ?? 0);

        entities.push({
          ...entity,

          // 칸 목록은 대상 DB 에서 읽어 채운 것이라 저장하지 않는다(머리말).
          fields: undefined,
          x: Math.round(geo?.x ?? entity.x ?? 0),
          y: Math.round(geo?.y ?? entity.y ?? 0),
          w: keptW ? grown.originalW : Math.round(geo?.width ?? entity.w ?? DEFAULT_W),
          h: keptH ? grown.originalH : Math.round(geo?.height ?? entity.h ?? DEFAULT_H),
        });
        idOfCell.set(cell, entity.id);
      });

      graph
        .getChildCells(graph.getDefaultParent(), false, true)
        .forEach((edge) => {
          // **외래키에서 그어진 선은 저장하지 않는다.** 불러올 때마다 다시
          // 그리므로, 저장하면 제약을 뗀 뒤에도 선이 남는다.
          if (autoEdges.has(edge)) return;

          const from = idOfCell.get(edge.source);
          const to = idOfCell.get(edge.target);
          if (!from || !to) return;
          relations.push({ from, to, label: String(edge.value ?? '') });
        });

      // 저장했으면 다음 변경부터 다시 알린다.
      notified = false;

      return { entities, relations };
    },

    /** 고른 도형·선을 지운다. 지운 개수를 돌려준다. */
    deleteSelection() {
      return deleteSelection();
    },

    /**
     * 도구상자에 보여 줄 도형 목록(묶음·이름·미리보기). **목록은 여기가 정본이다.**
     *
     * 미리보기는 그 도형을 실제로 그린 SVG 조각이다 — 화면은 그것을 그대로
     * 끼운다. 우리가 아이콘을 따로 그리면 캔버스의 모양과 언젠가 어긋난다.
     */
    shapes() {
      return [
        ...Object.entries(SHAPES).map(([kind, v]) => ({ kind, label: v.label, group: v.group })),
        ...stencils.map(({ kind, label, group }) => ({ kind, label, group })),
      ];
    },

    /**
     * 도구상자의 빈 칸(`[data-shape]`)에 **그 도형의 그림을 채운다.**
     *
     * [왜 마크업을 .NET 으로 보내지 않나 — 실제로 밟았다]
     *
     * 처음에는 `shapes()` 가 SVG 조각까지 함께 돌려줬다. 도형이 백여든 개라
     * 그 답이 수백 KB 가 되고, **Blazor 회로의 수신 한도(32KB)를 넘겨 연결이
     * 끊긴다.** 화면에는 「도구상자가 통째로 안 보인다」로만 나타난다.
     *
     * 그림은 브라우저 안에서 만들어 브라우저 안에서 끼운다. 회로로는 아무것도
     * 오가지 않는다.
     */
    paintPreviews() {
      const drawn = buildPreviews();
      let painted = 0;

      for (const host of document.querySelectorAll('[data-shape]')) {
        const kind = host.getAttribute('data-shape');
        const art = drawn[kind];

        // 이미 그려 둔 칸은 건너뛴다 — 거르개를 칠 때마다 다시 그리면
        // 도구상자가 깜빡인다.
        if (!art || host.dataset.painted === kind) { continue; }

        host.innerHTML =
          `<svg viewBox="0 0 ${art.w} ${art.h}" xmlns="http://www.w3.org/2000/svg" focusable="false">${art.svg}</svg>`;
        host.dataset.painted = kind;
        painted++;

        // **끌어다 놓기도 여기서 건다.** 칸을 만드는 곳과 끌기를 다는 곳이
        // 갈라지면 새로 생긴 칸에만 안 걸리는 날이 온다.
        const button = host.closest('button');
        if (!button || button.dataset.dragBound === '1') { continue; }

        button.draggable = true;
        button.dataset.dragBound = '1';
        button.addEventListener('dragstart', (event) => {
          event.dataTransfer.setData(DRAG_TYPE, kind);
          event.dataTransfer.effectAllowed = 'copy';
        });
      }

      return painted;
    },

    /** 도구상자의 「선」 칸에 보여 줄 목록. */
    edgeStyles() {
      return Object.entries(EDGE_STYLES).map(([kind, v]) => ({ kind, label: v.label }));
    },

    /**
     * 고른 선의 모양을 바꾼다. 도형이 골라져 있으면 건드리지 않는다 —
     * 선에만 뜻이 있는 값들이다.
     *
     * @returns 바꾼 선의 수. 0 이면 화면이 「선을 먼저 고르라」고 말한다.
     */
    styleEdge(kind) {
      const style = EDGE_STYLES[kind]?.style;
      if (!style) { return 0; }

      const edges = graph.getSelectionCells().filter((cell) => cell?.isEdge?.());
      if (edges.length === 0) { return 0; }

      graph.getDataModel().beginUpdate();
      try {
        // **`setCellStyles` 는 한 번에 한 칸씩 받는다**(키·값·대상).
        // 객체를 통째로 넘기면 조용히 아무 일도 안 한다.
        for (const [key, value] of Object.entries(style)) {
          graph.setCellStyles(key, value, edges);
        }
      } finally {
        graph.getDataModel().endUpdate();
      }

      return edges.length;
    },

    /**
     * **겹친 것을 고루 펼친다.**
     *
     * 배치(계층·유기·원형·격자)와 다르다 — 그것들은 그림을 **새로 짠다.**
     * 여기는 사람이 놓아 둔 자리를 그대로 두고 **겹친 것만 밀어낸다.**
     * 불러오기로 표가 새로 붙었을 때나, 저장본 좌표가 서로 겹칠 때 쓴다.
     *
     * 서로 밀어내기를 몇 번 되풀이한다 — 한 번에 풀면 멀리 튕겨 나가
     * 그림이 흩어진다.
     */
    spread(margin) {
      const parent = graph.getDefaultParent();
      const cells = graph.getChildCells(parent, true, false);
      if (cells.length < 2) { return 0; }

      const gap = Number(margin) > 0 ? Number(margin) : 24;
      const boxes = cells
        .map((cell) => ({ cell, geo: cell.getGeometry?.()?.clone() }))
        .filter((b) => b.geo);

      let moved = 0;

      // 한 번에 다 풀리지 않는다 — 한 쌍을 밀면 옆 것과 겹치기 때문이다.
      // 겹침이 없어지면 그 자리에서 멈추므로 넉넉히 돌린다(서른 개짜리
      // 그림에서 눈에 띄는 지연이 없다).
      for (let pass = 0; pass < 80; pass++) {
        let touched = false;

        for (let i = 0; i < boxes.length; i++) {
          for (let j = i + 1; j < boxes.length; j++) {
            const a = boxes[i].geo;
            const b = boxes[j].geo;

            const overlapX = Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x) + gap;
            const overlapY = Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y) + gap;

            if (overlapX <= 0 || overlapY <= 0) { continue; }

            touched = true;

            // **덜 밀어도 되는 쪽으로 민다.** 좌우가 조금 겹쳤는데 위아래로
            // 밀면 그림이 세로로만 길어진다.
            if (overlapX < overlapY) {
              const push = overlapX / 2;
              const left = a.x + a.width / 2 <= b.x + b.width / 2;
              a.x += left ? -push : push;
              b.x += left ? push : -push;
            }
            else {
              const push = overlapY / 2;
              const up = a.y + a.height / 2 <= b.y + b.height / 2;
              a.y += up ? -push : push;
              b.y += up ? push : -push;
            }
          }
        }

        if (!touched) { break; }
      }

      graph.getDataModel().beginUpdate();
      try {
        for (const { cell, geo } of boxes) {
          geo.x = Math.round(geo.x);
          geo.y = Math.round(geo.y);
          graph.getDataModel().setGeometry(cell, geo);
          moved++;
        }
      } finally {
        graph.getDataModel().endUpdate();
      }

      fitAll();

      return moved;
    },

    /**
     * **그림을 보기 좋게 펼친다.**
     *
     * maxgraph 가 가진 배치를 그대로 쓴다. 어느 것이 좋은지는 그림 나름이라
     * 고르게 둔다 — 관계선이 많으면 계층, 덩어리로 보고 싶으면 유기,
     * 수가 적으면 원, 관계가 없으면 격자다.
     *
     * **관계선이 하나도 없으면 계층·유기는 아무 일도 못 한다**(끌어당길 것이
     * 없다). 그때는 격자로 떨어뜨린다.
     */
    arrange(kind) {
      const parent = graph.getDefaultParent();
      const edges = graph.getChildCells(parent, false, true);
      const useGrid = kind === 'grid' || (edges.length === 0 && kind !== 'circle');

      graph.getDataModel().beginUpdate();
      try {
        if (useGrid) {
          gridArrange(graph, parent);
        }
        else if (kind === 'circle') {
          new CircleLayout(graph).execute(parent);
        }
        else if (kind === 'organic') {
          const layout = new FastOrganicLayout(graph);
          layout.forceConstant = 140;
          layout.execute(parent);
        }
        else {
          const layout = new HierarchicalLayout(graph);
          layout.intraCellSpacing = 40;
          layout.interRankCellSpacing = 90;
          layout.execute(parent);
        }
      } finally {
        graph.getDataModel().endUpdate();
      }

      fitAll();

      return useGrid ? 'grid' : kind;
    },

    /**
     * **도형을 하나 만든다.** 표에서 끌어올 것이 없는 그림(유즈케이스)이
     * 여기서 시작한다.
     *
     * 만든 도형을 **골라 둔다.** 이름은 더블클릭해 고친다 — 만들자마자
     * 편집으로 들어가면 그 편집기가 포커스를 가져가며 페이지가 굴러간다.
     *
     * 자리는 **보고 있는 화면의 가운데**다. 좌표 0,0 에 두면 멀리 끌어다
     * 놓고 보던 사람에게는 화면 밖에서 생긴다.
     */
    addEntity(name, kind) {
      // 보고 있는 화면의 가운데. 좌표 0,0 에 두면 멀리 끌어다 놓고 보던
      // 사람에게는 화면 밖에서 생긴다.
      const box = container.getBoundingClientRect();

      return insertShape(kind, name, box.left + box.width / 2, box.top + box.height / 2);
    },

    zoomIn() {
      graph.zoomIn();
    },

    zoomOut() {
      graph.zoomOut();
    },

    fit() {
      fitAll();
    },

    /** 회로가 끊기거나 화면을 떠날 때 부른다. 안 부르면 DOM 과 리스너가 샌다. */
    destroy() {
      container.removeEventListener('keydown', onKeyDown);
      container.removeEventListener('dragover', onDragOver);
      container.removeEventListener('drop', onDrop);
      container.removeEventListener('pointermove', onPointerMove);
      container.removeEventListener('pointerenter', onEnter);
      container.removeEventListener('pointerleave', onLeave);

      // **문서에 건 것이라 반드시 뗀다.** 캔버스와 함께 사라지지 않는다 —
      // 화면을 몇 번 드나들면 붙여넣기 한 번에 그림이 여러 장 생긴다.
      document.removeEventListener('paste', onPaste);
      cellToEntity.clear();
      graph.destroy?.();
    },
  };
}
