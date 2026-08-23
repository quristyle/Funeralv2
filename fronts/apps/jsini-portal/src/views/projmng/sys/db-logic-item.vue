<script setup lang="ts">
/**
 * [DB 로직 항목]
 *
 * 원본: ProjMngWasm `Pages/Sys/DbLogicItem.razor` (`/db-logic-item`).
 * 프로시저: `sp_devsqlresp_base_exec`
 *
 * [DB 로직] 화면이 쓰는 항목의 뼈대를 관리한다 — 어떤 이름의 시스템 쿼리가
 * 있는지, 그 기본값은 무엇인지. 여기 항목이 있어야 DB 종류별 쿼리를 등록할 수 있다.
 */
import { onMounted } from 'vue';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import { DynamicGrid, SearchBar, useProcGrid } from '../shared';

const { result, loading, load, save, remove } = useProcGrid(
  'sp_devsqlresp_base_exec',
);

function search() {
  return load({});
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
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
      export-name="DB로직항목"
      @save="save()"
      @delete="remove"
    />
  </Page>
</template>
