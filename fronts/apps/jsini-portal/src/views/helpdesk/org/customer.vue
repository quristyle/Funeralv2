<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { computed, onMounted } from 'vue';

import { Page } from '@vben/common-ui';

import { Tag } from 'ant-design-vue';

import {
  createCustomer,
  deleteCustomer,
  getCustomerList,
  updateCustomer,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import CrudTable from '../shared/crud-table.vue';
import { formatDateTime } from '../shared/constants';

/** [고객 사용자] 원본 Customer.vue */

const helpdesk = useHelpdeskStore();

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 70 },
  { dataIndex: 'loginId', key: 'loginId', title: '로그인 ID', width: 130 },
  { dataIndex: 'userName', key: 'userName', title: '이름', width: 120 },
  { dataIndex: ['company', 'name'], key: 'company', title: '회사', width: 140 },
  { dataIndex: 'email', key: 'email', title: '이메일' },
  { dataIndex: 'status', key: 'status', title: '상태', width: 90 },
  { dataIndex: 'createdAt', key: 'createdAt', title: '등록일', width: 160 },
];

const fields = computed<CrudField[]>(() => [
  { disabledOnEdit: true, key: 'loginId', label: '로그인 ID', required: true },
  { key: 'userName', label: '이름', required: true },
  { key: 'email', label: '이메일' },
  {
    key: 'companyId',
    label: '소속 회사',
    options: helpdesk.companies.map((c) => ({ label: c.name, value: c.id })),
    required: true,
    type: 'select',
  },
  // 비밀번호 칸은 없앴다 — 계정과 인증은 JSini 관리 포털이 단독으로 맡는다(결정 Q4).
]);

/** 고객 계정 상태 뱃지 색 */
function statusColor(status?: string) {
  if (status === 'Active') return 'success';
  if (status === 'Rejected') return 'error';
  return 'default';
}

onMounted(() => helpdesk.loadOrganizations());
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />
    <CrudTable
      :columns="columns"
      :create="createCustomer"
      :fetch="getCustomerList"
      :fields="fields"
      :remove="deleteCustomer"
      :search-keys="['loginId', 'userName', 'email']"
      :update="updateCustomer"
      entity-name="고객"
    >
      <template #cell="{ column, record }">
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
        <template v-else-if="column.key === 'status'">
          <Tag :color="statusColor(record.status)">{{ record.status }}</Tag>
        </template>
      </template>
    </CrudTable>
  </Page>
</template>
