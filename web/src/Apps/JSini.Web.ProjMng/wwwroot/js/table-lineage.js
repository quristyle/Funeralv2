// 테이블 계통도 캔버스 — TableLineageView 가 부른다.
//
// 도면 모양은 옛 WBS 도구(#/tables2)의 Vue Flow JSON 그대로다
// ({ lanes, flows, nodes:[{id,position,data}], edges:[{id,source,target,sourceHandle,targetHandle,label,data}] }).
// 번역하지 않는 까닭: 옮겨 온 도면(M20 은 칸 543 · 선 1995)을 한 글자도 안 바꾸고 열어야
// 하고, 서버는 이 글을 통째로 담아 두기만 한다.
//
// 끌기·확대·잇기·경유점은 전부 여기서 끝낸다. 회로로 넘기면 마우스가 움직일 때마다 왕복한다.
// 서버로 올라가는 것은 「골랐다」·「바뀌었다」·「레인을 눌렀다」 셋뿐이다.
//
// 업무항목을 고르면 그 항목의 칸과 그 칸끼리의 선만 DOM 에 만든다(scope). 흐리게 둔 채
// 전부 그리면 M20 에서 칸 543 · 선 1995 가 늘 살아 있어 끌기·확대가 버벅인다.
// 「칸을 눌러 담기」 동안은 담을 칸이 보여야 하므로 전부 그린다.

const GRID = 20;              // 칸이 붙는 눈금. 옮겨 온 배치가 전부 20 의 배수다 — 어긋나면 첫 끌기에서 칸이 튄다
const NODE_W = 196;
const NODE_H = 44;
const MIN_W = 120;
const MIN_H = 36;
const SVG_NS = 'http://www.w3.org/2000/svg';

export const EDGE_KINDS = [
  { ty: 'proc', label: '처리', color: '#5C6C7A', dash: false },
  { ty: 'trigger', label: '트리거', color: '#B33636', dash: true },
  { ty: 'ext', label: '외부', color: '#6A4FBF', dash: true },
  { ty: 'batch', label: '배치', color: '#2E7D4F', dash: false },
  { ty: 'read', label: '읽기', color: '#9AA3AB', dash: true },
];
// 도면에 선 종류가 아닌 글자(iface 등)가 적힌 줄이 있다 — 그릴 때도 거를 때도 「처리」로 본다.
const kindOf = (ty) => EDGE_KINDS.find((k) => k.ty === ty) ?? EDGE_KINDS[0];

const SEQ_COLORS = ['#1D6F9E', '#2E7D4F', '#B5630B', '#6A4FBF', '#0E7C86', '#B3263E', '#5C6C7A', '#A8327D'];

export const NODE_KINDS = {
  master: '기준', ledger: '원장', derived: '파생', snap: '스냅샷', ext: '외부',
  batch: '배치', proc: '프로시저', view: '뷰', func: '함수', iface: '인터페이스', eai: 'EAI',
};
const MARK = { iface: '⇄', eai: '⇶', view: '◫', func: 'ƒ' };
// 대상 DB 에 이 이름이 있어야 하는 칸 — 없으면 점선이다. 프로시저(proc)는 옛 도면에서 프로그램 이름으로도 써서 빼 둔다.
const DB_KINDS = ['master', 'ledger', 'derived', 'snap', 'view', 'func'];

// 그릴 때만 쓰는 표시 — 저장에 실리면 다음에 열 때 그대로 남는다.
const TRANSIENT = ['dim', 'flash', 'hot', 'near', 'found', 'inItem', 'missing'];

const snap = (v) => Math.round(v / GRID) * GRID;
const esc = (s) => String(s ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

export function create(host, dotnet) {
  return new Lineage(host, dotnet);
}

class Lineage {
  constructor(host, dotnet) {
    this.host = host;
    this.dotnet = dotnet;
    this.meta = {};
    this.lanes = [];
    this.flows = [];
    this.nodes = [];          // { id, position:{x,y}, data }
    this.edges = [];          // { id, source, target, sourceHandle, targetHandle, label, data }
    this.byId = new Map();
    this.adj = new Map();     // 칸 id → 닿는 선들
    this.view = { x: 0, y: 0, z: 1 };
    this.filter = { q: '', kinds: [], flows: [], lane: null, onlyLinked: false, keep: null, item: null, picking: false, seqOn: false, hover: true, dbTables: null };
    this.scope = null;        // 그린 칸들(Set). null 이면 전부
    this.sel = null;          // { kind:'node'|'edge', id }
    this.hoverId = null;
    this.linking = false;
    this.linkFrom = null;
    this.newTy = 'proc';
    this.dirtySent = false;
    this.edgeSeq = 0;

    host.classList.add('tl');
    host.tabIndex = 0;
    host.innerHTML = '';
    this.world = el('div', 'tl-world');
    this.laneLayer = el('div', 'tl-lanes');
    this.svg = document.createElementNS(SVG_NS, 'svg');
    this.svg.classList.add('tl-edges');
    this.svg.innerHTML = '<defs></defs>';
    this.nodeLayer = el('div', 'tl-nodes');
    this.ptLayer = el('div', 'tl-pts');
    this.world.append(this.laneLayer, this.svg, this.nodeLayer, this.ptLayer);
    this.map = el('canvas', 'tl-map');
    this.map.width = 200; this.map.height = 140;
    this.banner = el('div', 'tl-banner');
    host.append(this.world, this.map, this.banner);
    this.mapOn = true;

    this.defs = this.svg.querySelector('defs');
    for (const k of [...EDGE_KINDS, { ty: 'hi', color: 'var(--jsini-primary, #206bc4)' }]) {
      this.defs.insertAdjacentHTML('beforeend',
        `<marker id="tl-arrow-${k.ty}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="${k.color}"/></marker>`);
    }

    this.bind();
  }

  // ───────────────────────── 읽기 · 저장 ─────────────────────────

  load(graphJson, keepView) {
    let g = {};
    try { g = graphJson ? JSON.parse(graphJson) : {}; } catch { g = {}; }
    this.meta = {};
    for (const [k, v] of Object.entries(g)) if (!['lanes', 'flows', 'nodes', 'edges'].includes(k)) this.meta[k] = v;
    this.lanes = Array.isArray(g.lanes) ? g.lanes : [];
    this.flows = Array.isArray(g.flows) ? g.flows : [];
    this.nodes = (Array.isArray(g.nodes) ? g.nodes : [])
      .filter((n) => n && n.id && n.type !== 'lane')
      .map((n) => ({ id: String(n.id), position: { x: +n.position?.x || 0, y: +n.position?.y || 0 }, data: { ...(n.data || {}) } }));
    for (const n of this.nodes) for (const t of TRANSIENT) delete n.data[t];
    this.edges = (Array.isArray(g.edges) ? g.edges : [])
      .filter((e) => e && e.source && e.target)
      .map((e) => ({
        id: String(e.id), source: String(e.source), target: String(e.target),
        sourceHandle: e.sourceHandle ?? null, targetHandle: e.targetHandle ?? null,
        label: e.label ?? '', data: { ty: 'proc', key: 0, ...(e.data || {}) },
      }));
    this.edgeSeq = this.edges.reduce((m, e) => Math.max(m, +String(e.id).replace(/\D/g, '') || 0), 0);
    this.sel = null;
    this.hoverId = null;
    this.linkFrom = null;
    this.dirtySent = false;
    this._undo = null;
    // 고른 업무항목은 화면이 곧 다시 알려 준다 — 옛 범위로 한 번 그렸다가 또 그리지 않게 비운다.
    this.filter = { ...this.filter, keep: null, item: null, picking: false };
    this.reindex();
    this.renderAll();
    if (!keepView) requestAnimationFrame(() => this.fit());
    return this.summary();
  }

  summary() {
    const counts = {};
    for (const e of this.edges) { const t = kindOf(e.data?.ty).ty; counts[t] = (counts[t] || 0) + 1; }
    return {
      nodes: this.nodes.length,
      edges: this.edges.length,
      edgeCounts: counts,
      lanes: this.lanes.map((l) => ({ i: l.i, name: l.name })),
      flows: this.flows.filter((f) => f.id !== 'all' && Array.isArray(f.nodes)).map((f) => ({ id: f.id, name: f.name, count: f.nodes.length })),
      tables: this.nodes.map((n) => ({ id: n.id, kr: n.data.kr || '', kind: n.data.kind || 'master' })),
    };
  }

  /** 그릴 때 붙인 것은 빼고 뜻만 남긴다 — 옮겨 온 원본과 같은 모양이다. */
  save() {
    const g = {
      ...this.meta,
      v: 1,
      lanes: this.lanes,
      flows: this.flows,
      nodes: this.nodes.map((n) => {
        const data = { ...n.data };
        for (const t of TRANSIENT) delete data[t];
        return { id: n.id, type: 'table', position: { x: Math.round(n.position.x), y: Math.round(n.position.y) }, data };
      }),
      edges: this.edges.map((e) => ({
        id: e.id, source: e.source, target: e.target,
        sourceHandle: e.sourceHandle ?? null, targetHandle: e.targetHandle ?? null,
        label: e.label ?? '', type: 'way',
        data: {
          ty: e.data?.ty ?? 'proc',
          key: e.data?.key ? 1 : 0,
          ...(e.data?.pts?.length ? { pts: e.data.pts.map((p) => ({ x: Math.round(p.x), y: Math.round(p.y) })) } : {}),
          ...(Number(e.data?.seq) > 0 ? { seq: Number(e.data.seq), flow: Number(e.data.flow) || 0 } : {}),
        },
      })),
    };
    this.dirtySent = false;
    return JSON.stringify(g);
  }

  changed() {
    if (this.dirtySent) return;
    this.dirtySent = true;
    this.dotnet.invokeMethodAsync('NotifyChanged').catch(() => {});
  }

  reindex() {
    this.byId = new Map(this.nodes.map((n) => [n.id, n]));
    this.adj = new Map();
    for (const e of this.edges) {
      if (!this.adj.has(e.source)) this.adj.set(e.source, []);
      if (!this.adj.has(e.target)) this.adj.set(e.target, []);
      this.adj.get(e.source).push(e);
      if (e.target !== e.source) this.adj.get(e.target).push(e);
    }
  }

  // ───────────────────────── 그리기 ─────────────────────────

  /** 그릴 칸. 업무항목을 골랐고 담는 중이 아니면 그 항목의 칸만(비어 있으면 하나도 없다). */
  scopeOf(f) {
    return !f.picking && Array.isArray(f.keep) ? new Set(f.keep) : null;
  }

  renderAll() {
    this.scope = this.scopeOf(this.filter);
    const inScope = (id) => !this.scope || this.scope.has(id);
    this.renderLanes();
    this.nodeLayer.innerHTML = '';
    this.nodeEls = new Map();
    const shown = this.nodes.filter((n) => inScope(n.id));
    const frag = document.createDocumentFragment();
    for (const n of shown) frag.append(this.makeNode(n));
    this.nodeLayer.append(frag);
    // 칸 크기를 잰 뒤에 선을 긋는다 — 끝점이 칸 테두리에 붙어야 한다.
    for (const n of shown) this.measure(n);
    this.svg.querySelectorAll('g.tl-e').forEach((g) => g.remove());
    this.edgeEls = new Map();
    const lines = document.createDocumentFragment();
    for (const e of this.edges) if (inScope(e.source) && inScope(e.target)) lines.append(this.makeEdge(e));
    this.svg.append(lines);
    if (this.hoverId && !this.nodeEls.has(this.hoverId)) this.hoverId = null;
    if (this.linkFrom && !this.nodeEls.has(this.linkFrom)) this.linkFrom = null;
    // 고른 것이 범위 밖으로 나갔으면 놓는다 — 오른쪽 판이 안 보이는 칸을 붙들고 있지 않게.
    if (this.sel && !(this.sel.kind === 'node' ? this.nodeEls : this.edgeEls).has(this.sel.id)) {
      this.select(null);
    }
    this.paint();
    this.applyView();
  }

  renderLanes() {
    this.laneLayer.innerHTML = '';
    if (!this.lanes.length) return;
    const ys = this.nodes.map((n) => n.position.y);
    const bottom = (ys.length ? Math.max(...ys) : 600) + 160;
    for (const l of this.lanes) {
      const d = el('div', 'tl-lane');
      d.style.left = l.x + 'px';
      d.style.width = l.w + 'px';
      d.style.height = bottom + 'px';
      d.dataset.lane = l.i;
      d.innerHTML = `<button type="button" class="tl-lane__head" data-lane="${l.i}">${esc((l.i + 1) + '. ' + l.name)}</button>`;
      this.laneLayer.append(d);
    }
  }

  makeNode(n) {
    const d = el('div', 'tl-node');
    d.dataset.id = n.id;
    this.fillNode(d, n);
    this.nodeEls.set(n.id, d);
    return d;
  }

  fillNode(d, n) {
    const k = n.data.kind || 'master';
    d.className = 'tl-node tl-node--' + k;
    d.style.left = n.position.x + 'px';
    d.style.top = n.position.y + 'px';
    d.style.width = (n.data.w > 0 ? n.data.w : NODE_W) + 'px';
    d.style.height = n.data.h > 0 ? n.data.h + 'px' : '';
    const rows = n.data.rows == null || n.data.rows === '' ? '' : Number(n.data.rows).toLocaleString('ko-KR') + '행';
    const owner = n.data.owner ? `<span class="tl-node__owner">${esc(n.data.owner)}</span>` : '';
    d.innerHTML =
      `<div class="tl-node__head"><span class="tl-node__kind">${MARK[k] ? MARK[k] + ' ' : ''}${esc(NODE_KINDS[k] || k)}</span>` +
      `<b class="tl-node__id">${esc(n.id)}</b>${n.data.star ? '<span class="tl-node__star">★</span>' : ''}${owner}</div>` +
      `<div class="tl-node__kr">${esc(n.data.kr || '')}${rows ? ` <small>${rows}</small>` : ''}</div>` +
      (n.data.warn ? `<div class="tl-node__warn">⚠ ${esc(n.data.warn)}</div>` : '') +
      '<span class="tl-node__size" title="끌어서 크기를 바꿉니다 (두 번 누르면 기본 크기)"></span>';
    d.title = [
      `${n.id} · ${n.data.kr || ''}`,
      n.data.owner ? `스키마  ${n.data.owner}` : '',
      n.data.pk ? `PK  ${n.data.pk}` : '',
      rows,
      n.data.trg ? `트리거  ${n.data.trg}` : '',
      n.data.writers?.length ? `쓰는 곳  ${n.data.writers.join(' / ')}` : '',
      n.data.note ? `— ${n.data.note}` : '',
      n.data.warn ? `⚠ ${n.data.warn}` : '',
    ].filter(Boolean).join('\n');
  }

  measure(n) {
    const d = this.nodeEls.get(n.id);
    n._w = d?.offsetWidth || NODE_W;
    n._h = d?.offsetHeight || NODE_H;
  }

  makeEdge(e) {
    const g = document.createElementNS(SVG_NS, 'g');
    g.classList.add('tl-e');
    g.dataset.id = e.id;
    g.innerHTML = '<path class="tl-e__hit"/><path class="tl-e__line"/><text class="tl-e__label"/><g class="tl-e__seq"><circle r="8"/><text/></g>';
    this.edgeEls.set(e.id, g);
    this.drawEdge(e);
    return g;
  }

  /** 칸의 한 면 가운데. 면을 안 정했으면 상대 자리를 보고 고른다 — 왼쪽 칸을 오른쪽 칸에 이을 때 칸을 가로지르지 않게. */
  port(n, side) {
    const w = n._w || NODE_W, h = n._h || NODE_H, x = n.position.x, y = n.position.y;
    switch (side) {
      case 't': return { x: x + w / 2, y, side };
      case 'b': return { x: x + w / 2, y: y + h, side };
      case 'l': return { x, y: y + h / 2, side };
      default: return { x: x + w, y: y + h / 2, side: 'r' };
    }
  }

  autoSides(a, b) {
    const ax = a.position.x + (a._w || NODE_W) / 2, ay = a.position.y + (a._h || NODE_H) / 2;
    const bx = b.position.x + (b._w || NODE_W) / 2, by = b.position.y + (b._h || NODE_H) / 2;
    const dx = bx - ax, dy = by - ay;
    if (Math.abs(dx) >= Math.abs(dy) * 1.2 || Math.abs(dx) > (a._w || NODE_W)) return dx >= 0 ? ['r', 'l'] : ['l', 'r'];
    return dy >= 0 ? ['b', 't'] : ['t', 'b'];
  }

  geometry(e) {
    const a = this.byId.get(e.source), b = this.byId.get(e.target);
    if (!a || !b) return null;
    const [sa, sb] = this.autoSides(a, b);
    const p1 = this.port(a, e.sourceHandle || sa);
    const p2 = this.port(b, e.targetHandle || sb);
    const pts = Array.isArray(e.data?.pts) ? e.data.pts : [];
    return { p1, p2, pts };
  }

  drawEdge(e) {
    const g = this.edgeEls.get(e.id);
    const geo = this.geometry(e);
    if (!g || !geo) return;
    const { d, mid, mids } = pathOf(geo.p1, geo.p2, geo.pts);
    g.querySelector('.tl-e__hit').setAttribute('d', d);
    const line = g.querySelector('.tl-e__line');
    line.setAttribute('d', d);
    const k = kindOf(e.data?.ty);
    line.style.stroke = k.color;
    line.style.strokeWidth = e.data?.key ? 2.2 : 1.2;
    line.style.strokeDasharray = k.dash ? '6 4' : '';
    line.setAttribute('marker-end', `url(#tl-arrow-${k.ty})`);
    const t = g.querySelector('.tl-e__label');
    t.textContent = e.label || '';
    t.setAttribute('x', mid.x);
    t.setAttribute('y', mid.y - 4);
    const sq = g.querySelector('.tl-e__seq');
    const n = Number(e.data?.seq);
    if (n > 0) {
      sq.setAttribute('transform', `translate(${mid.x},${mid.y})`);
      sq.querySelector('circle').style.fill = SEQ_COLORS[(Number(e.data?.flow) || 0) % SEQ_COLORS.length];
      sq.querySelector('text').textContent = n;
    }
    sq.dataset.has = n > 0 ? '1' : '';
    e._mid = mid;
    e._mids = mids;
  }

  redrawEdgesOf(id) {
    for (const e of this.adj.get(id) || []) this.drawEdge(e);
    if (this.sel?.kind === 'edge') this.drawPts();
  }

  // ───────────────────────── 거르기 · 칠하기 ─────────────────────────

  setFilter(f) {
    const prev = this.filter;
    this.filter = { ...this.filter, ...f };
    const was = this.scope, now = this.scopeOf(this.filter);
    const same = was === now || (!!was && !!now && was.size === now.size && [...now].every((x) => was.has(x)));
    if (same) { this.paint(); return; }
    this.renderAll();
    // 다른 업무항목으로 옮겼을 때만 맞춘다 — 담기를 켜고 끌 때는 보던 자리에 그대로 둔다.
    if ((prev.item ?? null) !== (this.filter.item ?? null)) requestAnimationFrame(() => this.fit());
  }

  focusId() { return this.sel?.kind === 'node' ? this.sel.id : (this.filter.hover ? this.hoverId : null); }

  paint() {
    const f = this.filter;
    const and = (a, b) => (a && b ? new Set([...a].filter((x) => b.has(x))) : (a ?? b));
    const kw = (f.q || '').trim().toLowerCase();
    const qKeep = kw ? new Set(this.nodes.filter((n) => n.id.toLowerCase().includes(kw) || String(n.data.kr ?? '').toLowerCase().includes(kw)).map((n) => n.id)) : null;
    let flowKeep = null;
    if (f.flows?.length) {
      const s = new Set();
      for (const fl of this.flows) if (f.flows.includes(fl.id) && fl.nodes) for (const id of fl.nodes) s.add(id);
      flowKeep = s.size ? s : null;
    }
    // 레인은 칸의 col 로 가른다 — 칸을 옆으로 끌어도 「어느 단계의 표인가」는 바뀌지 않아야 한다.
    const laneKeep = f.lane == null ? null : new Set(this.nodes.filter((n) => n.data.col === f.lane).map((n) => n.id));
    const pin = f.keep?.length ? new Set(f.keep) : null;
    const keep = and(and(and(flowKeep, laneKeep), qKeep), pin);
    const id = this.focusId();
    let rel = null;
    if (id && this.byId.has(id)) {
      rel = new Set([id]);
      for (const e of this.adj.get(id) || []) { rel.add(e.source); rel.add(e.target); }
    }
    const pinned = (x) => !!pin && pin.has(x);
    const picking = !!f.picking;
    const db = f.dbTables ? new Set(f.dbTables.map((x) => String(x).toUpperCase())) : null;

    for (const n of this.nodes) {
      const d = this.nodeEls.get(n.id);
      if (!d) continue;
      const outFlow = keep ? !keep.has(n.id) : false;
      const outRel = rel ? !rel.has(n.id) && !pinned(n.id) : false;
      const hot = !!id && n.id === id;
      d.classList.toggle('is-dim', outFlow || outRel);
      d.classList.toggle('is-hot', hot);
      d.classList.toggle('is-near', !!rel && !outFlow && rel.has(n.id) && !hot);
      d.classList.toggle('is-found', !!qKeep && qKeep.has(n.id) && !(outFlow || outRel));
      d.classList.toggle('is-in', picking && !!pin && pin.has(n.id));
      d.classList.toggle('is-sel', this.sel?.kind === 'node' && this.sel.id === n.id);
      d.classList.toggle('is-from', this.linkFrom === n.id);
      // 대상 DB 에서 표 목록을 받아 왔을 때만 가른다. 프로시저·외부 칸은 표 목록에 없는 것이 정상이다.
      d.classList.toggle('is-missing', !!db && DB_KINDS.includes(n.data.kind || 'master') && !db.has(n.id.toUpperCase()));
      // 범위를 정한 뒤에 놓은 칸은 DOM 에 있어도 범위 밖이면 숨긴다.
      d.hidden = !!this.scope && !this.scope.has(n.id);
    }

    const kinds = new Set(f.kinds || []);
    const live = new Set();
    for (const e of this.edges) {
      const g = this.edgeEls.get(e.id);
      if (!g) continue;
      const inFlow = !keep || (keep.has(e.source) && keep.has(e.target));
      const inKind = kinds.has(kindOf(e.data?.ty).ty);
      const hit = !!id && (e.source === id || e.target === id);
      const inPin = pinned(e.source) && pinned(e.target);
      const selected = this.sel?.kind === 'edge' && this.sel.id === e.id;
      // 선 보기에서 안 고른 종류는 흐리지 않고 아예 안 그린다 — 1995개를 흐린 채 두면 끌기·확대가 계속 무겁다.
      // 다만 고른 칸에 닿는 선과 고른 선은 종류와 무관하게 세운다 — 칸을 눌렀는데 아무 선도 없으면 그 칸이 어디와 이어지는지 볼 길이 없다.
      const show = selected || (!picking && (inKind || (hit && inFlow)));
      const dim = !selected && (!inFlow || (!!id && !hit && !inPin));
      g.style.display = show ? '' : 'none';
      g.classList.toggle('is-dim', dim);
      g.classList.toggle('is-hi', (hit && inFlow) || selected);
      g.classList.toggle('is-sel', selected);
      g.classList.toggle('is-seq', !!f.seqOn && !dim);
      const line = g.querySelector('.tl-e__line');
      const k = kindOf(e.data?.ty);
      line.setAttribute('marker-end', `url(#tl-arrow-${(hit && inFlow) || selected ? 'hi' : k.ty})`);
      if (show && !dim) { live.add(e.source); live.add(e.target); }
      // 강조한 선은 맨 위로 올린다 — SVG 는 나중 것이 위다.
      if ((hit && inFlow) || selected) this.svg.append(g);
    }

    if (f.onlyLinked && kinds.size) {
      for (const n of this.nodes) {
        const d = this.nodeEls.get(n.id);
        if (d && !live.has(n.id)) d.classList.add('is-dim');
      }
    }

    for (const l of this.laneLayer.children) l.classList.toggle('is-on', String(f.lane) === l.dataset.lane);
    this.drawPts();
    this.drawMap();
  }

  // ───────────────────────── 보기 ─────────────────────────

  applyView() {
    const { x, y, z } = this.view;
    this.world.style.transform = `translate(${x}px,${y}px) scale(${z})`;
    this.host.classList.toggle('tl--far', z < 0.45);
    this.drawMap();
  }

  bounds(onlyVisible) {
    let x1 = Infinity, y1 = Infinity, x2 = -Infinity, y2 = -Infinity;
    for (const n of this.nodes) {
      if (onlyVisible) { const d = this.nodeEls.get(n.id); if (!d || d.hidden) continue; }
      x1 = Math.min(x1, n.position.x); y1 = Math.min(y1, n.position.y);
      x2 = Math.max(x2, n.position.x + (n._w || NODE_W)); y2 = Math.max(y2, n.position.y + (n._h || NODE_H));
    }
    if (!isFinite(x1)) return { x1: 0, y1: 0, x2: 800, y2: 600 };
    return { x1, y1, x2, y2 };
  }

  /** 표 전부가 한 화면에 들어오게. 레인은 범위에서 뺀다 — 맨 아래까지 그어 둔 배경이라 함께 맞추면 그림이 쓸데없이 작아진다. */
  fit() {
    const box = this.host.getBoundingClientRect();
    if (!box.width) return;
    const b = this.bounds(true);
    const PAD = 28;
    const z = Math.max(0.08, Math.min(2.4, Math.min((box.width - PAD * 2) / (b.x2 - b.x1), (box.height - PAD * 2) / (b.y2 - b.y1))));
    this.view = { z, x: (box.width - (b.x2 - b.x1) * z) / 2 - b.x1 * z, y: (box.height - (b.y2 - b.y1) * z) / 2 - b.y1 * z };
    this.applyView();
  }

  zoom(f, cx, cy) {
    const box = this.host.getBoundingClientRect();
    cx ??= box.width / 2; cy ??= box.height / 2;
    const z = Math.max(0.08, Math.min(3, this.view.z * f));
    const k = z / this.view.z;
    this.view = { z, x: cx - (cx - this.view.x) * k, y: cy - (cy - this.view.y) * k };
    this.applyView();
  }
  zoomIn() { this.zoom(1.2); }
  zoomOut() { this.zoom(1 / 1.2); }

  toWorld(clientX, clientY) {
    const box = this.host.getBoundingClientRect();
    return { x: (clientX - box.left - this.view.x) / this.view.z, y: (clientY - box.top - this.view.y) / this.view.z };
  }

  /** 그 칸으로 가서 고른다. 도면에 없으면 'missing', 고른 업무항목 밖이라 안 그렸으면 'out'. */
  goTo(id) {
    const n = this.byId.get(id);
    if (!n) return 'missing';
    if (!this.nodeEls.has(id)) return 'out';
    const box = this.host.getBoundingClientRect();
    const z = Math.max(this.view.z, 0.9);
    this.view = { z, x: box.width / 2 - (n.position.x + (n._w || NODE_W) / 2) * z, y: box.height / 2 - (n.position.y + (n._h || NODE_H) / 2) * z };
    this.applyView();
    this.select({ kind: 'node', id });
    this.flash([id]);
    return 'ok';
  }

  flash(ids) {
    for (const id of ids) {
      const d = this.nodeEls.get(id);
      if (!d) continue;
      d.classList.remove('is-flash');
      void d.offsetWidth;
      d.classList.add('is-flash');
      setTimeout(() => d.classList.remove('is-flash'), 2600);
    }
  }

  toggleMap(on) {
    this.mapOn = on ?? !this.mapOn;
    this.map.style.display = this.mapOn ? '' : 'none';
    this.drawMap();
    return this.mapOn;
  }

  drawMap() {
    if (!this.mapOn || !this.nodes.length) { this.map.style.display = this.mapOn && this.nodes.length ? '' : 'none'; return; }
    this.map.style.display = '';
    const c = this.map.getContext('2d');
    const W = this.map.width, H = this.map.height;
    const b = this.bounds(true);
    const s = Math.min(W / (b.x2 - b.x1 + 80), H / (b.y2 - b.y1 + 80));
    const ox = -b.x1 * s + (W - (b.x2 - b.x1) * s) / 2, oy = -b.y1 * s + (H - (b.y2 - b.y1) * s) / 2;
    this._mapT = { s, ox, oy };
    c.clearRect(0, 0, W, H);
    for (const n of this.nodes) {
      const d = this.nodeEls.get(n.id);
      if (!d || d.hidden) continue;
      c.fillStyle = d?.classList.contains('is-hot') ? '#206bc4' : d?.classList.contains('is-dim') ? '#d5d9de' : '#8b97a5';
      c.fillRect(ox + n.position.x * s, oy + n.position.y * s, Math.max(2, (n._w || NODE_W) * s), Math.max(1.5, (n._h || NODE_H) * s));
    }
    const box = this.host.getBoundingClientRect();
    c.strokeStyle = '#206bc4';
    c.lineWidth = 1.5;
    c.strokeRect(ox + (-this.view.x / this.view.z) * s, oy + (-this.view.y / this.view.z) * s, (box.width / this.view.z) * s, (box.height / this.view.z) * s);
  }

  // ───────────────────────── 고르기 · 고치기 ─────────────────────────

  select(sel) {
    this.sel = sel;
    this.paint();
    this.dotnet.invokeMethodAsync('NotifySelect', sel ? sel.kind : null, sel ? this.describe(sel) : null).catch(() => {});
  }

  describe(sel) {
    if (sel.kind === 'node') {
      const n = this.byId.get(sel.id);
      if (!n) return null;
      const links = (this.adj.get(n.id) || []).map((e) => ({
        edge: e.id, other: e.source === n.id ? e.target : e.source, out: e.source === n.id,
        label: e.label || '', ty: kindOf(e.data?.ty).ty,
      }));
      return JSON.stringify({ id: n.id, ...n.data, links });
    }
    const e = this.edges.find((x) => x.id === sel.id);
    if (!e) return null;
    return JSON.stringify({
      id: e.id, source: e.source, target: e.target, label: e.label || '', ty: kindOf(e.data?.ty).ty, rawTy: e.data?.ty || 'proc',
      key: !!e.data?.key, seq: Number(e.data?.seq) || 0, flow: Number(e.data?.flow) || 0,
      sourceHandle: e.sourceHandle || '', targetHandle: e.targetHandle || '', pts: e.data?.pts?.length || 0,
    });
  }

  reselect() {
    if (this.sel) this.dotnet.invokeMethodAsync('NotifySelect', this.sel.kind, this.describe(this.sel)).catch(() => {});
  }

  /** 칸의 속성을 고친다. 이름(id)을 바꾸면 선·흐름의 이름도 함께 옮긴다. 이미 있는 이름이면 거절한다. */
  patchNode(id, patchJson) {
    const n = this.byId.get(id);
    if (!n) return 'missing';
    const patch = JSON.parse(patchJson || '{}');
    if (patch.id != null) {
      const next = String(patch.id).trim();
      delete patch.id;
      if (next && next !== id) {
        if (this.byId.has(next)) return 'dup';
        n.id = next;
        n.data.id = next;
        for (const e of this.edges) { if (e.source === id) e.source = next; if (e.target === id) e.target = next; }
        for (const f of this.flows) if (Array.isArray(f.nodes)) f.nodes = f.nodes.map((x) => (x === id ? next : x));
        const d = this.nodeEls.get(id);
        this.nodeEls.delete(id);
        if (d) { this.nodeEls.set(next, d); d.dataset.id = next; }
        if (this.scope?.delete(id)) this.scope.add(next);
        if (this.sel?.id === id) this.sel.id = next;
        this.reindex();
      }
    }
    Object.assign(n.data, patch);
    const d = this.nodeEls.get(n.id);
    if (d) this.fillNode(d, n);
    this.measure(n);
    this.redrawEdgesOf(n.id);
    this.paint();
    this.changed();
    this.reselect();
    return 'ok';
  }

  patchEdge(id, patchJson) {
    const e = this.edges.find((x) => x.id === id);
    if (!e) return false;
    const p = JSON.parse(patchJson || '{}');
    if ('label' in p) e.label = p.label ?? '';
    if ('ty' in p) e.data.ty = p.ty;
    if ('key' in p) e.data.key = p.key ? 1 : 0;
    if ('seq' in p) { if (Number(p.seq) > 0) e.data.seq = Number(p.seq); else { delete e.data.seq; delete e.data.flow; } }
    if ('flow' in p && Number(e.data.seq) > 0) e.data.flow = Number(p.flow) || 0;
    if ('sourceHandle' in p) e.sourceHandle = p.sourceHandle || null;
    if ('targetHandle' in p) e.targetHandle = p.targetHandle || null;
    if (p.clearPts) delete e.data.pts;
    if (p.flip) {
      [e.source, e.target] = [e.target, e.source];
      [e.sourceHandle, e.targetHandle] = [e.targetHandle, e.sourceHandle];
      if (e.data.pts?.length) e.data.pts.reverse();
      this.reindex();
    }
    this.drawEdge(e);
    this.paint();
    this.changed();
    this.reselect();
    return true;
  }

  removeSelection() {
    if (!this.sel) return 0;
    if (this.sel.kind === 'edge') {
      const i = this.edges.findIndex((e) => e.id === this.sel.id);
      if (i < 0) return 0;
      this.edges.splice(i, 1);
      this.edgeEls.get(this.sel.id)?.remove();
      this.edgeEls.delete(this.sel.id);
    } else {
      const id = this.sel.id;
      const drop = new Set((this.adj.get(id) || []).map((e) => e.id));
      this.edges = this.edges.filter((e) => !drop.has(e.id));
      for (const eid of drop) { this.edgeEls.get(eid)?.remove(); this.edgeEls.delete(eid); }
      this.nodes = this.nodes.filter((n) => n.id !== id);
      this.nodeEls.get(id)?.remove();
      this.nodeEls.delete(id);
      for (const f of this.flows) if (Array.isArray(f.nodes)) f.nodes = f.nodes.filter((x) => x !== id);
    }
    this.reindex();
    this.changed();
    this.select(null);
    return 1;
  }

  setLinking(on, ty) {
    this.linking = !!on;
    if (ty) this.newTy = ty;
    this.linkFrom = null;
    this.host.classList.toggle('tl--linking', this.linking);
    this.banner.textContent = this.linking ? '잇기 — 출발 칸을 누르십시오 (Esc 로 멈춤)' : '';
    this.paint();
    return this.linking;
  }

  link(from, to) {
    if (from === to) return;
    if (this.edges.some((e) => e.source === from && e.target === to)) {
      this.dotnet.invokeMethodAsync('NotifyNotice', `이미 이어져 있습니다 — ${from} → ${to}`).catch(() => {});
      return;
    }
    const e = { id: 'e' + (++this.edgeSeq), source: from, target: to, sourceHandle: null, targetHandle: null, label: '', data: { ty: this.newTy, key: 0 } };
    this.edges.push(e);
    this.reindex();
    this.svg.append(this.makeEdge(e));
    this.changed();
    this.select({ kind: 'edge', id: e.id });
  }

  /** 표를 놓는다. 이미 있는 이름은 건너뛴다. 지금 보는 화면 가운데 근처에 격자로 편다. */
  addTables(json) {
    const list = JSON.parse(json || '[]');
    const box = this.host.getBoundingClientRect();
    const c = this.toWorld(box.left + box.width / 2, box.top + box.height / 2);
    const cols = Math.max(1, Math.min(6, Math.ceil(Math.sqrt(list.length))));
    const placed = [];
    let i = 0;
    for (const t of list) {
      const id = String(t.id || '').trim();
      if (!id || this.byId.has(id)) continue;
      const n = {
        id,
        position: { x: snap(c.x - (cols * 240) / 2 + (i % cols) * 240), y: snap(c.y - 40 + Math.floor(i / cols) * 80) },
        data: { id, kr: t.kr || '', kind: t.kind || 'ledger', rows: t.rows ?? null, pk: t.pk || '', note: t.note || '', owner: t.owner || '', writers: [] },
      };
      this.nodes.push(n);
      this.byId.set(id, n);
      this.nodeLayer.append(this.makeNode(n));
      this.measure(n);
      placed.push(id);
      i++;
    }
    if (placed.length) {
      this.reindex();
      this.paint();
      this.flash(placed);
      this.changed();
    }
    return placed.length;
  }

  // ───────────────────────── 펼치기 ─────────────────────────

  /**
   * 보이는 칸을 선 방향(왼쪽 → 오른쪽)으로 단을 나눠 다시 놓는다. 레인이 있으면 단은 레인이고, 같은 레인 안에서 이어진 칸만 옆 칸으로 민다.
   * 선 보기에서 끈 종류의 선은 단을 정하는 데 안 쓴다 — 읽기 선 수백 개가 한 단으로 몰아 버린다.
   * 손으로 꺾어 둔 점과 붙일 면은 걷는다 — 칸이 옮겨 간 뒤에는 엉뚱한 곳을 돌아간다. 걷은 것까지 undoArrange 가 되돌린다.
   */
  arrange() {
    const set = this.nodes.filter((n) => { const d = this.nodeEls.get(n.id); return d && !d.hidden; });
    if (set.length < 2) return 0;
    const ids = new Set(set.map((n) => n.id));
    for (const n of set) this.measure(n);

    this._undo = {
      pos: set.map((n) => [n.id, { ...n.position }]),
      edges: this.edges.map((e) => [e.id, e.sourceHandle, e.targetHandle, e.data?.pts]),
      lanes: this.lanes.map((l) => ({ ...l })),
    };

    const kinds = new Set(this.filter.kinds || []);
    const pairs = new Map();
    for (const e of this.edges) {
      if (e.source === e.target || !ids.has(e.source) || !ids.has(e.target)) continue;
      if (kinds.size && !kinds.has(kindOf(e.data?.ty).ty)) continue;
      pairs.set(e.source + '\u0001' + e.target, [e.source, e.target]);
    }
    const out = new Map(), inn = new Map();
    for (const n of set) { out.set(n.id, []); inn.set(n.id, []); }
    for (const [a, b] of pairs.values()) { out.get(a).push(b); inn.get(b).push(a); }

    const old = this.bounds(true);
    const whole = !this.scope;
    const laneIdx = new Map(this.lanes.map((l, i) => [l.i, i]));
    const useLanes = this.lanes.length > 0;
    const laneOf = (n) => {
      if (laneIdx.has(n.data.col)) return laneIdx.get(n.data.col);
      const cx = n.position.x + n._w / 2;
      let best = 0, d = Infinity;
      this.lanes.forEach((l, i) => { const dd = Math.abs(l.x + l.w / 2 - cx); if (dd < d) { d = dd; best = i; } });
      return best;
    };

    // 단 매기기 — 고리는 깊이 우선으로 거꾸로 가는 선을 빼고 끊는다. 같이 묶는 범위(group)를 넘는 선은 안 센다.
    const rankIn = (group) => {
      const inG = new Set(group.map((n) => n.id));
      const state = new Map(), order = [], back = new Set();
      const visit = (id) => {
        state.set(id, 1);
        for (const t of out.get(id)) {
          if (!inG.has(t)) continue;
          if (state.get(t) === 1) back.add(id + '\u0001' + t);
          else if (!state.has(t)) visit(t);
        }
        state.set(id, 2);
        order.push(id);
      };
      // 위에 있던 칸부터 — 같은 그림을 두 번 펼쳐도 같은 답이 나온다.
      for (const n of [...group].sort((a, b) => a.position.y - b.position.y || a.position.x - b.position.x)) if (!state.has(n.id)) visit(n.id);
      const rank = new Map(group.map((n) => [n.id, 0]));
      for (const id of order.reverse()) {
        for (const t of out.get(id)) {
          if (!inG.has(t) || back.has(id + '\u0001' + t)) continue;
          rank.set(t, Math.max(rank.get(t), rank.get(id) + 1));
        }
      }
      // 내보내기만 하는 칸은 받는 칸 바로 앞으로 당긴다 — 맨 왼쪽에 몰려 긴 선을 늘어뜨리지 않게.
      for (const id of [...order].reverse()) {
        if (inn.get(id).some((s) => inG.has(s))) continue;
        const next = out.get(id).filter((t) => inG.has(t) && !back.has(id + '\u0001' + t)).map((t) => rank.get(t));
        if (next.length) rank.set(id, Math.max(rank.get(id), Math.min(...next) - 1));
      }
      return rank;
    };

    const linked = (n) => out.get(n.id).length + inn.get(n.id).length > 0;
    const major = new Map(), minor = new Map();
    let loose = [];
    if (useLanes) {
      // 레인 안에서는 한 줄로 세운다 — 레인 안의 선까지 단으로 가르면 레인마다 옆으로 몇 칸씩 벌어져 그림이 가로로만 길어진다.
      for (const n of set) { major.set(n.id, laneOf(n)); minor.set(n.id, 0); }
    } else {
      const main = set.filter(linked);
      loose = set.filter((n) => !linked(n));
      const r = rankIn(main);
      for (const n of main) { major.set(n.id, r.get(n.id)); minor.set(n.id, 0); }
    }

    // 단 → 세로 줄. 한 줄이 너무 길면 옆 줄로 접는다 — 한 화면에 안 들어오는 기둥은 읽을 수 없다.
    const MAX_ROWS = Math.max(10, Math.ceil(Math.sqrt(set.length) * 1.6));
    const keyOf = (id) => major.get(id) * 1000 + minor.get(id);
    const colKeys = [...new Set([...major.keys()].map(keyOf))].sort((a, b) => a - b);
    let cols = colKeys.map((k) => set.filter((n) => major.has(n.id) && keyOf(n.id) === k).sort((a, b) => a.position.y - b.position.y));

    // 줄 안 차례 — 이웃의 평균 자리(무게중심)로 왼쪽→오른쪽, 오른쪽→왼쪽을 몇 번 오간다. 선이 덜 엇갈린다.
    const where = new Map();
    const mark = () => cols.forEach((c, ci) => c.forEach((n, i) => where.set(n.id, { c: ci, r: (i + 0.5) / c.length })));
    mark();
    for (let pass = 0; pass < 8; pass++) {
      const ltr = pass % 2 === 0;
      const seq = ltr ? cols.map((_, i) => i) : cols.map((_, i) => cols.length - 1 - i);
      for (const ci of seq) {
        const key = new Map();
        for (const n of cols[ci]) {
          const nb = [...inn.get(n.id), ...out.get(n.id)].map((x) => where.get(x)).filter((w) => w && (ltr ? w.c < ci : w.c > ci));
          key.set(n.id, nb.length ? nb.reduce((s, w) => s + w.r, 0) / nb.length : where.get(n.id).r);
        }
        cols[ci].sort((a, b) => key.get(a.id) - key.get(b.id));
        cols[ci].forEach((n, i) => where.set(n.id, { c: ci, r: (i + 0.5) / cols[ci].length }));
      }
    }
    // 긴 줄 접기는 차례를 정한 뒤에 한다 — 먼저 접으면 이웃끼리 다른 줄로 흩어진다.
    const folded = [], colMajor = [];
    cols.forEach((c, ci) => {
      const m = major.get(c[0].id);
      for (let i = 0; i < c.length; i += MAX_ROWS) { folded.push(c.slice(i, i + MAX_ROWS)); colMajor.push(m); }
    });
    cols = folded;

    // 가로 자리. 단 사이는 넓게(선 이름이 앉는다), 같은 단 안의 줄 사이는 좁게.
    const GAP_MAJOR = 140, GAP_MINOR = 60, GAP_Y = 40;
    const colW = cols.map((c) => Math.max(...c.map((n) => n._w)));
    const colX = [];
    const top = useLanes ? Math.max(old.y1, 60) : old.y1;
    if (useLanes && !whole) {
      // 일부만 펼칠 때 레인은 그대로다 — 칸을 제 레인 안에 세운다.
      const used = new Map();
      cols.forEach((c, ci) => {
        const l = this.lanes[colMajor[ci]];
        const at = used.get(colMajor[ci]) ?? l.x + 24;
        colX[ci] = at;
        used.set(colMajor[ci], at + colW[ci] + GAP_MINOR);
      });
    } else if (useLanes) {
      // 전부 펼칠 때는 레인 띠도 새 자리에 맞춘다. 칸이 없는 레인도 머리글이 읽힐 만큼은 남긴다.
      let x = Math.min(old.x1, this.lanes[0]?.x ?? old.x1);
      this.lanes.forEach((l, li) => {
        const mine = cols.map((_, ci) => ci).filter((ci) => colMajor[ci] === li);
        const start = snap(x);
        let cx = start + GRID;
        mine.forEach((ci, k) => { if (k) cx += GAP_MINOR; colX[ci] = cx; cx += colW[ci]; });
        l.x = start;
        l.w = Math.max(160, snap(cx + GRID - start));
        x = l.x + l.w + GAP_MAJOR - 48;
      });
    } else {
      let x = old.x1;
      cols.forEach((c, ci) => {
        if (ci > 0) x += colMajor[ci] === colMajor[ci - 1] ? GAP_MINOR : GAP_MAJOR;
        colX[ci] = x;
        x += colW[ci];
      });
    }

    // 세로 자리 — 쌓은 뒤, 이웃의 가운데로 끌어당기고 겹침만 푼다.
    const pos = new Map();
    const colH = cols.map((c) => c.reduce((s, n) => s + n._h, 0) + GAP_Y * (c.length - 1));
    const tallest = Math.max(0, ...colH);
    cols.forEach((c, ci) => {
      let y = top + (tallest - colH[ci]) / 2;
      for (const n of c) { pos.set(n.id, { x: colX[ci], y }); y += n._h + GAP_Y; }
    });
    // 줄 안의 칸은 촘촘히 붙여 둔 채 줄째 위아래로만 옮긴다 — 칸마다 이웃 쪽으로 끌면 모두 한가운데로 몰려 납작해진다.
    const mid = (id) => pos.get(id).y + this.byId.get(id)._h / 2;
    for (let pass = 0; pass < 4; pass++) {
      cols.forEach((c) => {
        let sum = 0, cnt = 0;
        for (const n of c) {
          const nb = [...inn.get(n.id), ...out.get(n.id)].filter((x) => pos.has(x) && !c.includes(this.byId.get(x)));
          if (!nb.length) continue;
          sum += nb.reduce((s2, x) => s2 + mid(x), 0) / nb.length - mid(n.id);
          cnt++;
        }
        if (!cnt) return;
        const shift = sum / cnt;
        for (const n of c) pos.get(n.id).y += shift;
      });
    }
    // 위로 삐져나간 만큼 통째로 내린다.
    const minY = Math.min(...[...pos.values()].map((p) => p.y));
    if (minY < top) for (const p of pos.values()) p.y += top - minY;

    // 이어진 데가 없는 칸은 아래에 바둑판으로.
    if (loose.length) {
      const bottom = pos.size ? Math.max(...[...pos].map(([id, p]) => p.y + this.byId.get(id)._h)) + 80 : top;
      const per = Math.max(1, Math.min(8, Math.ceil(Math.sqrt(loose.length * 2))));
      const w = Math.max(...loose.map((n) => n._w)) + 40;
      loose.sort((a, b) => a.id.localeCompare(b.id));
      let rowY = bottom, rowH = 0;
      loose.forEach((n, i) => {
        if (i && i % per === 0) { rowY += rowH + GAP_Y; rowH = 0; }
        pos.set(n.id, { x: old.x1 + (i % per) * w, y: rowY });
        rowH = Math.max(rowH, n._h);
      });
    }

    for (const n of set) {
      const p = pos.get(n.id);
      if (!p) continue;
      n.position = { x: snap(p.x), y: snap(p.y) };
      const d = this.nodeEls.get(n.id);
      d.style.left = n.position.x + 'px';
      d.style.top = n.position.y + 'px';
    }

    for (const e of this.edges) {
      if (!ids.has(e.source) || !ids.has(e.target)) continue;
      e.sourceHandle = null;
      e.targetHandle = null;
      if (e.data?.pts) delete e.data.pts;
    }

    this.renderLanes();
    for (const e of this.edges) this.drawEdge(e);
    this.paint();
    requestAnimationFrame(() => this.fit());
    this.changed();
    return set.length;
  }

  /** 펼치기 바로 전으로. 그 뒤에 옮긴 것도 함께 되돌아간다. */
  undoArrange() {
    const u = this._undo;
    if (!u) return false;
    this._undo = null;
    for (const [id, p] of u.pos) {
      const n = this.byId.get(id);
      if (!n) continue;
      n.position = p;
      const d = this.nodeEls.get(id);
      if (d) { d.style.left = p.x + 'px'; d.style.top = p.y + 'px'; }
    }
    const back = new Map(u.edges.map((x) => [x[0], x]));
    for (const e of this.edges) {
      const x = back.get(e.id);
      if (!x) continue;
      e.sourceHandle = x[1];
      e.targetHandle = x[2];
      if (x[3]) e.data.pts = x[3]; else delete e.data.pts;
    }
    this.lanes = u.lanes;
    this.renderLanes();
    for (const e of this.edges) this.drawEdge(e);
    this.paint();
    requestAnimationFrame(() => this.fit());
    this.changed();
    return true;
  }

  /** 대상 DB 에서 받은 설명을 비어 있는 칸에만 채운다 — 사람이 적은 설명은 덮지 않는다. */
  fillComments(json) {
    const map = JSON.parse(json || '{}');
    let n = 0;
    for (const node of this.nodes) {
      const kr = map[node.id.toUpperCase()] ?? map[node.id];
      if (kr && !node.data.kr) {
        node.data.kr = kr;
        const d = this.nodeEls.get(node.id);
        if (d) this.fillNode(d, node);
        n++;
      }
    }
    if (n) this.changed();
    return n;
  }

  // ───────────────────────── 경유점 ─────────────────────────

  drawPts() {
    this.ptLayer.innerHTML = '';
    if (this.sel?.kind !== 'edge') return;
    const e = this.edges.find((x) => x.id === this.sel.id);
    if (!e || !e._mids) return;
    const pts = e.data?.pts || [];
    pts.forEach((p, i) => this.ptLayer.append(ptEl(p, 'tl-pt', { pt: i })));
    // 구간 한가운데에 새 점의 씨앗을 둔다 — 끌면 그 자리에 경유점이 생긴다.
    e._mids.forEach((p, i) => this.ptLayer.append(ptEl(p, 'tl-pt tl-pt--seed', { seed: i })));
  }

  // ───────────────────────── 손짓 ─────────────────────────

  bind() {
    const h = this.host;
    this.onWheel = (ev) => {
      ev.preventDefault();
      const box = h.getBoundingClientRect();
      this.zoom(ev.deltaY < 0 ? 1.12 : 1 / 1.12, ev.clientX - box.left, ev.clientY - box.top);
    };
    h.addEventListener('wheel', this.onWheel, { passive: false });

    this.onDown = (ev) => {
      if (ev.button !== 0) return;
      const t = ev.target;
      h.focus({ preventScroll: true });

      if (t.closest('.tl-map')) { this.mapDrag(ev); return; }
      const lane = t.closest('.tl-lane__head');
      if (lane) { this.dotnet.invokeMethodAsync('NotifyLane', +lane.dataset.lane).catch(() => {}); return; }

      const pt = t.closest('.tl-pt');
      if (pt) { this.ptDrag(ev, pt); return; }

      const size = t.closest('.tl-node__size');
      if (size) { this.sizeDrag(ev, size.closest('.tl-node')); return; }

      const nodeEl = t.closest('.tl-node');
      if (nodeEl) { this.nodeDown(ev, nodeEl.dataset.id); return; }

      const edgeEl = t.closest('.tl-e');
      if (edgeEl) { this.select({ kind: 'edge', id: edgeEl.dataset.id }); return; }

      this.panDrag(ev);
    };
    h.addEventListener('pointerdown', this.onDown);

    this.onDbl = (ev) => {
      const size = ev.target.closest('.tl-node__size');
      if (size) {
        const id = size.closest('.tl-node').dataset.id;
        const n = this.byId.get(id);
        delete n.data.w; delete n.data.h;
        this.fillNode(this.nodeEls.get(id), n);
        this.measure(n);
        this.redrawEdgesOf(id);
        this.changed();
        return;
      }
      const pt = ev.target.closest('.tl-pt');
      if (pt && pt.dataset.pt != null) {
        const e = this.edges.find((x) => x.id === this.sel?.id);
        if (!e?.data?.pts) return;
        e.data.pts.splice(+pt.dataset.pt, 1);
        if (!e.data.pts.length) delete e.data.pts;
        this.drawEdge(e);
        this.drawPts();
        this.changed();
      }
    };
    h.addEventListener('dblclick', this.onDbl);

    this.onOver = (ev) => {
      if (!this.filter.hover || this.sel?.kind === 'node' || this.linking) return;
      const n = ev.target.closest?.('.tl-node');
      const id = n ? n.dataset.id : null;
      if (id === this.hoverId) return;
      this.hoverId = id;
      clearTimeout(this.hoverTimer);
      // 칸 위를 스쳐 지나가기만 해도 543칸을 다시 칠하지 않게 잠깐 머문 뒤에 칠한다.
      this.hoverTimer = setTimeout(() => this.paint(), id ? 90 : 160);
    };
    h.addEventListener('pointerover', this.onOver);

    this.onKey = (ev) => {
      if (ev.target.closest('input,textarea,select,[contenteditable]') && ev.target !== h) return;
      if (ev.key === 'Escape') {
        if (this.linking) this.dotnet.invokeMethodAsync('NotifyLinking', false).catch(() => {});
        this.setLinking(false);
        this.select(null);
      } else if ((ev.key === 'Delete' || ev.key === 'Backspace') && this.sel) {
        ev.preventDefault();
        this.dotnet.invokeMethodAsync('NotifyDeleteKey').catch(() => {});
      } else if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === 's') {
        ev.preventDefault();
        this.dotnet.invokeMethodAsync('NotifySave').catch(() => {});
      }
    };
    h.addEventListener('keydown', this.onKey);

    this.ro = new ResizeObserver(() => this.drawMap());
    this.ro.observe(h);
  }

  drag(ev, move, up) {
    const id = ev.pointerId;
    const mv = (e) => { if (e.pointerId === id) move(e); };
    const u = (e) => {
      if (e.pointerId !== id) return;
      window.removeEventListener('pointermove', mv);
      window.removeEventListener('pointerup', u);
      window.removeEventListener('pointercancel', u);
      up?.(e);
    };
    // 창에 단다 — 칸 밖으로 손이 벗어나도 끌기가 끊기지 않게.
    window.addEventListener('pointermove', mv);
    window.addEventListener('pointerup', u);
    window.addEventListener('pointercancel', u);
  }

  panDrag(ev) {
    const x0 = ev.clientX, y0 = ev.clientY, v0 = { ...this.view };
    let moved = false;
    this.host.classList.add('tl--panning');
    this.drag(ev, (e) => {
      if (Math.abs(e.clientX - x0) + Math.abs(e.clientY - y0) > 3) moved = true;
      this.view = { ...v0, x: v0.x + e.clientX - x0, y: v0.y + e.clientY - y0 };
      this.applyView();
    }, () => {
      this.host.classList.remove('tl--panning');
      if (!moved && this.sel) this.select(null);
    });
  }

  mapDrag(ev) {
    const go = (e) => {
      const r = this.map.getBoundingClientRect();
      const { s, ox, oy } = this._mapT || {};
      if (!s) return;
      const wx = (e.clientX - r.left - ox) / s, wy = (e.clientY - r.top - oy) / s;
      const box = this.host.getBoundingClientRect();
      this.view = { ...this.view, x: box.width / 2 - wx * this.view.z, y: box.height / 2 - wy * this.view.z };
      this.applyView();
    };
    go(ev);
    this.drag(ev, go);
  }

  nodeDown(ev, id) {
    if (this.linking) {
      if (!this.linkFrom) {
        this.linkFrom = id;
        this.banner.textContent = `잇기 — ${id} 에서 도착 칸을 누르십시오 (Esc 로 멈춤)`;
        this.paint();
      } else {
        const from = this.linkFrom;
        this.linkFrom = null;
        this.banner.textContent = '잇기 — 출발 칸을 누르십시오 (Esc 로 멈춤)';
        this.link(from, id);
      }
      return;
    }
    if (this.filter.picking) {
      this.dotnet.invokeMethodAsync('NotifyPick', id).catch(() => {});
      return;
    }
    const n = this.byId.get(id);
    const x0 = ev.clientX, y0 = ev.clientY, p0 = { ...n.position };
    let moved = false;
    if (!(this.sel?.kind === 'node' && this.sel.id === id)) this.select({ kind: 'node', id });
    this.drag(ev, (e) => {
      const dx = (e.clientX - x0) / this.view.z, dy = (e.clientY - y0) / this.view.z;
      if (!moved && Math.abs(dx) + Math.abs(dy) < 4) return;
      moved = true;
      n.position = { x: snap(p0.x + dx), y: snap(p0.y + dy) };
      const d = this.nodeEls.get(id);
      d.style.left = n.position.x + 'px';
      d.style.top = n.position.y + 'px';
      this.redrawEdgesOf(id);
    }, () => {
      if (moved) { this.changed(); this.drawMap(); }
    });
  }

  sizeDrag(ev, nodeEl) {
    ev.stopPropagation();
    const id = nodeEl.dataset.id;
    const n = this.byId.get(id);
    const w0 = nodeEl.offsetWidth, h0 = nodeEl.offsetHeight, x0 = ev.clientX, y0 = ev.clientY;
    this.drag(ev, (e) => {
      n.data.w = Math.max(MIN_W, snap(w0 + (e.clientX - x0) / this.view.z));
      n.data.h = Math.max(MIN_H, snap(h0 + (e.clientY - y0) / this.view.z));
      nodeEl.style.width = n.data.w + 'px';
      nodeEl.style.height = n.data.h + 'px';
      this.measure(n);
      this.redrawEdgesOf(id);
    }, () => this.changed());
  }

  ptDrag(ev, ptNode) {
    const e = this.edges.find((x) => x.id === this.sel?.id);
    if (!e) return;
    e.data.pts = e.data.pts || [];
    let idx;
    if (ptNode.dataset.seed != null) {
      // 씨앗은 i 번째 구간의 가운데다 — 경유점 i 자리에 끼워 넣는다.
      idx = +ptNode.dataset.seed;
      e.data.pts.splice(idx, 0, { ...e._mids[idx] });
    } else {
      idx = +ptNode.dataset.pt;
    }
    this.drag(ev, (m) => {
      const w = this.toWorld(m.clientX, m.clientY);
      e.data.pts[idx] = { x: snap(w.x), y: snap(w.y) };
      this.drawEdge(e);
      this.drawPts();
    }, () => { this.changed(); this.reselect(); });
  }

  destroy() {
    this.host.removeEventListener('wheel', this.onWheel);
    this.host.removeEventListener('pointerdown', this.onDown);
    this.host.removeEventListener('dblclick', this.onDbl);
    this.host.removeEventListener('pointerover', this.onOver);
    this.host.removeEventListener('keydown', this.onKey);
    this.ro?.disconnect();
    clearTimeout(this.hoverTimer);
    this.host.innerHTML = '';
  }
}

function el(tag, cls) {
  const d = document.createElement(tag);
  if (cls) d.className = cls;
  return d;
}

function ptEl(p, cls, data) {
  const d = el('span', cls);
  d.style.left = p.x + 'px';
  d.style.top = p.y + 'px';
  for (const [k, v] of Object.entries(data)) d.dataset[k] = v;
  d.title = data.seed != null ? '끌면 선이 휩니다' : '끌어 옮기고, 두 번 누르면 지웁니다';
  return d;
}

/** 끝점이 칸의 면에서 그 방향으로 나가야 선이 칸에 붙어 꺾이지 않는다. */
function lead(p, d) {
  switch (p.side) {
    case 'l': return { x: p.x - d, y: p.y };
    case 'r': return { x: p.x + d, y: p.y };
    case 't': return { x: p.x, y: p.y - d };
    default: return { x: p.x, y: p.y + d };
  }
}

function cubicAt(p1, c1, c2, p2, t) {
  const u = 1 - t, a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
  return { x: a * p1.x + b * c1.x + c * c2.x + d * p2.x, y: a * p1.y + b * c1.y + c * c2.y + d * p2.y };
}

/**
 * 경유점이 없으면 끝점 둘 사이의 베지어, 있으면 Catmull-Rom 으로 점을 「정확히 지나며」 휜다.
 * 각 구간의 한가운데(t=0.5)도 돌려준다 — 휜 선에서는 두 점의 평균이 선 위가 아니다.
 */
function pathOf(p1, p2, pts) {
  if (!pts.length) {
    const dist = Math.max(30, Math.min(160, Math.hypot(p2.x - p1.x, p2.y - p1.y) * 0.4));
    const c1 = lead(p1, dist), c2 = lead(p2, dist);
    const mid = cubicAt(p1, c1, c2, p2, 0.5);
    return { d: `M ${p1.x},${p1.y} C ${c1.x},${c1.y} ${c2.x},${c2.y} ${p2.x},${p2.y}`, mid, mids: [mid] };
  }
  const ps = [p1, lead(p1, 24), ...pts, lead(p2, 24), p2];
  let d = `M ${ps[0].x},${ps[0].y}`;
  const mids = [];
  for (let i = 0; i < ps.length - 1; i++) {
    const a0 = ps[i - 1] ?? ps[i], a1 = ps[i], a2 = ps[i + 1], a3 = ps[i + 2] ?? a2;
    const c1 = { x: a1.x + (a2.x - a0.x) / 6, y: a1.y + (a2.y - a0.y) / 6 };
    const c2 = { x: a2.x - (a3.x - a1.x) / 6, y: a2.y - (a3.y - a1.y) / 6 };
    d += ` C ${c1.x},${c1.y} ${c2.x},${c2.y} ${a2.x},${a2.y}`;
    // 짧은 들머리·날머리 구간은 씨앗 자리에서 뺀다 — 칸 테두리에 붙은 점은 잡기 어렵다.
    if (i >= 1 && i <= ps.length - 3) mids.push(cubicAt(a1, c1, c2, a2, 0.5));
  }
  const mid = mids[Math.floor(mids.length / 2)] ?? pts[0];
  return { d, mid, mids };
}
