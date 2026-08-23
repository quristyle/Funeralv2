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
  Table,
  Tag,
} from 'ant-design-vue';

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
 */

const helpdesk = useHelpdeskStore();

const loading = ref(false);
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

const columns = [
  { dataIndex: 'authUserId', key: 'authUserId', title: 'JSini 포털 계정' },
  { dataIndex: 'userType', key: 'userType', title: '구분', width: 100 },
  { dataIndex: 'userName', key: 'userName', title: '헬프데스크 사용자' },
  {
    dataIndex: 'helpdeskUserId',
    key: 'helpdeskUserId',
    title: '내부 ID',
    width: 90,
  },
  { dataIndex: 'createdAt', key: 'createdAt', title: '연결일', width: 160 },
  { key: 'action', title: '', width: 80 },
];

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

async function loadData() {
  loading.value = true;
  try {
    links.value = (await getAuthLinks()) ?? [];
  } finally {
    loading.value = false;
  }
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
  await Promise.all([
    helpdesk.loadIdentity(),
    helpdesk.loadOrganizations(),
    loadData(),
    loadAccounts(),
  ]);
});
</script>

<template>
  <Page auto-content-height>
    <Alert
      class="mb-3"
      show-icon
      type="info"
      message="JSini 포털 계정과 헬프데스크 사용자 연결"
      description="헬프데스크의 기존 데이터(요청 작성자·담당자·댓글)는 헬프데스크 내부 계정 ID를 참조합니다. JSini 포털 계정으로 로그인한 사용자가 그 데이터의 주인으로 인식되려면 이 화면에서 두 계정을 연결해야 합니다."
    />

    <Card class="mb-3" size="small" title="내 연결 상태">
      <Descriptions v-if="helpdesk.identity" :column="{ md: 4, xs: 1 }" size="small">
        <DescriptionsItem label="헬프데스크 사용자">
          {{ helpdesk.identity.userName }}
        </DescriptionsItem>
        <DescriptionsItem label="구분">
          <Tag :color="helpdesk.isAdmin ? 'blue' : 'green'">
            {{ helpdesk.isAdmin ? '담당자' : '고객' }}
          </Tag>
        </DescriptionsItem>
        <DescriptionsItem label="내부 ID">
          {{ helpdesk.identity.helpdeskUserId }}
        </DescriptionsItem>
        <DescriptionsItem label="소속 회사 ID">
          {{ helpdesk.identity.companyId ?? '-' }}
        </DescriptionsItem>
      </Descriptions>
      <Empty v-else description="현재 계정은 헬프데스크 사용자와 연결되어 있지 않습니다." />
    </Card>

    <Card :body-style="{ padding: 0 }" size="small" title="연결 목록">
      <template #extra>
        <Button v-perm:create size="small" type="primary" @click="openCreate">
          연결 추가
        </Button>
      </template>

      <Table
        :columns="columns"
        :data-source="links"
        :loading="loading"
        row-key="id"
        size="small"
      >
        <template #emptyText>
          <Empty description="등록된 연결이 없습니다." />
        </template>

        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'userType'">
            <Tag :color="record.userType === 'admin' ? 'blue' : 'green'">
              {{ record.userType === 'admin' ? '담당자' : '고객' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Popconfirm
              cancel-text="취소"
              ok-text="해제"
              title="연결을 해제할까요?"
              @confirm="onDelete(record as AuthUserLink)"
            >
              <Button danger size="small" type="link">해제</Button>
            </Popconfirm>
          </template>
        </template>
      </Table>
    </Card>

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
