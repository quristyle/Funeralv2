<script setup lang="ts">
/**
 * [Glue 서비스 추적]
 *
 * 원본: ProjMngWasm `Pages/Develop/GlueTraceMng.razor` (`/glue-trace`).
 * 프로시저: `sp_dev_activityinfo_exec`(조회), `md_glue_service`(재수집)
 *
 * Glue 프레임워크의 서비스 정의(`*-service.xml`, `*.glue_sql`)를 서버가 훑어
 * DB 에 쌓아 두고, 이 화면이 그것을 읽는다.
 * 액티비티 종류가 `sql` 인 행을 고르면 그 쿼리가 편집기에 뜬다.
 *
 * "재수집" 은 서버에서 백그라운드로 돈다 — 원본 안내 문구를 그대로 옮겼다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { message } from 'ant-design-vue';

import { mdCont } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { CodeSelect, DynamicGrid, SearchBar, SplitPane, useProcGrid } from '../shared';
import { CodeEditor } from '#/components/code-editor';

const projectCode = ref('');
const sourceCode = ref('');
const content = ref('');

const { result, loading, load } = useProcGrid('sp_dev_activityinfo_exec');

function search() {
  content.value = '';
  return load({ prj_rid: projectCode.value, src_rid: sourceCode.value });
}

function onSelect(row: null | ProjMngRow) {
  // sql 액티비티만 내용이 있다. 나머지는 비워 둔다 (원본과 같다).
  content.value =
    String(row?.activity_type ?? '') === 'sql'
      ? String(row?.active_context ?? '')
      : '';
}

async function recollect() {
  await mdCont('md_glue_service', {
    prj_rid: projectCode.value,
    src_rid: sourceCode.value,
  });
  message.info(
    '서버에서 백그라운드로 수집합니다. 잠시 뒤 다시 조회하면 반영됩니다.',
  );
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" @change="search" />
      <CodeSelect
        v-model="sourceCode"
        code-id="srclist"
        :code-key="projectCode"
        etc-fix
      />
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
        <GridIconButton
          v-perm:update
          icon="vxe-icon-repeat"
          title="재수집"
          @click="recollect"
        />
      </template>
    </SearchBar>

    <SplitPane :size="55">
      <template #first>
        <DynamicGrid
          :result="result"
          :loading="loading"
          export-name="glue추적"
          @row-click="onSelect"
        />
      </template>
      <template #second>
        <CodeEditor
          v-model="content"
          height="100%"
          language="pgsql"
          placeholder="종류가 sql 인 행을 고르면 쿼리가 나옵니다."
          readonly
        />
      </template>
    </SplitPane>
  </Page>
</template>
