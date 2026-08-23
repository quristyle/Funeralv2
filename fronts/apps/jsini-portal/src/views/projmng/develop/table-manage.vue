<script setup lang="ts">
/**
 * [테이블 · 컬럼 설명 관리]
 *
 * 원본: ProjMngWasm `Pages/Develop/ProjTableMng.razor` (`/proj-table-mng`).
 * 호출: 개발도구 `tablelist`, `columnsOftable`,
 *       `tableCommentUpdate`, `columnsCommentUpdate`, `columnsCommentAdd`
 *
 * 대상 DB 의 테이블·컬럼 설명(코멘트)을 읽고 고친다.
 * 왼쪽에서 테이블을 고르면 오른쪽에 컬럼이 나오고, 설명을 고쳐 저장하면
 * 그 즉시 대상 DB 의 코멘트를 갱신한다 — 우리 DB 가 아니라 **대상 DB** 를 고친다.
 */
import type { CommonCodeItem, ProjMngRow } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button, Input, message } from 'ant-design-vue';

import { isChanged, jsCont, projDbParams } from '#/api/projmng';

import {
  CodeSelect,
  DynamicGrid,
  SearchBar,
  SplitPane,
} from '../shared';

const projectCode = ref('');
const dbCode = ref('');
const dbItem = ref<null | CommonCodeItem>(null);
const keyword = ref('');

const tableResult = ref<any>(null);
const columnResult = ref<any>(null);
const loading = ref(false);
const columnLoading = ref(false);
const selectedTable = ref<null | ProjMngRow>(null);

function baseParams() {
  return projDbParams(dbItem.value);
}

async function search() {
  selectedTable.value = null;
  columnResult.value = null;
  loading.value = true;
  try {
    tableResult.value = await jsCont('tablelist', {
      ...baseParams(),
      param1: keyword.value,
    });
  } finally {
    loading.value = false;
  }
}

async function onTableSelect(row: null | ProjMngRow) {
  selectedTable.value = row;
  if (!row) return;

  columnLoading.value = true;
  try {
    columnResult.value = await jsCont('columnsOftable', {
      ...baseParams(),
      param1: String(row.TableName ?? row.tablename ?? ''),
    });
  } finally {
    columnLoading.value = false;
  }
}

function tableName(row?: null | ProjMngRow) {
  return String(row?.TableName ?? row?.tablename ?? '');
}

/** 테이블 설명을 대상 DB 에 반영한다. */
async function saveTableComment() {
  const changed = (tableResult.value?.data ?? []).filter((row: ProjMngRow) =>
    isChanged(row),
  );
  if (changed.length === 0) {
    message.warning('수정대상이 존재하지 않습니다.');
    return;
  }

  for (const row of changed) {
    // eslint-disable-next-line no-await-in-loop
    await jsCont('tableCommentUpdate', {
      ...baseParams(),
      param1: tableName(row),
      param2: String(row.Description ?? row.description ?? ''),
    });
    delete row.quri_ischange;
  }

  message.success(`${changed.length}건을 반영했습니다.`);
}

/**
 * 컬럼 설명을 대상 DB 에 반영한다.
 * 갱신이 안 되면(코멘트가 아직 없는 경우) 추가로 한 번 더 시도한다 — 원본과 같다.
 */
async function saveColumnComment() {
  const changed = (columnResult.value?.data ?? []).filter((row: ProjMngRow) =>
    isChanged(row),
  );
  if (changed.length === 0) {
    message.warning('수정대상이 존재하지 않습니다.');
    return;
  }

  for (const row of changed) {
    const payload = {
      ...baseParams(),
      param1: tableName(selectedTable.value),
      param2: String(row.ColumnName ?? row.columnname ?? ''),
      param3: String(row.Description ?? row.description ?? ''),
    };

    // eslint-disable-next-line no-await-in-loop
    const updated = await jsCont('columnsCommentUpdate', payload);
    if (updated.code < 0) {
      // eslint-disable-next-line no-await-in-loop
      await jsCont('columnsCommentAdd', payload);
    }
    delete row.quri_ischange;
  }

  message.success(`${changed.length}건을 반영했습니다.`);
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
        placeholder="테이블 검색"
        size="small"
        style="width: 200px"
        @press-enter="search"
      />
      <template #actions>
        <Button v-perm:search size="small" :loading="loading" @click="search">
          조회
        </Button>
      </template>
    </SearchBar>

    <Alert
      class="mb-2"
      message="여기서 저장하면 프로젝트관리 DB 가 아니라 대상 DB 의 코멘트가 바뀝니다."
      show-icon
      type="warning"
    />

    <SplitPane :size="45">
      <template #first>
        <div class="flex h-full flex-col">
          <div class="mb-1 flex justify-end">
            <Button v-perm:update size="small" @click="saveTableComment">
              테이블 설명 반영
            </Button>
          </div>
          <div class="min-h-0 flex-1">
            <DynamicGrid
              :result="tableResult"
              :loading="loading"
              export-name="테이블설명"
              hide-add
              @save="() => {}"
              @row-click="onTableSelect"
            />
          </div>
        </div>
      </template>

      <template #second>
        <div class="flex h-full flex-col">
          <div class="mb-1 flex justify-end">
            <Button v-perm:update size="small" @click="saveColumnComment">
              컬럼 설명 반영
            </Button>
          </div>
          <div class="min-h-0 flex-1">
            <DynamicGrid
              :result="columnResult"
              :loading="columnLoading"
              export-name="컬럼설명"
              hide-add
              @save="() => {}"
            />
          </div>
        </div>
      </template>
    </SplitPane>
  </Page>
</template>
