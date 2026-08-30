<script setup lang="ts">
/**
 * [DB 쿼리 테스터]
 *
 * 원본: ProjMngWasm `Pages/Develop/ProjDbTester.razor` (`/projdb-tester`).
 * 호출: `/Dev/sql` — 고른 프로젝트 DB 에 쿼리를 직접 실행한다
 *
 * 단축키도 원본과 같다.
 *   Ctrl+Enter → 커서가 놓인 문장만 실행
 *   F5         → 편집기 전체 실행
 *
 * 이 경로는 임의 SQL 실행이라 서버가 역할을 한 번 더 확인한다
 * (`DevTools:RawSqlRoles`, 기본값은 관리자 역할). 권한이 없으면 403 이 온다.
 */
import type { CommonCodeItem } from '#/api/projmng';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button } from 'ant-design-vue';

import { rawSql } from '#/api/projmng';

import { CodeSelect, DynamicGrid, SearchBar, SplitPane } from '../shared';
import { CodeEditor } from '#/components/code-editor';

const projectCode = ref('');
const dbCode = ref('');
const dbItem = ref<null | CommonCodeItem>(null);

const query = ref('');
const result = ref<any>(null);
const loading = ref(false);
const footer = ref('Ready ...');

/**
 * 커서가 놓인 문장만 잘라 낸다.
 * 앞뒤로 가장 가까운 빈 줄까지를 한 문장으로 본다 — 원본 편집기의
 * `GetCursorPosValue` 와 같은 어림이다.
 */
function statementAtCursor(text: string, caret: number) {
  const before = text.lastIndexOf('\n\n', Math.max(caret - 1, 0));
  const after = text.indexOf('\n\n', caret);
  return text
    .slice(before < 0 ? 0 : before, after < 0 ? text.length : after)
    .trim();
}

async function run(sql: string) {
  if (!sql.trim()) return;

  loading.value = true;
  footer.value = 'Loading ...';
  try {
    const ri = await rawSql(dbItem.value?.others?.db_nick ?? '', sql, true);
    result.value = ri;
    footer.value = ` time : ${ri.res?.dtgap ?? '-'} sec`;
  } finally {
    loading.value = false;
  }
}

function runAll() {
  return run(query.value);
}

function onKeydown(event: KeyboardEvent) {
  if (event.ctrlKey && event.key === 'Enter') {
    event.preventDefault();
    const target = event.target as HTMLTextAreaElement;
    run(statementAtCursor(query.value, target?.selectionStart ?? 0));
    return;
  }
  if (event.key === 'F5') {
    event.preventDefault();
    runAll();
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
      <template #actions>
        <Button
          v-perm:search
          size="small"
          type="primary"
          :loading="loading"
          @click="runAll"
        >
          실행 (F5)
        </Button>
      </template>
    </SearchBar>

    <Alert
      class="mb-2"
      message="직접 쿼리 실행은 관리자 역할에만 열려 있습니다. Ctrl+Enter 는 커서가 놓인 문장만, F5 는 전체를 실행합니다."
      show-icon
      type="warning"
    />

    <SplitPane :size="55" direction="vertical">
      <template #first>
        <div class="h-full" @keydown="onKeydown">
          <CodeEditor v-model="query" height="100%" language="pgsql" />
        </div>
      </template>
      <template #second>
        <DynamicGrid
          :result="result"
          :loading="loading"
          :footer-loading="loading"
          :footer-message="footer"
          export-name="쿼리결과"
        />
      </template>
    </SplitPane>
  </Page>
</template>
