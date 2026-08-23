<script setup lang="ts">
/**
 * [소스 추적]
 *
 * 원본: ProjMngWasm `Pages/Develop/SourceTraceMng.razor` (`/source-trace`).
 * 호출: `md_source_trace`(파일 목록), `md_source_context`(파일 내용)
 *
 * 프로젝트에 등록된 소스 경로를 서버가 훑어 파일 목록을 만들고,
 * 행을 고르면 그 파일 내용을 편집기에 띄운다.
 * 소스 경로는 [소스 정보] 화면에서 등록한다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import { mdCont } from '#/api/projmng';

import {
  CodeEditor,
  CodeSelect,
  DynamicGrid,
  SearchBar,
  SplitPane,
} from '../shared';

const projectCode = ref('');
const sourceCode = ref('');
const langCode = ref('');

const result = ref<any>(null);
const loading = ref(false);
const content = ref('');
const contentLoading = ref(false);

/** 편집기에 알려 줄 구문 종류. 원본과 같은 치환 규칙이다. */
const editorLanguage = computed(() => {
  const lang = langCode.value;
  if (lang === 'js') return 'javascript';
  if (lang === 'jsp') return 'html';
  return lang || 'plaintext';
});

async function search() {
  content.value = '';
  loading.value = true;
  try {
    result.value = await mdCont('md_source_trace', {
      prj_rid: projectCode.value,
      src_rid: sourceCode.value,
      src_lang: langCode.value,
    });
  } finally {
    loading.value = false;
  }
}

async function onSelect(row: null | ProjMngRow) {
  if (!row) return;

  contentLoading.value = true;
  try {
    const file = await mdCont('md_source_context', {
      fullpath: String(row.fullpath ?? ''),
    });
    content.value = String(file.data?.[0]?.context ?? '');
  } finally {
    contentLoading.value = false;
  }
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" />
      <CodeSelect
        v-model="sourceCode"
        code-id="srclist"
        :code-key="projectCode"
        etc-fix
      />
      <CodeSelect v-model="langCode" code-id="srclang" show-all />
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

    <SplitPane :size="40">
      <template #first>
        <DynamicGrid
          :result="result"
          :loading="loading"
          export-name="소스추적"
          @row-click="onSelect"
        />
      </template>
      <template #second>
        <CodeEditor
          v-model="content"
          height="100%"
          :language="editorLanguage"
          :placeholder="contentLoading ? '읽는 중 ...' : '왼쪽에서 파일을 고르세요.'"
          readonly
        />
      </template>
    </SplitPane>
  </Page>
</template>
