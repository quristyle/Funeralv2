<script lang="ts" setup>
import type { AppRole, HelpdeskMenu, RoleMenuPermission } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Checkbox,
  Col,
  Empty,
  Form,
  FormItem,
  Input,
  List,
  ListItem,
  message,
  Modal,
  Popconfirm,
  Row,
  Space,
  Spin,
  Table,
  Tabs,
  TabPane,
  Transfer,
} from 'ant-design-vue';

import {
  addUserToRole,
  createRole,
  deleteRole,
  getAllUsers,
  getManageMenus,
  getRolePermissions,
  getRoles,
  getRoleUsers,
  removeUserFromRole,
  saveRolePermissionsBatch,
  updateRole,
} from '#/api/helpdesk';

/**
 * [역할 관리]
 *
 * 원본(RoleManagement.vue). 역할별로
 *  - 소속 사용자 배정
 *  - 메뉴별 조회/등록/수정/삭제 권한
 * 두 가지를 관리한다.
 */

const loadingRoles = ref(false);
const loadingDetail = ref(false);
const savingRole = ref(false);
const savingPerms = ref(false);

const roles = ref<AppRole[]>([]);
const selectedRoleId = ref<number | undefined>();
const menus = ref<HelpdeskMenu[]>([]);
const permissions = ref<Record<number, RoleMenuPermission>>({});
const allUsers = ref<any[]>([]);
const assignedUserKeys = ref<string[]>([]);

const roleModalOpen = ref(false);
const editingRole = reactive<Partial<AppRole>>({});
const isEditRole = computed(() => Boolean(editingRole.id));

const PERM_KEYS = [
  { key: 'canRead', label: '조회' },
  { key: 'canCreate', label: '등록' },
  { key: 'canUpdate', label: '수정' },
  { key: 'canDelete', label: '삭제' },
] as const;

const permissionColumns = [
  { dataIndex: 'label', key: 'label', title: '메뉴' },
  ...PERM_KEYS.map((p) => ({ key: p.key, title: p.label, width: 80 })),
];

/** 사용자 키는 "종류:ID" 로 만든다. 담당자와 고객의 ID 가 겹칠 수 있기 때문. */
function userKey(user: { userId: number; userType: string }) {
  return `${user.userType}:${user.userId}`;
}

const transferUsers = computed(() =>
  allUsers.value.map((u) => ({
    key: userKey(u),
    title: `${u.userName} (${u.userType === 'admin' ? '담당자' : '고객'})`,
  })),
);

async function loadRoles() {
  loadingRoles.value = true;
  try {
    roles.value = (await getRoles()) ?? [];
    if (!selectedRoleId.value) selectedRoleId.value = roles.value[0]?.id;
  } finally {
    loadingRoles.value = false;
  }
}

async function loadDetail(roleId?: number) {
  if (!roleId) return;

  loadingDetail.value = true;
  try {
    const [perms, users] = await Promise.all([
      getRolePermissions(roleId).catch(() => []),
      getRoleUsers(roleId).catch(() => []),
    ]);

    const map: Record<number, RoleMenuPermission> = {};
    (perms ?? []).forEach((p) => {
      map[p.menuId] = p;
    });
    permissions.value = map;

    assignedUserKeys.value = (users ?? []).map((u: any) => userKey(u));
  } finally {
    loadingDetail.value = false;
  }
}

/** 권한 체크박스 값. 표의 column.key 는 문자열|숫자 어느 쪽으로도 올 수 있어 문자열로 맞춘다. */
function permValue(menuId: number, key: unknown) {
  return Boolean((permissions.value[menuId] as any)?.[String(key)]);
}

function setPerm(menuId: number, rawKey: unknown, value: boolean) {
  const key = String(rawKey);
  const current = permissions.value[menuId] ?? {
    menuId,
    roleId: selectedRoleId.value!,
  };
  permissions.value = {
    ...permissions.value,
    [menuId]: { ...current, [key]: value },
  };
}

async function savePermissions() {
  if (!selectedRoleId.value) return;

  savingPerms.value = true;
  try {
    const payload = menus.value.map((m) => ({
      canCreate: permValue(m.id, 'canCreate'),
      canDelete: permValue(m.id, 'canDelete'),
      canRead: permValue(m.id, 'canRead'),
      canUpdate: permValue(m.id, 'canUpdate'),
      menuId: m.id,
      roleId: selectedRoleId.value!,
    }));
    await saveRolePermissionsBatch(payload);
    message.success('권한을 저장했습니다.');
  } finally {
    savingPerms.value = false;
  }
}

/** 사용자 배정이 바뀌면 추가·제거된 것만 서버에 반영한다. */
async function onUsersChange(rawKeys: (number | string)[]) {
  const nextKeys = rawKeys.map(String);
  if (!selectedRoleId.value) return;

  const before = new Set(assignedUserKeys.value);
  const after = new Set(nextKeys);

  const added = nextKeys.filter((k) => !before.has(k));
  const removed = assignedUserKeys.value.filter((k) => !after.has(k));

  assignedUserKeys.value = nextKeys;

  try {
    await Promise.all([
      ...added.map((k) => {
        const [type = '', id = '0'] = k.split(':');
        return addUserToRole(selectedRoleId.value!, type as any, Number(id));
      }),
      ...removed.map((k) => {
        const [type = '', id = '0'] = k.split(':');
        return removeUserFromRole(
          selectedRoleId.value!,
          type as any,
          Number(id),
        );
      }),
    ]);
    message.success('사용자 배정을 저장했습니다.');
  } catch {
    await loadDetail(selectedRoleId.value);
  }
}

function openCreateRole() {
  Object.keys(editingRole).forEach((k) => delete (editingRole as any)[k]);
  Object.assign(editingRole, { description: '', displayName: '', name: '' });
  roleModalOpen.value = true;
}

function openEditRole(role: AppRole) {
  Object.keys(editingRole).forEach((k) => delete (editingRole as any)[k]);
  Object.assign(editingRole, { ...role });
  roleModalOpen.value = true;
}

async function onSaveRole() {
  if (!editingRole.name?.trim()) {
    message.warning('역할 코드를 입력하세요.');
    return;
  }

  savingRole.value = true;
  try {
    await (isEditRole.value
      ? updateRole(editingRole.id!, { ...editingRole })
      : createRole({ ...editingRole }));
    message.success(`역할을 ${isEditRole.value ? '수정' : '등록'}했습니다.`);
    roleModalOpen.value = false;
    await loadRoles();
  } finally {
    savingRole.value = false;
  }
}

async function onDeleteRole(role: AppRole) {
  await deleteRole(role.id);
  message.success('역할을 삭제했습니다.');
  if (selectedRoleId.value === role.id) selectedRoleId.value = undefined;
  await loadRoles();
}

watch(selectedRoleId, (id) => loadDetail(id));

onMounted(async () => {
  const [, menuList, users] = await Promise.all([
    loadRoles(),
    getManageMenus().catch(() => []),
    getAllUsers().catch(() => []),
  ]);
  menus.value = menuList ?? [];
  allUsers.value = users ?? [];
  await loadDetail(selectedRoleId.value);
});
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="[12, 12]">
      <Col :lg="6" :xs="24">
        <Card :body-style="{ padding: 0 }" size="small" title="역할">
          <template #extra>
            <Button size="small" type="primary" @click="openCreateRole">
              추가
            </Button>
          </template>

          <Spin :spinning="loadingRoles">
            <List
              :data-source="roles"
              :locale="{ emptyText: '등록된 역할이 없습니다.' }"
              size="small"
            >
              <template #renderItem="{ item }">
                <ListItem
                  class="cursor-pointer px-3"
                  :class="item.id === selectedRoleId ? 'bg-accent' : ''"
                  @click="selectedRoleId = item.id"
                >
                  <div class="min-w-0 flex-1">
                    <div class="truncate font-medium">
                      {{ item.displayName || item.name }}
                    </div>
                    <div class="truncate text-xs text-muted-foreground">
                      {{ item.name }}
                    </div>
                  </div>
                  <Space @click.stop>
                    <Button size="small" type="link" @click="openEditRole(item)">
                      수정
                    </Button>
                    <Popconfirm
                      cancel-text="취소"
                      ok-text="삭제"
                      title="역할을 삭제할까요?"
                      @confirm="onDeleteRole(item)"
                    >
                      <Button danger size="small" type="link">삭제</Button>
                    </Popconfirm>
                  </Space>
                </ListItem>
              </template>
            </List>
          </Spin>
        </Card>
      </Col>

      <Col :lg="18" :xs="24">
        <Card size="small">
          <Empty v-if="!selectedRoleId" description="역할을 선택하세요." />

          <Spin v-else :spinning="loadingDetail">
            <Tabs>
              <TabPane key="perm" tab="메뉴 권한">
                <div class="mb-2 flex justify-end">
                  <Button
                    :loading="savingPerms"
                    type="primary"
                    @click="savePermissions"
                  >
                    권한 저장
                  </Button>
                </div>

                <Table
                  :columns="permissionColumns"
                  :data-source="menus"
                  :pagination="false"
                  :scroll="{ y: 420 }"
                  row-key="id"
                  size="small"
                >
                  <template #bodyCell="{ column, record }">
                    <template
                      v-if="PERM_KEYS.some((p) => p.key === column.key)"
                    >
                      <Checkbox
                        :checked="permValue(record.id, column.key)"
                        @change="
                          (e: any) =>
                            setPerm(record.id, column.key, e.target.checked)
                        "
                      />
                    </template>
                  </template>
                </Table>
              </TabPane>

              <TabPane key="users" tab="소속 사용자">
                <Transfer
                  :data-source="transferUsers"
                  :list-style="{ height: '420px', width: '46%' }"
                  :render="(item: any) => item.title"
                  :target-keys="assignedUserKeys"
                  :titles="['미배정', '배정됨']"
                  show-search
                  @change="onUsersChange"
                />
                <div class="mt-2 text-xs text-muted-foreground">
                  항목을 옮기면 즉시 저장됩니다.
                </div>
              </TabPane>
            </Tabs>
          </Spin>
        </Card>
      </Col>
    </Row>

    <Modal
      v-model:open="roleModalOpen"
      :confirm-loading="savingRole"
      :title="isEditRole ? '역할 수정' : '역할 등록'"
      cancel-text="취소"
      ok-text="저장"
      @ok="onSaveRole"
    >
      <Form layout="vertical">
        <FormItem label="역할 코드" required>
          <Input v-model:value="editingRole.name" placeholder="ADMIN" />
        </FormItem>
        <FormItem label="표시 이름">
          <Input v-model:value="editingRole.displayName" />
        </FormItem>
        <FormItem label="설명">
          <Input v-model:value="editingRole.description" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
