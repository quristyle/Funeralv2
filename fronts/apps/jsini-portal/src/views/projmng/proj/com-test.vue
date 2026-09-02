<script setup lang="ts">
/**
 * [그리드 부품 테스트]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjComTest.razor` (`/proj-com-test`).
 * 프로시저: `sp_dev_proj_exec`(조회), `sp_projdbsave`(저장)
 *
 * 메타 구동 그리드가 제대로 도는지 확인하는 개발용 화면이다.
 * 조회와 저장이 서로 다른 프로시저인 것도 원본 그대로다.
 *
 * 조회 프로시저만 바꿨다. 원본은 `sp_projlist` 를 불렀는데 DB 에 그 프로시저가
 * 없어(`sp_projdblist` 만 있다) 이식 전에도 이 화면은 열리지 않았다.
 * 화면의 목적이 "그리드가 도는지 보는 것"이라 같은 성격의 프로젝트 목록
 * 프로시저로 바꿨다. 되돌리려면 아래 한 줄을 `sp_projlist` 로 두면 된다.
 */
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { dbCont, dbSave } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { DynamicGrid, SearchBar } from '../shared';

const result = ref<any>(null);
const loading = ref(false);

async function search() {
  loading.value = true;
  try {
    result.value = await dbCont('sp_dev_proj_exec', {});
  } finally {
    loading.value = false;
  }
}

function save() {
  return dbSave('sp_projdbsave', {}, result.value?.data ?? []);
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
      </template>
    </SearchBar>

    <DynamicGrid
      :result="result"
      :loading="loading"
      export-name="부품테스트"
      @save="save"
    />
  </Page>
</template>
