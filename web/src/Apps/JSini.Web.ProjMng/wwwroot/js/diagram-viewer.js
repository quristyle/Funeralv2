/**
 * 다이어그램 JS interop. Vue 의 erd-diagram.vue(maxgraph)를 잇는다. 편집기다 — 도형 이동·크기·선 긋기·선 이름.
 * 도형 이름·설명은 DB 메타가 정본이라 여기서 고치지 않는다(선만 편집). maxgraph 는 ../lib/maxgraph 로컬 자산이다.
 */
import {
  CircleLayout,
  FastOrganicLayout,
  Graph,
  HierarchicalLayout,
  InternalEvent,
  Outline,
  RubberBandHandler,
  Shape,
  ShapeRegistry,
  StencilShape,
  StencilShapeRegistry,
} from '../lib/maxgraph/esm/index.js';

const DEFAULT_W = 180;
const DEFAULT_H = 60;

/** 크기가 적힌 도형의 하한. 기본값(DEFAULT)은 크기가 안 적힌 새 도형용이고, 이쪽은 적힌 것을 쓰되 집을 수 있게 하는 선이다. */
const MIN_W = 24;
const MIN_H = 20;

/** 배율의 아래·위 한계. 버튼·휠·저장본이 모두 이 안으로 든다. */
const MIN_SCALE = 0.2;
const MAX_SCALE = 4;

/** 휠 한 칸이 바꾸는 배율. 단추(1.2)보다 작다 — 휠은 여러 칸이 연달아 온다. */
const WHEEL_FACTOR = 1.12;

/** 휠이 멎고 배율을 확정하기까지 기다리는 시간(ms). 짧으면 굴리는 중 확정이 끼어들고, 길면 흐린 미리보기가 남아 보인다. */
const ZOOM_COMMIT_MS = 120;

/** 끄는 동안 미니맵을 다시 그리는 최소 간격(ms). 한 번이 9.5ms 라 프레임마다 그리면 끌기가 두 배로 무거워진다. */
const OUTLINE_PAN_MS = 120;

/** 바탕 격자 한 칸(그래프 좌표). graph.gridSize 에 그대로 넣는다 — 보이는 칸과 붙는 칸이 다르면 자석이 고장 난 것처럼 보인다. */
const GRID_STEP = 20;

/** 그린 격자의 최소 간격(화면 px). 더 촘촘해지면 배수로 성기게 그린다. 배수라서 그려진 줄은 언제나 붙는 자리다. */
const GRID_MIN_PX = 8;

/** 점을 타일 왼쪽 위에서 이만큼 안쪽에 찍는다. 모서리에 찍으면 원의 4분의 1만 남는다. */
const DOT_INSET = 1.5;

/** 칸 한 줄의 높이. 상자를 얼마나 키울지 재는 데만 쓴다(CSS 와 맞춘다). */
const FIELD_LINE_H = 18;

/** 머리(이름 + 설명)가 차지하는 높이. 위와 같은 이유로 CSS 와 맞춘다. */
const HEAD_H = 42;

/** 표 상자의 기본 너비. 칸 이름과 자료형이 한 줄에 들어갈 만큼. */
const TABLE_W = 230;

/** 칸을 안 펼친 표 상자의 높이. 이름 한 줄만 들어가는 높이다. */
const PLAIN_H = 34;

/** 표 상자의 속. 글자를 그대로 끼우지 않는다 — 표·칸 이름에 < 나 & 가 있으면 라벨이 깨진다. 모양은 CSS(.pm-erd-*)가 맡는다. */
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

  // 칸이 없으면 머리띠도 없다 — 띠와 아래 선은 「여기서부터 칸 목록」 표시라, 목록이 없으면 빈자리로 보인다.
  const plain = rows.length === 0;

  return `<div class="pm-erd${plain ? ' pm-erd--plain' : ''}">`
    + `<div class="pm-erd__head"><div class="pm-erd__title">${esc(entity.name || entity.id)}</div>${desc}</div>`
    + (rows ? `<div class="pm-erd__body">${rows}</div>` : '')
    + '</div>';
}

/** 격자로 떨어뜨린다. 관계선이 없으면 배치가 할 일이 없지만 겹쳐 쌓인 것은 풀어야 한다. 키가 제각각이라 열 단위로 쌓는다. */
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

/* ── 도구상자 미리보기 — 도형을 실제로 그려 작은 그림으로 보여 준다. 처음 열 때 한 번만 한다 ── */

/** 끌어다 놓을 때 싣는 꼬리표. 브라우저 이름이 아니라 우리 것이라 다른 데서 끌어온 것과 섞이지 않는다. */
const DRAG_TYPE = 'text/pm-shape';

/** 미리보기 상자. 모두 같은 크기라야 도구상자가 깔끔하다. */
const PREVIEW_BOX = 40;

/* ── 붙여넣은 그림 — 원본 대신 줄인 그림을 담는다(회로 수신 한도와 저장 칸 크기 때문) ── */

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

        // 미리보기는 테두리와 면만 본다 — 표 상자의 HTML 라벨과 그림자가 끼면 지저분해진다.
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

/** 외래키에서 저절로 그어진 선의 모양. 사람이 끈 선과 갈라 보여야 한다 — 지워도 다음 불러오기에 되살아난다. */
const AUTO_EDGE_STYLE = {
  ...EDGE_STYLE,
  strokeColor: '#3f8f5f',
  fontColor: '#2f6b47',
  dashed: true,
};

/** 표 상자. HTML 라벨로 그린다 — 머리와 칸 목록이 한 도형에 들어가야 ERD 로 읽힌다. 모양은 CSS 가 맡는다(.pm-erd-*). */
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

/** 손으로 만든 도형의 모양. 표에서 온 것과 한눈에 갈라야 한다 — 이름을 고칠 수 있고 DB 가 덮어쓰지 않는다. */
const MANUAL_STYLE = {
  ...VERTEX_STYLE,
  fillColor: '#f0f7ff',
  strokeColor: '#5b7fa6',

  // 글자를 그대로 쓴다 — 더블클릭해 고치는 대상이라 HTML 을 넣으면 편집기에 태그가 보인다.
  // 긴 글자는 접히지 않는다(접기는 HTML 라벨에서만 된다). 줄바꿈이 있으면 그대로 그려진다.
  // visible 을 둔다 — width 로 두면 상자 높이를 넘는 줄이 잘린다. 넘쳐 보이는 쪽이 안 보이는 쪽보다 낫다.
  overflow: 'visible',
  verticalAlign: 'middle',
  align: 'center',
};

/** 붙여넣은 그림의 모양. 테두리와 바탕이 없다 — 그림이 내용이라 상자를 두르면 두 겹으로 보인다. 이름은 그림 아래에 붙인다. */
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

/** 그림을 줄여 data URL 로 만든다. PNG 를 먼저 재고 한도를 넘으면 흰 바탕을 깔아 JPEG 로 바꾼다. @returns 한도 안에 못 들어오면 null. */
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

/** DB 에서 사라진 표의 모양. 글자로만 알리면 눈에 안 띄므로 흐리게 그린다. 지우지는 않는다 — 사라졌다는 사실이 이 화면이 보려는 것이다. */
const GONE_STYLE = {
  ...VERTEX_STYLE,
  fillColor: '#fafafa',
  strokeColor: '#d0d0d0',
  fontColor: '#8c8c8c',
  dashed: true,
  shadow: false,
};


/* ── 우리가 더 그리는 모양 — maxgraph 기본에 없는 순서도 모양을 경로로 그린다 ── */

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

// 한 번만 등록한다 — 모듈은 한 번 실리고, 이후 만들어지는 그래프가 모두 이 이름을 안다.
for (const [name, shape] of Object.entries(CUSTOM_SHAPES)) {
  ShapeRegistry.add(name, shape);
}

/* ── stencil 도형 묶음 — mxGraph 의 135개를 저장소에 싣고 로컬에서 읽는다(CDN 아님, Apache 2.0). ── */

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

          // 묶음마다 제 비율이 있다 — 그 비율로 넣어야 처음부터 제 모양이다.
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

/** 도구상자에 놓는 도형들. maxgraph 기본 모양을 쓰고 이름만 고른다 — 여기 없는 모양이 필요하면 한 줄 더한다. */
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

/** 고른 선의 모양. 선은 만들기보다 고쳐 쓰는 일이 많다(점선·양쪽 화살표 등). */
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
 * 컨테이너에 그래프를 만든다. Blazor 가 돌려받은 객체로 load/save/zoomIn/zoomOut/fit/deleteSelection/destroy 를 부른다.
 * @param {HTMLElement} container 그림이 들어갈 자리
 * @param {object} [dotnet] 바뀔 때 알려 줄 .NET 객체 참조(NotifyChanged). 안 주면 알리지 않는다.
 * @param {HTMLElement} [minimapHost] 미니맵이 들어갈 자리(setMinimap). 안 주면 미니맵을 켤 수 없다.
 */
export async function create(container, dotnet, minimapHost) {
  // 도형 묶음을 먼저 등록한다 — 그림을 그린 뒤 등록하면 그사이 도형이 빈 네모로 남는다.
  await ensureStencils();

  InternalEvent.disableContextMenu(container);

  const graph = new Graph(container);
  graph.setPanning(true);
  graph.setConnectable(true);
  graph.setCellsResizable(true);
  graph.setAllowDanglingEdges(false);
  graph.setHtmlLabels(true);
  graph.setDropEnabled(false);

  // 선과 손으로 만든 도형만 고친다 — 표 도형의 이름·설명은 DB 가 정본이라 여기서 고치면 다음 불러오기에 되돌아간다.
  graph.setCellsEditable(true);
  graph.isCellEditable = (cell) => {
    if (cell?.isEdge?.()) { return true; }

    return Boolean(cellToEntity.get(cell)?.manual);
  };

  // 빈 자리를 왼쪽 단추로 끌면 화면이 움직인다(maxgraph 기본은 오른쪽 단추). 도형 위에서 끌면 도형을 옮긴다.
  // 고무줄 고르기는 그래서 Alt 를 누른 채 끈다.
  const panningHandler = graph.getPlugin('PanningHandler');

  if (panningHandler) {
    panningHandler.useLeftButtonForPanning = true;

    // 끌기의 시작과 끝을 알아 둔다 — 끄는 동안 미니맵은 네모만 옮기므로 시작 자리를 여기서 쥔다.
    panningHandler.addListener(InternalEvent.PAN_START, () => {
      panFrom = outline?.selectionBorder?.bounds
        ? { x: outline.selectionBorder.bounds.x, y: outline.selectionBorder.bounds.y }
        : null;
    });

    panningHandler.addListener(InternalEvent.PAN_END, () => {
      panFrom = null;
      queueFrame();
    });
  }

  graph.getStylesheet().putDefaultEdgeStyle({
    ...graph.getStylesheet().getDefaultEdgeStyle(),
    ...EDGE_STYLE,
  });

  new RubberBandHandler(graph);

  /** 셀 → 엔터티. 저장할 때 좌표를 되돌려 담기 위해 들고 있는다. */
  const cellToEntity = new Map();

  /** 외래키에서 그어진 선들. 저장에서 뺀다(머리말). */
  const autoEdges = new Set();

  /** 칸 목록 때문에 늘려 준 크기. 저장에서 되돌린다 — 그대로 저장하면 「컬럼 보기」를 끈 채 열었을 때 속이 빈 큰 상자만 남는다. 사람이 끌어 바꾼 크기는 새 값을 저장한다. */
  const autoSize = new Map();

  /** 그리는 중인가. 불러오는 동안의 변경은 「사람이 고친 것」이 아니다. */
  let loading = false;

  /** 바뀐 것을 화면에 알린다. 저장 안 하고 나가면 배치가 사라지므로 화면이 알아야 한다. 도형을 끌 때마다 보내지 않도록 한 번만 알린다. */
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
   * 전체가 보이게 맞추고 가운데로 둔다. graph.fit() 은 0.24 에서 플러그인(fit)으로 옮겨 가 옛 이름은 던진다.
   * 배율 상한은 인자가 아니라 플러그인 값(maxFitScale)이다 — fitCenter 는 margin 만 받아 다른 인자는 조용히 무시된다.
   */
  const fitPlugin = graph.getPlugin('fit');

  if (fitPlugin) {
    // 줄이는 데는 제한이 없고, 늘리는 쪽만 막는다 — 도형 두세 개짜리 그림을 8배로 키울 이유가 없다.
    fitPlugin.maxFitScale = 1.2;
  }

  function fitAll() {
    // 「맞춤」·정렬이 배율을 바꾸므로 미리보기를 먼저 접는다.
    commitZoom();
    fitPlugin?.fitCenter?.();
  }

  /* ── 한 프레임에 한 번만 그린다 — 끌기·휠이 손짓 하나에 수십 번 와서, 그리는 일만 프레임마다 한 번으로 묶는다. 모델을 고치는 일은 미루지 않는다. ── */

  /** 프레임을 잡아 두었나. 잡아 둔 것이 있으면 더 잡지 않는다. */
  let frameHandle = 0;


  /** 미니맵을 마지막으로 그린 때. */
  let outlinePaintedAt = 0;

  /** 끌기를 시작할 때의 미니맵 네모 자리. 끄는 중이 아니면 `null`. */
  let panFrom = null;

  function runFrame() {
    frameHandle = 0;

    paintZoomPreview();
    paintBackground();
    paintOutline();
  }

  /**
   * 미니맵을 다시 그린다. 끄는 중에는 뜸하게 그린다 — Outline.update 는 조건이 자주 참이 되어 미니맵을 통째로 다시 재고, 한 번이 9.5ms 다.
   * 끄는 동안의 네모는 초당 여덟 번이면 충분하고, 손을 떼면 한 번 더 그려 제자리에 맞춘다.
   */
  function paintOutline() {
    // 끄는 중에는 손대지 않는다 — 그때 네모를 쥐고 있는 것은 미니맵 자신이다.
    if (!outline || outline.active || outline.suspended) return;

    // 끄는 동안 바뀌는 것은 「지금 보는 자리」뿐이라 네모와 손잡이만 옮긴다(9.5ms → 0.1ms).
    // 네모는 화면을 panDx 픽셀 밀면 미니맵 배율을 곱한 만큼 반대로 간다.
    if (panFrom && (graph.panDx || graph.panDy)) {
      const border = outline.selectionBorder;
      const sizer = outline.sizer;

      if (!border?.bounds) return;

      const k = outline.outline.getView().scale / graph.getView().scale;

      border.bounds.x = panFrom.x - graph.panDx * k;
      border.bounds.y = panFrom.y - graph.panDy * k;
      border.redraw();

      if (sizer?.bounds) {
        sizer.bounds.x = border.bounds.x + border.bounds.width - sizer.bounds.width / 2;
        sizer.bounds.y = border.bounds.y + border.bounds.height - sizer.bounds.height / 2;

        if (sizer.node?.style.visibility !== 'hidden') sizer.redraw();
      }

      return;
    }

    outlinePaintedAt = performance.now();
    outline.update();
  }

  function queueFrame() {
    if (frameHandle) return;

    frameHandle = requestAnimationFrame(runFrame);
  }

  /* ── 배율 — 단추와 Shift+휠이 같은 길로 간다 ── */

  function clampScale(value) {
    return Math.min(MAX_SCALE, Math.max(MIN_SCALE, value));
  }

  // 배율은 가상으로 먼저 보여 주고 손이 멎으면 확정한다 — 셀을 다시 재는 확정이 한 번 39ms 라 휠 한 손짓마다 하면 버벅인다.
  // 미리보기 중에는 maxgraph 의 배율이 옛 값이라, 캔버스를 누르기 전·저장 전·읽기 전에 commitZoom() 으로 확정한다.

  /** 아직 확정 안 한 미리보기 변환. 화면 좌표 `p` → `p * k + t`. 없으면 `null`. */
  let zoomPreview = null;

  /** 손이 멎기를 기다리는 시계. */
  let zoomTimer = 0;

  /** 그림이 실린 SVG 묶음. maxgraph 의 `panGraph` 도 이 자리를 쓴다. */
  function canvasNode() {
    return graph.getView().getCanvas?.() ?? null;
  }

  function paintZoomPreview() {
    const node = canvasNode();

    if (!node) return;

    if (!zoomPreview) {
      // 끄는 중이면 그 변환은 maxgraph 의 것이다. 건드리지 않는다.
      if (!graph.panDx && !graph.panDy) node.removeAttribute('transform');
      return;
    }

    const { k, tx, ty } = zoomPreview;

    node.setAttribute('transform', `translate(${tx},${ty}) scale(${k})`);
  }

  /**
   * 가리킨 자리를 붙박아 두고 배율을 바꾼다 — 아직 가상이다. graph.zoomIn() 은 화면 가운데가 기준이라, 휠은 가리킨 자리를 지켜야 한다.
   */
  function queueZoom(factor, clientX, clientY) {
    const view = graph.getView();
    const box = container.getBoundingClientRect();
    const ax = clientX - box.left;
    const ay = clientY - box.top;
    const base = zoomPreview ?? { k: 1, tx: 0, ty: 0 };

    // 한계는 미리보기까지 합친 값으로 잰다 — 더 못 키우는 자리에서 미리보기만 계속 커지지 않게.
    const now = view.scale * base.k;
    const to = clampScale(now * factor);
    const f = to / now;

    if (f === 1) return;

    // 가리킨 자리를 축으로 한 번 더 키운다 — 앞의 변환에 이어 붙인다.
    zoomPreview = {
      k: base.k * f,
      tx: (base.tx - ax) * f + ax,
      ty: (base.ty - ay) * f + ay,
    };

    queueFrame();

    window.clearTimeout(zoomTimer);
    zoomTimer = window.setTimeout(commitZoom, ZOOM_COMMIT_MS);
  }

  /**
   * 미리보기를 진짜 배율로 확정한다. 확정 뒤 그림이 미리보기와 한 픽셀도 달라서는 안 되므로 이동값을 되푼다.
   * t₁ = t₀ + t / s₁ (s₁ = s₀·k).
   */
  function commitZoom() {
    window.clearTimeout(zoomTimer);
    zoomTimer = 0;

    if (!zoomPreview) return;

    const { k, tx, ty } = zoomPreview;

    zoomPreview = null;

    const view = graph.getView();
    const to = clampScale(view.scale * k);
    const t = view.translate;

    canvasNode()?.removeAttribute('transform');
    view.scaleAndTranslate(to, t.x + tx / to, t.y + ty / to);
  }

  /** 화면 가운데를 기준으로 확대·축소한다. 단추가 쓴다 — **곧바로 확정한다.** */
  function zoomCenter(factor) {
    const box = container.getBoundingClientRect();

    queueZoom(factor, box.left + box.width / 2, box.top + box.height / 2);
    commitZoom();
  }

  /**
   * Shift+휠로 확대·축소한다. 그냥 휠은 손대지 않는다 — 그림이 쪽 한가운데 있어 휠을 가로채면 쪽이 안 굴러간다.
   */
  const onWheel = (event) => {
    if (!event.shiftKey) return;

    event.preventDefault();

    // deltaX 도 본다 — Shift+휠을 브라우저가 가로 스크롤로 옮기면 세로 값이 0 으로 온다(리눅스·윈도 크롬).
    const delta = event.deltaY || event.deltaX;

    if (delta === 0) return;

    // 진짜 확대는 여기서 하지 않는다 — 가상으로 키워 두고 손이 멎으면 확정한다. 끄는 중이면 maxgraph 의 변환이라 비켜 준다.
    if (graph.panDx || graph.panDy) return;

    queueZoom(delta < 0 ? WHEEL_FACTOR : 1 / WHEEL_FACTOR, event.clientX, event.clientY);
  };

  // passive: false — 안 주면 preventDefault 가 무시되어 확대되면서 쪽도 같이 굴러간다.
  container.addEventListener('wheel', onWheel, { passive: false });

  // 누르기 전에 배율을 확정한다 — 미리보기 중에는 짚는 자리가 어긋난다.
  // 캡처 단계여야 maxgraph 가 처리하기 전에 끝난다.
  container.addEventListener('pointerdown', commitZoom, true);

  /* ── Ctrl+S — 문서에 건다. 화면이 저장을 받을 준비가 됐을 때만 가로챈다(saveShortcut). ── */

  let saveShortcut = false;

  const onDocumentKeyDown = (event) => {
    if (!saveShortcut) return;

    // **`code` 로 본다.** 한글 자판에서 `key` 는 `ㄴ` 으로 올 수 있다.
    const isS = event.code === 'KeyS' || String(event.key).toLowerCase() === 's';

    if (!isS || event.altKey || !(event.ctrlKey || event.metaKey)) return;

    event.preventDefault();

    // 이름을 고치던 중이면 먼저 확정한다 — 아니면 방금 친 글자를 뺀 채로 저장된다.
    if (graph.isEditing()) {
      graph.stopEditing(false);
    }

    dotnet?.invokeMethodAsync('NotifySave').catch(() => {
      // 회로가 끊겼다. 저장할 곳이 없을 뿐 그림은 그대로다.
    });
  };

  document.addEventListener('keydown', onDocumentKeyDown);

  /* ── 바탕(격자·점) — CSS 로 깐다. maxgraph 는 셀마다 화면 좌표를 찍으므로 SVG 격자는 끌 때 따라오지 않는다. 자석도 여기 딸려 있다. ── */

  /** 지금 바탕. `none` · `grid` · `dots`. */
  let background = 'none';

  function mod(value, m) {
    return ((value % m) + m) % m;
  }

  function paintBackground() {
    container.classList.toggle('pm-diagram--grid', background === 'grid');
    container.classList.toggle('pm-diagram--dots', background === 'dots');

    if (background === 'none') {
      container.style.backgroundSize = '';
      container.style.backgroundPosition = '';
      return;
    }

    const view = graph.getView();

    // 미리보기 중에는 바탕도 같이 가상으로 커져야 한다 — 안 그러면 격자만 제자리에 남아 도형이 격자 위를 미끄러진다.
    const k = zoomPreview?.k ?? 1;
    const ptx = zoomPreview?.tx ?? 0;
    const pty = zoomPreview?.ty ?? 0;
    const scale = view.scale * k;

    let step = GRID_STEP * scale;

    while (step < GRID_MIN_PX) {
      step *= 2;
    }

    // 그래프 좌표 0 이 화면의 어디인가. 끄는 중에는 panDx·panDy 를 얹는다 — 안 그러면 도형만 움직이고 바탕이 서 있다가 놓을 때 튀어 붙는다.
    const inset = background === 'dots' ? DOT_INSET : 0;
    const ox = view.translate.x * view.scale * k + ptx + (graph.panDx ?? 0) - inset;
    const oy = view.translate.y * view.scale * k + pty + (graph.panDy ?? 0) - inset;

    container.style.backgroundSize = `${step}px ${step}px`;
    container.style.backgroundPosition = `${mod(ox, step)}px ${mod(oy, step)}px`;
  }

  /**
   * 바탕을 고른다. 모르는 이름은 none 으로 받는다.
   * @returns {string} 실제로 적용된 이름.
   */
  function setBackground(kind) {
    background = kind === 'grid' || kind === 'dots' ? kind : 'none';

    graph.setGridSize(GRID_STEP);
    graph.setGridEnabled(background !== 'none');

    paintBackground();
    return background;
  }

  // 배율·이동이 바뀌면 바탕도 따라간다. PAN 은 끄는 중에 온다.
  const backgroundView = graph.getView();

  backgroundView.addListener(InternalEvent.SCALE, queueFrame);
  backgroundView.addListener(InternalEvent.TRANSLATE, queueFrame);
  backgroundView.addListener(InternalEvent.SCALE_AND_TRANSLATE, queueFrame);
  graph.addListener(InternalEvent.PAN, queueFrame);

  // 기본은 바탕 없음이다 — maxgraph 기본 격자(10px)는 보이지 않는 자석이라 「조금씩 어긋나게 놓인다」로 나타난다.
  setBackground('none');

  /**
   * 브라우저 좌표(clientX/Y)를 그래프 좌표로 옮긴다. 재는 때가 중요하다 — 알림이 뜨면 캔버스가 아래로 밀리므로 마우스가 있던 그때 재어 둔다.
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
   * 저장본의 엔터티를 어떤 모양으로 그릴지 정한다. 손으로 만든 도형은 kind 와 image 를 보고 그려야 저장 뒤 다시 열어도 네모로 돌아가지 않는다.
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
    // 관계선이 이 id 를 가리키므로 한 번 정하면 바꾸지 않는다 — 이름을 고쳐도 id 는 그대로다.
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

      // 도형 종류를 저장본에 남긴다 — 안 남기면 다시 열 때 모두 네모가 된다.
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

    // 고르기만 한다 — 만들자마자 이름 편집으로 들어가면 편집기가 포커스를 가져가며 페이지가 굴러간다. 이름은 더블클릭으로 고친다.
    graph.setSelectionCell(cell);

    return entity.id;
  }

  /**
   * 그림 한 장을 그 자리에 넣는다. 처음 크기는 IMAGE_PLACE_EDGE 로 맞추고 비율은 그대로 둔다 — 원본 크기로 넣으면 캔버스를 덮는다.
   */
  function insertImage(dataUrl, naturalW, naturalH, at) {
    const shrink = Math.min(1, IMAGE_PLACE_EDGE / Math.max(naturalW, naturalH));
    const w = Math.max(MIN_W, Math.round(naturalW * shrink));
    const h = Math.max(MIN_H, Math.round(naturalH * shrink));

    const entity = {
      id: manualId(),

      // 이름은 비워 둔다 — 기본 이름을 넣으면 그림 아래에 「그림」 글자가 따라다닌다.
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
   * 그림 덩어리를 줄여서 캔버스에 놓는다. 붙여넣기와 파일 끌어놓기가 함께 쓴다. 줄이는 동안 말이 없으면 안 된 줄 알고 또 붙인다.
   */
  async function placeImage(blob, clientX, clientY) {
    if (!blob) { return; }

    // 먼저 자리를 잡아 둔다 — 아래 한마디가 캔버스를 밀어내기 때문이다(graphPointAt).
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
   * 마지막으로 마우스가 있던 자리. 붙여넣기는 좌표를 들고 오지 않으므로(Ctrl+V 에는 마우스가 없다) 여기 놓는다. 한 번도 안 움직였으면 캔버스 가운데다.
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

  /* 클립보드의 그림 붙여넣기 — 문서에 건다(paste 는 포커스가 있는 곳으로만 가므로). 캔버스 안이나 위에 있을 때만 처리한다. */
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

  /* 도구상자에서 끌어다 놓기 — 끌기는 paintPreviews 가 건다. dragover 에서 기본 동작을 막아야 drop 이 온다. */
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

  // Delete 로 지운다 — 글자를 고치는 중에는 건드리지 않는다(그때 Delete 는 글자를 지우는 키다).
  const onKeyDown = (event) => {
    if (event.key !== 'Delete') { return; }
    if (graph.isEditing()) { return; }

    if (deleteSelection() > 0) {
      event.preventDefault();
    }
  };

  container.setAttribute('tabindex', '0');
  container.addEventListener('keydown', onKeyDown);

  /* ── 미니맵 — 같은 모델을 보는 Outline 을 한 벌 더 그린다. 자리는 캔버스 밖(형제 칸)에 둔다. 꺼 두는 것이 기본이다. ── */
  let outline = null;

  function setMinimap(on) {
    if (!minimapHost) { return false; }

    if (!on) {
      outline?.destroy();
      outline = null;

      // Outline.destroy() 는 자기가 만든 그래프만 거둔다 — 껍데기가 남으면 다음에 켤 때 한 벌 더 그려진다.
      minimapHost.replaceChildren();
      minimapHost.classList.remove('pm-minimap--on');
      return false;
    }

    if (!outline) {
      // 자리를 먼저 보인다 — Outline 은 만들어질 때 칸의 크기를 재는데 display:none 이면 0×0 이다.
      minimapHost.classList.add('pm-minimap--on');

      outline = new Outline(graph, minimapHost);

      // updateOnPan 을 켜지 않는다 — 끄는 동안 미니맵을 통째로 다시 그리면 끌기가 무거워진다. 네모는 프레임마다 한 번 갱신한다(runFrame).
      outline.updateOnPan = false;
    }

    outline.suspended = false;
    outline.update(true);
    queueFrame();
    return true;
  }

  /**
   * 지금 보고 있는 자리. 배율만 담으면 되돌렸을 때 엉뚱한 곳을 보므로 이동값도 담는다.
   */
  function viewState() {
    const view = graph.getView();

    return {
      scale: Number(view.scale.toFixed(4)),
      dx: Math.round(view.translate.x),
      dy: Math.round(view.translate.y),
      minimap: Boolean(outline),
      background,
    };
  }

  return {
    /** 모델을 캔버스에 그린다. 기존 도형은 모두 지운다. */
    load(model) {
      // 미리보기가 떠 있으면 옛 배율이 담긴다(위 `commitZoom` 머리말).
      commitZoom();
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

          // 표 상자는 HTML 로 그린다. 손으로 만든 도형은 글자 그대로다 — 더블클릭해 고치는 대상이다.
          const fields = entity.fields ?? [];

          // 그림은 이름이 비어 있는 것이 보통이다 — id 로 메우면 그림 아래에 m-3f2a9c11 같은 글자가 붙는다.
          const label = entity.manual
            ? (entity.image ? (entity.name ?? '') : (entity.name || entity.id))
            : erdLabel(entity, fields);

          // 칸이 많으면 상자가 커야 한다. 사람이 늘려 둔 크기가 더 크면 그것을 쓴다.
          const needed = HEAD_H + fields.length * FIELD_LINE_H + 10;

          // 칸이 없는 도형은 적힌 크기를 그대로 쓴다 — 바닥(DEFAULT)으로 밀면 작게 그린 도형이 전부 커진다.
          // 칸이 있는 상자는 줄이 늘면 커져야 해서 바닥이 뜻을 가진다.
          const sized = entity.w > 0 && entity.h > 0;
          const size = fields.length > 0
            ? [Math.max(entity.w || 0, TABLE_W), Math.max(entity.h || 0, needed)]
            : entity.manual
              ? (sized
                // 너무 작으면 집을 수가 없다. 끌거나 지우려면 과녁이 있어야 한다.
                ? [Math.max(entity.w, MIN_W), Math.max(entity.h, MIN_H)]
                : [DEFAULT_W, DEFAULT_H])
              // 표인데 칸을 안 펼쳤다 — 폭은 펼쳤을 때와 같게 두고 높이만 한 줄로 줄인다. 적혀 있던 높이는 autoSize 가 들고 있다가 저장 때 되돌린다.
              : [Math.max(entity.w || 0, TABLE_W), PLAIN_H];

          const cell = graph.insertVertex({
            parent: graph.getDefaultParent(),
            position: [x, y],
            size,
            style: styleOf(entity),
            value: label,
          });

          byId.set(entity.id, cell);
          cellToEntity.set(cell, entity);

          // 표 상자의 원래 크기를 기억한다 — 그대로 저장하면 「컬럼 보기」를 끈 채 저장하는 것만으로 사람이 맞춘 크기가 사라진다. 사람이 끌어 바꾼 경우만 저장 때 새 값을 담는다.
          if (!entity.manual) {
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

      // 보던 자리를 되돌린다 — 갱신 묶음이 닫힌 뒤에 한다. 저장본에 값이 없으면(옛 그림) 아무것도 건드리지 않는다.
      const saved = model?.view;

      if (saved) {
        if (saved.scale > 0) {
          graph.getView().scaleAndTranslate(
            clampScale(saved.scale), saved.dx ?? 0, saved.dy ?? 0);
        }

        setMinimap(Boolean(saved.minimap));

        // 바탕에는 자석이 딸려 있다 — 되돌리는 것은 무늬이면서 도형이 칸에 붙는지이기도 하다.
        setBackground(saved.background);
      }

      // 지금 상태를 돌려준다 — 화면이 이것으로 미니맵 단추의 불을 맞춘다.
      return viewState();
    },

    /** 캔버스의 현재 상태를 모델로 되돌린다. 좌표·크기만 갱신한다 — 이름·설명은 DB 메타가 정본이다. */
    save() {
      // 미리보기가 떠 있으면 **옛 배율이 저장된다.**
      commitZoom();

      const entities = [];
      const relations = [];
      const idOfCell = new Map();

      cellToEntity.forEach((entity, cell) => {
        // graph.getCellGeometry 가 아니라 셀이 들고 있는 geometry 를 쓴다 — 0.24 에는 그 이름이 없어 저장이 통째로 터진다.
        const geo = cell.getGeometry?.();

        // 칸 목록 때문에 늘려 준 크기는 원래 값으로 되돌려 저장한다 — 늘려 준 그 값 그대로면 사람이 안 건드린 것이다.
        // 원래 값이 없던 새 도형은 지금 값을 쓴다(undefined 를 저장하면 기본 크기로 떨어진다).
        const grown = autoSize.get(cell);
        const keptW = grown?.originalW > 0 && Math.round(geo?.width ?? 0) === Math.round(grown.w ?? 0);
        const keptH = grown?.originalH > 0 && Math.round(geo?.height ?? 0) === Math.round(grown.h ?? 0);

        entities.push({
          ...entity,

          // 칸 목록은 대상 DB 에서 읽은 값이라 저장하지 않는다.
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
          // 외래키 선은 저장하지 않는다 — 불러올 때마다 다시 그리므로, 저장하면 제약을 뗀 뒤에도 선이 남는다.
          if (autoEdges.has(edge)) return;

          const from = idOfCell.get(edge.source);
          const to = idOfCell.get(edge.target);
          if (!from || !to) return;
          relations.push({ from, to, label: String(edge.value ?? '') });
        });

      // 저장했으면 다음 변경부터 다시 알린다.
      notified = false;

      // 보던 자리와 미니맵도 그림과 함께 저장된다(viewState). 이것이 바뀌어도 저장 안 한 변경으로 치지 않는다 — 휠마다 경고가 뜨면 뜻을 알 수 없다.
      return { entities, relations, view: viewState() };
    },

    /** 고른 도형·선을 지운다. 지운 개수를 돌려준다. */
    deleteSelection() {
      return deleteSelection();
    },

    /** 도구상자에 보여 줄 도형 목록. 목록은 여기가 정본이다. 미리보기는 그 도형을 실제로 그린 SVG 라 캔버스와 어긋나지 않는다. */
    shapes() {
      return [
        ...Object.entries(SHAPES).map(([kind, v]) => ({ kind, label: v.label, group: v.group })),
        ...stencils.map(({ kind, label, group }) => ({ kind, label, group })),
      ];
    },

    /**
     * 도구상자의 빈 칸([data-shape])에 그 도형의 그림을 채운다. 그림은 브라우저 안에서 만들어 끼운다.
     * shapes() 가 SVG 를 회로로 보내면 수신 한도(32KB)를 넘겨 연결이 끊긴다.
     */
    paintPreviews() {
      const drawn = buildPreviews();
      let painted = 0;

      for (const host of document.querySelectorAll('[data-shape]')) {
        const kind = host.getAttribute('data-shape');
        const art = drawn[kind];

        // 이미 그린 칸은 건너뛴다 — 거르개를 칠 때마다 다시 그리면 도구상자가 깜빡인다.
        if (!art || host.dataset.painted === kind) { continue; }

        host.innerHTML =
          `<svg viewBox="0 0 ${art.w} ${art.h}" xmlns="http://www.w3.org/2000/svg" focusable="false">${art.svg}</svg>`;
        host.dataset.painted = kind;
        painted++;

        // 끌어다 놓기도 여기서 건다 — 칸을 만드는 곳과 끌기를 다는 곳이 갈라지면 새 칸에만 안 걸리는 날이 온다.
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
     * 고른 선의 모양을 바꾼다. 도형이 골라져 있으면 건드리지 않는다.
     * @returns 바꾼 선의 수. 0 이면 화면이 「선을 먼저 고르라」고 말한다.
     */
    styleEdge(kind) {
      const style = EDGE_STYLES[kind]?.style;
      if (!style) { return 0; }

      const edges = graph.getSelectionCells().filter((cell) => cell?.isEdge?.());
      if (edges.length === 0) { return 0; }

      graph.getDataModel().beginUpdate();
      try {
        // setCellStyles 는 한 번에 한 칸씩 받는다 — 객체를 통째로 넘기면 조용히 아무 일도 안 한다.
        for (const [key, value] of Object.entries(style)) {
          graph.setCellStyles(key, value, edges);
        }
      } finally {
        graph.getDataModel().endUpdate();
      }

      return edges.length;
    },

    /**
     * 겹친 것을 고루 펼친다. 배치와 달리 사람이 놓은 자리를 그대로 두고 겹친 것만 밀어낸다.
     * 한 번에 풀면 멀리 튕겨 나가므로 되풀이해 민다.
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

      // 한 쌍을 밀면 옆 것과 겹치므로 한 번에 다 풀리지 않는다 — 넉넉히 돌리되 겹침이 없으면 멈춘다.
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

            // 덜 밀어도 되는 쪽으로 민다 — 좌우가 조금 겹쳤는데 위아래로 밀면 그림이 세로로만 길어진다.
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
     * 그림을 보기 좋게 펼친다. 관계선이 많으면 계층, 덩어리로 보면 유기, 적으면 원, 관계가 없으면 격자다.
     * 관계선이 없으면 계층·유기는 할 일이 없으므로 격자로 떨어뜨린다.
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
     * 도형을 하나 만든다. 골라 두고 이름은 더블클릭으로 고친다 — 만들자마자 편집하면 페이지가 굴러간다.
     */
    addEntity(name, kind) {
      // 보고 있는 화면의 가운데에 둔다 — 좌표 0,0 에 두면 멀리 보던 사람에게는 화면 밖에서 생긴다.
      const box = container.getBoundingClientRect();

      return insertShape(kind, name, box.left + box.width / 2, box.top + box.height / 2);
    },

    zoomIn() {
      zoomCenter(1.2);
    },

    zoomOut() {
      zoomCenter(1 / 1.2);
    },

    fit() {
      fitAll();
    },

    /** Ctrl+S 를 가로챌지 정한다. 화면이 저장을 받을 준비가 됐을 때만 켠다. */
    saveShortcut(on) {
      saveShortcut = Boolean(on);
    },

    /** 바탕을 고른다. 돌려주는 것이 실제 상태다 — 격자·점을 고르면 도형이 그 칸에 붙는다. */
    setBackground(kind) {
      return setBackground(kind);
    },

    /** 미니맵을 켜고 끈다. 그릴 자리를 못 받았으면 켜 달라고 해도 꺼진 채로 돌아간다. 돌려주는 것이 실제 상태다. */
    minimap(on) {
      return setMinimap(Boolean(on));
    },

    /** 회로가 끊기거나 화면을 떠날 때 부른다. 안 부르면 DOM 과 리스너가 샌다. */
    destroy() {
      container.removeEventListener('keydown', onKeyDown);
      container.removeEventListener('dragover', onDragOver);
      container.removeEventListener('drop', onDrop);
      container.removeEventListener('pointermove', onPointerMove);
      container.removeEventListener('pointerenter', onEnter);
      container.removeEventListener('pointerleave', onLeave);

      // 문서에 건 것이라 반드시 뗀다 — 화면을 몇 번 드나들면 붙여넣기 한 번에 그림이 여러 장 생긴다.
      container.removeEventListener('wheel', onWheel);
      container.removeEventListener('pointerdown', commitZoom, true);
      window.clearTimeout(zoomTimer);

      // 잡아 둔 프레임을 놓는다. 남으면 이미 걷힌 그래프를 그리려 든다.
      if (frameHandle) {
        cancelAnimationFrame(frameHandle);
        frameHandle = 0;
      }

      document.removeEventListener('paste', onPaste);

      // 문서에 건 것이라 반드시 뗀다 — 남으면 화면을 떠난 뒤 Ctrl+S 가 없어진 회로를 부른다.
      document.removeEventListener('keydown', onDocumentKeyDown);

      // 본 그래프보다 먼저 거둔다 — 미니맵은 그 그래프의 모델과 뷰에 리스너를 걸어 두고 있다.
      setMinimap(false);
      cellToEntity.clear();
      graph.destroy?.();
    },
  };
}
