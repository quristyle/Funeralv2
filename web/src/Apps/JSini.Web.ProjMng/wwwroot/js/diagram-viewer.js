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
 * Vue 원본과 같다 — 도형을 끌어 옮기고, 크기를 바꾸고, 도형끼리 선으로 잇는다.
 * 이름·설명은 여기서 고치지 않는다(setCellsEditable(false)). 그 값들은 DB 메타가
 * 정본이고, 다이어그램이 저장하는 것은 배치(좌표·크기)와 관계선뿐이다.
 */
import {
  Graph,
  InternalEvent,
  RubberBandHandler,
} from '../lib/maxgraph/esm/index.js';

const DEFAULT_W = 180;
const DEFAULT_H = 60;

/**
 * 컨테이너에 그래프를 만든다. Blazor 가 돌려받은 객체 참조로
 * load / save / zoomIn / zoomOut / fit / destroy 를 부른다.
 * @param {HTMLElement} container
 */
export function create(container) {
  InternalEvent.disableContextMenu(container);

  const graph = new Graph(container);
  graph.setPanning(true);
  graph.setConnectable(true);
  graph.setCellsEditable(false);
  graph.setCellsResizable(true);
  graph.setAllowDanglingEdges(false);
  graph.setHtmlLabels(true);
  new RubberBandHandler(graph);

  /** 셀 → 엔터티. 저장할 때 좌표를 되돌려 담기 위해 들고 있는다. */
  const cellToEntity = new Map();

  return {
    /** 모델을 캔버스에 그린다. 기존 도형은 모두 지운다. */
    load(model) {
      cellToEntity.clear();
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

          const label = entity.desc
            ? `${entity.name}\n${entity.desc}`
            : entity.name || entity.id;

          const cell = graph.insertVertex({
            parent: graph.getDefaultParent(),
            position: [x, y],
            size: [entity.w || DEFAULT_W, entity.h || DEFAULT_H],
            style: {
              fillColor: '#ffffff',
              strokeColor: '#8c8c8c',
              fontColor: '#262626',
              rounded: true,
              whiteSpace: 'wrap',
            },
            value: label,
          });

          byId.set(entity.id, cell);
          cellToEntity.set(cell, entity);
        });

        (model?.relations ?? []).forEach((relation) => {
          const source = byId.get(relation.from);
          const target = byId.get(relation.to);
          if (!source || !target) return;

          graph.insertEdge({
            parent: graph.getDefaultParent(),
            source,
            target,
            value: relation.label ?? '',
          });
        });
      } finally {
        graph.getDataModel().endUpdate();
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
        const geo = graph.getCellGeometry(cell);
        entities.push({
          ...entity,
          x: Math.round(geo?.x ?? entity.x ?? 0),
          y: Math.round(geo?.y ?? entity.y ?? 0),
          w: Math.round(geo?.width ?? entity.w ?? DEFAULT_W),
          h: Math.round(geo?.height ?? entity.h ?? DEFAULT_H),
        });
        idOfCell.set(cell, entity.id);
      });

      graph
        .getChildCells(graph.getDefaultParent(), false, true)
        .forEach((edge) => {
          const from = idOfCell.get(edge.source);
          const to = idOfCell.get(edge.target);
          if (!from || !to) return;
          relations.push({ from, to, label: String(edge.value ?? '') });
        });

      return { entities, relations };
    },

    zoomIn() {
      graph.zoomIn();
    },

    zoomOut() {
      graph.zoomOut();
    },

    fit() {
      graph.fit();
      graph.center();
    },

    /** 회로가 끊기거나 화면을 떠날 때 부른다. 안 부르면 DOM 과 리스너가 샌다. */
    destroy() {
      cellToEntity.clear();
      graph.destroy?.();
    },
  };
}
