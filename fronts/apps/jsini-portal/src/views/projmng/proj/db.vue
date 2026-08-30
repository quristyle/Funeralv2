<script setup lang="ts">
/**
 * [프로젝트 DB · 속성]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjDb.razor` (`/projdb`).
 * 프로시저: `sp_projdblist`(DB 목록), `sp_projdbsave`/`sp_projdbdel`(DB 저장·삭제),
 *           `sp_dev_db_prop_exec`(DB 속성)
 *
 * 왼쪽에서 DB 를 고르면 가운데에 그 DB 의 속성이 나오고,
 * 속성을 고르면 아래 편집기에 값(주로 SQL·템플릿)이 열린다.
 * 이 속성값이 다른 화면들의 재료다 — 예: `db_pkey='erd'` 는 ERD JSON,
 * `db_pkey='sp_fmt'` 는 프로시저 생성 템플릿.
 */
import type { ProjMngRow } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import { CodeSelect, DynamicGrid, SearchBar, SplitPane, useProcGrid } from '../shared';
import { CodeEditor } from '#/components/code-editor';

const projectCode = ref('');
const selectedDb = ref<null | ProjMngRow>(null);
const selectedProp = ref<null | ProjMngRow>(null);
const propValue = ref('');

const {
  result: dbResult,
  loading: dbLoading,
  load: loadDb,
} = useProcGrid('sp_projdblist');

const {
  result: propResult,
  loading: propLoading,
  load: loadProp,
  save: saveProp,
} = useProcGrid('sp_dev_db_prop_exec');

const { save: saveDbRows } = useProcGrid('sp_projdbsave');
const { remove: removeDbRow } = useProcGrid('sp_projdbdel');

async function search() {
  selectedDb.value = null;
  selectedProp.value = null;
  propValue.value = '';
  propResult.value = null;
  await loadDb({ proj_rid: projectCode.value });
}

async function onDbSelect(row: null | ProjMngRow) {
  selectedDb.value = row;
  selectedProp.value = null;
  propValue.value = '';
  if (!row) return;
  await loadProp({ db_rid: String(row.db_rid ?? '') });
}

function onPropSelect(row: null | ProjMngRow) {
  selectedProp.value = row;
  propValue.value = String(row?.db_pvalue ?? '');
}

/** 속성 행을 새로 만들면 DB 키를 채운다 (원본 OnAddPropEvent). */
function onPropAdd(row: ProjMngRow) {
  row.db_rid = selectedDb.value?.db_rid ?? '';
}

/** 편집기 내용을 그 속성 한 건에 저장한다 (원본 OnSaveWrk). */
async function saveEditor() {
  const prop = selectedProp.value;
  if (!prop) return;

  prop.db_pvalue = propValue.value;
  await saveProp({
    db_rid: prop.db_rid,
    db_prid: prop.db_prid,
    db_pkey: prop.db_pkey,
    db_pvalue: propValue.value,
  });
}

/** DB 목록의 저장·삭제는 별도 프로시저를 쓴다 (원본과 같다). */
function saveDbList() {
  return saveDbRows({ stp: 'sp_projdbsave' });
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" @change="search" />
      <template #actions>
        <Button v-perm:search size="small" @click="search">조회</Button>
        <Button v-perm:update size="small" type="primary" @click="saveEditor">
          편집기 저장
        </Button>
      </template>
    </SearchBar>

    <SplitPane :size="35">
      <template #first>
        <DynamicGrid
          :result="dbResult"
          :loading="dbLoading"
          export-name="프로젝트DB"
          @save="saveDbList"
          @delete="removeDbRow"
          @row-click="onDbSelect"
        />
      </template>

      <template #second>
        <SplitPane :size="55" direction="vertical">
          <template #first>
            <DynamicGrid
              :result="propResult"
              :loading="propLoading"
              hidden-cols="db_pvalue"
              export-name="DB속성"
              @save="saveProp()"
              @add="onPropAdd"
              @row-click="onPropSelect"
            />
          </template>
          <template #second>
            <CodeEditor v-model="propValue" language="pgsql" height="100%" />
          </template>
        </SplitPane>
      </template>
    </SplitPane>
  </Page>
</template>
