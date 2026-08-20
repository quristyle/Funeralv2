<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { Page } from '@vben/common-ui';

import {
  createCompany,
  deleteCompany,
  getCompanyList,
  updateCompany,
} from '#/api/helpdesk';

import CrudTable from '../shared/crud-table.vue';
import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime } from '../shared/constants';

/** [고객사] 원본 Company.vue */

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 80 },
  { dataIndex: 'name', key: 'name', title: '회사명' },
  { dataIndex: 'createdAt', key: 'createdAt', title: '등록일', width: 160 },
];

const fields: CrudField[] = [
  { key: 'name', label: '회사명', required: true },
];
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />
    <CrudTable
      :columns="columns"
      :create="createCompany"
      :fetch="getCompanyList"
      :fields="fields"
      :remove="deleteCompany"
      :update="updateCompany"
      :search-keys="['name']"
      entity-name="고객사"
    >
      <template #cell="{ column, record }">
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
      </template>
    </CrudTable>
  </Page>
</template>
