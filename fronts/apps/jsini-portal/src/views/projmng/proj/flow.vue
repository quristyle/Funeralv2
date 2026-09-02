<script setup lang="ts">
/**
 * [업무 플로우]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjFlow.razor` (`/proj-flow`).
 * 프로시저: `sp_dev_db_prop_exec` (`db_pkey='erd'`), 개발도구 `tablelist`
 *
 * ERD 화면과 같은 자료를 쓰지만 보는 관점이 다르다. 원본에서도 두 화면이
 * 같은 속성(`db_pkey='erd'`)을 읽었고, 다른 점은 DB 에서 사라진 테이블에
 * `(삭제됨)` 표시를 붙인다는 것뿐이다. 그 차이를 그대로 옮겼다.
 */
import GridIconButton from '#/components/GridIconButton.vue';
import type { ErdModel } from '../shared';

import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { message } from 'ant-design-vue';

import { dbCont, dbSave, jsCont, projDbParams } from '#/api/projmng';

import { CodeSelect, ErdDiagram, parseErdModel, SearchBar } from '../shared';

const PROC = 'sp_dev_db_prop_exec';
const PROP_KEY = 'erd';

const projectCode = ref('');
const dbCode = ref('');
const dbItem = ref<any>(null);
const loading = ref(false);
const propRowId = ref('');
const diagram = ref<any>(null);

async function load() {
  if (!dbCode.value) {
    message.warning('DB 를 먼저 고르세요.');
    return;
  }

  loading.value = true;
  try {
    const saved = await dbCont(PROC, {
      db_rid: dbCode.value,
      db_pkey: PROP_KEY,
    });
    const first = saved.data?.[0];
    propRowId.value = String(first?.db_prid ?? '');
    const model = parseErdModel(String(first?.db_pvalue ?? ''));

    const tables = await jsCont('tablelist', projDbParams(dbItem.value));

    const liveNames = new Set<string>();
    const known = new Map(model.entities.map((e) => [e.id, e]));

    (tables.data ?? []).forEach((row) => {
      const name = String(row.TableName ?? row.tablename ?? '');
      if (!name) return;
      liveNames.add(name);

      const desc = String(row.Description ?? row.description ?? '');
      const exist = known.get(name);
      if (exist) {
        exist.desc = desc;
      } else {
        model.entities.push({ id: name, name, desc, x: 10, y: 10 });
      }
    });

    // DB 에 없어진 것은 지우지 않고 표시만 남긴다 (원본과 같다).
    model.entities.forEach((entity) => {
      if (!liveNames.has(entity.id) && !entity.desc?.includes('(삭제됨)')) {
        entity.desc = `${entity.desc ?? ''}(삭제됨)`;
      }
    });

    await diagram.value?.load(model);
  } finally {
    loading.value = false;
  }
}

async function save() {
  const model: ErdModel | undefined = diagram.value?.save();
  if (!model) return;

  const payload = {
    db_rid: dbCode.value,
    db_pkey: PROP_KEY,
    db_prid: propRowId.value,
    db_pvalue: JSON.stringify(model, null, 2),
  };
  await dbSave(PROC, payload, [{ ...payload, quri_ischange: true }]);
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
        <GridIconButton
          icon="vxe-icon-fullscreen"
          title="맞춤"
          @click="diagram?.fit()"
        />
        <GridIconButton
          v-perm:search
          :loading="loading"
          icon="vxe-icon-download"
          title="불러오기"
          @click="load"
        />
        <GridIconButton
          v-perm:update
          icon="vxe-icon-save"
          title="저장"
          @click="save"
        />
      </template>
    </SearchBar>

    <ErdDiagram ref="diagram" height="100%" />
  </Page>
</template>
