<script setup lang="ts">
/**
 * [Fast 호출 테스트]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjFastTest.razor` (`/proj-fast-test`).
 * 프로시저: `sp_proj_wbs_exec`
 *
 * 프로시저 호출 경로가 살아 있는지 확인하는 개발용 화면이다.
 * 원본은 프로젝트 키를 `7` 로 박아 두고 호출만 해 봤는데,
 * 이식본은 프로젝트를 고를 수 있게 하고 `IsFast` 를 켜서 보낸다 —
 * 화면 이름이 뜻하는 그대로 동작하도록 맞춘 것이다.
 */
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Descriptions, DescriptionsItem } from 'ant-design-vue';

import { dbCont } from '#/api/projmng';

import { CodeSelect, DynamicGrid, SearchBar } from '../shared';

const projectCode = ref('7');
const result = ref<any>(null);
const loading = ref(false);

async function search() {
  loading.value = true;
  try {
    result.value = await dbCont(
      'sp_proj_wbs_exec',
      { prj_rid: projectCode.value },
      'srch',
      { isFast: true },
    );
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
          호출
        </Button>
      </template>
    </SearchBar>

    <Descriptions v-if="result" bordered class="mb-2" size="small">
      <DescriptionsItem label="코드">{{ result.code }}</DescriptionsItem>
      <DescriptionsItem label="메시지">{{ result.message }}</DescriptionsItem>
      <DescriptionsItem label="소요(초)">
        {{ result.res?.dtgap ?? '-' }}
      </DescriptionsItem>
    </Descriptions>

    <DynamicGrid :result="result" :loading="loading" export-name="fast-test" />
  </Page>
</template>
