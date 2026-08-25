<script lang="ts" setup>
import type { CrudField } from '../shared/crud-table.vue';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Button, Space } from 'ant-design-vue';

import { fetchBizOptions } from '#/api/biz-select';
import {
  createProject,
  deleteProject,
  getProjects,
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
  { key: 'projectStart', label: '시작일', type: 'date' },
  { key: 'projectEnd', label: '종료일', type: 'date' },
]);

/** DatePicker 는 'YYYY-MM-DD' 만 받으므로 ISO 문자열을 잘라 넘긴다. */
async function fetchProjects() {
  const list = (await getProjects()) ?? [];
  return list.map((p) => ({
    ...p,
    projectEnd: p.projectEnd ? String(p.projectEnd).slice(0, 10) : undefined,
    projectStart: p.projectStart ? String(p.projectStart).slice(0, 10) : undefined,
  }));
}

onMounted(async () => {
  // 팀 목록은 메타데이터(helpdesk_team)가 가리키는 API 에서 읽는다.
  teams.value = (await fetchBizOptions('helpdesk_team')).options;
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />
    <CrudTable
      :columns="columns"
      :create="createProject"
      :fetch="fetchProjects"
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
