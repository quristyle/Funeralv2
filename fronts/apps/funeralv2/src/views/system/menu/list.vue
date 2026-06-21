<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions, } from '#/adapter/vxe-table';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';
import { onMounted, ref, watch } from 'vue';

import { MenuBadge } from '@vben-core/menu-ui';

import { Button, message, Tooltip, Tabs, Tree, Popconfirm } from 'ant-design-vue';
import type { TreeProps } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteMenu, getMenuList, moveMenu, SystemMenuApi, updateMenu } from '#/api/system/menu';
import { $t } from '#/locales';
import I18nEditModal from '#/components/i18n/I18nEditModal.vue';

import { useColumns } from './data';
import MenuForm from './modules/form.vue';

const [FormDrawer, formDrawerApi] = useVbenDrawer({ connectedComponent: MenuForm, destroyOnClose: true, });
const activeTab = ref('grid');
const treeData = ref<any[]>([]);
const expandedKeys = ref<any[]>([]);

function getMenuItems(response: any): SystemMenuApi.SystemMenu[] {
  if (Array.isArray(response)) {
    return response;
  }

  if (Array.isArray(response?.result)) {
    return response.result;
  }

  if (Array.isArray(response?.data?.result)) {
    return response.data.result;
  }

  return [];
}

/**
 * API에서 가져온 메뉴 데이터를 Tree 컴포넌트 형식으로 변환
 */
function mapToTree(list: SystemMenuApi.SystemMenu[]): any[] {
  return list.map((item) => ({
    ...item,
    key: item.id,
    title: $t(item.meta?.title || item.name),
    children: item.children ? mapToTree(item.children) : undefined,
    pid: item.pid,
    orderNo: item.meta?.order ?? 0,
    icon: item.meta?.icon,
    type: item.type,
  }));
}


/**
 * 모든 트리 키를 수집하는 헬퍼 함수
 */
function getAllKeys(list: any[]): any[] {
  const keys: any[] = [];
  function traverse(nodes: any[]) {
    for (const node of nodes) {
      keys.push(node.key);
      if (node.children && node.children.length > 0) {
        traverse(node.children);
      }
    }
  }
  traverse(list);
  return keys;
}

/**
 * 메뉴 목록 새로고침 및 트리 데이터 갱신
 * @param init true인 경우 전체 노드를 강제 확장합니다.
 */
async function onRefresh(init = false) {
  if (init) {
    if (activeTab.value === 'grid') {
      gridApi.query();
    } else {
      const list = getMenuItems(await getMenuList());
      treeData.value = mapToTree(list);
      expandedKeys.value = getAllKeys(treeData.value);
    }
    return;
  }

  // 현재 활성화된 탭만 조건부 재조회하여 서버 리소스 낭비 방지
  if (activeTab.value === 'grid') {
    gridApi.query();
  } else if (activeTab.value === 'tree') {
    const list = getMenuItems(await getMenuList());
    treeData.value = mapToTree(list);
  }
}

// 탭 전환 감시 및 전환 시 활성 탭 데이터 동기화 리프레시
watch(activeTab, (newTab) => {
  if (newTab === 'grid') {
    gridApi.query();
  } else if (newTab === 'tree') {
    getMenuList().then((response) => {
      treeData.value = mapToTree(getMenuItems(response));
      if (expandedKeys.value.length === 0) {
        expandedKeys.value = getAllKeys(treeData.value);
      }
    });
  }
});

function onTreeExpandAll() {
  expandedKeys.value = getAllKeys(treeData.value);
}

function onTreeCollapseAll() {
  expandedKeys.value = [];
}

/**
 * 트리 드래그 앤 드롭 처리
 */
const onDrop: TreeProps['onDrop'] = async (info) => {
  const dropKey = info.node.key;
  const dragKey = info.dragNode.key;
  const dropPos = info.node.pos ? info.node.pos.split('-') : [];
  const dropPosition = info.dropPosition - Number(dropPos[dropPos.length - 1] || 0);

  const targetNode = info.node;
  const targetPid = targetNode.pid || targetNode.dataRef?.pid || null;
  const targetOrderNo = typeof targetNode.orderNo === 'number'
    ? targetNode.orderNo
    : (typeof targetNode.dataRef?.orderNo === 'number' ? targetNode.dataRef.orderNo : 0);

  const newParentId = info.dropToGap ? targetPid : dropKey;

  let newOrderNo = 0;
  if (info.dropToGap) {
    if (dropPosition === -1) {
      newOrderNo = targetOrderNo;
    } else if (dropPosition === 1) {
      newOrderNo = targetOrderNo + 1;
    } else {
      newOrderNo = targetOrderNo;
    }
  } else {
    newOrderNo = 0;
  }

  try {
    await moveMenu(dragKey as string, newParentId as string, newOrderNo);
    message.success('메뉴가 이동되었습니다.');
    onRefresh();
  } catch (error) {
    message.error('메뉴 이동에 실패했습니다.');
  }
};

const [Grid, gridApi] = useVbenVxeGrid({
  gridEvents: {
    // 드래그 시작 시 이벤트 로그 확인
    rowDragstart: (params: any) => {
      console.log('Drag started:', params);
    },
    // 드래그 앤 드롭 종료 시 이벤트 핸들러
    rowDragend: async (params: any) => {
      const { dragRow, targetRow, dropType } = params;
      let newParentId = targetRow.pid;
      let newOrderNo = targetRow.meta?.order || 0;

      if (dropType === 'inner') {
        newParentId = targetRow.id;
        newOrderNo = 0;
      } else if (dropType === 'after') {
        newOrderNo = (targetRow.meta?.order || 0) + 1;
      } else if (dropType === 'before') {
        newOrderNo = targetRow.meta?.order || 0;
      }

      try {
        await moveMenu(dragRow.id, newParentId, newOrderNo);
        message.success('메뉴가 이동되었습니다.');
        onRefresh();
      } catch (error) {
        console.error(error);
        message.error('메뉴 이동에 실패했습니다.');
        onRefresh();
      }
    },
  },
  gridOptions: {
    columns: useColumns(onActionClick),
    height: 'auto',
    editConfig: {
      trigger: 'dblclick',
      mode: 'cell',
      showStatus: true,
    },
    mouseConfig: {
      selected: true,
      drag: true,
    },
    proxyConfig: {
      ajax: {
        query: async (_params) => {
         return await getMenuList();
        },
      },
    },
    rowConfig: {
      keyField: 'id',
      isHover: true,
    },
    treeConfig: {
      parentField: 'pid',
      rowField: 'id',
      transform: false,
    },
  } as VxeTableGridOptions,
});

/**
 * 모든 메뉴 노드를 확장합니다.
 */
function onExpandAll() { gridApi.grid?.setAllTreeExpand(true); }

/**
 * 모든 메뉴 노드를 축소합니다.
 */
function onCollapseAll() { gridApi.grid?.setAllTreeExpand(false); }

function onActionClick({ code, row, }: OnActionClickParams<SystemMenuApi.SystemMenu>) {
  switch (code) {
    case 'append': { onAppend(row); break; }
    case 'delete': { onDelete(row); break; }
    case 'edit': { onEdit(row); break; }
    default: { break; }
  }
}

function onEdit(row: SystemMenuApi.SystemMenu) { formDrawerApi.setData(row).open(); }
function onCreate() { formDrawerApi.setData({}).open(); }
function onAppend(row: SystemMenuApi.SystemMenu) { formDrawerApi.setData({ pid: row.id }).open(); }

function onDelete(row: SystemMenuApi.SystemMenu) {
  deleteMenu(row.id)
    .then(() => {
      message.success({
        content: $t('ui.actionMessage.deleteSuccess', [row.name]),
      });
      onRefresh();
    })
    .catch((error) => {
      console.error(error);
    });
}

const getPopupContainer = () => document.body;

const i18nEditModalRef = ref<any>(null);

/**
 * 다국어 편집 모달을 엽니다.
 */
function onOpenI18nModal(row: SystemMenuApi.SystemMenu) {
  const key = row.meta?.title || `menu.title.${row.id}`;
  i18nEditModalRef.value?.open({
    id: row.id,
    key,
    category: 'menu',
    onSuccess: async (updatedKey: string) => {
      if (row.meta?.title !== updatedKey) {
        const updateData = {
          name: row.name,
          path: row.path,
          pid: row.pid,
          type: row.type,
          authCode: row.authCode,
          redirect: row.redirect,
          meta: {
            ...row.meta,
            title: updatedKey
          },
          status: row.status,
        };
        await updateMenu(row.id, updateData);
      }
      onRefresh();
    }
  });
}

onMounted(() => {
    onRefresh(true);
});
</script>
<template>
  <Page auto-content-height>
    <FormDrawer @success="onRefresh" />
    <Tabs v-model:activeKey="activeTab" class="h-full px-4">
      <Tabs.TabPane key="grid" tab="그리드 뷰">
        <Grid>
          <template #drag>
            <div class="cursor-move flex items-center justify-center select-none h-full w-full">
                <IconifyIcon icon="lucide:grip-vertical" class="text-muted-foreground pointer-events-none" />
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
              <Button type="primary" @click="onCreate">
                <Plus class="size-5" />
                {{ $t('ui.actionTitle.create', [$t('system.menu.name')]) }}
              </Button>
            </div>
          </template>
          <template #title="{ row }">
            <div class="flex w-full items-center gap-1 group">
              <div class="size-5 shrink-0">
                <IconifyIcon
                  v-if="row.type === 'BUTTON'"
                  icon="carbon:security"
                  class="size-full"
                />
                <IconifyIcon
                  v-else-if="row.meta?.icon"
                  :icon="row.meta?.icon || 'carbon:circle-dash'"
                  class="size-full"
                />
              </div>
              <span class="flex-auto">{{ $t(row.meta?.title) }}</span>
              <div class="flex items-center justify-end shrink-0">
                <Tooltip title="다국어 번역 수정">
                  <Button size="small" type="link" class="p-0.5 opacity-0 group-hover:opacity-100 transition-opacity" @click.stop="onOpenI18nModal(row)">
                    <template #icon>
                      <IconifyIcon icon="lucide:globe" class="size-3.5 text-primary" />
                    </template>
                  </Button>
                </Tooltip>
              </div>
            </div>
            <MenuBadge
              v-if="row.meta?.badgeType"
              class="menu-badge"
              :badge="row.meta.badge"
              :badge-type="row.meta.badgeType"
              :badge-variants="row.meta.badgeVariants"
            />
          </template>
        </Grid>
      </Tabs.TabPane>
      <Tabs.TabPane key="tree" tab="트리 뷰">
        <div class="p-4 flex flex-col h-full gap-2">
          <div class="flex gap-2 justify-end mb-2">
            <Tooltip :title="$t('common.expandAll')">
              <Button @click="onTreeExpandAll">
                <template #icon>
                  <IconifyIcon icon="ant-design:expand-outlined" />
                </template>
              </Button>
            </Tooltip>
            <Tooltip :title="$t('common.collapseAll')">
              <Button @click="onTreeCollapseAll">
                <template #icon>
                  <IconifyIcon icon="ant-design:compress-outlined" />
                </template>
              </Button>
            </Tooltip>
            <Button type="primary" @click="onCreate">
              <Plus class="size-5" />
              {{ $t('ui.actionTitle.create', [$t('system.menu.name')]) }}
            </Button>
          </div>
          <div class="flex-1 overflow-y-auto">
            <Tree
                v-model:expandedKeys="expandedKeys"
                :tree-data="treeData"
                draggable
                block-node
                @drop="onDrop"
            >
              <template #title="node">
                <div class="flex items-center justify-between w-full group pr-4">
                  <div class="flex items-center gap-2">
                    <IconifyIcon
                      v-if="node.icon || node.type === 'BUTTON'"
                      :icon="node.icon || 'carbon:security'"
                      class="size-4 shrink-0"
                    />
                    <IconifyIcon
                      v-else
                      icon="carbon:circle-dash"
                      class="size-4 shrink-0 text-muted-foreground"
                    />
                    <span>{{ node.title }}</span>
                  </div>
                  <div class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <!-- 하위 추가 -->
                    <Tooltip title="하위 추가">
                      <Button size="small" type="link" class="p-1" @click.stop="onAppend(node.dataRef)">
                        <template #icon>
                          <IconifyIcon icon="lucide:plus" class="size-4 text-primary" />
                        </template>
                      </Button>
                    </Tooltip>
                    <!-- 다국어 수정 -->
                    <Tooltip title="다국어 번역 수정">
                      <Button size="small" type="link" class="p-1" @click.stop="onOpenI18nModal(node.dataRef)">
                        <template #icon>
                          <IconifyIcon icon="lucide:globe" class="size-4 text-primary" />
                        </template>
                      </Button>
                    </Tooltip>
                    <!-- 수정 -->
                    <Tooltip :title="$t('common.edit')">
                      <Button size="small" type="link" class="p-1" @click.stop="onEdit(node.dataRef)">
                        <template #icon>
                          <IconifyIcon icon="lucide:edit" class="size-4 text-primary" />
                        </template>
                      </Button>
                    </Tooltip>
                    <!-- 삭제 -->
                    <Popconfirm
                      :get-popup-container="getPopupContainer"
                      @confirm="onDelete(node.dataRef)"
                      placement="topLeft"
                      :title="$t('ui.actionMessage.deleteConfirm', [node.dataRef?.name || node.title])"
                      @click.stop
                    >
                      <Tooltip :title="$t('common.delete')">
                        <Button size="small" type="link" class="p-1" danger>
                          <template #icon>
                            <IconifyIcon icon="lucide:trash-2" class="size-4 text-destructive" />
                          </template>
                        </Button>
                      </Tooltip>
                    </Popconfirm>
                  </div>
                </div>
              </template>
            </Tree>
          </div>
        </div>
      </Tabs.TabPane>
    </Tabs>

    <!-- 다국어 편집 공통 모달 -->
    <I18nEditModal ref="i18nEditModalRef" @success="() => onRefresh()" />
  </Page>
</template>
<style lang="scss" scoped>
.vxe-grid {
  user-select: none; /* 그리드 전체의 텍스트 선택 방지 */
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

:deep(.ant-tabs) {
  display: flex;
  flex-direction: column;
  height: 100%;

  .ant-tabs-content {
    flex: 1;
    min-height: 0;
    height: 100%;

    .ant-tabs-tabpane {
      height: 100%;
      display: flex;
      flex-direction: column;
    }
  }
}
</style>