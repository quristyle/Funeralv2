<script lang="ts" setup>
import type { VxeTableGridOptions } from '#/adapter/vxe-table';
import type { MenuPermission } from '#/api/core/menu';

import { computed, h, onActivated, onBeforeUnmount, onDeactivated, ref, watchEffect } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  CheckableTag,
  message,
  Space,
  Switch,
  Tabs,
  TabPane,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { useMenuPermission } from '#/composables/use-menu-permission';
import { EMPTY_PERMISSION, useMenuPermissionStore } from '#/store/menu-permission';
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
 *
 * 왼쪽 카드의 **체험 모드**를 켜면 권한을 눌러 가며 바꿔 볼 수 있다.
 * 역할 관리에서 체크박스를 켜고 새로고침하는 왕복 없이, 네 가지 방법이
 * 각각 어떻게 달라지는지 바로 보인다. 자세한 것은 아래 `SAMPLE_PATH` 주석에.
 */

/** 이 화면의 경로. 체험 모드가 덮어쓸 대상이며, 이 경로 하나만 덮어쓴다. */
const SAMPLE_PATH = '/system/perm-sample';

const perm = useMenuPermission();
const permStore = useMenuPermissionStore();

/** 권한 하나. `arg` 는 v-perm 인자, `key` 는 스토어 필드 이름이다. */
interface PermAction {
  arg: string;
  key: keyof MenuPermission;
  label: string;
}

/** 기본 권한 7종 — v-perm 인자 이름과 스토어 필드가 짝이다. */
const BASE_ACTIONS: PermAction[] = [
  { arg: 'view', key: 'canView', label: '열람' },
  { arg: 'search', key: 'canSearch', label: '조회' },
  { arg: 'create', key: 'canCreate', label: '추가' },
  { arg: 'update', key: 'canUpdate', label: '수정' },
  { arg: 'delete', key: 'canDelete', label: '삭제' },
  { arg: 'print', key: 'canPrint', label: '출력' },
  { arg: 'excel', key: 'canExcel', label: '엑셀' },
];

/**
 * 사용자 정의 8종. 뜻은 화면이 정한다 — 여기 적은 이름을
 * 메뉴 관리(`cust1_name` …)에도 같이 넣어 두면 역할 관리 화면에 그 이름이 나온다.
 */
const CUSTOM_ACTIONS: PermAction[] = [
  { arg: 'cust1', key: 'canCust1', label: '승인' },
  { arg: 'cust2', key: 'canCust2', label: '반려' },
  { arg: 'cust3', key: 'canCust3', label: '마감' },
  { arg: 'cust4', key: 'canCust4', label: '재계산' },
  { arg: 'cust5', key: 'canCust5', label: '이력조회' },
  { arg: 'cust6', key: 'canCust6', label: '알림발송' },
  { arg: 'cust7', key: 'canCust7', label: '일괄변경' },
  { arg: 'cust8', key: 'canCust8', label: '잠금해제' },
];

const ALL_ACTIONS = [...BASE_ACTIONS, ...CUSTOM_ACTIONS];

/**
 * 역할이 하나도 배정되지 않은 계정은 권한 목록이 비어서 온다.
 * 이때는 **전부 막힌다.** 화면에 그 사실을 알려 주지 않으면
 * "왜 버튼이 하나도 없지" 하고 코드부터 뒤지게 된다.
 *
 * 예전에는 이런 계정을 전부 허용으로 다뤘다. 권한을 하나도 주지 않은 계정이
 * 오히려 가장 센 권한을 갖는 셈이라 방향이 거꾸로였다.
 */
const noRoleData = computed(() => permStore.isLoaded && !permStore.hasAnyData);

/** 이 메뉴가 권한 표에 없을 때. 경로를 메뉴에 등록하지 않으면 이렇게 된다. */
const notRegistered = computed(
  () => permStore.isLoaded && permStore.hasAnyData && !permStore.findExact(SAMPLE_PATH),
);

function run(label: string) {
  message.success(`[${label}] 실행`);
}

// ============================================================
// 체험 모드 — 권한을 눌러 가며 결과를 본다
//
// "이 권한을 주면 버튼이 어떻게 보이나" 를 확인하려면 원래는 역할 관리에서
// 체크박스를 켜고 → 저장하고 → 새로고침해야 했다. 그 왕복을 없앤다.
//
// 스토어의 `resolve()` 만 이 경로에 대해 덮어쓴다. 그래서 v-perm · can() ·
// useMenuPermission() 이 **전부 같은 값을 보고** 동시에 다시 그려진다.
// 실제 권한과 서버 판단에는 아무 영향이 없다 — 화면이 보여 주는 모습만 바뀐다.
//
// 라우터 가드가 쓰는 `findExact()` 는 일부러 덮어쓰지 않는다. 열람을 끈 순간
// 보고 있던 이 화면에서 스스로 튕겨 나가면 실험을 끝낼 방법이 없기 때문이다.
// ============================================================

/** 체험 모드 스위치. 화면을 떠났다 돌아와도 유지하려고 컴포넌트가 들고 있다. */
const simOn = ref(false);

/** 체험 중에 만지는 값. 켤 때 실제 권한을 복사해 와서 시작한다. */
const draft = ref<Record<string, boolean>>({});

/**
 * 실제 권한 값. 체험 중에는 `resolve()` 가 덮인 값을 주므로 비교 대상으로 쓸 수 없다.
 * 그래서 체험이 꺼져 있는 동안의 값을 계속 받아 둔다.
 */
const realPermission = ref<MenuPermission>({ ...EMPTY_PERMISSION });

watchEffect(() => {
  if (!permStore.isLoaded || permStore.isSimulating) return;
  realPermission.value = permStore.resolve(SAMPLE_PATH);
});

/** 지금 화면이 실제로 쓰는 값. 체험 중이면 체험 값이다. */
const effective = computed<MenuPermission>(() => perm.permission.value);

function isOn(action: PermAction) {
  return Boolean(effective.value[action.key]);
}

/** 체험 값이 실제 권한과 다른 항목. 무엇을 건드렸는지 표시하는 데 쓴다. */
function isChanged(action: PermAction) {
  return (
    simOn.value &&
    Boolean(effective.value[action.key]) !== Boolean(realPermission.value[action.key])
  );
}

function draftToPermission(): MenuPermission {
  const result: MenuPermission = {
    ...EMPTY_PERMISSION,
    menuId: 'simulation',
    path: SAMPLE_PATH,
  };
  ALL_ACTIONS.forEach((action) => {
    (result as any)[action.key] = Boolean(draft.value[action.key]);
  });
  return result;
}

function pushSimulation() {
  permStore.startSimulation(SAMPLE_PATH, draftToPermission());
}

/** 실제 권한을 초안으로 복사한다. 체험은 항상 현재 상태에서 시작한다. */
function resetDraftFromReal() {
  const next: Record<string, boolean> = {};
  ALL_ACTIONS.forEach((action) => {
    next[action.key] = Boolean(realPermission.value[action.key]);
  });
  draft.value = next;
}

function onSimSwitch(checked: boolean) {
  simOn.value = checked;
  if (checked) {
    resetDraftFromReal();
    pushSimulation();
  } else {
    permStore.stopSimulation();
  }
}

function setAction(action: PermAction, value: boolean) {
  draft.value = { ...draft.value, [action.key]: value };
  pushSimulation();
}

/** 프리셋 — 자주 보는 조합을 한 번에 만든다. */
const PRESETS: { keys: string[]; label: string }[] = [
  { label: '전체', keys: ALL_ACTIONS.map((a) => a.key as string) },
  { label: '읽기만', keys: ['canView', 'canSearch'] },
  {
    label: '편집자',
    keys: ['canView', 'canSearch', 'canCreate', 'canUpdate', 'canExcel', 'canPrint'],
  },
  { label: '없음', keys: [] },
];

function applyPreset(keys: string[]) {
  const on = new Set(keys);
  const next: Record<string, boolean> = {};
  ALL_ACTIONS.forEach((action) => {
    next[action.key] = on.has(action.key as string);
  });
  draft.value = next;
  pushSimulation();
}

/**
 * 화면을 떠날 때는 체험을 끈다. 켜 둔 채로 다른 화면에 가도 그 화면은 영향이
 * 없지만(경로가 다르다), 켜져 있다는 사실이 안 보이는 상태가 되면 나중에
 * 이 화면에 돌아왔을 때 왜 이렇게 보이는지 알 수 없다.
 *
 * 탭이 살아 있는 경우(keep-alive)를 위해 `onDeactivated` 도 함께 잡고,
 * 돌아오면 스위치가 켜져 있던 대로 다시 적용한다.
 */
onDeactivated(() => permStore.stopSimulation());
onBeforeUnmount(() => permStore.stopSimulation());
onActivated(() => {
  if (simOn.value) pushSimulation();
});

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

/**
 * 권한 조합을 문자열 하나로 압축한 값.
 *
 * vxe 액션 컬럼은 슬롯 안에서 `can()` 을 부르므로 값이 바뀌면 다시 그려져야 하는데,
 * 표가 언제 다시 그릴지는 vxe 사정이다. 체험 모드로 값을 바꾼 즉시 보이게
 * 하려고 이 값을 `key` 로 걸어 표를 다시 만든다(행 3개라 비용이 없다).
 */
const permSignature = computed(() =>
  ALL_ACTIONS.map((action) => (isOn(action) ? '1' : '0')).join(''),
);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <Alert
        v-if="simOn"
        banner
        type="info"
      >
        <template #message>
          <span class="text-xs">
            <b>체험 모드</b> — 이 화면에 보이는 모습만 바뀝니다. 실제 권한과 서버 판단은
            그대로이고, 다른 화면도 영향을 받지 않습니다.
          </span>
        </template>
      </Alert>

      <div class="grid min-h-0 flex-1 grid-cols-1 gap-3 lg:grid-cols-[340px_1fr]">
        <!-- 왼쪽: 지금 이 화면에 적용된 권한 · 체험 모드 조작판 -->
        <Card class="overflow-auto" size="small" title="이 화면의 현재 권한">
          <template #extra>
            <span class="flex items-center gap-1 text-xs">
              체험
              <Switch
                :checked="simOn"
                size="small"
                @change="(checked) => onSimSwitch(Boolean(checked))"
              />
            </span>
          </template>

          <Alert
            v-if="noRoleData"
            class="mb-2"
            description="이 계정에 역할을 배정하면 그 권한이 그대로 적용됩니다."
            message="역할이 배정되지 않아 모든 권한이 없습니다."
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

          <p v-if="simOn" class="mb-2 text-xs text-gray-500">
            아래 항목을 눌러 켜고 끄면 오른쪽 네 가지 방법이 모두 즉시 바뀐다.
            <b>*</b> 가 붙은 것은 실제 권한과 달라진 항목이다.
          </p>
          <p v-else class="mb-2 text-xs text-gray-500">
            값을 실제로 바꾸려면
            <RouterLink class="text-primary underline" to="/system/role-map">
              역할 관리
            </RouterLink>
            에서 이 메뉴의 체크박스를 켜고 끈 뒤 새로고침한다. 그냥 결과만 보려면
            위의 <b>체험</b> 스위치를 켠다.
          </p>

          <!-- 체험 중에는 누를 수 있는 태그, 아닐 때는 읽기 전용 -->
          <div v-if="simOn" class="flex flex-wrap gap-1">
            <CheckableTag
              v-for="action in ALL_ACTIONS"
              :key="action.arg"
              :checked="isOn(action)"
              @change="(checked) => setAction(action, Boolean(checked))"
            >
              {{ action.label }}<b v-if="isChanged(action)">*</b>
            </CheckableTag>
          </div>
          <div v-else class="flex flex-wrap gap-1">
            <Tag
              v-for="action in ALL_ACTIONS"
              :key="action.arg"
              :color="isOn(action) ? 'green' : 'default'"
            >
              {{ action.label }}
            </Tag>
          </div>

          <div v-if="simOn" class="mt-3 border-t pt-3">
            <p class="mb-2 text-xs text-gray-500">자주 보는 조합</p>
            <Space wrap>
              <Button
                v-for="preset in PRESETS"
                :key="preset.label"
                size="small"
                @click="applyPreset(preset.keys)"
              >
                {{ preset.label }}
              </Button>
              <Button size="small" type="link" @click="resetDraftFromReal(); pushSimulation()">
                실제 권한으로
              </Button>
            </Space>
          </div>
        </Card>

        <!-- 오른쪽: 쓰는 방법 네 가지 + 한눈에 비교 -->
        <Card class="min-h-0 overflow-auto" size="small">
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
                (이 버튼은 체험 모드의 영향을 받지 않는다. 체험은 이 화면 경로만
                덮어쓰기 때문이다.)
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
              <p class="mt-3 text-xs text-gray-500">
                체험 모드에서 <b>수정</b>만 켜고 <b>삭제</b>를 끄면 첫 번째 버튼이
                나타난다. 둘 다 켜면 다시 사라진다 — 조건 조합이 실제로 도는 것을
                눌러서 확인할 수 있다.
              </p>
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
              <Grid :key="permSignature" />
            </TabPane>

            <TabPane key="matrix" tab="5. 한눈에 비교">
              <p class="mb-2 text-xs text-gray-500">
                같은 권한을 <b>감추기</b>와 <b>잠그기</b>로 각각 그린 결과를 나란히 둔다.
                왼쪽에서 권한을 켜고 끄면 이 표의 같은 줄이 함께 바뀐다.
                감춰진 자리는 비어 보이므로 <i>숨겨짐</i> 이라고 적어 둔다.
              </p>
              <div class="max-h-[calc(100vh-22rem)] overflow-auto">
                <table class="w-full text-xs">
                  <thead class="sticky top-0 bg-gray-50 dark:bg-gray-800">
                    <tr class="text-left">
                      <th class="p-2 font-medium">동작</th>
                      <th class="p-2 font-medium">v-perm 인자</th>
                      <th class="p-2 font-medium">권한</th>
                      <th class="p-2 font-medium">감추기</th>
                      <th class="p-2 font-medium">잠그기 (.disable)</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="action in ALL_ACTIONS"
                      :key="action.arg"
                      class="border-t"
                    >
                      <td class="p-2">
                        {{ action.label }}
                        <b v-if="isChanged(action)" class="text-primary">*</b>
                      </td>
                      <td class="p-2">
                        <code>{{ action.arg }}</code>
                      </td>
                      <td class="p-2">
                        <Tag :color="isOn(action) ? 'green' : 'default'">
                          {{ isOn(action) ? '있음' : '없음' }}
                        </Tag>
                      </td>
                      <td class="p-2">
                        <Button
                          v-perm:[action.arg]
                          size="small"
                          @click="run(action.label)"
                        >
                          {{ action.label }}
                        </Button>
                        <i v-if="!isOn(action)" class="text-gray-400">숨겨짐</i>
                      </td>
                      <td class="p-2">
                        <Button
                          v-perm:[action.arg].disable
                          size="small"
                          @click="run(action.label)"
                        >
                          {{ action.label }}
                        </Button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </TabPane>
          </Tabs>
        </Card>
      </div>
    </div>
  </Page>
</template>
