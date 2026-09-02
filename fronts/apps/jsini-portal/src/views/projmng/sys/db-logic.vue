<script setup lang="ts">
/**
 * [DB 로직]
 *
 * 원본: ProjMngWasm `Pages/Sys/DbLogic.razor` (`/db-logic`).
 * 프로시저: `sp_projdbrspolist`(조회·저장), `/Sys` 의 `refresh_appdata`(캐시 갱신)
 *
 * 개발 도구가 DB 종류마다 쓰는 시스템 쿼리를 등록한다 —
 * `tablelist`, `proclist`, `columnsOftable` 같은 것들의 실제 SQL 이 여기 있다.
 * 서버가 이 값을 캐시하므로 저장 후 캐시 갱신까지 함께 부른다(원본과 같다).
 */
import type { ProjMngRow } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { dbSave, sysClearCache } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { CodeSelect, DynamicGrid, SearchBar, SplitPane, useProcGrid } from '../shared';
import { CodeEditor } from '#/components/code-editor';

const PROC = 'sp_projdbrspolist';

const dbTypeCode = ref('');
const selected = ref<null | ProjMngRow>(null);
const query = ref('');

const { result, loading, load, reload } = useProcGrid(PROC);

function search() {
  selected.value = null;
  query.value = '';
  return load({ dsl_type: dbTypeCode.value });
}

function onSelect(row: null | ProjMngRow) {
  selected.value = row;
  query.value = String(row?.dsl_query ?? '');
}

/** 편집기 내용을 그 행에 저장하고 서버 캐시를 비운다. */
async function save() {
  const row = selected.value;
  if (!row) return;

  row.dsl_query = query.value;

  const saved = await dbSave(
    PROC,
    { ...row, dsl_type: dbTypeCode.value },
    [{ ...row, quri_ischange: true }],
  );
  if (saved.code < 0) return;

  await sysClearCache('refresh_appdata', { dsl_type: dbTypeCode.value });
  await reload();
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="dbTypeCode" code-id="db" @change="search" />
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
        <GridIconButton
          v-perm:update
          icon="vxe-icon-save"
          title="저장"
          @click="save"
        />
      </template>
    </SearchBar>

    <SplitPane :size="35">
      <template #first>
        <DynamicGrid
          :result="result"
          :loading="loading"
          hidden-cols="dsl_id,dsl_type,dsl_query"
          export-name="DB로직"
          @row-click="onSelect"
        />
      </template>
      <template #second>
        <CodeEditor
          v-model="query"
          height="100%"
          language="pgsql"
          placeholder="왼쪽에서 항목을 고르세요."
        />
      </template>
    </SplitPane>
  </Page>
</template>
