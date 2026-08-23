<script setup lang="ts">
/**
 * [프로젝트 소스 정보]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjSource.razor` (`/proj-source`).
 * 프로시저: `sp_dev_srcinfo_exec`(소스 묶음), `sp_dev_srcinfo_dtl_exec`(상세)
 *
 * 프로젝트별 소스 경로·언어 정보를 등록한다. 이 정보가 있어야
 * 소스 추적·Glue 추적 화면이 서버에서 파일을 훑을 수 있다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import {
  CodeSelect,
  DynamicGrid,
  SearchBar,
  SplitPane,
  useProcGrid,
} from '../shared';

const projectCode = ref('');
const selectedSrc = ref<null | ProjMngRow>(null);

const {
  result: srcResult,
  loading: srcLoading,
  load: loadSrc,
  save: saveSrc,
  remove: removeSrc,
} = useProcGrid('sp_dev_srcinfo_exec');

const {
  result: dtlResult,
  loading: dtlLoading,
  load: loadDtl,
  save: saveDtl,
  remove: removeDtl,
} = useProcGrid('sp_dev_srcinfo_dtl_exec');

async function search() {
  selectedSrc.value = null;
  dtlResult.value = null;
  await loadSrc({ prj_rid: projectCode.value });
}

async function onSrcSelect(row: null | ProjMngRow) {
  selectedSrc.value = row;
  if (!row) return;
  await loadDtl({ src_rid: String(row.src_rid ?? '') });
}

/** 상세 행을 새로 만들면 상위 소스 키를 채운다 (원본 OnAddDtlInfo). */
function onDtlAdd(row: ProjMngRow) {
  row.src_rid = selectedSrc.value?.src_rid ?? '';
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
      <template #actions>
        <Button v-perm:search size="small" type="primary" @click="search">
          조회
        </Button>
      </template>
    </SearchBar>

    <SplitPane :size="45">
      <template #first>
        <DynamicGrid
          :result="srcResult"
          :loading="srcLoading"
          export-name="소스정보"
          @save="saveSrc"
          @delete="removeSrc"
          @row-click="onSrcSelect"
        />
      </template>
      <template #second>
        <DynamicGrid
          :result="dtlResult"
          :loading="dtlLoading"
          export-name="소스정보-상세"
          @save="saveDtl"
          @delete="removeDtl"
          @add="onDtlAdd"
        />
      </template>
    </SplitPane>
  </Page>
</template>
