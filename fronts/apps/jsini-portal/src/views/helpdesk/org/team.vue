<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { Page } from '@vben/common-ui';

import { createTeam, deleteTeam, getTeamList, updateTeam } from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import CrudTable from '../shared/crud-table.vue';
import { formatDateTime } from '../shared/constants';

/** [팀] 원본 Teams.vue */

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 80 },
  { dataIndex: 'name', key: 'name', title: '팀명' },
  { dataIndex: 'remark', key: 'remark', title: '비고' },
  { dataIndex: 'createdAt', key: 'createdAt', title: '등록일', width: 160 },
];

const fields: CrudField[] = [
  { key: 'name', label: '팀명', required: true },
  { key: 'remark', label: '비고' },
];
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />
    <CrudTable
      :columns="columns"
      :create="createTeam"
      :fetch="getTeamList"
      :fields="fields"
      :remove="deleteTeam"
      :search-keys="['name', 'remark']"
      :update="updateTeam"
      entity-name="팀"
    >
      <template #cell="{ column, record }">
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
      </template>
    </CrudTable>
  </Page>
</template>
