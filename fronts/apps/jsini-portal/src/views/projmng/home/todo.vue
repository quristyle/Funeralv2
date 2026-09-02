<script setup lang="ts">
/**
 * [할일]
 *
 * 원본: ProjMngWasm `Pages/Home/HomeTodo.razor` (`/home-todo`).
 * 프로시저: `sp_home_todo_exec`(조회·저장·삭제), `sp_home_todo_make`(생성)
 *
 * 날짜별 할일을 다룬다. "생성" 은 그 날짜의 할일을 규칙에 따라
 * 서버가 한꺼번에 만들어 주는 동작이다(원본 `OnMakeWrk`).
 *
 * 담당자 드롭다운은 **포털 계정**을 읽는다. 이식 전에는 프로젝트관리가 들고 있던
 * 자체 사용자 테이블(`projmng.dev_user`)을 `sp_projCommon` 의 `user` 코드로 읽었는데,
 * 사용자의 정본은 포털 한 곳이라 그쪽으로 옮겼다.
 * 값은 로그인 아이디다 — `home_todo.target_user` 에 쌓인 값이 아이디이기 때문이다.
 */
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { DatePicker } from 'ant-design-vue';
import dayjs from 'dayjs';

import { dbCont } from '#/api/projmng';
import BizSelect from '#/components/BizSelect.vue';

import GridIconButton from '#/components/GridIconButton.vue';
import { CodeSelect, DynamicGrid, SearchBar, useProcGrid } from '../shared';

const PROC = 'sp_home_todo_exec';

const targetDate = ref(dayjs());
const userCode = ref('');
const completeYn = ref('');
const todoState = ref('');

const { result, loading, load, save, remove } = useProcGrid(PROC);

function params() {
  return {
    target_user: userCode.value,
    target_day: targetDate.value?.format('YYYYMMDD') ?? '',
    is_complete: completeYn.value,
    todo_state: todoState.value,
  };
}

function search() {
  return load(params());
}

/** 그 날짜의 할일을 서버가 만들어 준다. 만든 뒤 바로 다시 읽는다. */
async function make() {
  await dbCont('sp_home_todo_make', {
    target_day: targetDate.value?.format('YYYYMMDD') ?? '',
  });
  await search();
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
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
      />
      <CodeSelect v-model="completeYn" code-id="yn" show-all />
      <CodeSelect v-model="todoState" code-id="todo_state" show-all />
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
        <GridIconButton
          v-perm:create
          icon="vxe-icon-add"
          title="생성"
          @click="make"
        />
        <GridIconButton
          v-perm:update
          icon="vxe-icon-save"
          title="저장"
          @click="save()"
        />
      </template>
    </SearchBar>

    <DynamicGrid
      :result="result"
      :loading="loading"
      export-name="할일"
      @save="save()"
      @delete="remove"
    />
  </Page>
</template>
