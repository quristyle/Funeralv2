<script lang="ts" setup>
import type { AuthUserLink, HelpdeskUserType } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Descriptions,
  DescriptionsItem,
  Empty,
  Form,
  FormItem,
  message,
  Modal,
  Popconfirm,
  Select,
  Space,
  Tag,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import {
  deleteAuthLink,
  getAuthLinks,
  saveAuthLink,
} from '#/api/helpdesk';
import { getAccounts } from '#/api/portal/system/account';
import { useHelpdeskStore } from '#/store/helpdesk';

import { formatDateTime } from '../shared/constants';

/**
 * [계정 연결]
 *
 * funeralv2 계정과 헬프데스크 계정(담당자/고객)을 이어주는 화면.
 *
 * 계정은 funeralv2 로 단일화했지만, 기존 헬프데스크 데이터(요청 작성자·담당자·댓글)는
 * 모두 헬프데스크 내부 ID 를 참조한다. 그 ID 를 버릴 수 없어 매핑 테이블로 두 체계를 잇는다.
 * 로그인 아이디가 같아도 서로 다른 사람인 경우가 실제로 있어 자동 매칭은 걸지 않았다.
 * 여기서 사람이 직접 확인하고 연결한다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 * 전량 조회는 그대로다 — 연결은 많아야 수십 건이다.
 * ------------------------------------------------------------
 */

const helpdesk = useHelpdeskStore();

/** 조회한 연결 목록. 표가 그리는 것과 같은 배열이다(중복 연결 표시에 쓴다). */
const links = ref<AuthUserLink[]>([]);
/**
 * 포털 계정 목록.
 *
 * 예전에는 이 화면이 아이디를 직접 타이핑해서 받았다. 오타가 나면 아무 데도 연결되지 않는
 * 매핑이 조용히 만들어지고, 그 계정으로 로그인해도 헬프데스크 데이터가 비어 보였다.
 * 실재하는 계정 중에서 고르게 바꾼다.
 */
const accounts = ref<any[]>([]);
const accountsError = ref('');
const modalOpen = ref(false);
const saving = ref(false);

const form = reactive<{
  authUserId: string;
  helpdeskUserId: number | undefined;
  userType: HelpdeskUserType;
}>({
  authUserId: '',
  helpdeskUserId: undefined,
  userType: 'admin',
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'authUserId', minWidth: 180, title: 'JSini 포털 계정' },
      {
        field: 'userType',
        params: {
          filterOptions: [
            { label: '담당자', value: 'admin' },
            { label: '고객', value: 'customer' },
          ],
        },
        slots: { default: 'userType' },
        title: '구분',
        width: 100,
      },
      { field: 'userName', minWidth: 160, title: '헬프데스크 사용자' },
      { field: 'helpdeskUserId', title: '내부 ID', width: 90 },
      {
        field: 'createdAt',
        params: { filterText: (row: any) => formatDateTime(row.createdAt) },
        slots: { default: 'createdAt' },
        title: '연결일',
        width: 160,
      },
      { field: 'action', slots: { default: 'action' }, title: '', width: 90 },
    ],
    emptyText: '등록된 연결이 없습니다.',
    // 아래 도구줄의 [추가] — 위쪽 아이콘과 같은 함수를 부른다.
    // (`gridFeatures` 는 vxe 타입에 없다. 공통 레이어가 읽고 떼어 낸다.)
    gridFeatures: { onCreate: () => openCreate() },
    height: 'auto',
    // 전량 조회다. 페이저를 켜 두면 vxe 가 응답을 `{result,page}` 로 읽어 한 줄도 안 나온다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          links.value = (await getAuthLinks()) ?? [];
          return links.value;
        },
      },
    },
    rowConfig: { keyField: 'id' },
  } as any,
});

/** 이미 연결된 포털 계정. 목록에 표시해 중복 연결을 눈으로 막는다. */
const linkedAuthUserIds = computed(
  () => new Set(links.value.map((l) => l.authUserId)),
);

/** 연결할 포털 계정 선택 목록 */
const accountOptions = computed(() =>
  accounts.value.map((a) => {
    const id = String(a.loginId ?? a.username ?? a.userId ?? '');
    const name = String(a.userName ?? a.realName ?? '');
    return {
      label: `${id}${name ? ` — ${name}` : ''}${linkedAuthUserIds.value.has(id) ? ' (연결됨)' : ''}`,
      value: id,
    };
  }),
);

/** 선택한 구분에 따라 담당자 목록 또는 고객 목록을 보여준다. */
const userOptions = computed(() =>
  form.userType === 'admin'
    ? helpdesk.admins.map((a) => ({
        label: `${a.userName} (${a.loginId})`,
        value: a.id,
      }))
    : helpdesk.customers.map((c) => ({
        label: `${c.userName} (${c.loginId})`,
        value: c.id,
      })),
);

/** 목록을 다시 조회한다. 실제 조회는 그리드의 `proxyConfig` 가 한다. */
async function loadData() {
  await gridApi.query();
}

/** 포털 계정 목록. 실패해도 화면은 뜨게 두고 안내만 남긴다. */
async function loadAccounts() {
  try {
    accounts.value = (await getAccounts()) ?? [];
    accountsError.value = '';
  } catch {
    accounts.value = [];
    accountsError.value =
      '포털 계정 목록을 불러오지 못했습니다. 계정 관리 권한이 필요할 수 있습니다.';
  }
}

function openCreate() {
  form.authUserId = '';
  form.userType = 'admin';
  form.helpdeskUserId = undefined;
  modalOpen.value = true;
}

async function onSave() {
  if (!form.authUserId.trim()) {
    message.warning('연결할 JSini 포털 계정을 선택하세요.');
    return;
  }
  if (!form.helpdeskUserId) {
    message.warning('연결할 헬프데스크 사용자를 선택하세요.');
    return;
  }

  saving.value = true;
  try {
    await saveAuthLink({
      authUserId: form.authUserId.trim(),
      helpdeskUserId: form.helpdeskUserId,
      userType: form.userType,
    });
    message.success('계정 연결을 저장했습니다.');
    modalOpen.value = false;
    await loadData();
    // 내 계정을 방금 연결했을 수 있으니 신원을 다시 읽는다.
    await helpdesk.loadIdentity(true);
  } finally {
    saving.value = false;
  }
}

async function onDelete(row: AuthUserLink) {
  await deleteAuthLink(row.id);
  message.success('연결을 해제했습니다.');
  await loadData();
  await helpdesk.loadIdentity(true);
}

onMounted(async () => {
  // 연결 목록은 그리드가 뜨면서 스스로 한 번 조회한다.
  await Promise.all([
    helpdesk.loadIdentity(),
    helpdesk.loadOrganizations(),
    loadAccounts(),
  ]);
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Alert
      class="mb-3"
      show-icon
      type="info"
      message="JSini 포털 계정과 헬프데스크 사용자 연결"
      description="헬프데스크의 기존 데이터(요청 작성자·담당자·댓글)는 헬프데스크 내부 계정 ID를 참조합니다. JSini 포털 계정으로 로그인한 사용자가 그 데이터의 주인으로 인식되려면 이 화면에서 두 계정을 연결해야 합니다."
    />

    <Card class="mb-3" size="small" title="내 연결 상태">
      <!-- 연결이 있을 때만 헬프데스크 레코드를 적는다. -->
      <Descriptions v-if="helpdesk.isLinked" :column="{ md: 4, xs: 1 }" size="small">
        <DescriptionsItem label="헬프데스크 사용자">
          {{ helpdesk.identity?.userName }}
        </DescriptionsItem>
        <DescriptionsItem label="구분">
          <Tag
            :color="helpdesk.identity?.loginType === 'admin' ? 'blue' : 'green'"
          >
            {{ helpdesk.identity?.loginType === 'admin' ? '담당자' : '고객' }}
          </Tag>
        </DescriptionsItem>
        <DescriptionsItem label="내부 ID">
          {{ helpdesk.identity?.helpdeskUserId }}
        </DescriptionsItem>
        <DescriptionsItem label="소속 회사 ID">
          {{ helpdesk.identity?.companyId ?? '-' }}
        </DescriptionsItem>
      </Descriptions>
      <!--
        연결이 없어도 포털 관리자 역할이면 조회·관리는 된다. 그 상태를 '아무것도 안 됨'
        으로 보여 주면 사실과 다르므로 무엇이 되고 무엇이 안 되는지 적는다.
      -->
      <Alert
        v-else-if="helpdesk.isUnlinkedAdmin"
        show-icon
        type="info"
        message="포털 관리자 역할로 조회·관리하고 있습니다."
        description="헬프데스크 담당자 레코드에는 이어져 있지 않습니다. 나에게 배정된 요청·내가 쓴 댓글·알림 구독처럼 '내 것'을 가리키는 기능만 비어 있습니다. 필요하면 아래에서 이 계정을 담당자 레코드와 연결하세요."
      />
      <Empty
        v-else
        description="현재 계정은 헬프데스크 사용자와 연결되어 있지 않습니다."
      />
    </Card>

    <!-- 표를 카드로 감싸지 않는다 — 감싸면 page-fill-last 가 표에 높이를 못 준다. -->
    <div class="mb-2 flex items-center justify-between">
      <span class="text-sm font-medium">연결 목록</span>
      <GridIconButton
        v-perm:create
        icon="vxe-icon-add"
        title="연결 추가"
        @click="openCreate"
      />
    </div>

    <Grid>
      <template #userType="{ row }">
        <Tag :color="row.userType === 'admin' ? 'blue' : 'green'">
          {{ row.userType === 'admin' ? '담당자' : '고객' }}
        </Tag>
      </template>
      <template #createdAt="{ row }">
        {{ formatDateTime(row.createdAt) }}
      </template>
      <template #action="{ row }">
        <Popconfirm
          cancel-text="취소"
          ok-text="해제"
          title="연결을 해제할까요?"
          @confirm="onDelete(row as AuthUserLink)"
        >
          <Button danger size="small" type="link">해제</Button>
        </Popconfirm>
      </template>
    </Grid>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      cancel-text="취소"
      ok-text="저장"
      title="계정 연결 추가"
      @ok="onSave"
    >
      <Form layout="vertical">
        <FormItem label="JSini 포털 계정" required>
          <Select
            v-model:value="form.authUserId"
            :options="accountOptions"
            option-filter-prop="label"
            placeholder="연결할 포털 계정을 선택하세요"
            show-search
          />
          <div v-if="accountsError" class="mt-1 text-xs text-warning">
            {{ accountsError }}
          </div>
        </FormItem>
        <FormItem label="구분">
          <Select
            v-model:value="form.userType"
            :options="[
              { label: '담당자(관리자)', value: 'admin' },
              { label: '고객', value: 'customer' },
            ]"
            @change="form.helpdeskUserId = undefined"
          />
        </FormItem>
        <FormItem label="헬프데스크 사용자" required>
          <Select
            v-model:value="form.helpdeskUserId"
            :options="userOptions"
            option-filter-prop="label"
            placeholder="연결할 사용자를 선택하세요"
            show-search
          />
        </FormItem>
      </Form>

      <Space direction="vertical" size="small">
        <span class="text-xs text-muted-foreground">
          아이디가 같아도 다른 사람일 수 있으니 이름을 확인하고 연결하세요.
        </span>
      </Space>
    </Modal>
  </Page>
</template>
