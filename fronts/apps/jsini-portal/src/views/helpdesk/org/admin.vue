<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Tag } from 'ant-design-vue';

import {
  createAdmin,
  deleteAdmin,
  getAdminList,
  getTeamList,
  updateAdmin,
} from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import CrudTable from '../shared/crud-table.vue';
import { formatDateTime } from '../shared/constants';

/**
 * [담당자] 원본 Admins.vue
 *
 * 원본은 표 안에서 소속 팀을 MultiSelect 로 바꿨다. 여기서는 등록/수정 폼에서 고른다.
 * 서버는 팀 목록을 `adminTeams: [{ teamId }]` 형태로 받으므로 저장 직전에 변환한다.
 */

const teams = ref<{ label: string; value: number }[]>([]);

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 70 },
  { dataIndex: 'loginId', key: 'loginId', title: '로그인 ID', width: 130 },
  { dataIndex: 'userName', key: 'userName', title: '이름', width: 120 },
  { dataIndex: 'email', key: 'email', title: '이메일' },
  { dataIndex: 'adminTeams', key: 'adminTeams', title: '소속 팀', width: 200 },
  { dataIndex: 'createdAt', key: 'createdAt', title: '등록일', width: 160 },
];

const fields = computed<CrudField[]>(() => [
  { disabledOnEdit: true, key: 'loginId', label: '로그인 ID', required: true },
  { key: 'userName', label: '이름', required: true },
  { key: 'email', label: '이메일' },
  {
    key: 'teamIds',
    label: '소속 팀',
    options: teams.value,
    type: 'multiselect',
  },
  { createOnly: true, key: 'password', label: '비밀번호', type: 'password' },
]);

/** 폼이 다루는 teamIds 를 서버가 받는 adminTeams 로 바꿔 보낸다. */
function toPayload(data: Record<string, any>) {
  const { teamIds, ...rest } = data;
  return {
    ...rest,
    adminTeams: (teamIds ?? []).map((teamId: number) => ({ teamId })),
  };
}

/** 목록을 받아 폼이 쓰는 teamIds 를 채워 둔다. */
async function fetchAdmins() {
  const list = (await getAdminList()) ?? [];
  return list.map((admin) => ({
    ...admin,
    teamIds: (admin.adminTeams ?? []).map((at) => at.teamId),
  }));
}

onMounted(async () => {
  const list = (await getTeamList()) ?? [];
  teams.value = list.map((t) => ({ label: t.name, value: t.id }));
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />
    <CrudTable
      :columns="columns"
      :create="(data) => createAdmin(toPayload(data))"
      :fetch="fetchAdmins"
      :fields="fields"
      :remove="deleteAdmin"
      :search-keys="['loginId', 'userName', 'email']"
      :update="(id, data) => updateAdmin(id, toPayload(data))"
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
