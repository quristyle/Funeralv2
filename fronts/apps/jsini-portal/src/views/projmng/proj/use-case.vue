<script setup lang="ts">
/**
 * [유즈케이스 다이어그램]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjUseCase.razor` (`/proj-usecase`).
 * 프로시저: `sp_dev_proj_prop_exec` (`prop_type='USE_CASE'`)
 *
 * 프로젝트 속성에 다이어그램을 그려 넣는다. ERD 와 달리 DB 메타를 합치지 않고
 * 사람이 직접 그린 것을 저장한다.
 *
 * 원본은 mxgraph XML 로 저장했다. 이식본은 ERD 화면과 같은 JSON 형식으로 저장한다 —
 * 저장 형식이 다르면 두 화면이 부품을 공유할 수 없고, 유즈케이스 쪽은
 * 아직 쌓인 자료가 없어 형식을 맞추는 편이 낫다고 판단했다.
 * (기존 XML 자료가 있다면 열리지 않는다. 문서에 남겨 두었다.)
 */
import type { ErdModel } from '../shared';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, message } from 'ant-design-vue';

import { dbCont, dbSave } from '#/api/projmng';

import { CodeSelect, ErdDiagram, parseErdModel, SearchBar } from '../shared';

const PROC = 'sp_dev_proj_prop_exec';
const PROP_TYPE = 'USE_CASE';

const projectCode = ref('');
const propCode = ref('');
const loading = ref(false);
const diagram = ref<any>(null);

function params() {
  return {
    prop_type: PROP_TYPE,
    prj_rid: projectCode.value,
    prop_cd: propCode.value,
  };
}

async function load() {
  if (!projectCode.value) {
    message.warning('프로젝트를 먼저 고르세요.');
    return;
  }

  loading.value = true;
  try {
    const saved = await dbCont(PROC, params());
    const raw = String(saved.data?.[0]?.prop_val ?? '');
    await diagram.value?.load(parseErdModel(raw));
  } finally {
    loading.value = false;
  }
}

async function save() {
  const model: ErdModel | undefined = diagram.value?.save();
  if (!model) return;

  const payload = { ...params(), prop_val: JSON.stringify(model) };
  await dbSave(PROC, payload, [{ ...payload, quri_ischange: true }]);
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" />
      <CodeSelect v-model="propCode" code-id="schedule_type" show-all />
      <template #actions>
        <Button size="small" @click="diagram?.fit()">맞춤</Button>
        <Button v-perm:search size="small" :loading="loading" @click="load">
          불러오기
        </Button>
        <Button v-perm:update size="small" type="primary" @click="save">
          저장
        </Button>
      </template>
    </SearchBar>

    <ErdDiagram ref="diagram" height="100%" />
  </Page>
</template>
