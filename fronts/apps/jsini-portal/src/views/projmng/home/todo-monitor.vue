<script setup lang="ts">
/**
 * [할일 정산 현황]
 *
 * 원본: ProjMngWasm `Pages/Home/HomeTodoMonitor.razor` (`/home-todo-monitor`).
 * 프로시저: `sp_home_todo_pay`(정산 집계), `sp_home_todo_exec`(할일 목록)
 *
 * 원본은 차트를 붙이려 했다가 그만두고 정산 집계(`sp_home_todo_pay`)만
 * 부르는 상태였다(`OnLoadWrk` 첫 줄이 `OnLoadPay(); return;` 이다).
 * 이식본은 그 동작을 살리고, 원본이 남겨 둔 할일 목록 조회도 탭으로 함께 붙였다.
 *
 * 담당자 드롭다운은 **포털 계정**을 읽는다. 이유는 [할일] 화면과 같다 —
 * 사용자의 정본이 포털 한 곳이라 `projmng.dev_user` 를 보지 않는다.
 */
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { DatePicker, TabPane, Tabs } from 'ant-design-vue';
import dayjs from 'dayjs';

import BizSelect from '#/components/BizSelect.vue';

import GridIconButton from '#/components/GridIconButton.vue';
import { DynamicGrid, SearchBar, useProcGrid } from '../shared';

const targetDate = ref(dayjs());
const userCode = ref('');
const tab = ref('pay');

const {
  result: payResult,
  loading: payLoading,
  load: loadPay,
} = useProcGrid('sp_home_todo_pay');

const {
  result: todoResult,
  loading: todoLoading,
  load: loadTodo,
  save: saveTodo,
  remove: removeTodo,
} = useProcGrid('sp_home_todo_exec');

async function search() {
  if (tab.value === 'pay') {
    await loadPay({ target_user: userCode.value });
  } else {
    await loadTodo({
      target_user: userCode.value,
      target_day: targetDate.value?.format('YYYYMMDD') ?? '',
    });
  }
}

onMounted(search);
</script>

<template>
  <Page auto-content-height>
    <SearchBar class="mb-2">
      <DatePicker v-model:value="targetDate" size="small" @change="search" />
      <BizSelect
        v-model="userCode"
        type="portal_account"
        show-all
        show-search
        option-filter-prop="label"
        placeholder="담당자"
        style="width: 180px"
        @change="search"
      />
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
      </template>
    </SearchBar>

    <Tabs v-model:activeKey="tab" size="small" @change="search">
      <TabPane key="pay" tab="정산 집계">
        <DynamicGrid
          :result="payResult"
          :loading="payLoading"
          export-name="할일정산"
        />
      </TabPane>
      <TabPane key="todo" tab="할일 목록">
        <DynamicGrid
          :result="todoResult"
          :loading="todoLoading"
          export-name="할일목록"
          @save="saveTodo()"
          @delete="removeTodo"
        />
      </TabPane>
    </Tabs>
  </Page>
</template>
