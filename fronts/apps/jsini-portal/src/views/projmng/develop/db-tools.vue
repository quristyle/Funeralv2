<script setup lang="ts">
/**
 * [DB 도구]
 *
 * 원본: ProjMngWasm `Pages/Develop/DBTools.razor` (`/dbtools`).
 * 호출: 개발도구 `proclist` / `tablelist`, `/Dev/sql`(미리보기),
 *       프로시저 `sp_dev_db_prop_exec` (`db_pkey='sp_fmt'` — 프로시저 생성 템플릿)
 *
 * 고른 프로젝트 DB 의 속을 들여다보는 화면이다. 원본과 같은 네 개 탭이다.
 *   · 프로시저 — 목록과 본문
 *   · 함수     — 목록과 본문 (`proclist` 결과에서 종류가 sql 인 것)
 *   · 테이블   — 목록
 *   · 미리보기 — 고른 테이블의 앞 10건
 *
 * 프로시저 생성 템플릿은 DB 속성에 등록된 것을 쓰고, 없으면 DB 종류별
 * 기본 템플릿을 쓴다(원본과 같은 판단).
 */
import type { CommonCodeItem, ProjMngRow } from '#/api/projmng';

import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Input, message, TabPane, Tabs } from 'ant-design-vue';

import { dbCont, jsCont, projDbParams, rawSql } from '#/api/projmng';

import { CodeSelect, DynamicGrid, SearchBar, SplitPane } from '../shared';
import { CodeEditor } from '#/components/code-editor';

const MSSQL_TEMPLATE = [
    'CREATE OR ALTER PROCEDURE {schema}.{name}',
    '  @p_param1 nvarchar(100) = NULL',
    'AS',
    'BEGIN',
    '  SET NOCOUNT ON;',
    '  -- TODO',
  'END',
].join('\n');

const POSTGRESQL_TEMPLATE = [
    'CREATE OR REPLACE PROCEDURE {schema}.{name}(',
    '  INOUT p_cursor refcursor,',
    '  p_param1 text DEFAULT NULL',
    ')',
    'LANGUAGE plpgsql AS $$',
    'BEGIN',
    '  OPEN p_cursor FOR SELECT 1;',
    'END;',
  '$$;',
].join('\n');

/**
 * DB 종류별 기본 프로시저 템플릿. 원본 `WasmUtil` 의 상수를 옮긴 것이다.
 * EDB 는 PostgreSQL 호환이라 같은 템플릿을 쓴다(원본과 같은 분기).
 */
const DEFAULT_TEMPLATE: Record<string, string> = {
  MSSQL: MSSQL_TEMPLATE,
  POSTGRESQL: POSTGRESQL_TEMPLATE,
  EDB: POSTGRESQL_TEMPLATE,
};

const projectCode = ref('');
const dbCode = ref('');
const dbItem = ref<null | CommonCodeItem>(null);
const keyword = ref('');
const tab = ref('proc');

const loading = ref(false);
const procResult = ref<any>(null);
const funcResult = ref<any>(null);
const tableResult = ref<any>(null);
const previewResult = ref<any>(null);

const procBody = ref('');
const funcBody = ref('');
const template = ref('');
const selectedTable = ref<null | ProjMngRow>(null);

const dbNick = computed(() => dbItem.value?.others?.db_nick ?? '');
const dbType = computed(() => dbItem.value?.others?.db_type ?? '');

function params() {
  return { ...projDbParams(dbItem.value), param1: keyword.value };
}

/** 프로시저 생성 템플릿을 준비한다. DB 속성에 등록된 것이 우선이다. */
async function loadTemplate() {
  const saved = await dbCont('sp_dev_db_prop_exec', {
    db_rid: dbCode.value,
    db_pkey: 'sp_fmt',
  });

  const registered = String(saved.data?.[0]?.db_pvalue ?? '');
  template.value =
    registered || DEFAULT_TEMPLATE[dbType.value] || POSTGRESQL_TEMPLATE;
}

/**
 * 프로시저·함수 목록을 한 번에 읽고 종류로 나눈다.
 * 원본도 `proclist` 한 번 호출로 둘을 갈랐다(`PgType === 'sql'` 이 함수).
 */
async function loadRoutines() {
  const result = await jsCont('proclist', params());
  const rows = result.data ?? [];

  const isFunc = (row: ProjMngRow) =>
    String(row.PgType ?? row.pgtype ?? '') === 'sql';

  procResult.value = { ...result, data: rows.filter((r) => !isFunc(r)) };
  funcResult.value = { ...result, data: rows.filter((r) => isFunc(r)) };
}

async function loadTables() {
  tableResult.value = await jsCont('tablelist', params());
}

/** 고른 테이블의 앞 10건을 읽는다. DB 종류에 따라 문법이 달라 나눠 준다. */
async function loadPreview() {
  const table = selectedTable.value;
  if (!table) {
    message.warning('테이블 탭에서 테이블을 먼저 고르세요.');
    return;
  }

  const name = String(table.TableName ?? table.tablename ?? '');
  // 원본은 오라클 문법(`rownum`)만 있었다. 실제 대상이 PostgreSQL·MSSQL 이라
  // 종류에 맞는 문법으로 나눴다.
  const sql =
    dbType.value === 'MSSQL'
      ? `select top 10 * from ${name}`
      : `select * from ${name} limit 10`;

  previewResult.value = await rawSql(dbNick.value, sql, true);
}

async function search() {
  if (!dbCode.value) {
    message.warning('DB 를 먼저 고르세요.');
    return;
  }

  loading.value = true;
  try {
    await loadTemplate();

    if (tab.value === 'proc' || tab.value === 'func') await loadRoutines();
    else if (tab.value === 'table') await loadTables();
    else if (tab.value === 'preview') await loadPreview();
  } finally {
    loading.value = false;
  }
}

function bodyOf(row: null | ProjMngRow) {
  return String(row?.Routine_Definition ?? row?.routine_definition ?? '');
}
</script>

<template>
  <Page auto-content-height>
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
        placeholder="이름 검색"
        size="small"
        style="width: 180px"
        @press-enter="search"
      />
      <template #actions>
        <Button
          v-perm:search
          size="small"
          type="primary"
          :loading="loading"
          @click="search"
        >
          조회
        </Button>
      </template>
    </SearchBar>

    <Tabs v-model:activeKey="tab" size="small" @change="search">
      <TabPane key="proc" tab="프로시저">
        <SplitPane :size="45">
          <template #first>
            <DynamicGrid
              :result="procResult"
              :loading="loading"
              hidden-cols="Routine_Definition,routine_definition"
              export-name="프로시저목록"
              @row-click="(row) => (procBody = bodyOf(row))"
            />
          </template>
          <template #second>
            <CodeEditor v-model="procBody" height="100%" language="pgsql" />
          </template>
        </SplitPane>
      </TabPane>

      <TabPane key="func" tab="함수">
        <SplitPane :size="45">
          <template #first>
            <DynamicGrid
              :result="funcResult"
              :loading="loading"
              hidden-cols="Routine_Definition,routine_definition"
              export-name="함수목록"
              @row-click="(row) => (funcBody = bodyOf(row))"
            />
          </template>
          <template #second>
            <CodeEditor v-model="funcBody" height="100%" language="pgsql" />
          </template>
        </SplitPane>
      </TabPane>

      <TabPane key="table" tab="테이블">
        <DynamicGrid
          :result="tableResult"
          :loading="loading"
          export-name="테이블목록"
          @row-click="(row) => (selectedTable = row)"
        />
      </TabPane>

      <TabPane key="preview" tab="미리보기">
        <div class="text-muted-foreground mb-1 text-xs">
          대상 테이블:
          {{ selectedTable?.TableName ?? selectedTable?.tablename ?? '(없음)' }}
        </div>
        <DynamicGrid
          :result="previewResult"
          :loading="loading"
          export-name="미리보기"
        />
      </TabPane>

      <TabPane key="template" tab="생성 템플릿">
        <CodeEditor v-model="template" height="440" language="pgsql" readonly />
      </TabPane>
    </Tabs>
  </Page>
</template>
