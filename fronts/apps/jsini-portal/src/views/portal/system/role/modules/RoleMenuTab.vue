<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Button, Checkbox, message } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { useVbenVxeGrid, type VxeTableGridOptions } from '#/adapter/vxe-table';
import { getRoleMenus, saveRoleMenus, type SystemRolePermissionApi } from '#/api/system/role-permission';

const props = defineProps({
  roleId: {
    type: String,
    required: true,
  },
});

// 원본 플랫 데이터
const rawMenuList = ref<SystemRolePermissionApi.RoleMenu[]>([]);
const loading = ref(false);

// 트리 형태 데이터 변환 헬퍼
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

// 트리 그리드 컬럼 설정
const columns = [
  { field: 'menuName', title: '메뉴명', minWidth: 220, treeNode: true },
  { field: 'canView', title: '열람', width: 65, align: 'center' as const, slots: { default: 'canView' } },
  { field: 'canSearch', title: '조회', width: 65, align: 'center' as const, slots: { default: 'canSearch' } },
  { field: 'canCreate', title: '추가', width: 65, align: 'center' as const, slots: { default: 'canCreate' } },
  { field: 'canDelete', title: '삭제', width: 65, align: 'center' as const, slots: { default: 'canDelete' } },
  { field: 'canUpdate', title: '수정', width: 65, align: 'center' as const, slots: { default: 'canUpdate' } },
  { field: 'canPrint', title: '출력', width: 65, align: 'center' as const, slots: { default: 'canPrint' } },
  { field: 'canExcel', title: '엑셀', width: 65, align: 'center' as const, slots: { default: 'canExcel' } },
  { field: 'canCust1', title: 'C1', width: 55, align: 'center' as const, slots: { default: 'canCust1' } },
  { field: 'canCust2', title: 'C2', width: 55, align: 'center' as const, slots: { default: 'canCust2' } },
  { field: 'canCust3', title: 'C3', width: 55, align: 'center' as const, slots: { default: 'canCust3' } },
  { field: 'canCust4', title: 'C4', width: 55, align: 'center' as const, slots: { default: 'canCust4' } },
  { field: 'canCust5', title: 'C5', width: 55, align: 'center' as const, slots: { default: 'canCust5' } },
  { field: 'canCust6', title: 'C6', width: 55, align: 'center' as const, slots: { default: 'canCust6' } },
  { field: 'canCust7', title: 'C7', width: 55, align: 'center' as const, slots: { default: 'canCust7' } },
  { field: 'canCust8', title: 'C8', width: 55, align: 'center' as const, slots: { default: 'canCust8' } },
];

// VXETable 등록
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: columns,
    height: 'auto',
    treeConfig: {
      transform: false,
      rowField: 'menuId',
      parentField: 'parentId',
      childrenField: 'children',
      expandAll: true,
    },
    rowConfig: {
      keyField: 'menuId',
    },
    data: [],
  } as VxeTableGridOptions<SystemRolePermissionApi.RoleMenu>,
});

// 메뉴 권한 로드
async function fetchRolePermissions() {
  if (!props.roleId) return;
  loading.value = true;
  try {
    const res = await getRoleMenus(props.roleId);
    const list = (res as any)?.result ?? res ?? [];
    rawMenuList.value = list;
    
    // 계층 트리를 만들어 그리드에 바인딩
    const treeData = listToTree(list);
    gridApi.grid?.loadData(treeData);
  } catch (error) {
    message.error('메뉴 권한 정보 로드 실패');
  } finally {
    loading.value = false;
  }
}

// 트리 데이터를 평탄화 리스트로 복구하기
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

// 권한 일괄 저장
async function handleSavePermissions() {
  const gridData = gridApi.grid?.getTableData()?.fullData ?? [];
  if (gridData.length === 0) return;

  const flatList = flattenTree(gridData);
  
  // 저장용 DTO에 맞추어 menuName, parentId 제거
  const postData = flatList.map((item) => {
    const { menuName, parentId, ...rest } = item as any;
    return rest;
  });

  loading.value = true;
  try {
    await saveRoleMenus(props.roleId, postData);
    message.success('메뉴 권한 설정이 성공적으로 저장되었습니다.');
    fetchRolePermissions();
  } catch (error) {
    message.error('저장 중 오류 발생');
  } finally {
    loading.value = false;
  }
}

watch(() => props.roleId, () => {
  fetchRolePermissions();
}, { immediate: true });

onMounted(() => {
  fetchRolePermissions();
});
</script>

<template>
  <div class="flex-1 flex flex-col overflow-hidden h-full">
    <!-- 상단 저장 툴바 -->
    <div class="flex justify-end items-center mb-3 pt-2">
      <Button type="primary" :loading="loading" @click="handleSavePermissions">
        <template #icon>
          <IconifyIcon class="size-4 mr-1 inline-block align-text-bottom" icon="lucide:save" />
        </template>
        권한 설정 저장
      </Button>
    </div>

    <!-- 트리 그리드 영역 -->
    <div class="flex-1 overflow-auto border rounded-lg  relative">
      <Grid class="h-full w-full">
        <!-- 각 권한 체크박스 셀 슬롯 정의 -->
        <template #canView="{ row }">
          <Checkbox v-model:checked="row.canView" />
        </template>
        <template #canSearch="{ row }">
          <Checkbox v-model:checked="row.canSearch" />
        </template>
        <template #canCreate="{ row }">
          <Checkbox v-model:checked="row.canCreate" />
        </template>
        <template #canDelete="{ row }">
          <Checkbox v-model:checked="row.canDelete" />
        </template>
        <template #canUpdate="{ row }">
          <Checkbox v-model:checked="row.canUpdate" />
        </template>
        <template #canPrint="{ row }">
          <Checkbox v-model:checked="row.canPrint" />
        </template>
        <template #canExcel="{ row }">
          <Checkbox v-model:checked="row.canExcel" />
        </template>
        <template #canCust1="{ row }">
          <Checkbox v-model:checked="row.canCust1" />
        </template>
        <template #canCust2="{ row }">
          <Checkbox v-model:checked="row.canCust2" />
        </template>
        <template #canCust3="{ row }">
          <Checkbox v-model:checked="row.canCust3" />
        </template>
        <template #canCust4="{ row }">
          <Checkbox v-model:checked="row.canCust4" />
        </template>
        <template #canCust5="{ row }">
          <Checkbox v-model:checked="row.canCust5" />
        </template>
        <template #canCust6="{ row }">
          <Checkbox v-model:checked="row.canCust6" />
        </template>
        <template #canCust7="{ row }">
          <Checkbox v-model:checked="row.canCust7" />
        </template>
        <template #canCust8="{ row }">
          <Checkbox v-model:checked="row.canCust8" />
        </template>
      </Grid>
    </div>
  </div>
</template>

<style lang="css" scoped>
/* 트리 그리드 스타일 고정 */
</style>
