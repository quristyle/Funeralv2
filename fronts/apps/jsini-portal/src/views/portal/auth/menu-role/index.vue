<script lang="ts" setup>
import type { MenuRoleApi } from '#/api/portal/system/menu-role';
import type { SystemMenuApi } from '#/api/portal/system/menu';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Card,
  Checkbox,
  Empty,
  Input,
  message,
  Popconfirm,
  Spin,
  Table,
  Tabs,
  TabPane,
  Tag,
  Tooltip,
  Tree,
} from 'ant-design-vue';

import { getMenuRole } from '#/api/portal/system/menu-role';
import { getMenuList } from '#/api/portal/system/menu';
import { getRoleMenus, saveRoleMenus } from '#/api/portal/system/role-permission';
import { removeRoleScope } from '#/api/portal/system/role-scope';
import { can } from '#/utils/permission';

/**
 * [메뉴롤 — 메뉴 기준 권한 현황]
 *
 * `/system/role-map` 은 **역할**에서 출발한다 — "이 역할은 어떤 메뉴를 쓰나".
 * 이 화면은 반대로 **"이 메뉴는 누가 쓸 수 있나"** 를 본다.
 *
 * 지금까지 그 방향으로 볼 방법이 없었다. "이 메뉴에 파트너도 들어오나?" 를 알려면
 * 역할을 하나씩 열어 메뉴 목록을 훑어야 했고, 역할이 넷이면 네 번 확인해야 했다.
 *
 * [무엇을 보여 주나]
 *   왼쪽   메뉴 트리
 *   오른쪽 이 메뉴를 쓸 수 있는 역할 × 권한  (여기서 바로 고칠 수 있다)
 *          그 역할을 통해 닿는 회사 · 부서 · 사용자  (어느 역할 때문인지 함께)
 *
 * [저장은 기존 경로를 쓴다]
 * 역할↔메뉴 권한은 `saveRoleMenus`(role-permission), 대상 해제는 `removeRoleScope`
 * (role-scope) 를 그대로 쓴다. 같은 일을 하는 저장 경로를 새로 만들면
 * 한쪽에만 규칙이 붙어 갈라진다.
 *
 * [권한 항목은 메뉴가 정한다]
 * 메뉴가 "쓰지 않는다" 고 지정한 항목(`system_menus.use_*`)은 역할에 켜 두어도
 * 효과가 없다(서버가 AND 로 묶는다). 그래서 쓰는 항목만 체크박스로 보여 준다 —
 * 켜도 아무 일이 없는 칸을 보여 주면 "켰는데 왜 안 되지" 로 헤매게 된다.
 */

/** 권한 항목 정의. `key` 는 DTO 필드, `used` 는 메뉴가 쓰는지 보는 필드다. */
const PERMISSION_ITEMS = [
  { key: 'canView', used: 'view', label: '열람' },
  { key: 'canSearch', used: 'search', label: '조회' },
  { key: 'canCreate', used: 'create', label: '등록' },
  { key: 'canUpdate', used: 'update', label: '수정' },
  { key: 'canDelete', used: 'delete', label: '삭제' },
  { key: 'canPrint', used: 'print', label: '출력' },
  { key: 'canExcel', used: 'excel', label: '엑셀' },
  { key: 'canCust1', used: 'cust1', label: '사용자 정의 1', nameKey: 'cust1Name' },
  { key: 'canCust2', used: 'cust2', label: '사용자 정의 2', nameKey: 'cust2Name' },
  { key: 'canCust3', used: 'cust3', label: '사용자 정의 3', nameKey: 'cust3Name' },
  { key: 'canCust4', used: 'cust4', label: '사용자 정의 4', nameKey: 'cust4Name' },
  { key: 'canCust5', used: 'cust5', label: '사용자 정의 5', nameKey: 'cust5Name' },
  { key: 'canCust6', used: 'cust6', label: '사용자 정의 6', nameKey: 'cust6Name' },
  { key: 'canCust7', used: 'cust7', label: '사용자 정의 7', nameKey: 'cust7Name' },
  { key: 'canCust8', used: 'cust8', label: '사용자 정의 8', nameKey: 'cust8Name' },
] as const;

const menus = ref<SystemMenuApi.SystemMenu[]>([]);
const loadingMenus = ref(false);
const menuKeyword = ref('');
const expandedKeys = ref<string[]>([]);
const selectedMenuId = ref<string>('');

const detail = ref<MenuRoleApi.MenuRole | null>(null);
const loadingDetail = ref(false);
const saving = ref(false);

const canEdit = computed(() => can('update', '/auth/menu-role'));

/** 메뉴 트리. 검색어가 있으면 걸리는 것과 그 부모만 남긴다. */
const treeData = computed(() => {
  const kw = menuKeyword.value.trim().toLowerCase();

  function build(list: SystemMenuApi.SystemMenu[]): any[] {
    return list
      .map((m) => {
        const children = build((m as any).children ?? []);
        const label = String((m as any).title || m.name || '');
        const hit =
          !kw ||
          label.toLowerCase().includes(kw) ||
          String(m.path ?? '').toLowerCase().includes(kw);

        // 자식이 걸리면 부모도 남긴다 — 그러지 않으면 트리가 끊겨 찾은 것이 안 보인다.
        if (!hit && children.length === 0) return null;

        return {
          key: m.id,
          title: label || m.path,
          path: m.path,
          isDir: !m.component,
          children: children.length > 0 ? children : undefined,
        };
      })
      .filter(Boolean);
  }

  return build(menus.value);
});

/** 이 메뉴가 실제로 쓰는 권한 항목만 남긴다. */
const visibleItems = computed(() => {
  const used = detail.value?.used;
  if (!used) return [];
  return PERMISSION_ITEMS.filter((it) => (used as any)[it.used]).map((it) => ({
    ...it,
    // 사용자 정의 항목은 메뉴 관리에서 붙인 이름을 쓴다. 없으면 기본 이름.
    label: (it as any).nameKey
      ? (used as any)[(it as any).nameKey] || it.label
      : it.label,
  }));
});

async function loadMenus() {
  loadingMenus.value = true;
  try {
    const res = await getMenuList();
    menus.value = (res as any)?.result ?? res ?? [];
    // 처음에는 최상위만 펼쳐 둔다. 전부 펼치면 메뉴가 100개가 넘어 훑기 어렵다.
    expandedKeys.value = menus.value.map((m) => m.id);
  } catch (error) {
    console.error(error);
    message.error('메뉴 목록을 불러오지 못했습니다.');
  } finally {
    loadingMenus.value = false;
  }
}

/**
 * 현황을 못 불러왔는지.
 *
 * 실패했는데 빈 표만 보여 주면 "이 메뉴는 아무도 못 쓴다" 로 읽힌다 — 거짓말이다.
 * 권한 화면에서 그 오해는 위험하다(열려 있는데 닫힌 줄 알게 된다).
 */
const loadFailed = ref(false);

async function loadDetail(menuId: string) {
  loadingDetail.value = true;
  try {
    detail.value = (await getMenuRole(menuId)) ?? null;
    loadFailed.value = detail.value === null;
  } catch (error) {
    console.error(error);
    detail.value = null;
    loadFailed.value = true;
  } finally {
    loadingDetail.value = false;
  }
}

function onSelectMenu(keys: any[], info: any) {
  const node = info?.node?.dataRef ?? info?.node;
  const id = keys?.[0];
  if (!id) return;

  // 디렉터리(화면 없는 메뉴)도 권한 행이 있을 수 있어 그대로 조회한다.
  selectedMenuId.value = String(id);
  loadDetail(selectedMenuId.value);
  void node;
}

/**
 * 역할 한 줄의 권한을 저장한다.
 *
 * `saveRoleMenus` 는 **그 역할의 메뉴 권한 목록을 통째로** 받는다. 이 화면은 메뉴 하나만
 * 보고 있으므로, 다른 메뉴의 권한을 지우지 않으려면 그 역할의 현재 목록을 먼저 받아
 * 이 메뉴 한 줄만 갈아 끼워 보내야 한다.
 */
async function saveRole(row: MenuRoleApi.RoleGrant) {
  if (!detail.value) return;

  saving.value = true;
  try {
    const current = (await getRoleMenus(row.roleId)) ?? [];
    const list = ((current as any)?.result ?? current ?? []) as any[];

    const next = list
      .filter((m: any) => m.menuId !== detail.value!.menuId)
      .map((m: any) => ({ ...m }));

    next.push({
      menuId: detail.value.menuId,
      canView: row.canView,
      canSearch: row.canSearch,
      canCreate: row.canCreate,
      canUpdate: row.canUpdate,
      canDelete: row.canDelete,
      canPrint: row.canPrint,
      canExcel: row.canExcel,
      canCust1: row.canCust1,
      canCust2: row.canCust2,
      canCust3: row.canCust3,
      canCust4: row.canCust4,
      canCust5: row.canCust5,
      canCust6: row.canCust6,
      canCust7: row.canCust7,
      canCust8: row.canCust8,
    });

    await saveRoleMenus(row.roleId, next as any);
    message.success(`${row.roleName} 권한을 저장했습니다.`);
    await loadDetail(detail.value.menuId);
  } catch (error) {
    console.error(error);
    message.error('권한 저장에 실패했습니다.');
  } finally {
    saving.value = false;
  }
}

/** 대상에서 역할을 푼다. 그 역할이 이 메뉴를 주던 유일한 길이면 접근이 끊긴다. */
async function detachTarget(
  kind: 'account' | 'company' | 'department',
  target: MenuRoleApi.Target,
  roleId: string,
) {
  try {
    await removeRoleScope(kind as any, target.id, roleId);
    message.success(`${target.name} 에서 역할을 해제했습니다.`);
    if (detail.value) await loadDetail(detail.value.menuId);
  } catch (error) {
    console.error(error);
    message.error('역할 해제에 실패했습니다.');
  }
}

const targetColumns = [
  { title: '이름', key: 'name' },
  { title: '소속', key: 'company' },
  { title: '인원', key: 'userCount', width: 70, align: 'right' as const },
  { title: '연결된 역할', key: 'roles' },
  { title: '', key: 'action', width: 60, align: 'center' as const },
];

onMounted(loadMenus);
</script>

<template>
  <Page auto-content-height>
    <div class="menu-role flex h-full gap-3">
      <!-- 왼쪽: 메뉴 트리 -->
      <Card
        class="flex w-[300px] shrink-0 flex-col"
        :body-style="{
          padding: '8px',
          flex: 1,
          minHeight: 0,
          display: 'flex',
          flexDirection: 'column',
        }"
        size="small"
        title="메뉴"
      >
        <Input
          v-model:value="menuKeyword"
          allow-clear
          class="mb-2"
          placeholder="메뉴명 · 경로 검색"
          size="small"
        >
          <template #prefix>
            <IconifyIcon class="text-muted-foreground" icon="lucide:search" />
          </template>
        </Input>

        <Spin :spinning="loadingMenus">
          <div class="min-h-0 flex-1 overflow-auto">
            <Tree
              v-if="treeData.length > 0"
              v-model:expanded-keys="expandedKeys"
              block-node
              :selected-keys="selectedMenuId ? [selectedMenuId] : []"
              :tree-data="treeData"
              @select="onSelectMenu"
            >
              <template #title="node">
                <div class="flex items-center gap-1.5">
                  <IconifyIcon
                    class="size-4 shrink-0"
                    :class="node.isDir ? 'text-muted-foreground' : 'text-primary'"
                    :icon="node.isDir ? 'lucide:folder' : 'lucide:file-text'"
                  />
                  <!--
                    크기를 이 요소에 직접 준다. `rem` 은 항상 루트를 기준으로 계산되므로
                    사용자가 정한 글꼴 크기를 반드시 따라간다. antd 가 트리 노드 칸에
                    걸어 둔 고정 크기와 싸우지 않는 가장 확실한 자리다.
                  -->
                  <span class="truncate text-sm">{{ node.title }}</span>
                </div>
              </template>
            </Tree>
            <Empty
              v-else
              :description="
                menuKeyword ? '검색 결과가 없습니다.' : '메뉴가 없습니다.'
              "
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />
          </div>
        </Spin>
      </Card>

      <!-- 오른쪽: 이 메뉴의 권한 현황 -->
      <div class="flex min-w-0 flex-1 flex-col gap-3">
        <Card
          v-if="!selectedMenuId"
          class="flex flex-1 items-center justify-center"
        >
          <Empty description="왼쪽에서 메뉴를 선택해 주세요." />
        </Card>

        <template v-else>
          <!--
            못 불러왔을 때. 빈 표를 보여 주면 "아무도 못 쓴다" 로 읽혀서 위험하다 —
            권한 화면에서는 열려 있는데 닫힌 줄 아는 오해가 가장 나쁘다.
          -->
          <Card v-if="loadFailed && !loadingDetail" :body-style="{ padding: '12px' }">
            <Alert
              description="이 메뉴의 권한 현황을 읽지 못했습니다. 표가 비어 있는 것이 '권한 없음' 을 뜻하지는 않습니다. 잠시 후 다시 선택해 주세요."
              message="현황을 불러오지 못했습니다."
              show-icon
              type="error"
            />
          </Card>

          <Spin v-else :spinning="loadingDetail">
            <!-- 역할 × 권한 -->
            <Card
              :body-style="{ padding: '12px' }"
              size="small"
              :title="
                detail
                  ? `${detail.menuName} — 이 메뉴를 쓸 수 있는 역할`
                  : '역할'
              "
            >
              <template #extra>
                <span
                  v-if="detail"
                  class="text-muted-foreground text-xs"
                >
                  {{ detail.menuPath }} · 열람 가능 사용자
                  <b class="text-foreground">{{ detail.effectiveUserCount }}</b>
                  명
                </span>
              </template>

              <Alert
                v-if="detail && visibleItems.length === 0"
                class="mb-2"
                description="메뉴 관리 화면에서 이 메뉴가 쓰는 권한 항목을 먼저 켜야 합니다."
                message="이 메뉴는 쓰는 권한 항목이 없습니다."
                show-icon
                type="warning"
              />

              <div v-if="detail" class="overflow-auto">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b">
                      <th class="p-2 text-left font-medium">역할</th>
                      <th
                        v-for="item in visibleItems"
                        :key="item.key"
                        class="p-2 text-center font-medium whitespace-nowrap"
                      >
                        {{ item.label }}
                      </th>
                      <th class="p-2 text-right font-medium whitespace-nowrap">
                        걸린 대상
                      </th>
                      <th class="p-2"></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="row in detail.roles"
                      :key="row.roleId"
                      class="hover:bg-accent/50 border-b"
                    >
                      <td class="p-2">
                        <div class="flex items-center gap-1.5">
                          <span>{{ row.roleName }}</span>
                          <Tag v-if="!row.granted" color="default">미설정</Tag>
                        </div>
                      </td>
                      <td
                        v-for="item in visibleItems"
                        :key="item.key"
                        class="p-2 text-center"
                      >
                        <Checkbox
                          :checked="(row as any)[item.key]"
                          :disabled="!canEdit || saving"
                          @change="
                            (e: any) => ((row as any)[item.key] = e.target.checked)
                          "
                        />
                      </td>
                      <td class="text-muted-foreground p-2 text-right text-xs whitespace-nowrap">
                        회사 {{ row.companyCount }} · 부서
                        {{ row.departmentCount }} · 사람
                        {{ row.accountCount }}
                      </td>
                      <td class="p-2 text-right">
                        <Tooltip title="이 역할의 권한을 저장합니다">
                          <a
                            v-if="canEdit"
                            class="text-primary text-xs"
                            @click="saveRole(row)"
                          >
                            저장
                          </a>
                        </Tooltip>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </Card>
          </Spin>

          <!-- 닿는 대상 -->
          <Card
            v-if="!loadFailed"
            class="min-h-0 flex-1"
            :body-style="{ padding: '8px 12px' }"
            size="small"
          >
            <Tabs v-if="detail" size="small">
              <TabPane
                key="company"
                :tab="`회사 (${detail.companies.length})`"
              >
                <Table
                  :columns="targetColumns"
                  :data-source="detail.companies"
                  :pagination="false"
                  row-key="id"
                  size="small"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'name'">
                      {{ record.name }}
                    </template>
                    <template v-else-if="column.key === 'company'">
                      <span class="text-muted-foreground">-</span>
                    </template>
                    <template v-else-if="column.key === 'userCount'">
                      {{ record.userCount }}
                    </template>
                    <template v-else-if="column.key === 'roles'">
                      <Tag
                        v-for="(name, i) in record.viaRoleNames"
                        :key="record.viaRoleIds[i]"
                        color="blue"
                      >
                        {{ name }}
                      </Tag>
                    </template>
                    <template v-else-if="column.key === 'action'">
                      <Popconfirm
                        v-if="canEdit"
                        cancel-text="취소"
                        ok-text="해제"
                        :title="`${record.name} 에서 '${record.viaRoleNames[0]}' 역할을 해제하시겠습니까?`"
                        @confirm="
                          detachTarget('company', record, record.viaRoleIds[0])
                        "
                      >
                        <a class="text-xs text-red-500">해제</a>
                      </Popconfirm>
                    </template>
                  </template>
                </Table>
              </TabPane>

              <TabPane
                key="department"
                :tab="`부서 (${detail.departments.length})`"
              >
                <Table
                  :columns="targetColumns"
                  :data-source="detail.departments"
                  :pagination="false"
                  row-key="id"
                  size="small"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'name'">
                      {{ record.name }}
                    </template>
                    <template v-else-if="column.key === 'company'">
                      {{ record.companyName ?? '-' }}
                    </template>
                    <template v-else-if="column.key === 'userCount'">
                      {{ record.userCount }}
                    </template>
                    <template v-else-if="column.key === 'roles'">
                      <Tag
                        v-for="(name, i) in record.viaRoleNames"
                        :key="record.viaRoleIds[i]"
                        color="blue"
                      >
                        {{ name }}
                      </Tag>
                    </template>
                    <template v-else-if="column.key === 'action'">
                      <Popconfirm
                        v-if="canEdit"
                        cancel-text="취소"
                        ok-text="해제"
                        :title="`${record.name} 에서 '${record.viaRoleNames[0]}' 역할을 해제하시겠습니까?`"
                        @confirm="
                          detachTarget(
                            'department',
                            record,
                            record.viaRoleIds[0],
                          )
                        "
                      >
                        <a class="text-xs text-red-500">해제</a>
                      </Popconfirm>
                    </template>
                  </template>
                </Table>
              </TabPane>

              <TabPane
                key="account"
                :tab="`사용자 (${detail.accounts.length})`"
              >
                <Table
                  :columns="targetColumns"
                  :data-source="detail.accounts"
                  :pagination="false"
                  row-key="id"
                  size="small"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'name'">
                      {{ record.name }}
                      <span class="text-muted-foreground ml-1 text-xs">
                        ({{ record.loginId }})
                      </span>
                    </template>
                    <template v-else-if="column.key === 'company'">
                      {{ record.companyName ?? '-' }}
                    </template>
                    <template v-else-if="column.key === 'userCount'">
                      -
                    </template>
                    <template v-else-if="column.key === 'roles'">
                      <Tag
                        v-for="(name, i) in record.viaRoleNames"
                        :key="record.viaRoleIds[i]"
                        color="blue"
                      >
                        {{ name }}
                      </Tag>
                    </template>
                    <template v-else-if="column.key === 'action'">
                      <Popconfirm
                        v-if="canEdit"
                        cancel-text="취소"
                        ok-text="해제"
                        :title="`${record.name} 에서 '${record.viaRoleNames[0]}' 역할을 해제하시겠습니까?`"
                        @confirm="
                          detachTarget('account', record, record.viaRoleIds[0])
                        "
                      >
                        <a class="text-xs text-red-500">해제</a>
                      </Popconfirm>
                    </template>
                  </template>
                </Table>
              </TabPane>
            </Tabs>

            <!--
              세 목록이 모두 비면 이유를 알려 준다. '열람' 권한을 준 역할이 없으면
              아무도 닿지 않는데, 표만 비어 있으면 데이터를 못 받은 것과 구분되지 않는다.
            -->
            <Alert
              v-if="
                detail &&
                detail.companies.length === 0 &&
                detail.departments.length === 0 &&
                detail.accounts.length === 0
              "
              class="mt-2"
              description="이 메뉴에 '열람' 권한을 준 역할이 없거나, 그 역할에 회사·부서·사용자가 걸려 있지 않습니다. 위에서 역할의 열람을 켜고 저장한 뒤, 역할 관리에서 대상을 지정하세요."
              message="이 메뉴에 닿는 대상이 없습니다."
              show-icon
              type="info"
            />
          </Card>
        </template>
      </div>
    </div>
  </Page>
</template>

<style scoped>
/*
  [글자 크기를 다른 화면에 맞춘다]

  이 포털의 글자 크기는 **사용자가 정한 값을 따른다.** 환경설정의 '글꼴 크기' 가
  `html` 의 루트 크기가 되고(기본 14px), 화면들은 `rem` 비율로 그려서 그 값에 맞춰 함께
  커지고 작아진다. 기준은 `text-sm` = `0.875rem` 이다 (루트 14px 이면 12.25px).

  그런데 antd 부품(Tree · Table · Tabs …)은 자기 토큰의 **고정 px(14px)** 로 그린다.
  그래서 이 화면의 왼쪽 메뉴 트리와 오른쪽 회사·부서·사용자 표만 다른 화면보다 크게
  보였고, 사용자가 글꼴 크기를 바꿔도 그 부분만 따라 변하지 않았다.

  손으로 쓴 역할 표(`table.text-sm`)는 처음부터 `rem` 이라 올바른 크기였다.
  **그 표를 기준으로 antd 쪽을 내린다.** 반대로 기준을 올리면 이 화면 밖의 모든 화면이
  함께 커진다.

  같은 처리를 프로필의 '계정 정보' 탭에서도 했다
  (views/_core/profile/account-info.vue — 거기서는 Descriptions·Table 이 대상이었다).
*/
.menu-role {
  font-size: 0.875rem;
}

/* 왼쪽 메뉴 트리 */
.menu-role :deep(.ant-tree),
/* 오른쪽 회사·부서·사용자 표 */
.menu-role :deep(.ant-table),
.menu-role :deep(.ant-table-thead > tr > th),
.menu-role :deep(.ant-table-tbody > tr > td),
/* 그 밖의 부품 */
.menu-role :deep(.ant-tabs),
.menu-role :deep(.ant-tabs-tab),
.menu-role :deep(.ant-input),
.menu-role :deep(.ant-empty-description),
.menu-role :deep(.ant-alert-message),
.menu-role :deep(.ant-alert-description) {
  font-size: 0.875rem;
}

/*
  트리 노드 칸(`.ant-tree-node-content-wrapper`)은 여기서 건드리지 않는다.

  antd 가 이 칸에 크기를 고정해 두어 바깥에서 `rem` 으로 덮어써도 따라오지 않았다
  (루트를 18px 로 올려도 이 칸만 12.25px 에 머물렀다 — 실제로 재어 확인했다).
  그래서 **글자 크기는 우리가 그리는 제목 요소에 직접 준다** — 템플릿의
  `#title` 슬롯 안 `<span class="truncate text-sm">` 이다.
  `rem` 은 항상 루트 기준이라 사용자가 정한 글꼴 크기를 반드시 따라간다.
  이 칸 자체에는 글자가 없으므로 크기가 남아 있어도 보이는 것에 영향이 없다.
*/

/*
  검색칸.

  `#prefix` 슬롯을 쓰므로 antd 가 `.ant-input-affix-wrapper` 로 한 겹 감싸고,
  그 감싼 요소에 자기 크기를 넣는다. antd 는 스타일을 CSS-in-JS 로 만들어 넣기 때문에
  선택자 우선순위만으로는 밀리지 않아, 여기서만 `!important` 로 못을 박는다.
  (같은 이유로 트리 노드 칸도 밖에서 덮이지 않았다 — 그쪽은 우리가 그리는 제목 요소에
   직접 크기를 줘서 피했고, 검색칸은 우리가 그리는 요소가 없어 이 방법을 쓴다.)
*/
.menu-role :deep(.ant-input-affix-wrapper) {
  font-size: 0.875rem !important;
}

/*
  카드 제목은 조금만 키운다. 본문과 같은 크기면 묶음의 머리인지 구분이 안 된다.
  프로필 '계정 정보' 탭의 표 제목과 같은 값을 쓴다.
*/
.menu-role :deep(.ant-card-head-title) {
  font-size: 0.9375rem;
}

/*
  Tag 는 원래도 작지만(12px) 고정 px 이라 사용자 설정을 따르지 않는다.
  같이 비율로 바꿔 둔다.
*/
.menu-role :deep(.ant-tag) {
  font-size: 0.75rem;
  line-height: 1.5;
}
</style>
