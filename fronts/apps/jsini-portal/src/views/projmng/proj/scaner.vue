<script setup lang="ts">
/**
 * [소스 스캐너]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjScaner.razor` (`/proj-scaner`).
 * 호출: `md_blazor_scan` (서버가 프로젝트 소스 경로를 훑는다)
 *
 * 소스에서 화면 목록을 뽑아 보여 준다. 메뉴 관리 화면의
 * "파일에서 메뉴 읽기" 가 쓰는 것과 같은 스캔 결과다.
 *
 * 원본에도 저장 동작은 비어 있었다(등록은 메뉴 관리 화면에서 한다).
 * 그 상태를 그대로 두고, 대신 어디서 등록하는지 안내를 남겼다.
 */
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button } from 'ant-design-vue';

import { mdCont } from '#/api/projmng';

import { CodeSelect, DynamicGrid, SearchBar } from '../shared';

const projectCode = ref('');
const result = ref<any>(null);
const loading = ref(false);

async function search() {
  loading.value = true;
  try {
    result.value = await mdCont('md_blazor_scan', {
      prj_rid: projectCode.value,
    });
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" @change="search" />
      <template #actions>
        <Button v-perm:search size="small" type="primary" :loading="loading" @click="search">
          스캔
        </Button>
      </template>
    </SearchBar>

    <Alert
      class="mb-2"
      message="스캔 결과를 메뉴로 등록하는 것은 [화면 메뉴 관리] 화면에서 합니다. 트리에서 상위 메뉴를 우클릭해 '파일에서 메뉴 읽기' 를 고르세요."
      show-icon
      type="info"
    />

    <DynamicGrid :result="result" :loading="loading" export-name="소스스캔" />
  </Page>
</template>
