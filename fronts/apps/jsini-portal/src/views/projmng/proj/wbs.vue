<script setup lang="ts">
/**
 * [프로젝트 WBS]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjWbs.razor` (`/proj-wbs`).
 * 프로시저: `sp_proj_wbs_exec` (`schedule_type = 'WBS'` 고정)
 *
 * 프로젝트를 고르면 그 프로젝트의 WBS 항목이 나온다.
 * 행을 새로 만들거나 복사하면 프로젝트 키(`prj_rid`)를 자동으로 채운다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import { CodeSelect, DynamicGrid, SearchBar, useProcGrid } from '../shared';

const projectCode = ref('');
const completeState = ref('');

const { result, loading, load, save, remove } = useProcGrid('sp_proj_wbs_exec');

function params() {
  return {
    prj_rid: projectCode.value,
    compstat: completeState.value,
    schedule_type: 'WBS',
  };
}

function search() {
  return load(params());
}

/** 새 행·복사 행에 프로젝트 키를 채운다 (원본 NewRowCreate / CopyRowEvent). */
function fillProject(row: ProjMngRow) {
  row.prj_rid = projectCode.value;
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect
        v-model="projectCode"
        code-id="projlist"
        @change="search"
      />
      <CodeSelect v-model="completeState" code-id="compstat" show-all />
      <template #actions>
        <Button v-perm:search size="small" @click="search">조회</Button>
        <Button v-perm:update size="small" type="primary" @click="save()">
          저장
        </Button>
      </template>
    </SearchBar>

    <DynamicGrid
      :result="result"
      :loading="loading"
      dropdown-cols="schedule_type|schedule_type"
      hidden-cols="prj_rid,proc_lvl,build_user,build_status,build_chk_dt,qc_chk_dt,cre_user,cre_dt,mod_user,mod_dt"
      export-name="WBS"
      @save="save()"
      @delete="remove"
      @add="fillProject"
      @copy="fillProject"
    />
  </Page>
</template>
