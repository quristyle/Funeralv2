/**
 * 마인드맵 JS interop. 글 형식은 mermaid 의 mindmap 을 본떴고(싣지는 않았다), 그리는 것은 maxgraph 다.
 * 자리를 저장하지 않는다 — 배치는 나무에서 나오고, 상자는 끌 수 없고, 선도 사람이 긋지 않는다.
 * 정본은 state.root 다. maxgraph 의 셀은 그때그때 그린 그림일 뿐이다.
 */
import {
  Graph,
  InternalEvent,
  Shape,
  ShapeRegistry,
} from '../lib/maxgraph/esm/index.js';

/* ── 크기와 사이 ─────────────────────────────────────────────── */

/** 가로 사이(부모 오른쪽 끝 ↔ 자식 왼쪽 끝). */
const H_GAP = 54;

/** 세로 사이(형제끼리). */
const V_GAP = 14;

/** 상자 속 여백. */
const PAD_X = 14;
const PAD_Y = 8;

/** 글자가 이보다 길면 줄을 접는다. 깊이마다 다르다 — 뿌리는 넓게 쓴다. */
const MAX_W = [260, 230, 200];

/** 상자의 최소 폭. 글자 한 자짜리 마디도 집을 수 있어야 한다. */
const MIN_W = 56;

/** 깊이별 글자 크기·굵기. 셋째 칸부터는 마지막 것을 되쓴다. */
const FONTS = [
  { size: 15, bold: true },
  { size: 14, bold: true },
  { size: 13, bold: false },
];

/** 가지 색. mermaid 의 section-0..11 을 본떠 포털 바탕에 맞춘 값이다 — 가지를 색으로 가르는 것이 요점이다. */
const SECTIONS = [
  { fill: '#e8f1ff', stroke: '#3f7ad6', text: '#1d3f73' },
  { fill: '#fdeef0', stroke: '#d64f63', text: '#7a2432' },
  { fill: '#eaf7ec', stroke: '#3f9553', text: '#1f5430' },
  { fill: '#fff5e3', stroke: '#cf8c25', text: '#6f4a10' },
  { fill: '#f1ecfd', stroke: '#7a5bd6', text: '#3f2c78' },
  { fill: '#e6f6f8', stroke: '#2e8f9e', text: '#154e57' },
  { fill: '#fdeefa', stroke: '#b84fae', text: '#67265f' },
  { fill: '#eef1f4', stroke: '#6b7a8c', text: '#33414f' },
];

/** 뿌리. 가지 색을 안 쓴다 — 어느 가지에도 속하지 않는 자리다. */
const ROOT_COLORS = { fill: '#3f5871', stroke: '#2c3e52', text: '#ffffff' };

/* ── mermaid 의 모양 여섯 ────────────────────────────────────── */

/** 경로 하나로 끝나는 모양을 짧게 만든다(`diagram-viewer.js` 와 같은 꼴). */
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

/** mermaid 의 bang(터지는 모양). 바깥 반지름을 상자에 맞추고 안쪽을 파내어 글자 자리를 남긴다. */
const BANG_SPIKES = 14;

const MIND_SHAPES = {
  pmMindBang: pathShape((c, x, y, w, h) => {
    const cx = x + w / 2;
    const cy = y + h / 2;
    const rx = w / 2;
    const ry = h / 2;

    for (let i = 0; i < BANG_SPIKES * 2; i++) {
      const factor = i % 2 === 0 ? 1 : 0.84;
      const angle = (Math.PI * i) / BANG_SPIKES - Math.PI / 2;
      const px = cx + Math.cos(angle) * rx * factor;
      const py = cy + Math.sin(angle) * ry * factor;

      if (i === 0) c.moveTo(px, py);
      else c.lineTo(px, py);
    }
  }),
};

// 등록은 한 벌뿐이라 diagram-viewer.js 의 이름과 겹치지 않게 pmMind 를 앞에 붙인다.
for (const [name, shape] of Object.entries(MIND_SHAPES)) {
  ShapeRegistry.add(name, shape);
}

/** 모양 이름 → maxgraph 스타일. 이름은 mermaid 가 정한 것이라 바꾸지 않는다 — 저장본과 글 형식이 같은 글자를 쓴다. */
const SHAPES = {
  default: { label: '기본', style: { rounded: true, arcSize: 10 } },
  square: { label: '네모', style: { rounded: false } },
  rounded: { label: '둥근', style: { rounded: true, arcSize: 40 } },
  circle: { label: '원', style: { shape: 'ellipse' }, padX: 22, padY: 14 },
  bang: { label: '폭발', style: { shape: 'pmMindBang' }, padX: 24, padY: 16 },
  cloud: { label: '구름', style: { shape: 'cloud' }, padX: 26, padY: 18 },
  hexagon: { label: '육각', style: { shape: 'hexagon' }, padX: 22, padY: 6 },
};

/* ── 글자 재기 ───────────────────────────────────────────────── */

/** 글자 폭을 잰다. 어림값은 우리말에서 20% 넘게 어긋나므로 실제 글꼴로 잰다. */
let measureCanvas = null;

function measureCtx(font) {
  measureCanvas ??= document.createElement('canvas');
  const ctx = measureCanvas.getContext('2d');
  ctx.font = font;
  return ctx;
}

/** 상자 폭에 맞게 줄을 접는다. 우리말은 띄어쓰기가 드무니 글자 단위로도 끊는다. */
function wrap(text, font, maxWidth) {
  const ctx = measureCtx(font);
  const lines = [];

  for (const paragraph of String(text ?? '').split('\n')) {
    if (paragraph.length === 0) {
      lines.push('');
      continue;
    }

    let line = '';

    for (const ch of paragraph) {
      if (line.length > 0 && ctx.measureText(line + ch).width > maxWidth) {
        // 낱말 가운데를 자르지 않아도 되면 마지막 띄어쓰기에서 끊는다.
        const space = line.lastIndexOf(' ');

        if (space > 0 && ctx.measureText(`${line.slice(space + 1)}${ch}`).width <= maxWidth) {
          lines.push(line.slice(0, space));
          line = `${line.slice(space + 1)}${ch}`;
        }
        else {
          lines.push(line);
          line = ch;
        }
      }
      else {
        line += ch;
      }
    }

    lines.push(line);
  }

  return lines;
}

/* ── 나무 다루기 ─────────────────────────────────────────────── */

/** 저장본에서 온 마디를 우리 모양으로 맞춘다. 빠진 칸을 채운다. */
function adopt(raw, seq) {
  const node = {
    id: raw?.id || `n${seq.next++}`,
    text: typeof raw?.text === 'string' ? raw.text : '',
    shape: SHAPES[raw?.shape] ? raw.shape : 'default',
    icon: raw?.icon ?? null,
    className: raw?.class ?? raw?.className ?? null,
    collapsed: Boolean(raw?.collapsed),
    children: [],
  };

  for (const child of raw?.children ?? []) {
    node.children.push(adopt(child, seq));
  }

  return node;
}

/** 나무를 저장본 모양으로 되돌린다. 좌표는 담지 않는다. */
function shed(node) {
  return {
    id: node.id,
    text: node.text,
    shape: node.shape,
    icon: node.icon || null,
    class: node.className || null,
    collapsed: node.collapsed,
    children: node.children.map(shed),
  };
}

/** 나무를 훑으며 부모를 함께 넘긴다. */
function walk(node, visit, parent = null, depth = 0, section = -1) {
  visit(node, parent, depth, section);

  node.children.forEach((child, index) => {
    walk(child, visit, node, depth + 1, depth === 0 ? index : section);
  });
}

function findNode(root, id) {
  let found = null;
  walk(root, (node) => { if (node.id === id) found = node; });
  return found;
}

function findParent(root, id) {
  let found = null;
  walk(root, (node, parent) => { if (node.id === id) found = parent; });
  return found;
}

/** 접힌 가지는 세지 않는다 — 배치가 보고 있는 것만 다루기 때문이다. */
function visibleLeaves(node) {
  if (node.collapsed || node.children.length === 0) {
    return 1;
  }

  return node.children.reduce((sum, child) => sum + visibleLeaves(child), 0);
}

/* ── 배치 ────────────────────────────────────────────────────── */

/**
 * 마인드맵 배치. 뿌리를 가운데 두고 가지를 좌우로 나눈다. 나누는 자리는 잎의 수로 정한다 — 가지 하나가 굵으면 그것으로 한쪽이 차야 좌우 높이가 맞는다.
 * 순서는 지킨다(앞쪽 절반 오른쪽, 뒤쪽 절반 왼쪽). 큰 것부터 번갈아 놓으면 적은 순서가 흐트러진다.
 */
function layout(root, fontFamily) {
  // ① 마디마다 상자 크기를 정한다.
  walk(root, (node, parent, depth) => {
    const font = FONTS[Math.min(depth, FONTS.length - 1)];
    const css = `${font.bold ? '600 ' : ''}${font.size}px ${fontFamily}`;
    const shape = SHAPES[node.shape] ?? SHAPES.default;

    const padX = shape.padX ?? PAD_X;
    const padY = shape.padY ?? PAD_Y;
    const maxWidth = (MAX_W[Math.min(depth, MAX_W.length - 1)]) - padX * 2;

    const label = labelOf(node);
    const lines = wrap(label, css, maxWidth);
    const ctx = measureCtx(css);
    const textWidth = Math.max(...lines.map((line) => ctx.measureText(line).width));

    node.lines = lines;
    node.font = font;

    // 지난 배치의 좌표를 지운다. 접힌 가지는 자리를 안 잡으므로 낡은 값이 남으면 아래 끌어오기가 그것까지 센다.
    node.x = undefined;
    node.y = undefined;
    node.w = Math.max(MIN_W, Math.ceil(textWidth) + padX * 2);
    node.h = Math.max(28, Math.ceil(lines.length * font.size * 1.45) + padY * 2);
  });

  // ② 가지마다 차지할 높이. 자식들을 세로로 쌓은 높이와 자기 높이 중 큰 쪽이다.
  const blocks = new Map();

  function block(node) {
    if (blocks.has(node)) {
      return blocks.get(node);
    }

    let height = node.h;

    if (!node.collapsed && node.children.length > 0) {
      const stacked = node.children.reduce(
        (sum, child, i) => sum + block(child) + (i > 0 ? V_GAP : 0), 0);

      height = Math.max(height, stacked);
    }

    blocks.set(node, height);
    return height;
  }

  // ③ 한쪽을 내려 놓는다. dir 은 +1(오른쪽) · -1(왼쪽). x 는 부모 쪽 가장자리다.
  function place(node, x, centerY, dir) {
    node.x = dir > 0 ? x : x - node.w;
    node.y = centerY - node.h / 2;
    node.dir = dir;

    if (node.collapsed || node.children.length === 0) {
      return;
    }

    const total = node.children.reduce(
      (sum, child, i) => sum + block(child) + (i > 0 ? V_GAP : 0), 0);

    let top = centerY - total / 2;
    const nextX = dir > 0 ? node.x + node.w + H_GAP : node.x - H_GAP;

    for (const child of node.children) {
      const height = block(child);
      place(child, nextX, top + height / 2, dir);
      top += height + V_GAP;
    }
  }

  root.x = -root.w / 2;
  root.y = -root.h / 2;
  root.dir = 0;

  const kids = root.collapsed ? [] : root.children;

  // 앞에서부터 잎을 세어 절반을 넘는 자리에서 가른다.
  const totalLeaves = kids.reduce((sum, kid) => sum + visibleLeaves(kid), 0);
  let seen = 0;
  let cut = kids.length;

  for (let i = 0; i < kids.length; i++) {
    seen += visibleLeaves(kids[i]);

    if (seen * 2 >= totalLeaves) {
      cut = i + 1;
      break;
    }
  }

  const sides = [
    { kids: kids.slice(0, cut), dir: 1, x: root.x + root.w + H_GAP },
    { kids: kids.slice(cut), dir: -1, x: root.x - H_GAP },
  ];

  for (const side of sides) {
    const total = side.kids.reduce(
      (sum, kid, i) => sum + block(kid) + (i > 0 ? V_GAP : 0), 0);

    let top = -total / 2;

    for (const kid of side.kids) {
      const height = block(kid);
      place(kid, side.x, top + height / 2, side.dir);
      top += height + V_GAP;
    }
  }

  // ④ 왼쪽 가지 때문에 좌표가 음수다. 화면 왼쪽 위로 끌어온다 — 음수 좌표는 맞춤이 어긋나고 스크롤이 이상해진다.
  let minX = Infinity;
  let minY = Infinity;

  walk(root, (node) => {
    if (node.x === undefined) return;
    minX = Math.min(minX, node.x);
    minY = Math.min(minY, node.y);
  });

  const dx = 20 - minX;
  const dy = 20 - minY;

  walk(root, (node) => {
    if (node.x === undefined) return;
    node.x = Math.round(node.x + dx);
    node.y = Math.round(node.y + dy);
  });
}

/** 상자 글자. 접힌 마디에는 감춘 수를 붙인다 — 테두리(점선)만으로는 가지가 원래 없는 마디와 구별되지 않는다. */
function labelOf(node) {
  const text = node.text || '(이름 없음)';

  if (node.collapsed && node.children.length > 0) {
    return `${text}  ⊕${node.children.length}`;
  }

  return text;
}

/* ── 그래프 ──────────────────────────────────────────────────── */

/**
 * 컨테이너에 마인드맵을 만든다.
 *
 * @param {HTMLElement} container 그림이 들어갈 자리
 * @param {object} [dotnet] 바뀔 때 알려 줄 .NET 객체 참조(`NotifyChanged`).
 */
export function create(container, dotnet) {
  InternalEvent.disableContextMenu(container);

  const graph = new Graph(container);

  // 자리는 우리가 정한다 — 끌 수 있게 열면 옮긴 자리가 다음 편집에서 사라져 「가끔 되돌아가는 그림」이 된다.
  graph.setCellsMovable(false);
  graph.setCellsResizable(false);
  graph.setConnectable(false);
  graph.setDropEnabled(false);
  graph.setPanning(true);

  // 글자는 SVG 로 그린다(HTML 라벨 끄기). HTML 라벨의 foreignObject 는 셀보다 크게 잡혀 고르기와 화면 끌기를 망친다.
  // 줄 접기는 우리가 하므로(wrap) 끌 수 있다.
  graph.setHtmlLabels(false);

  // 글자는 고칠 수 있다 — 마인드맵에서 그것이 본업이다.
  graph.setCellsEditable(true);
  graph.isCellEditable = (cell) => Boolean(cell?.isVertex?.());

  // Enter 로 글자를 확정한다(maxgraph 기본은 F2 확정·Enter 줄바꿈). 마인드맵에서는 적고 다음으로 가는 것이 잦은 동작이다.
  // 줄바꿈은 Shift+Enter 다(위 isStopEditingEvent 참고).
  graph.setEnterStopsCellEditing(true);

  // 빈 자리를 끌면 그림이 움직인다 — 상자를 못 끄니 왼쪽 단추를 이것에 쓴다.
  const panning = graph.getPlugin('PanningHandler');

  if (panning) {
    panning.useLeftButtonForPanning = true;
  }

  const fontFamily = getComputedStyle(container).fontFamily || 'sans-serif';

  /** 나무. **여기가 정본이다** — 셀은 이것을 보고 그린 그림일 뿐이다. */
  const state = {
    root: adopt({ text: '중심 주제', shape: 'circle' }, { next: 1 }),
    seq: { next: 2 },
    selected: null,
  };

  /** 셀 → 마디, 마디 id → 셀. 고른 것을 오가는 데 쓴다. */
  const nodeOfCell = new Map();
  const cellOfId = new Map();

  /** 그리는 중인가. 그때 나는 변경은 「사람이 고친 것」이 아니다. */
  let drawing = false;

  function notifyChanged() {
    if (drawing || !dotnet) return;

    dotnet.invokeMethodAsync('NotifyChanged').catch(() => {
      // 회로가 끊겼다. 알릴 곳이 없을 뿐 그림은 그대로 쓸 수 있다.
    });
  }

  /**
   * 전체가 보이게 맞춘다. graph.fit() 은 0.24 에서 플러그인(fit)으로 옮겨 가 옛 이름은 던진다.
   * 배율 상한은 인자가 아니라 플러그인 값(maxFitScale)이다 — fitCenter 는 margin 만 받아 다른 인자는 조용히 무시된다.
   */
  const fitPlugin = graph.getPlugin('fit');

  if (fitPlugin) {
    // 줄이는 데는 제한이 없고, 늘리는 쪽만 막는다 — 마디 둘짜리 그림을 8배로 키울 이유가 없다.
    fitPlugin.maxFitScale = 1.2;
  }

  function fitAll() {
    fitPlugin?.fitCenter?.();
  }

  /** 마디의 스타일. 깊이와 가지 색이 정한다. */
  function styleOf(node, depth, section) {
    const shape = SHAPES[node.shape] ?? SHAPES.default;
    const colors = depth === 0
      ? ROOT_COLORS
      : SECTIONS[((section < 0 ? 0 : section) % SECTIONS.length + SECTIONS.length) % SECTIONS.length];

    // 첫 가지는 색을 채우고 그 아래는 테두리만 같은 색이다 — 다 채우면 깊은 마인드맵이 색 덩어리가 된다.
    const filled = depth <= 1;

    return {
      ...shape.style,
      fillColor: depth === 0 ? colors.fill : (filled ? colors.fill : '#ffffff'),
      strokeColor: colors.stroke,
      strokeWidth: depth === 0 ? 2 : 1.5,
      fontColor: colors.text,
      fontSize: node.font.size,
      fontStyle: node.font.bold ? 1 : 0,
      fontFamily,
      verticalAlign: 'middle',
      align: 'center',

      // whiteSpace 를 주지 않는다 — 줄은 이미 wrap 이 상자 폭에 맞춰 끊었고, 여기서 또 접으면 잰 높이와 그려진 줄 수가 어긋난다.
      shadow: depth === 0,

      // 접은 가지는 점선으로 알린다. 글자의 `⊕n` 과 짝이다.
      dashed: node.collapsed && node.children.length > 0,
    };
  }

  /** 부모와 자식을 잇는 선. 뻗는 쪽으로 나가서 반대쪽으로 들어간다. */
  function edgeStyle(depth, section, dir) {
    const colors = SECTIONS[
      ((section < 0 ? 0 : section) % SECTIONS.length + SECTIONS.length) % SECTIONS.length];

    return {
      strokeColor: colors.stroke,
      strokeWidth: depth <= 1 ? 2 : 1.4,
      endArrow: 'none',
      startArrow: 'none',
      curved: true,
      rounded: true,

      // 나가고 들어오는 자리를 못 박는다 — 안 박으면 위아래로 늘어선 형제 사이에서 선이 상자 위를 지난다.
      exitX: dir > 0 ? 1 : 0,
      exitY: 0.5,
      exitDx: 0,
      exitDy: 0,
      entryX: dir > 0 ? 0 : 1,
      entryY: 0.5,
      entryDx: 0,
      entryDy: 0,
    };
  }

  /**
   * 나무를 보고 그래프를 새로 짓는다. 고쳐 그리면 마디 하나를 지울 때 아래가 옮겨 앉아 따져야 하므로, 다시 짓는 쪽이 짧다.
   */
  function render(keepView = true) {
    drawing = true;

    layout(state.root, fontFamily);

    nodeOfCell.clear();
    cellOfId.clear();

    graph.getDataModel().beginUpdate();

    try {
      graph.removeCells(graph.getChildCells(graph.getDefaultParent(), true, true));

      const parentCell = graph.getDefaultParent();

      walk(state.root, (node, parent, depth, section) => {
        // 접힌 가지의 자식은 그리지 않는다 — 자리도 안 잡혀 있다.
        // 부모가 그려졌는지만으로는 모자라다: 접힌 마디 자신은 그려져서 그 아래가 왼쪽 위에 겹친다.
        if (parent && (parent.collapsed || !cellOfId.has(parent.id))) {
          return;
        }

        const cell = graph.insertVertex({
          parent: parentCell,
          position: [node.x, node.y],
          size: [node.w, node.h],
          style: styleOf(node, depth, section),
          value: node.lines.join('\n'),
        });

        nodeOfCell.set(cell, node);
        cellOfId.set(node.id, cell);

        if (parent) {
          graph.insertEdge({
            parent: parentCell,
            source: cellOfId.get(parent.id),
            target: cell,
            value: '',
            style: edgeStyle(depth, section, node.dir),
          });
        }
      });
    }
    finally {
      graph.getDataModel().endUpdate();
      drawing = false;
    }

    // 고른 것을 되찾는다 — 안 그러면 하나 더할 때마다 선택이 풀려 자식을 이으려면 매번 다시 집어야 한다.
    const cell = state.selected ? cellOfId.get(state.selected) : null;

    if (cell) {
      graph.setSelectionCell(cell);
    }
    else {
      graph.clearSelection();
      state.selected = null;
    }

    // 보던 자리는 지켜진다 — 배율·이동은 뷰의 값이라 셀을 갈아 끼워도 그대로다. 첫 그림에서만 맞춘다.
    if (!keepView) {
      fitAll();
    }
  }

  /** 고른 마디. 없으면 `null`. */
  function selected() {
    return state.selected ? findNode(state.root, state.selected) : null;
  }

  /** 마디를 고르고 그 자리로 화면을 맞춘다. */
  function select(id) {
    state.selected = id;
    const cell = cellOfId.get(id);

    if (cell) {
      graph.setSelectionCell(cell);
    }
  }

  function newNode(text) {
    return adopt({ text: text ?? '', shape: 'default' }, state.seq);
  }

  // 고른 것이 바뀌면 들고 있는다 — 다시 그릴 때 되찾으려는 것이다.
  graph.getSelectionModel().addListener(InternalEvent.CHANGE, () => {
    if (drawing) return;

    const cell = graph.getSelectionCell();
    state.selected = cell ? (nodeOfCell.get(cell)?.id ?? null) : null;
  });

  /** 글자를 고치면 줄 접은 것이 아니라 원래 글자를 편집기에 넣는다 — 그대로 고치면 줄바꿈과 ⊕n 꼬리표가 글자에 눌러 붙는다. */
  graph.getEditingValue = (cell) => nodeOfCell.get(cell)?.text ?? '';

  graph.addListener(InternalEvent.LABEL_CHANGED, (_sender, event) => {
    const cell = event.getProperty('cell');
    const node = nodeOfCell.get(cell);

    if (!node) return;

    node.text = String(event.getProperty('value') ?? '').trim();

    // 이 이벤트 안에서 다시 그리지 않는다 — 갱신 묶음이 안 닫혔고, 셀을 지우면 편집기가 없는 셀을 붙들고 던진다.
    queueMicrotask(() => {
      render();
      notifyChanged();
      focusCanvas();
    });
  });

  /* ── 키 ───────────────────────────────────────────────────────── */

  /**
   * 캔버스에 초점을 돌려준다. maxgraph 가 마우스를 처리하며 preventDefault 를 부르면 브라우저가 초점을 옮기지 않아, 그대로 두면 Tab·Enter 가 우리 처리기에 닿지 않는다.
   * 글자를 고치는 중에는 가져오지 않는다 — 그때 초점은 편집기의 것이다.
   */
  function focusCanvas() {
    if (graph.isEditing()) return;
    if (document.activeElement === container) return;

    // preventScroll — 캔버스가 화면 아래에 있으면 초점만 줘도 창이 굴러가므로 막는다.
    container.focus({ preventScroll: true });
  }

  const onKeyDown = (event) => {
    // 글자를 고치는 중에는 편집기의 키다(Enter·F2·Escape 는 편집기가 전파를 끊는다).
    // Tab 만은 편집기가 모른다 — 그대로 두면 초점이 캔버스 밖으로 나가 편집이 굳고 단축키가 죽는다. 그래서 확정하고 자식을 더한다.
    if (graph.isEditing()) {
      if (event.key !== 'Tab') return;

      event.preventDefault();

      // 확정이 먼저다. 그 뒤에 바로 마디를 더하면 다시 그리기가 새 마디의 편집기를 걷어 가므로 한 칸 뒤로 미룬다.
      graph.stopEditing(false);

      // Shift+Tab 은 확정만 하고 마디는 더하지 않는다.
      if (!event.shiftKey) {
        queueMicrotask(() => addChild());
      }

      return;
    }

    const node = selected();
    const parent = node ? findParent(state.root, node.id) : null;

    switch (event.key) {
      case 'Tab':
        // 고른 것이 없어도 받는다 — 뿌리 아래에 붙는다. 새 마인드맵에서 Tab 이 가장 흔한 첫 동작이다.
        event.preventDefault();
        addChild();
        break;

      case 'Enter':
        // 뿌리를 골랐거나 아무것도 안 골랐으면 자식이 된다(`addSibling` 안).
        event.preventDefault();
        addSibling();
        break;

      case 'Delete':
        if (!parent) return;   // 뿌리는 못 지운다
        event.preventDefault();
        remove();
        break;

      case 'F2':
        if (!node) return;
        event.preventDefault();
        rename();
        break;

      case ' ':
        if (!node || node.children.length === 0) return;
        event.preventDefault();
        toggleCollapse();
        break;

      default:
        break;
    }
  };

  container.setAttribute('tabindex', '0');
  container.addEventListener('keydown', onKeyDown);

  // 캔버스 누름은 pointerdown 으로 잡는다 — maxgraph 가 preventDefault 해서 mousedown 은 오지 않는다.
  // 캡처 단계로 걸어야 maxgraph 의 전파 차단 전에 받는다.
  container.addEventListener('pointerdown', focusCanvas, true);

  /**
   * 글자 고치기가 끝나면 초점을 캔버스로 돌린다 — 편집기가 걷히며 초점을 body 로 떨어뜨리기 때문이다.
   */
  graph.addListener(InternalEvent.EDITING_STOPPED, () => {
    // 편집기가 걷히고 난 뒤에 가져와야 한다 — 이 이벤트는 그 정리보다 먼저 온다.
    queueMicrotask(focusCanvas);
  });

  /** 만든 마디를 고르고 바로 이름 고치기에 들어간다 — 다시 찾아 더블클릭하게 하면 손이 한 번 더 간다. */
  function focusNew(node) {
    render();
    select(node.id);

    const cell = cellOfId.get(node.id);

    if (cell) {
      graph.startEditingAtCell(cell);
    }

    notifyChanged();
  }

  function addChild() {
    const node = selected() ?? state.root;
    const child = newNode('');

    // 접어 둔 가지에 자식을 넣으면 그것이 어디로 갔는지 안 보인다.
    node.collapsed = false;
    node.children.push(child);

    focusNew(child);
    return child.id;
  }

  function addSibling() {
    const node = selected();

    // 고른 것이 없거나 뿌리면 형제를 만들 수 없다 — 뿌리는 하나이므로 자식을 만든다.
    const parent = node ? findParent(state.root, node.id) : null;

    if (!parent) {
      return addChild();
    }

    const sibling = newNode('');
    parent.children.splice(parent.children.indexOf(node) + 1, 0, sibling);

    focusNew(sibling);
    return sibling.id;
  }

  /** 고른 마디와 그 아래를 지운다. **뿌리는 못 지운다.** */
  function remove() {
    const node = selected();
    if (!node) return 0;

    const parent = findParent(state.root, node.id);
    if (!parent) return 0;

    const count = node.children.length + 1;
    const index = parent.children.indexOf(node);

    parent.children.splice(index, 1);

    // 지운 자리 가까이로 옮긴다 — 선택이 풀리면 이어서 지울 수 없다.
    state.selected = (parent.children[index] ?? parent.children[index - 1] ?? parent).id;

    render();
    notifyChanged();

    // 단추로 지웠으면 초점이 그 단추에 있으므로 캔버스로 돌려준다.
    focusCanvas();

    return count;
  }

  function rename() {
    const node = selected();
    if (!node) return false;

    const cell = cellOfId.get(node.id);
    if (!cell) return false;

    graph.startEditingAtCell(cell);
    return true;
  }

  function toggleCollapse() {
    const node = selected();
    if (!node || node.children.length === 0) return false;

    node.collapsed = !node.collapsed;

    render();
    notifyChanged();
    focusCanvas();

    return true;
  }

  return {
    /** 저장본을 그린다. **보이는 만큼 화면에 맞춘다**(첫 그림이라 보던 자리가 없다). */
    load(model) {
      state.seq = { next: 1 };
      state.selected = null;
      state.root = adopt(model?.root ?? { text: '중심 주제', shape: 'circle' }, state.seq);

      // 저장본이 들고 온 이름과 새로 지을 이름이 겹치지 않게 번호를 밀어 둔다.
      walk(state.root, (node) => {
        const n = /^n(\d+)$/.exec(node.id);

        if (n) {
          state.seq.next = Math.max(state.seq.next, Number(n[1]) + 1);
        }
      });

      render(false);
    },

    /** 지금 나무를 저장본 모양으로 돌려준다. */
    save() {
      return { root: shed(state.root) };
    },

    /** 도구에 놓을 모양 목록. **여기가 정본이다** — mermaid 의 여섯이다. */
    shapes() {
      return Object.entries(SHAPES).map(([kind, v]) => ({ kind, label: v.label }));
    },

    /**
     * 고른 마디의 지금 상태. 화면이 단추를 켜고 끄는 데 쓴다.
     * 아무것도 안 골랐으면 `null`.
     */
    selection() {
      const node = selected();
      if (!node) return null;

      return {
        id: node.id,
        text: node.text,
        shape: node.shape,
        isRoot: findParent(state.root, node.id) === null,
        childCount: node.children.length,
        collapsed: node.collapsed,
      };
    },

    addChild,
    addSibling,
    rename,
    toggleCollapse,

    /** @returns 지운 마디 수. 0 이면 화면이 「먼저 고르라」고 말한다. */
    remove,

    /** 고른 마디의 모양을 바꾼다. @returns 바꿨으면 참. */
    setShape(kind) {
      const node = selected();
      if (!node || !SHAPES[kind]) return false;

      node.shape = kind;

      render();
      notifyChanged();
      focusCanvas();

      return true;
    },

    /** 접은 가지를 모두 편다. @returns 편 가지 수. */
    expandAll() {
      let count = 0;

      walk(state.root, (node) => {
        if (node.collapsed) { node.collapsed = false; count++; }
      });

      if (count > 0) { render(); notifyChanged(); }
      focusCanvas();

      return count;
    },

    /** 첫 가지만 남기고 접는다. 뿌리는 접지 않는다 — 접으면 상자 하나가 되어 여는 길이 안 보인다. */
    collapseAll() {
      let count = 0;

      walk(state.root, (node, _parent, depth) => {
        if (depth >= 1 && node.children.length > 0 && !node.collapsed) {
          node.collapsed = true;
          count++;
        }
      });

      if (count > 0) { render(); notifyChanged(); }
      focusCanvas();

      return count;
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
      container.removeEventListener('pointerdown', focusCanvas, true);
      nodeOfCell.clear();
      cellOfId.clear();
      graph.destroy?.();
    },
  };
}
