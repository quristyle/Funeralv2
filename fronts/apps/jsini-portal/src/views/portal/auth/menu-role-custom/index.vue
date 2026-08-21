<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, Tree, message } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMenuRoles } from '#/api/portal/system/role-mapping';
import { getMenuList } from '#/api/portal/system/menu';

const menuTreeData = ref<any[]>([]);
const selectedMenuId = ref<string>('');
const selectedMenuKeys = ref<string[]>([]);
const expandedKeys = ref<string[]>([]);

// 메뉴 트리 로드
async function fetchMenuTree() {
  try {
    const rawMenus = await getMenuList();
    menuTreeData.value = buildMenuTree(rawMenus || []);
    expandedKeys.value = menuTreeData.value.map(item => item.key);
    if (menuTreeData.value.length > 0 && menuTreeData.value[0]?.key) {
      selectedMenuKeys.value = [menuTreeData.value[0].key];
      selectedMenuId.value = menuTreeData.value[0].key;
    }
  } catch (error) {
    message.error('메뉴 목록을 가져오는 중 오류가 발생했습니다.');
  }
}

function buildMenuTree(list: any[]): any[] {
  const map: Record<string, any> = {};
  const roots: any[] = [];
  
  list.forEach(item => {
    map[item.id] = {
      key: item.id,
      title: item.title,
      children: [],
      ...item
    };
  });

  list.forEach(item => {
    const parentId = item.pid || item.parentId;
    if (parentId && map[parentId]) {
      map[parentId].children.push(map[item.id]);
    } else {
      roots.push(map[item.id]);
    }
  });

  return roots;
}

// 트리 노드 선택 시 호출
function onSelect(keys: any[]) {
  if (keys.length > 0 && keys[0]) {
    selectedMenuId.value = keys[0];
  }
}

// 테이블 정의
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'roleName', title: '롤 명칭', minWidth: 150 },
      { field: 'roleId', title: '롤 ID (코드)', minWidth: 150 },
      { field: 'assignedAt', title: '배정일자', minWidth: 180 },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          if (!selectedMenuId.value) return [];
          return await getMenuRoles(selectedMenuId.value);
        },
      },
    },
  },
});

// 메뉴 선택 변경 시 그리드 재로드
watch(selectedMenuId, () => {
  gridApi.query();
});

onMounted(() => {
  fetchMenuTree();
});
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="16" class="h-full">
      <!-- 좌측 메뉴 트리 선택 영역 -->
      <Col :span="8" class="h-full">
        <Card title="시스템 메뉴 구조" class="h-full flex flex-col" :body-style="{ padding: '12px' }">
          <div class="flex-1 overflow-y-auto max-h-[600px] border p-2 rounded">
            <Tree
              v-if="menuTreeData.length > 0"
              v-model:selected-keys="selectedMenuKeys"
              v-model:expanded-keys="expandedKeys"
              :tree-data="menuTreeData"
              @select="onSelect"
            />
          </div>
        </Card>
      </Col>

      <!-- 우측 메뉴 접근 롤 목록 영역 -->
      <Col :span="16" class="h-full">
        <Grid table-title="해당 메뉴 접근 가능 롤 목록" />
      </Col>
    </Row>
  </Page>
</template>
