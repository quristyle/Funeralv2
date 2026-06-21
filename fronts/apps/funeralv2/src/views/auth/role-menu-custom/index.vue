<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Card, Row, Col, List, message, Tree } from 'ant-design-vue';
import { getRoleMenus, saveRoleMenus } from '#/api/system/role-mapping';
import { getRoleList } from '#/api/system/role';
import { getMenuList } from '#/api/system/menu';

const roles = ref<any[]>([]);
const selectedRoleId = ref<string>('');
const menuTreeData = ref<any[]>([]);
const checkedKeys = ref<string[]>([]);
const expandedKeys = ref<string[]>([]);

// 롤 목록 로드
async function fetchRoles() {
  try {
    const data = await getRoleList({});
    roles.value = data || [];
    if (roles.value.length > 0) {
      selectedRoleId.value = roles.value[0].id;
    }
  } catch (error) {
    message.error('롤 목록을 불러오는 중 오류가 발생했습니다.');
  }
}

// 메뉴 목록 로드 및 트리 구조 변환
async function fetchMenuTree() {
  try {
    const rawMenus = await getMenuList();
    menuTreeData.value = buildMenuTree(rawMenus || []);
    // 기본적으로 최상위 노드들은 확장
    expandedKeys.value = menuTreeData.value.map(item => item.key);
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

// 롤별 메뉴 권한 로드
async function fetchRoleMenus() {
  if (!selectedRoleId.value) return;
  try {
    const data = await getRoleMenus(selectedRoleId.value);
    checkedKeys.value = data.map((item: any) => item.menuId);
  } catch (error) {
    message.error('롤 권한을 로드하는 중 오류가 발생했습니다.');
  }
}

// 롤 선택 변경 시 권한 로드
watch(selectedRoleId, () => {
  fetchRoleMenus();
});

// 메뉴 권한 저장
async function handleSave() {
  if (!selectedRoleId.value) {
    message.warning('롤을 선택해주세요.');
    return;
  }
  try {
    // 트리 노드에서 선택된 노드들 맵핑 데이터 빌드
    const mappings = checkedKeys.value.map(menuId => ({
      menuId,
      menuName: '', // 백엔드에서 ID 기준 맵핑 처리
      menuCode: '',
      permissions: ['READ', 'WRITE'] // 기본 권한 세트 지정
    }));
    await saveRoleMenus(selectedRoleId.value, mappings);
    message.success('메뉴 권한이 성공적으로 저장되었습니다.');
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchRoles();
  fetchMenuTree();
});
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="16" class="h-full">
      <!-- 좌측 롤 선택 영역 -->
      <Col :span="6" class="h-full">
        <Card title="롤 목록" class="h-full" :body-style="{ padding: '12px' }">
          <List size="small" bordered :data-source="roles">
            <template #renderItem="{ item }">
              <List.Item
                :class="['cursor-pointer hover:bg-accent p-2 rounded transition-colors', { 'bg-primary text-primary-foreground hover:bg-primary/90': selectedRoleId === item.id }]"
                @click="selectedRoleId = item.id"
              >
                <div>
                  <div class="font-semibold">{{ item.name }}</div>
                  <div class="text-xs opacity-80">{{ item.code }}</div>
                </div>
              </List.Item>
            </template>
          </List>
        </Card>
      </Col>

      <!-- 우측 메뉴 권한 트리 영역 -->
      <Col :span="18" class="h-full">
        <Card title="메뉴 권한 설정" class="h-full flex flex-col">
          <template #extra>
            <Button type="primary" @click="handleSave">권한 설정 저장</Button>
          </template>
          <div class="flex-1 overflow-y-auto max-h-[600px] border p-4 rounded bg-card">
            <Tree
              v-if="menuTreeData.length > 0"
              v-model:checked-keys="checkedKeys"
              v-model:expanded-keys="expandedKeys"
              checkable
              :tree-data="menuTreeData"
              :selectable="false"
            />
            <div v-else class="text-center text-muted-foreground p-8">
              메뉴 데이터가 존재하지 않습니다.
            </div>
          </div>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
