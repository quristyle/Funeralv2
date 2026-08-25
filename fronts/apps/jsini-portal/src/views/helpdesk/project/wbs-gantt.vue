<script lang="ts" setup>
import type { WbsLink, WbsTreeNode } from '#/api/helpdesk';

import { ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Checkbox, message, Space, Spin } from 'ant-design-vue';

import { getWbsLinks, getWbsTree, updateWbs } from '#/api/helpdesk';
import BizSelect from '#/components/BizSelect.vue';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import GanttChart from './modules/gantt-chart.vue';

/**
 * [WBS 간트]
 *
 * 원본(WbsGantt.vue)의 dhtmlx-gantt 화면. 막대를 끌어 일정을 바꾸면 바로 저장한다.
 */

const props = withDefaults(defineProps<{ readonly?: boolean }>(), {
  readonly: false,
});

const loading = ref(false);
const selectedProjectId = ref<number | undefined>();
const nodes = ref<WbsTreeNode[]>([]);
const links = ref<WbsLink[]>([]);
const showLinks = ref(true);

async function loadData() {
  if (!selectedProjectId.value) {
    nodes.value = [];
    links.value = [];
    return;
  }

  loading.value = true;
  try {
    const [tree, linkList] = await Promise.all([
      getWbsTree(selectedProjectId.value),
      getWbsLinks(selectedProjectId.value).catch(() => []),
    ]);
    nodes.value = tree ?? [];
    links.value = linkList ?? [];
  } finally {
    loading.value = false;
  }
}

/** 막대를 옮기거나 늘리면 즉시 저장한다(원본과 동일). */
async function onBarChange(payload: {
  planEnd: string;
  planStart: string;
  wbsRid: number;
}) {
  await updateWbs(payload.wbsRid, {
    planEnd: payload.planEnd,
    planStart: payload.planStart,
    wbsRid: payload.wbsRid,
  });
  message.success('일정을 저장했습니다.');
  await loadData();
}

watch(selectedProjectId, loadData);
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Card class="mb-3" size="small">
      <Space wrap>
        <!-- BizSelect 는 너비 100% 라 바깥에서 폭을 정한다 -->
        <div style="width: 240px">
          <BizSelect
            v-model:value="selectedProjectId"
            auto-select-first
            option-filter-prop="label"
            placeholder="프로젝트"
            show-search
            type="helpdesk_project"
          />
        </div>
        <Checkbox v-model:checked="showLinks">연결선 표시</Checkbox>
        <span class="text-xs text-muted-foreground">
          눈금 단위는 차트 왼쪽 위에서 일/주/월로 바꿉니다.
        </span>
      </Space>
    </Card>

    <Card :body-style="{ padding: '12px' }" size="small">
      <Spin :spinning="loading">
        <GanttChart
          :links="links"
          :nodes="nodes"
          :readonly="props.readonly"
          :show-links="showLinks"
          @change="onBarChange"
        />
      </Spin>
    </Card>
  </Page>
</template>
