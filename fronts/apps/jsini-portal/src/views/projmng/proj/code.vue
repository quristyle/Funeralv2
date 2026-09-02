<script setup lang="ts">
/**
 * [프로젝트 코드 정보]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjCodeMng.razor` (`/proj-code-mng`).
 * 호출: 개발도구 `code_master` / `code_detail` (선택한 프로젝트 DB 로 직접 붙는다)
 *
 * 관리 대상 프로젝트의 코드 마스터를 그 프로젝트 DB 에서 직접 읽는다.
 * 마스터가 `Dynamic` 이면 등록된 쿼리를 그대로 실행해 상세를 만든다.
 *
 * 편집기에서 Ctrl+Enter 를 누르면 그 쿼리를 직접 실행한다(원본과 같다).
 */
import type { CommonCodeItem, ProjMngRow } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Input } from 'ant-design-vue';

import { jsProcDb, rawSql } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { CodeSelect, DynamicGrid, SearchBar, SplitPane } from '../shared';
import { CodeEditor } from '#/components/code-editor';

const projectCode = ref('');
const dbCode = ref('');
const dbItem = ref<null | CommonCodeItem>(null);
const keyword = ref('');
const query = ref('');

const masterResult = ref<any>(null);
const detailResult = ref<any>(null);
const masterLoading = ref(false);
const detailLoading = ref(false);

/**
 * 쿼리에 남아 있는 바인드 변수를 빈 문자열로 바꾼다.
 * `:name` 은 치환하고 `::type` (형변환) 은 건드리지 않는다 — 원본과 같은 정규식이다.
 */
function stripBinds(sql: string) {
  return sql.replace(/(?<!:):\w+/g, "''");
}

async function search() {
  detailResult.value = null;
  query.value = '';
  masterLoading.value = true;
  try {
    masterResult.value = await jsProcDb('code_master', dbItem.value, {
      parameter1: keyword.value,
    });
  } finally {
    masterLoading.value = false;
  }
}

async function onMasterSelect(row: null | ProjMngRow) {
  detailResult.value = null;
  query.value = '';
  if (!row) return;

  detailLoading.value = true;
  try {
    if (String(row.ctype ?? '') === 'Dynamic') {
      const sql = String(row.query_text ?? '');
      query.value = sql;
      detailResult.value = await rawSql(
        dbItem.value?.others?.db_nick ?? '',
        stripBinds(sql),
        true,
      );
    } else {
      detailResult.value = await jsProcDb('code_detail', dbItem.value, {
        parameter1: String(row.MASTER_CD ?? row.master_cd ?? ''),
      });
    }
  } finally {
    detailLoading.value = false;
  }
}

/** 편집기의 쿼리를 직접 실행한다. */
async function runQuery() {
  detailLoading.value = true;
  try {
    detailResult.value = await rawSql(
      dbItem.value?.others?.db_nick ?? '',
      stripBinds(query.value),
      true,
    );
  } finally {
    detailLoading.value = false;
  }
}

function onEditorKeydown(event: KeyboardEvent) {
  if (event.ctrlKey && event.key === 'Enter') {
    event.preventDefault();
    runQuery();
  }
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" />
      <CodeSelect
        v-model="dbCode"
        code-id="projdb"
        :code-key="projectCode"
        etc-fix
        @change="(item) => (dbItem = item)"
      />
      <Input
        v-model:value="keyword"
        allow-clear
        placeholder="코드 검색"
        size="small"
        style="width: 200px"
        @press-enter="search"
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

    <SplitPane :size="40">
      <template #first>
        <DynamicGrid
          :result="masterResult"
          :loading="masterLoading"
          hidden-cols="query_text,ctype"
          export-name="코드마스터"
          @row-click="onMasterSelect"
        />
      </template>

      <template #second>
        <SplitPane :size="70" direction="vertical">
          <template #first>
            <DynamicGrid
              :result="detailResult"
              :loading="detailLoading"
              export-name="코드상세"
            />
          </template>
          <template #second>
            <div class="h-full" @keydown="onEditorKeydown">
              <CodeEditor v-model="query" language="pgsql" height="100%" />
            </div>
          </template>
        </SplitPane>
      </template>
    </SplitPane>
  </Page>
</template>
