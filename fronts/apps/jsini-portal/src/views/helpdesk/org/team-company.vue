<script lang="ts" setup>
import type { Company, Team } from '#/api/helpdesk';

import { computed, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Card,
  Col,
  List,
  ListItem,
  message,
  Row,
  Spin,
  Transfer,
} from 'ant-design-vue';

import {
  getCompanyList,
  getTeamCompanies,
  getTeamList,
  setTeamCompanies,
} from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [팀-고객사 배정]
 *
 * 원본(TeamCompany.vue)의 PickList 를 AntD Transfer 로 옮겼다.
 * 원본과 동일하게 옮기는 즉시 저장한다.
 */

const loading = ref(false);
const saving = ref(false);
const teams = ref<Team[]>([]);
const companies = ref<Company[]>([]);
const selectedTeamId = ref<number | undefined>();
/** 이 팀에 배정된 회사 ID 목록 = Transfer 의 오른쪽 */
const assignedKeys = ref<string[]>([]);

const transferData = computed(() =>
  companies.value.map((c) => ({ key: String(c.id), title: c.name })),
);

async function loadTeamCompanies(teamId?: number) {
  if (!teamId) {
    assignedKeys.value = [];
    return;
  }

  loading.value = true;
  try {
    const assigned = (await getTeamCompanies(teamId)) ?? [];
    assignedKeys.value = assigned.map((c) => String(c.id));
  } finally {
    loading.value = false;
  }
}

/** 옮기는 즉시 저장한다(원본의 autoSave 와 같은 동작). */
async function onTransferChange(nextTargetKeys: string[]) {
  assignedKeys.value = nextTargetKeys;
  if (!selectedTeamId.value) return;

  saving.value = true;
  try {
    await setTeamCompanies(
      selectedTeamId.value,
      nextTargetKeys.map((k) => Number(k)),
    );
    message.success('배정을 저장했습니다.');
  } catch {
    // 저장에 실패하면 서버 상태로 되돌린다.
    await loadTeamCompanies(selectedTeamId.value);
  } finally {
    saving.value = false;
  }
}

watch(selectedTeamId, (id) => loadTeamCompanies(id));

onMounted(async () => {
  loading.value = true;
  try {
    const [teamList, companyList] = await Promise.all([
      getTeamList(),
      getCompanyList(),
    ]);
    teams.value = teamList ?? [];
    companies.value = companyList ?? [];
    selectedTeamId.value = teams.value[0]?.id;
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Row :gutter="[12, 12]">
      <Col :lg="6" :xs="24">
        <Card :body-style="{ padding: 0 }" size="small" title="팀">
          <List
            :data-source="teams"
            :locale="{ emptyText: '등록된 팀이 없습니다.' }"
            size="small"
          >
            <template #renderItem="{ item }">
              <ListItem
                class="cursor-pointer px-3"
                :class="
                  item.id === selectedTeamId ? 'bg-accent font-medium' : ''
                "
                @click="selectedTeamId = item.id"
              >
                {{ item.name }}
              </ListItem>
            </template>
          </List>
        </Card>
      </Col>

      <Col :lg="18" :xs="24">
        <Card size="small" title="배정 고객사">
          <Spin :spinning="loading || saving">
            <Transfer
              :data-source="transferData"
              :list-style="{ height: '420px', width: '46%' }"
              :render="(item: any) => item.title"
              :target-keys="assignedKeys"
              :titles="['미배정', '배정됨']"
              show-search
              @change="onTransferChange"
            />
          </Spin>
          <div class="mt-2 text-xs text-muted-foreground">
            항목을 옮기면 즉시 저장됩니다.
          </div>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
