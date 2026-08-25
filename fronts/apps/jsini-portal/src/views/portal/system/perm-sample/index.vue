<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';

import { computed, h } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  message,
  Space,
  Tabs,
  TabPane,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { useMenuPermission } from '#/composables/use-menu-permission';
import { useMenuPermissionStore } from '#/store/menu-permission';
import { can } from '#/utils/permission';

/**
 * [권한 제어 샘플]
 *
 * 이 화면 하나로 권한을 화면에 적용하는 네 가지 방법을 다 보여 준다.
 * 새 화면을 만들 때 여기서 필요한 조각만 베껴 가면 된다.
 *
 *   1. `v-perm:<동작>`          — 권한이 없으면 요소를 감춘다 (기본)
 *   2. `v-perm:<동작>.disable`  — 감추는 대신 잠근다
 *   3. `useMenuPermission()`    — 다른 조건과 조합해야 할 때
 *   4. `can(동작)`              — h() 로 직접 그리는 자리 (vxe 액션 컬럼 등)
 *
 * 권한 값의 출처는 하나다. `scom.role_menus` 의 이 메뉴 행이고,
 * 사용자가 가진 여러 역할의 값을 서버가 OR 로 합쳐 내려준다.
 * 켜고 끄는 곳은 [역할 관리](/system/role-map) 화면이다.
 *
 * 사용자 정의 1~8 의 뜻은 **화면이 정한다.** 이 화면은 아래처럼 쓴다고 정했고,
 * 같은 이름을 메뉴 관리에 적어 두어 역할 관리 화면에도 그 이름이 뜬다.
 */

const perm = useMenuPermission();

/** 기본 권한 7종 — v-perm 인자 이름과 스토어 필드가 짝이다. */
const BASE_ACTIONS = [
  { arg: 'view', label: '열람', field: perm.canView },
  { arg: 'search', label: '조회', field: perm.canSearch },
  { arg: 'create', label: '추가', field: perm.canCreate },
  { arg: 'update', label: '수정', field: perm.canUpdate },
  { arg: 'delete', label: '삭제', field: perm.canDelete },
  { arg: 'print', label: '출력', field: perm.canPrint },
  { arg: 'excel', label: '엑셀', field: perm.canExcel },
] as const;

/**
 * 사용자 정의 8종. 뜻은 화면이 정한다 — 여기 적은 이름을
 * 메뉴 관리(`cust1_name` …)에도 같이 넣어 두면 역할 관리 화면에 그 이름이 나온다.
 */
const CUSTOM_ACTIONS = [
  { arg: 'cust1', label: '승인', field: perm.canCust1 },
  { arg: 'cust2', label: '반려', field: perm.canCust2 },
  { arg: 'cust3', label: '마감', field: perm.canCust3 },
  { arg: 'cust4', label: '재계산', field: perm.canCust4 },
  { arg: 'cust5', label: '이력조회', field: perm.canCust5 },
  { arg: 'cust6', label: '알림발송', field: perm.canCust6 },
  { arg: 'cust7', label: '일괄변경', field: perm.canCust7 },
  { arg: 'cust8', label: '잠금해제', field: perm.canCust8 },
] as const;

const ALL_ACTIONS = [...BASE_ACTIONS, ...CUSTOM_ACTIONS];

/**
 * 역할이 하나도 배정되지 않은 계정은 권한 목록이 비어서 온다.
 * 이때는 전부 허용으로 동작하므로(스토어 규칙) 화면에 그 사실을 알린다.
 * 그러지 않으면 "권한을 켰는데 왜 다 보이지" 하고 헤매게 된다.
 */
const permStore = useMenuPermissionStore();
const noRoleData = computed(() => permStore.isLoaded && !permStore.hasAnyData);

/** 이 메뉴가 권한 표에 없을 때. 경로를 메뉴에 등록하지 않으면 이렇게 된다. */
const notRegistered = computed(
  () => permStore.isLoaded && permStore.hasAnyData && !permStore.findExact('/system/perm-sample'),
);

function run(label: string) {
  message.success(`[${label}] 실행`);
}

// ============================================================
// 4. h() 로 직접 그리는 자리 — can()
//
// vxe 액션 컬럼은 디렉티브를 붙일 수 없다. 렌더 함수 안에서 can() 을 부르면
// 스토어를 읽으므로 권한이 늦게 도착해도 다시 그려질 때 반영된다.
// ============================================================
const ROWS = [
  { id: 1, name: '가상 데이터 1', status: '작성중' },
  { id: 2, name: '가상 데이터 2', status: '승인대기' },
  { id: 3, name: '가상 데이터 3', status: '마감' },
];

function actionButton(label: string, danger = false) {
  return h(
    Tooltip,
    { title: label },
    {
      default: () =>
        h(
          Button,
          { danger, size: 'small', type: 'link', onClick: () => run(label) },
          { default: () => label },
        ),
    },
  );
}

const [Grid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { type: 'seq', width: 50 },
      { field: 'name', title: '이름', minWidth: 160 },
      { field: 'status', title: '상태', width: 100 },
      {
        title: '작업',
        width: 260,
        slots: {
          default: ({ row }) =>
            h(
              'div',
              { class: 'flex gap-1' },
              [
                can('update') && actionButton('수정'),
                can('delete') && actionButton('삭제', true),
                // 마감된 건은 권한이 있어도 승인하지 않는다 — 권한과 업무 규칙은 별개다.
                can('cust1') && row.status !== '마감' && actionButton('승인'),
              ].filter(Boolean),
            ),
        },
      },
    ],
    data: ROWS,
    pagerConfig: { enabled: false },
  } as VxeTableGridOptions,
});
</script>

<template>
  <Page auto-content-height>
    <div class="grid h-full grid-cols-1 gap-3 lg:grid-cols-[320px_1fr]">
      <!-- 왼쪽: 지금 이 화면에 실제로 적용된 권한 값 -->
      <Card size="small" title="이 화면의 현재 권한">
        <Alert
          v-if="noRoleData"
          class="mb-2"
          message="역할이 배정되지 않아 전부 허용으로 동작합니다."
          show-icon
          type="warning"
        />
        <Alert
          v-else-if="notRegistered"
          class="mb-2"
          description="이 경로가 메뉴에 없습니다. 가장 가까운 상위 메뉴의 권한을 물려받고 있습니다."
          message="메뉴 미등록"
          show-icon
          type="warning"
        />
        <p class="mb-2 text-xs text-gray-500">
          값을 바꾸려면
          <RouterLink class="text-primary underline" to="/system/role-map">
            역할 관리
          </RouterLink>
          에서 이 메뉴의 체크박스를 켜고 끈 뒤 새로고침한다.
        </p>
        <div class="flex flex-wrap gap-1">
          <Tag
            v-for="action in ALL_ACTIONS"
            :key="action.arg"
            :color="action.field.value ? 'green' : 'default'"
          >
            {{ action.label }}
          </Tag>
        </div>
      </Card>

      <!-- 오른쪽: 쓰는 방법 네 가지 -->
      <Card size="small">
        <Tabs size="small">
          <TabPane key="hide" tab="1. 감추기 (기본)">
            <p class="mb-3 text-xs text-gray-500">
              <code>v-perm:create</code> — 권한이 없으면 요소가 사라진다.
              목록 화면의 등록·삭제 버튼처럼 없어도 화면이 어색하지 않은 자리에 쓴다.
            </p>
            <Space wrap>
              <Button v-perm:create type="primary" @click="run('추가')">
                추가
              </Button>
              <Button v-perm:update @click="run('수정')">수정</Button>
              <Button v-perm:delete danger @click="run('삭제')">삭제</Button>
              <Button v-perm:search @click="run('조회')">조회</Button>
              <Button v-perm:excel @click="run('엑셀')">엑셀</Button>
              <Button v-perm:print @click="run('출력')">출력</Button>
            </Space>
            <div class="mt-4 border-t pt-3">
              <p class="mb-2 text-xs text-gray-500">
                동작 이름을 변수로 받아야 하면 동적 인자를 쓴다 —
                <code>v-perm:[action.arg]</code>
              </p>
              <Space wrap>
                <Button
                  v-for="action in BASE_ACTIONS"
                  :key="action.arg"
                  v-perm:[action.arg]
                  size="small"
                  @click="run(action.label)"
                >
                  {{ action.label }}
                </Button>
              </Space>
            </div>
            <div class="mt-4 border-t pt-3">
              <p class="mb-2 text-xs text-gray-500">
                사용자 정의 1~8. 뜻은 화면이 정하고, 같은 이름을 메뉴 관리에 적어
                두면 역할 관리 화면에도 그 이름이 나온다.
              </p>
              <Space wrap>
                <Button v-perm:cust1 type="primary" @click="run('승인')">
                  승인
                </Button>
                <Button v-perm:cust2 danger @click="run('반려')">반려</Button>
                <Button v-perm:cust3 @click="run('마감')">마감</Button>
                <Button v-perm:cust4 @click="run('재계산')">재계산</Button>
                <Button v-perm:cust5 @click="run('이력조회')">이력조회</Button>
                <Button v-perm:cust6 @click="run('알림발송')">알림발송</Button>
                <Button v-perm:cust7 @click="run('일괄변경')">일괄변경</Button>
                <Button v-perm:cust8 @click="run('잠금해제')">잠금해제</Button>
              </Space>
            </div>
          </TabPane>

          <TabPane key="disable" tab="2. 잠그기 (.disable)">
            <p class="mb-3 text-xs text-gray-500">
              <code>v-perm:update.disable</code> — 감추는 대신 잠근다. 버튼이
              사라지면 배치가 어색해지는 자리(상세 화면의 저장 버튼 등)에 쓴다.
              잠긴 버튼에는 안내 툴팁이 붙는다.
            </p>
            <Space wrap>
              <Button v-perm:create.disable type="primary" @click="run('추가')">
                추가
              </Button>
              <Button v-perm:update.disable @click="run('저장')">저장</Button>
              <Button v-perm:delete.disable danger @click="run('삭제')">
                삭제
              </Button>
              <Button v-perm:cust1.disable @click="run('승인')">승인</Button>
              <Button v-perm:cust3.disable @click="run('마감')">마감</Button>
            </Space>
            <p class="mt-4 border-t pt-3 text-xs text-gray-500">
              다른 화면의 권한을 봐야 하면 값으로 경로를 넘긴다 —
              <code>v-perm:update="'/helpdesk/request/manage'"</code>
            </p>
            <Space class="mt-2" wrap>
              <Button
                v-perm:update.disable="'/helpdesk/request/manage'"
                @click="run('요청 처리 수정')"
              >
                요청 처리 화면의 수정 권한으로 판단
              </Button>
            </Space>
          </TabPane>

          <TabPane key="hook" tab="3. 훅 (useMenuPermission)">
            <p class="mb-3 text-xs text-gray-500">
              권한만으로 정해지지 않고 업무 조건과 조합해야 할 때는 디렉티브보다
              훅이 읽기 쉽다. 디렉티브는 요소 하나를 감추거나 잠그는 것만 한다.
            </p>
            <Space wrap>
              <Button
                v-if="perm.canUpdate.value && !perm.canDelete.value"
                @click="run('수정만 가능')"
              >
                수정은 되고 삭제는 안 되는 사용자용 버튼
              </Button>
              <Button
                :disabled="!perm.canCust1.value || perm.isLoading.value"
                type="primary"
                @click="run('승인')"
              >
                승인 (권한 목록 도착 전에는 잠김)
              </Button>
              <Tag v-if="perm.isLoading.value" color="orange">
                권한 목록 수신 중
              </Tag>
            </Space>
            <pre
              class="mt-4 overflow-x-auto rounded bg-gray-50 p-3 text-xs dark:bg-gray-800"
            >
const perm = useMenuPermission();

// 템플릿
&lt;Button v-if="perm.canCreate.value" @click="onCreate"&gt;등록&lt;/Button&gt;
&lt;Button :disabled="!perm.canUpdate.value" @click="onSave"&gt;저장&lt;/Button&gt;</pre
            >
          </TabPane>

          <TabPane key="render" tab="4. 렌더 함수 (can)">
            <p class="mb-3 text-xs text-gray-500">
              vxe 액션 컬럼처럼 <code>h()</code> 로 직접 그리는 자리에는 디렉티브를
              붙일 수 없다. <code>can('update')</code> 를 렌더 함수 안에서 부른다.
              세 번째 행은 '마감' 상태라 승인 권한이 있어도 승인 버튼이 안 나온다 —
              권한과 업무 규칙은 별개다.
            </p>
            <Grid />
          </TabPane>
        </Tabs>
      </Card>
    </div>
  </Page>
</template>
