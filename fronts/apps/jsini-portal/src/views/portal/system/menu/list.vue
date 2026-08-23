<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions } from '#/adapter/vxe-table';

import { onMounted, ref } from 'vue';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';

import { MenuBadge } from '@vben-core/menu-ui';

import { Button, message, Tooltip } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  deleteMenu,
  getMenuList,
  reorderMenus,
  SystemMenuApi,
  updateMenu,
} from '#/api/portal/system/menu';
import I18nEditModal from '#/components/i18n/I18nEditModal.vue';
import { $t } from '#/locales';

import { useColumns } from './data';
import MenuForm from './modules/form.vue';

/**
 * [메뉴 관리]
 *
 * 예전에는 '그리드 뷰'와 '트리 뷰' 두 탭으로 나뉘어 있었다.
 * 계층 편집은 트리에서, 속성 확인은 그리드에서 하도록 갈라놓은 것인데,
 * 상태가 이중이 되고 트리 쪽 드래그가 눈에 띄게 느렸다.
 * 이제 vxe-table 트리 그리드 하나로 합쳤고, 느렸던 원인도 함께 걷어냈다.
 *
 *  1. 드롭한 자리를 화면에서 먼저 확정하고 저장은 뒤에서 한다.
 *     서버 왕복과 전체 재조회를 기다리지 않으므로 드래그가 즉시 반응한다.
 *     저장에 실패하면 그때만 서버 상태로 되돌린다.
 *  2. 형제들의 바뀐 순번을 한 번에 모아 보낸다(`/system/menu/reorder`).
 *  3. 행마다 붙던 Tooltip 을 걷어냈다(data.ts 참고).
 *
 * 두 탭에 흩어져 있던 기능은 모두 남아 있다.
 * 계층 표시·전체 펼침/접기·드래그로 부모와 순서 변경·하위 추가·수정·삭제·
 * 다국어 편집·배지 표시, 그리고 그리드에만 있던 셀 인라인 편집까지.
 *
 * 가상 스크롤(`scrollY`)은 켜지 않았다. 켜려면 평면 데이터 + `treeConfig.transform`
 * 조합이 필요한데, `height: 'auto'` 로 부모 높이를 따라가는 지금 구조에서는
 * 컨테이너 높이가 0 으로 잡히는 순간 행이 하나도 그려지지 않는다.
 * 실제 화면에서 높이를 확인한 뒤에 다시 손대는 편이 안전하다.
 */

const [FormDrawer, formDrawerApi] = useVbenDrawer({
  connectedComponent: MenuForm,
  destroyOnClose: true,
});

const i18nEditModalRef = ref<any>(null);
/** 순서 저장이 진행 중인지. 저장 중에는 다시 드래그해도 요청이 겹치지 않게 막는다. */
const savingOrder = ref(false);

/** 응답이 배열/`result`/`data.result` 중 무엇으로 와도 목록을 꺼낸다. */
function getMenuItems(response: any): SystemMenuApi.SystemMenu[] {
  if (Array.isArray(response)) return response;
  if (Array.isArray(response?.result)) return response.result;
  if (Array.isArray(response?.data?.result)) return response.data.result;
  return [];
}

/** 중첩 트리를 훑어 평면 목록으로 만든다. 기준선 기록·순번 계산에 쓴다. */
function flatten(
  list: SystemMenuApi.SystemMenu[],
  out: SystemMenuApi.SystemMenu[] = [],
) {
  for (const item of list) {
    out.push(item);
    const children = (item as any).children as
      | SystemMenuApi.SystemMenu[]
      | undefined;
    if (children?.length) flatten(children, out);
  }
  return out;
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridEvents: {
    /**
     * 셀을 고쳐 닫으면 그 행만 저장한다.
     * 값이 그대로면 요청을 보내지 않는다.
     */
    editClosed: async ({ column, row }: any) => {
      const field = column?.field;
      if (!field) return;

      const grid = gridApi.grid;
      if (!grid?.isUpdateByRow(row, field)) return;

      try {
        await updateMenu(row.id, toUpdatePayload(row));
        grid.reloadRow(row, {});
        message.success($t('ui.actionMessage.operationSuccess'));
      } catch (error) {
        console.error(error);
        message.error($t('ui.actionMessage.operationFailed'));
        await refresh();
      }
    },

    /**
     * 드래그로 자리를 옮겼을 때.
     *
     * vxe-table 이 이미 화면상의 배치를 바꿔 놓은 뒤에 불린다.
     * 그래서 여기서는 화면에 보이는 그대로를 읽어 바뀐 것만 서버에 보낸다.
     * 화면을 다시 그리거나 목록을 다시 받아오지 않는다 — 이게 체감 속도의 핵심이다.
     */
    rowDragend: async () => {
      if (savingOrder.value) return;

      const changed = collectOrderChanges();
      if (changed.length === 0) return;

      savingOrder.value = true;
      try {
        await reorderMenus(changed);
        // 저장에 성공하면 방금 보낸 값이 곧 서버 값이다. 기준선을 갱신해 둔다.
        for (const item of changed) {
          const base = orderBaseline.get(item.id);
          if (base) {
            base.pid = item.pid;
            base.orderNo = item.orderNo;
          }
        }
        message.success('메뉴 위치가 저장되었습니다.');
      } catch (error) {
        console.error(error);
        message.error('메뉴 위치 저장에 실패했습니다. 원래 위치로 되돌립니다.');
        await refresh();
      } finally {
        savingOrder.value = false;
      }
    },
  },
  gridOptions: {
    columns: useColumns(onActionClick, onStatusToggle),
    // 셀을 두 번 눌러 바로 고친다. 예전 그리드 뷰의 동작을 그대로 뒀다.
    editConfig: {
      mode: 'cell',
      showStatus: true,
      trigger: 'dblclick',
    },
    height: 'auto',
    // 트리 그리드이므로 페이지네이션은 사용하지 않는다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      // 응답은 { result: [...중첩 트리...] } 형태로 온다(다른 목록 화면과 동일).
      // 목록의 위치를 'result' 로 명시해, 페이저 유무와 상관없이 안전하게 목록을 꺼낸다.
      // (이전에는 query 에서 배열만 반환해 vxe 가 result 를 찾지 못하고 0 행이 되었다.)
      response: { list: 'result' },
      ajax: {
        query: async () => {
          const tree = getMenuItems(await getMenuList());
          // 드래그 전 기준선(부모·형제 순번)을 중첩 트리에서 위치 기반으로 기록한다.
          captureOrderBaseline(tree);
          // treeConfig.transform=true 는 "평면 데이터"를 받아 pid 로 트리를 조립한다.
          // (vxe 의 행 드래그는 transform=true 에서만 실제 이동이 반영된다.)
          // 모든 노드를 평면으로 펼치고, 각 노드가 들고 있던 children 은 제거한다.
          const flat = flatten(tree).map((node) => {
            const { children: _children, ...rest } = node as any;
            return rest as SystemMenuApi.SystemMenu;
          });
          return { result: flat };
        },
      },
    },
    rowConfig: {
      // 행 드래그의 마스터 스위치. 이 값이 없으면 dragSort 컬럼과 rowDragConfig 가
      // 있어도 vxe 가 드래그 핸들·이벤트를 전혀 연결하지 않아 드래그가 동작하지 않는다.
      drag: true,
      isHover: true,
      keyField: 'id',
    },
    // 드래그 손잡이는 맨 앞 `dragSort` 컬럼이다(data.ts).
    rowDragConfig: {
      isCrossDrag: true, // 다른 부모 밑으로 옮길 수 있다
      isPeerDrag: false, // 같은 부모 안으로만 제한하지 않는다
      isToChildDrag: true, // 다른 메뉴의 하위로 넣을 수 있다
      showGuidesStatus: true, // 드롭 위치 안내선
      trigger: 'cell',
    },
    treeConfig: {
      childrenField: 'children',
      expandAll: true,
      parentField: 'pid',
      rowField: 'id',
      // 평면 데이터를 pid 로 트리로 조립한다.
      // vxe 의 행 드래그(순서·부모 변경)와 드롭 위치 안내선은 transform=true 에서만
      // 실제 데이터에 반영된다(transform=false 이면 드롭이 무시된다).
      transform: true,
    },
  } as VxeTableGridOptions,
});

/**
 * 서버에서 받은 시점의 부모·순번을 기억해 둔다.
 * 드래그 뒤에 이 기준선과 비교해 "실제로 바뀐 행"만 골라 보낸다.
 */
const orderBaseline = new Map<string, { orderNo: number; pid: null | string }>();

function captureOrderBaseline(tree: SystemMenuApi.SystemMenu[]) {
  orderBaseline.clear();
  // collectOrderChanges 와 동일하게 "형제 안에서의 위치(0부터)"를 순번으로 삼는다.
  // meta.order 값이 아니라 위치를 기준으로 맞춰야 드래그 후 오탐(변경으로 오인)이 없다.
  const walk = (nodes: SystemMenuApi.SystemMenu[], pid: null | string) => {
    nodes.forEach((node, orderNo) => {
      orderBaseline.set(node.id, { orderNo, pid: pid ?? null });
      const children = (node as any).children as
        | SystemMenuApi.SystemMenu[]
        | undefined;
      if (children?.length) walk(children, node.id);
    });
  };
  walk(tree, null);
}

/**
 * 지금 화면에 보이는 배치를 읽어, 기준선과 달라진 행만 추린다.
 *
 * vxe-table 은 드래그 결과를 자기 데이터(중첩 `children`)에 이미 반영해 둔다.
 * 그 트리를 그대로 훑으면서 부모별로 0부터 순번을 다시 매기면 저장할 값이 나온다.
 * 기준선(서버에서 받은 시점의 부모·순번)과 다른 행만 골라 보낸다.
 */
function collectOrderChanges() {
  const grid = gridApi.grid;
  if (!grid) return [];

  const roots: any[] = grid.getTableData()?.fullData ?? [];
  const changed: { id: string; orderNo: number; pid: null | string }[] = [];

  const walk = (nodes: any[], pid: null | string) => {
    nodes.forEach((row, orderNo) => {
      // 드래그로 부모가 바뀌었어도 행의 pid 가 갱신되지 않을 수 있어
      // 트리에서의 실제 위치를 기준으로 삼는다.
      const base = orderBaseline.get(row.id);
      if (!base || base.pid !== pid || base.orderNo !== orderNo) {
        changed.push({ id: row.id, orderNo, pid });
      }

      // 화면에 보이는 값도 맞춰 둔다. 순서 컬럼이 실제와 어긋나지 않게.
      row.pid = pid;
      if (row.meta) row.meta.order = orderNo;

      if (row.children?.length) walk(row.children, row.id);
    });
  };

  walk(roots, null);
  return changed;
}

/** 인라인 편집 결과를 수정 API 형식으로 옮긴다. */
function toUpdatePayload(row: SystemMenuApi.SystemMenu) {
  return {
    authCode: row.authCode,
    component: row.component,
    meta: { ...row.meta },
    name: row.name,
    path: row.path,
    pid: row.pid,
    redirect: row.redirect,
    status: row.status,
    type: row.type,
  } as any;
}

/**
 * 상태 배지를 눌렀을 때. 그 자리에서 활성 ↔ 비활성을 바꾼다.
 *
 * **켤 때는 비활성인 상위 메뉴까지 함께 켠다.** 메뉴 조회 API 는 비활성 메뉴를 아예
 * 내려주지 않아서, 이 메뉴만 켜면 부모 가지가 끊겨 사이드바에 나타나지 않는다.
 * 위에서 아래로 순서대로 저장한다.
 *
 * 끌 때는 하위 메뉴를 건드리지 않는다. 부모가 꺼지면 그 아래는 트리에서 함께 사라지므로
 * 굳이 자식까지 꺼서 되돌리기 어렵게 만들 이유가 없다.
 *
 * 화면을 먼저 바꾸고 저장은 뒤에서 한다(드래그와 같은 방식).
 * 실패하면 되돌린 뒤 서버 상태로 다시 맞춘다.
 */
async function onStatusToggle(row: SystemMenuApi.SystemMenu) {
  if ((row as any).__statusSaving) return;

  const next = row.status === 1 ? 0 : 1;
  // 켜는 경우에만 상위 메뉴를 함께 담는다. 위(먼 조상)부터 저장하도록 역순으로 둔다.
  const ancestors =
    next === 1 ? collectInactiveAncestors(row).toReversed() : [];
  const targets = [...ancestors, row];

  const before = new Map(targets.map((t) => [t.id, t.status]));
  targets.forEach((t) => {
    (t as any).__statusSaving = true;
    t.status = next;
  });

  try {
    for (const target of targets) {
      await updateMenu(target.id, {
        ...toUpdatePayload(target),
        status: next,
      });
    }

    message.success(
      ancestors.length > 0
        ? `상위 메뉴 ${ancestors.length}건을 함께 활성으로 바꿨습니다.`
        : $t('ui.actionMessage.operationSuccess'),
    );
  } catch (error) {
    console.error(error);
    // 여러 건을 저장하다 중간에 실패하면 일부만 반영된 상태가 된다.
    // 화면을 먼저 되돌려 즉시 반응하게 하고, 곧바로 서버 값으로 다시 맞춘다.
    targets.forEach((t) => {
      t.status = before.get(t.id) ?? t.status;
    });
    message.error($t('ui.actionMessage.operationFailed'));
    await refresh();
  } finally {
    targets.forEach((t) => {
      (t as any).__statusSaving = false;
    });
  }
}

/** 비활성 상태인 조상들. 가까운 것부터 담는다. 없으면 빈 배열. */
function collectInactiveAncestors(row: SystemMenuApi.SystemMenu) {
  const byId = new Map<string, SystemMenuApi.SystemMenu>();
  const walk = (nodes: any[]) => {
    nodes.forEach((node) => {
      byId.set(node.id, node);
      if (node.children?.length) walk(node.children);
    });
  };
  walk(gridApi.grid?.getTableData()?.fullData ?? []);

  const found: SystemMenuApi.SystemMenu[] = [];
  const seen = new Set<string>([row.id]);
  let parent = row.pid ? byId.get(row.pid) : undefined;

  while (parent && !seen.has(parent.id)) {
    seen.add(parent.id);
    if (parent.status === 0) found.push(parent);
    parent = parent.pid ? byId.get(parent.pid) : undefined;
  }
  return found;
}

/** 목록을 다시 받아온다. 저장 실패를 되돌릴 때와 등록/삭제 후에 쓴다. */
async function refresh() {
  await gridApi.query();
}

function onExpandAll() {
  gridApi.grid?.setAllTreeExpand(true);
}

function onCollapseAll() {
  gridApi.grid?.setAllTreeExpand(false);
}

function onActionClick({
  code,
  row,
}: OnActionClickParams<SystemMenuApi.SystemMenu>) {
  switch (code) {
    case 'append': {
      onAppend(row);
      break;
    }
    case 'delete': {
      onDelete(row);
      break;
    }
    case 'edit': {
      onEdit(row);
      break;
    }
    case 'i18n': {
      onOpenI18nModal(row);
      break;
    }
    default: {
      break;
    }
  }
}

function onEdit(row: SystemMenuApi.SystemMenu) {
  formDrawerApi.setData(row).open();
}

function onCreate() {
  formDrawerApi.setData({}).open();
}

function onAppend(row: SystemMenuApi.SystemMenu) {
  formDrawerApi.setData({ pid: row.id }).open();
}

function onDelete(row: SystemMenuApi.SystemMenu) {
  deleteMenu(row.id)
    .then(() => {
      message.success({
        content: $t('ui.actionMessage.deleteSuccess', [row.name]),
      });
      refresh();
    })
    .catch((error) => {
      console.error(error);
    });
}

/** 다국어 편집 모달. 키가 바뀌면 메뉴의 title 도 함께 갱신한다. */
function onOpenI18nModal(row: SystemMenuApi.SystemMenu) {
  const key = row.meta?.title || `menu.title.${row.id}`;
  i18nEditModalRef.value?.open({
    category: 'menu',
    id: row.id,
    key,
    onSuccess: async (updatedKey: string) => {
      if (row.meta?.title !== updatedKey) {
        await updateMenu(row.id, {
          ...toUpdatePayload(row),
          meta: { ...row.meta, title: updatedKey },
        });
      }
      refresh();
    },
  });
}

onMounted(() => {
  refresh();
});
</script>

<template>
  <Page auto-content-height>
    <FormDrawer @success="refresh" />

    <Grid>
      <!-- 드래그 손잡이 -->
      <template #drag>
        <div
          class="flex h-full w-full cursor-move select-none items-center justify-center"
        >
          <IconifyIcon
            class="pointer-events-none text-muted-foreground"
            icon="lucide:grip-vertical"
          />
        </div>
      </template>

      <template #toolbar-tools>
        <div class="flex gap-2">
          <Tooltip :title="$t('common.expandAll')">
            <Button @click="onExpandAll">
              <template #icon>
                <IconifyIcon icon="ant-design:expand-outlined" />
              </template>
            </Button>
          </Tooltip>
          <Tooltip :title="$t('common.collapseAll')">
            <Button @click="onCollapseAll">
              <template #icon>
                <IconifyIcon icon="ant-design:compress-outlined" />
              </template>
            </Button>
          </Tooltip>
          <Button v-perm:create type="primary" @click="onCreate">
            <Plus class="size-5" />
            {{ $t('ui.actionTitle.create', [$t('system.menu.name')]) }}
          </Button>
        </div>
      </template>

      <!-- 메뉴명: 아이콘 + 제목 + 배지 -->
      <template #title="{ row }">
        <div class="group flex w-full items-center gap-1">
          <div class="size-5 shrink-0">
            <IconifyIcon
              v-if="row.type === 'BUTTON'"
              class="size-full"
              icon="carbon:security"
            />
            <IconifyIcon
              v-else-if="row.meta?.icon"
              class="size-full"
              :icon="row.meta?.icon || 'carbon:circle-dash'"
            />
          </div>
          <span class="flex-auto">{{ $t(row.meta?.title) }}</span>
        </div>
        <MenuBadge
          v-if="row.meta?.badgeType"
          :badge="row.meta.badge"
          :badge-type="row.meta.badgeType"
          :badge-variants="row.meta.badgeVariants"
          class="menu-badge"
        />
      </template>
    </Grid>

    <!-- 다국어 편집 공통 모달 -->
    <I18nEditModal ref="i18nEditModalRef" @success="() => refresh()" />
  </Page>
</template>

<style lang="scss" scoped>
.vxe-grid {
  user-select: none; /* 드래그 중 텍스트가 잡히지 않게 */
}

.menu-badge {
  top: 50%;
  right: 0;
  transform: translateY(-50%);

  & > :deep(div) {
    padding-top: 0;
    padding-bottom: 0;
  }
}
</style>
