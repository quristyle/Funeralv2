<script setup lang="ts">
/**
 * [프로젝트 공통코드 관리]
 *
 * 원본: ProjMngWasm `Pages/Comm/CommCodeMng.razor` (`/commcode`).
 * 프로시저: `sp_devcomm_exec`
 *
 * 왼쪽에서 코드 그룹(main)을 고르면 오른쪽에 그 하위 코드가 나온다.
 * 하위에 행을 추가하면 상위 코드(`cm_pcd`)를 자동으로 채운다.
 *
 * 포털에도 공통코드 화면(`/system/common-code`)이 있지만 그건 포털 자신의
 * 코드(`scom`)다. 이 화면은 프로젝트관리(`projmng`) 코드를 다룬다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { clearCommonCache } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { DynamicGrid, SearchBar, SplitPane, useProcGrid } from '../shared';

const PROC = 'sp_devcomm_exec';

const {
  result: mainResult,
  loading: mainLoading,
  load: loadMain,
  save: saveMainRows,
} = useProcGrid(PROC);

const {
  result: detailResult,
  loading: detailLoading,
  load: loadDetail,
  save: saveDetailRows,
} = useProcGrid(PROC);

/** 선택한 상위 코드. 하위 행을 새로 만들 때 부모 키로 쓴다. */
const selectedMain = ref<null | ProjMngRow>(null);

async function search() {
  selectedMain.value = null;
  detailResult.value = null;
  await loadMain({ srch_type: 'main' });
}

async function onMainSelect(row: null | ProjMngRow) {
  selectedMain.value = row;
  if (!row) return;
  await loadDetail({ cm_pcd: String(row.cm_cd ?? '') });
}

async function saveMain() {
  await saveMainRows();
  // 코드가 바뀌면 다른 화면의 드롭다운도 새로 읽어야 한다.
  clearCommonCache();
}

async function saveDetail() {
  await saveDetailRows({ cm_pcd: String(selectedMain.value?.cm_cd ?? '') });
  clearCommonCache();
}

/** 하위 행을 새로 만들면 상위 코드를 채워 준다 (원본 `AddBtnEvent2`). */
function onDetailAdd(row: ProjMngRow) {
  row.cm_pcd = selectedMain.value?.cm_cd ?? '';
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
      </template>
    </SearchBar>

    <SplitPane :size="35">
      <template #first>
        <DynamicGrid
          :result="mainResult"
          :loading="mainLoading"
          hidden-cols="cm_rid,cm_prop,cm_pcd,cm_val,cm_val2,cm_val3,cm_srt"
          dropdown-cols="cm_type|CODE_TYPE"
          export-name="공통코드-그룹"
          @save="saveMain"
          @row-click="onMainSelect"
        />
      </template>

      <template #second>
        <DynamicGrid
          :result="detailResult"
          :loading="detailLoading"
          hidden-cols="cm_rid,cm_prop"
          export-name="공통코드-상세"
          @save="saveDetail"
          @add="onDetailAdd"
        />
      </template>
    </SplitPane>
  </Page>
</template>
