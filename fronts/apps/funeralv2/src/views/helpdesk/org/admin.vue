<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { Page } from '@vben/common-ui';

import { Tag } from 'ant-design-vue';

import {
  createAdmin,
  deleteAdmin,
  getAdminList,
  updateAdmin,
} from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import CrudTable from '../shared/crud-table.vue';
import { formatDateTime } from '../shared/constants';

/** [담당자] 원본 Admins.vue */

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 70 },
  { dataIndex: 'loginId', key: 'loginId', title: '로그인 ID', width: 130 },
  { dataIndex: 'userName', key: 'userName', title: '이름', width: 120 },
  { dataIndex: 'email', key: 'email', title: '이메일' },
  { dataIndex: 'adminTeams', key: 'adminTeams', title: '소속 팀', width: 200 },
  { dataIndex: 'createdAt', key: 'createdAt', title: '등록일', width: 160 },
];

const fields: CrudField[] = [
  { disabledOnEdit: true, key: 'loginId', label: '로그인 ID', required: true },
  { key: 'userName', label: '이름', required: true },
  { key: 'email', label: '이메일' },
  { createOnly: true, key: 'password', label: '비밀번호', type: 'password' },
];
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />
    <CrudTable
      :columns="columns"
      :create="createAdmin"
      :fetch="getAdminList"
      :fields="fields"
      :remove="deleteAdmin"
      :search-keys="['loginId', 'userName', 'email']"
      :update="updateAdmin"
      entity-name="담당자"
    >
      <template #cell="{ column, record }">
        <template v-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
        <template v-else-if="column.key === 'adminTeams'">
          <Tag v-for="at in record.adminTeams ?? []" :key="at.teamId">
            {{ at.team?.name ?? at.teamId }}
          </Tag>
        </template>
      </template>
    </CrudTable>
  </Page>
</template>
