<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { computed, onMounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Alert, Button, Checkbox, message, Tooltip } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  getRoleMenus,
  saveRoleMenus,
  type SystemRolePermissionApi,
} from '#/api/portal/system/role-permission';
import { useMenuPermission } from '#/composables/use-menu-permission';

/**
 * [역할 - 메뉴 권한]
 *
 * 메뉴마다 쓰는 권한 항목이 다르다. 그 설정은 메뉴 관리 화면에서 정하고
 * (`system_menus.use_*`, `cust*_name`), 이 화면은 그 설정을 따라간다.
 *
 *  - 그 메뉴가 쓰지 않는 항목은 체크박스를 잠근다.
 *  - 사용자 정의 1~8 은 아무 메뉴도 쓰지 않으면 열 자체를 감춘다.
 *    쓰는 메뉴가 있으면 거기 붙인 이름을 열 제목으로 쓴다.
 *
 * 예전에는 모든 메뉴에 15개 체크박스를 똑같이 띄우고 사용자 정의 칸은
 * 'C1'~'C8' 로만 보여서, 무엇을 켜는 것인지 알 수 없었다.
 *
 * 체크박스가 메뉴 177개 × 최대 15칸이라 하나씩 누르는 것이 번거롭다.
 * 그래서 메뉴명 옆에 한 번에 켜고 끄는 단추 둘을 둔다 —
 * **이 메뉴만**, 그리고 **이 메뉴와 하위 전체**. 둘 다 누를 때마다 켜기↔끄기가 바뀐다.
 * 그 메뉴가 쓰지 않는 항목(잠긴 칸)은 건너뛴다 — 저장할 때 어차피 꺼져서 나가므로
 * 켜 두면 화면과 저장 결과가 어긋난다.
 */

const props = defineProps({
  roleId: {
    required: true,
    type: String,
  },
});

const rawMenuList = ref<SystemRolePermissionApi.RoleMenu[]>([]);
const loading = ref(false);

/** 기본 권한 7종 */
const BASE_PERMS = [
  { field: 'canView', title: '열람', use: 'useView' },
  { field: 'canSearch', title: '조회', use: 'useSearch' },
  { field: 'canCreate', title: '추가', use: 'useCreate' },
  { field: 'canUpdate', title: '수정', use: 'useUpdate' },
  { field: 'canDelete', title: '삭제', use: 'useDelete' },
  { field: 'canPrint', title: '출력', use: 'usePrint' },
  { field: 'canExcel', title: '엑셀', use: 'useExcel' },
] as const;

const CUSTOM_NOS = [1, 2, 3, 4, 5, 6, 7, 8] as const;

/**
 * 실제로 쓰이는 사용자 정의 권한만 추린다.
 * 열 제목은 그 칸을 쓰는 메뉴들이 붙인 이름이다. 이름이 서로 다르면
 * 하나로 고를 수 없으므로 'C1' 같은 번호로 두고, 각 칸의 이름은 셀에 달아 준다.
 */
const activeCustoms = computed(() => {
  return CUSTOM_NOS.map((no) => {
    const useKey = `useCust${no}`;
    const nameKey = `cust${no}Name`;

    const users = rawMenuList.value.filter((m) => (m as any)[useKey]);
    if (users.length === 0) return null;

    const names = new Set(
      users
        .map((m) => String((m as any)[nameKey] ?? '').trim())
        .filter(Boolean),
    );

    return {
      field: `canCust${no}`,
      nameKey,
      no,
      title: names.size === 1 ? [...names][0]! : `C${no}`,
      useKey,
    };
  }).filter((c) => c !== null);
});

/** 아직 아무 메뉴도 사용자 정의 권한을 쓰지 않는지 */
const noCustomConfigured = computed(
  () => rawMenuList.value.length > 0 && activeCustoms.value.length === 0,
);

/**
 * 이 화면의 저장 권한. 없으면 일괄 단추도 내리지 않는다 —
 * [권한 설정 저장] 단추와 같은 기준이다(`v-perm:update`).
 * setup 최상위 ref 라 템플릿에서 `.value` 없이 쓴다.
 */
const { canUpdate } = useMenuPermission();

/**
 * 이 행에서 실제로 켜고 끌 수 있는 권한 필드.
 *
 * 그 메뉴가 쓰지 않는 항목은 뺀다(체크박스도 잠겨 있고, 저장할 때 서버와 화면
 * 양쪽에서 꺼진 값으로 처리된다). 사용자 정의는 열이 보이는 것만 다룬다.
 */
function usableFields(row: any): string[] {
  const fields: string[] = [];
  BASE_PERMS.forEach((p) => {
    if (isUsed(row, p.use)) fields.push(p.field);
  });
  activeCustoms.value.forEach((c) => {
    if (isUsed(row, c.useKey)) fields.push(c.field);
  });
  return fields;
}

/** 하위 메뉴가 있는가. `children` 은 `listToTree` 가 붙이므로 DTO 타입에는 없다. */
function hasChildren(row: any) {
  return Boolean(row?.children?.length);
}

/** 이 행과 그 하위 전부. 하위 일괄 단추의 범위다. */
function subtreeRows(row: any): any[] {
  const out: any[] = [];
  const walk = (node: any) => {
    out.push(node);
    (node?.children ?? []).forEach(walk);
  };
  walk(row);
  return out;
}

/** 범위 안에서 켜고 끌 수 있는 칸의 수와, 그중 켜져 있는 칸의 수 */
function scopeCount(rows: any[]) {
  let usable = 0;
  let on = 0;
  for (const row of rows) {
    for (const field of usableFields(row)) {
      usable += 1;
      if (row[field]) on += 1;
    }
  }
  return { allOn: usable > 0 && on === usable, on, usable };
}

/** 이 행만 */
function rowScope(row: any) {
  return scopeCount([row]);
}

/** 이 행과 하위 전체 */
function subtreeScope(row: any) {
  return scopeCount(subtreeRows(row));
}

/**
 * 범위 안의 권한을 한 번에 켜거나 끈다.
 *
 * 모두 켜져 있으면 끄고, 아니면 켠다 — 단추 하나로 켜기·끄기를 다 하기 위해서다.
 * 저장은 하지 않는다. 다른 체크박스와 똑같이 [권한 설정 저장] 을 눌러야 반영된다.
 */
function toggleScope(rows: any[]) {
  const next = !scopeCount(rows).allOn;
  rows.forEach((row) => {
    usableFields(row).forEach((field) => {
      row[field] = next;
    });
  });
}

/** 평면 목록을 트리로 조립 */
function listToTree(list: SystemRolePermissionApi.RoleMenu[]) {
  const map: Record<string, any> = {};
  const tree: any[] = [];

  list.forEach((item) => {
    map[item.menuId] = { ...item, children: [] };
  });

  list.forEach((item) => {
    const node = map[item.menuId];
    if (item.parentId && map[item.parentId]) {
      map[item.parentId].children.push(node);
    } else {
      tree.push(node);
    }
  });

  return tree;
}

const columns = computed(() => [
  {
    field: 'menuName',
    minWidth: 300,
    slots: { default: 'menuName' },
    title: '메뉴명',
    treeNode: true,
  },
  ...BASE_PERMS.map((p) => ({
    align: 'center' as const,
    field: p.field,
    slots: { default: p.field },
    title: p.title,
    width: 65,
  })),
  ...activeCustoms.value.map((c) => ({
    align: 'center' as const,
    field: c.field,
    slots: { default: 'custom' },
    params: { custom: c },
    title: c.title,
    width: 80,
  })),
]);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: columns.value,
    data: [],
    height: 'auto',
    rowConfig: {
      keyField: 'menuId',
    },
    treeConfig: {
      childrenField: 'children',
      expandAll: true,
      parentField: 'parentId',
      rowField: 'menuId',
      transform: false,
    },
  } as VxeTableGridOptions<SystemRolePermissionApi.RoleMenu>,
});

// 사용자 정의 열은 데이터를 받아 봐야 몇 개인지 알 수 있으므로, 정해지면 다시 심는다.
watch(columns, (cols) => {
  gridApi.grid?.loadColumn(cols as any);
});

/** 그 메뉴가 해당 항목을 쓰는지 */
function isUsed(row: any, useKey: string) {
  return Boolean(row?.[useKey]);
}

/** 셀에 달아 줄 안내. 잠긴 이유나 이 칸의 이름을 알려준다. */
function cellHint(row: any, useKey: string, label: string, nameKey?: string) {
  if (!isUsed(row, useKey)) {
    return `${row.menuName} 메뉴는 '${label}' 권한을 사용하지 않습니다. 메뉴 관리에서 켤 수 있습니다.`;
  }
  const name = nameKey ? String(row[nameKey] ?? '').trim() : '';
  return name || label;
}

async function fetchRolePermissions() {
  if (!props.roleId) return;
  loading.value = true;
  try {
    const res = await getRoleMenus(props.roleId);
    const list = (res as any)?.result ?? res ?? [];
    rawMenuList.value = list;
    gridApi.grid?.loadData(listToTree(list));
  } catch {
    message.error('메뉴 권한 정보 로드 실패');
  } finally {
    loading.value = false;
  }
}

/** 트리 데이터를 평탄화 리스트로 복구 */
function flattenTree(nodes: any[]): SystemRolePermissionApi.RoleMenu[] {
  const result: SystemRolePermissionApi.RoleMenu[] = [];

  function recurse(n: any) {
    const { children, ...rest } = n;
    result.push(rest);
    if (children && children.length > 0) {
      children.forEach((c: any) => recurse(c));
    }
  }

  nodes.forEach((node) => recurse(node));
  return result;
}

async function handleSavePermissions() {
  const gridData = gridApi.grid?.getTableData()?.fullData ?? [];
  if (gridData.length === 0) return;

  const flatList = flattenTree(gridData);

  // 저장용 DTO 로 줄인다. 화면 표시용 항목(메뉴명·부모·사용 설정)은 빼고 보낸다.
  const postData = flatList.map((item) => {
    const row = item as any;
    const payload: Record<string, any> = { menuId: row.menuId };

    BASE_PERMS.forEach((p) => {
      // 쓰지 않는 항목은 켜서 보내지 않는다. 서버도 같은 규칙으로 한 번 더 막는다.
      payload[p.field] = isUsed(row, p.use) && Boolean(row[p.field]);
    });

    CUSTOM_NOS.forEach((no) => {
      payload[`canCust${no}`] =
        isUsed(row, `useCust${no}`) && Boolean(row[`canCust${no}`]);
    });

    return payload;
  });

  loading.value = true;
  try {
    await saveRoleMenus(props.roleId, postData as any);
    message.success('메뉴 권한 설정이 성공적으로 저장되었습니다.');
    fetchRolePermissions();
  } catch {
    message.error('저장 중 오류 발생');
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.roleId,
  () => {
    fetchRolePermissions();
  },
  { immediate: true },
);

onMounted(() => {
  fetchRolePermissions();
});
</script>

<template>
  <div class="flex h-full flex-1 flex-col overflow-hidden">
    <!-- 상단 저장 툴바 -->
    <div class="mb-3 flex items-center justify-between pt-2">
      <span class="text-xs text-muted-foreground">
        회색으로 잠긴 칸은 그 메뉴가 쓰지 않는 권한입니다. 메뉴 관리 화면에서
        사용 여부와 이름을 정할 수 있습니다. 메뉴명 옆 단추로 그 메뉴 또는 하위
        전체를 한 번에 켜고 끌 수 있습니다.
      </span>
      <Button v-perm:update :loading="loading" type="primary" @click="handleSavePermissions">
        <template #icon>
          <IconifyIcon
            class="mr-1 inline-block size-4 align-text-bottom"
            icon="lucide:save"
          />
        </template>
        권한 설정 저장
      </Button>
    </div>

    <Alert
      v-if="noCustomConfigured"
      class="mb-2"
      description="사용자 정의 권한(C1~C8)을 쓰는 메뉴가 아직 없어 해당 열은 표시하지 않습니다. 메뉴 관리 화면에서 켜고 이름을 붙이면 여기에 그 이름으로 나타납니다."
      show-icon
      type="info"
    />

    <!-- 트리 그리드 영역 -->
    <div class="relative flex-1 overflow-auto rounded-lg border">
      <Grid class="h-full w-full">
        <!--
          메뉴명 + 일괄 켜기/끄기 단추 둘.

          손가락으로도 닿아야 하므로 hover 로 숨기지 않고 늘 보이게 둔다(40번 문서).
          쓸 수 있는 칸이 없는 메뉴(디렉터리 등)는 잠그고 이유를 툴팁으로 알린다 —
          단추가 사라지면 왜 없는지 알 수 없다.
        -->
        <template #menuName="{ row }">
          <div class="flex w-full items-center gap-1">
            <span class="flex-auto truncate">{{ row.menuName }}</span>

            <template v-if="canUpdate">
              <Tooltip
                :title="
                  rowScope(row).usable === 0
                    ? `${row.menuName} 메뉴는 쓰는 권한 항목이 없습니다.`
                    : rowScope(row).allOn
                      ? '이 메뉴의 권한을 모두 끕니다'
                      : '이 메뉴의 권한을 모두 켭니다'
                "
              >
                <Button
                  :disabled="rowScope(row).usable === 0"
                  size="small"
                  type="link"
                  @click="toggleScope([row])"
                >
                  <IconifyIcon
                    class="size-4"
                    :class="
                      rowScope(row).allOn ? 'text-primary' : 'text-muted-foreground'
                    "
                    icon="lucide:square-check-big"
                  />
                </Button>
              </Tooltip>

              <Tooltip
                v-if="hasChildren(row)"
                :title="
                  subtreeScope(row).usable === 0
                    ? '이 아래에 켤 수 있는 권한 항목이 없습니다.'
                    : subtreeScope(row).allOn
                      ? '이 메뉴와 하위 메뉴의 권한을 모두 끕니다'
                      : '이 메뉴와 하위 메뉴의 권한을 모두 켭니다'
                "
              >
                <Button
                  :disabled="subtreeScope(row).usable === 0"
                  size="small"
                  type="link"
                  @click="toggleScope(subtreeRows(row))"
                >
                  <IconifyIcon
                    class="size-4"
                    :class="
                      subtreeScope(row).allOn
                        ? 'text-primary'
                        : 'text-muted-foreground'
                    "
                    icon="lucide:list-checks"
                  />
                </Button>
              </Tooltip>
            </template>
          </div>
        </template>

        <!-- 기본 권한 7종 -->
        <template
          v-for="p in BASE_PERMS"
          :key="p.field"
          #[p.field]="{ row }"
        >
          <Tooltip :title="cellHint(row, p.use, p.title)">
            <Checkbox
              v-model:checked="row[p.field]"
              :disabled="!isUsed(row, p.use)"
            />
          </Tooltip>
        </template>

        <!-- 사용자 정의 권한 -->
        <template #custom="{ column, row: rawRow }">
          <Tooltip
            :title="
              cellHint(
                rawRow,
                column.params.custom.useKey,
                column.params.custom.title,
                column.params.custom.nameKey,
              )
            "
          >
            <Checkbox
              v-model:checked="(rawRow as any)[column.params.custom.field]"
              :disabled="!isUsed(rawRow, column.params.custom.useKey)"
            />
          </Tooltip>
        </template>
      </Grid>
    </div>
  </div>
</template>
