<script setup lang="ts">
/**
 * [프로젝트 목록]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjMng.razor` (`/projmng`).
 * 프로시저: `sp_dev_proj_exec`
 *
 * 관리 대상 프로젝트를 등록·수정한다. 여기 등록된 프로젝트가
 * 나머지 화면의 프로젝트 드롭다운(`projlist` 공통코드)에 나온다.
 */
import { onMounted } from 'vue';

import { Page } from '@vben/common-ui';

import { Button } from 'ant-design-vue';

import { DynamicGrid, SearchBar, useProcGrid } from '../shared';

const { result, loading, load, save } = useProcGrid('sp_dev_proj_exec');

function search() {
  return load({});
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <template #actions>
        <Button v-perm:search size="small" type="primary" @click="search">
          조회
        </Button>
      </template>
    </SearchBar>

    <DynamicGrid
      :result="result"
      :loading="loading"
      export-name="프로젝트목록"
      @save="save"
    />
  </Page>
</template>
