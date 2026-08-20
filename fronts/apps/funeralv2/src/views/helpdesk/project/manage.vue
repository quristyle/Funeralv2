<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Button, Space } from 'ant-design-vue';

import {
  createProject,
  deleteProject,
  getProjects,
  getTeamList,
  updateProject,
} from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import CrudTable from '../shared/crud-table.vue';
import { formatDate } from '../shared/constants';

/** [프로젝트 관리] 원본 MngProject.vue */

const router = useRouter();
const teams = ref<{ label: string; value: number }[]>([]);

const columns = [
  { dataIndex: 'id', key: 'id', title: 'ID', width: 70 },
  { dataIndex: 'name', key: 'name', title: '프로젝트명' },
  { dataIndex: ['team', 'name'], key: 'team', title: '담당 팀', width: 150 },
  { dataIndex: 'projectStart', key: 'projectStart', title: '시작일', width: 120 },
  { dataIndex: 'projectEnd', key: 'projectEnd', title: '종료일', width: 120 },
  { key: 'links', title: '바로가기', width: 190 },
];

const fields = computed<CrudField[]>(() => [
  { key: 'name', label: '프로젝트명', required: true },
  { key: 'teamId', label: '담당 팀', options: teams.value, type: 'select' },
  { key: 'projectStart', label: '시작일 (YYYY-MM-DD)' },
  { key: 'projectEnd', label: '종료일 (YYYY-MM-DD)' },
]);

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
      :create="createProject"
      :fetch="getProjects"
      :fields="fields"
      :remove="deleteProject"
      :search-keys="['name']"
      :update="updateProject"
      entity-name="프로젝트"
    >
      <template #cell="{ column, record }">
        <template v-if="column.key === 'projectStart'">
          {{ formatDate(record.projectStart) }}
        </template>
        <template v-else-if="column.key === 'projectEnd'">
          {{ formatDate(record.projectEnd) }}
        </template>
        <template v-else-if="column.key === 'links'">
          <Space @click.stop>
            <Button
              size="small"
              type="link"
              @click="router.push(`/helpdesk/project/info?projectId=${record.id}`)"
            >
              현황
            </Button>
            <Button size="small" type="link" @click="router.push('/helpdesk/project/wbs')">
              WBS
            </Button>
          </Space>
        </template>
      </template>
    </CrudTable>
  </Page>
</template>
