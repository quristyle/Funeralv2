<script setup lang="ts">
/**
 * [ERD]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjERD.razor` (`/proj-erd`).
 * 프로시저: `sp_dev_db_prop_exec` (`db_pkey='erd'`), 개발도구 `tablelist`
 *
 * 저장된 ERD JSON 을 읽고, 대상 DB 의 실제 테이블 목록과 합쳐 그린다.
 *   · 저장본에 있는 테이블 → 배치(x/y)를 유지하고 설명만 갱신
 *   · 저장본에 없는 새 테이블 → 새로 추가
 *   · DB 에서 사라진 테이블 → 지우지 않는다 (원본 판단을 그대로 따랐다)
 *
 * 저장은 배치까지 포함한 JSON 을 같은 속성에 되돌려 쓴다.
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
/** 저장본의 행 키. 저장할 때 같은 행을 갱신하려면 필요하다. */
const propRowId = ref('');

const diagram = ref<any>(null);

async function load() {
  if (!dbCode.value) {
    message.warning('DB 를 먼저 고르세요.');
    return;
  }

  loading.value = true;
  try {
    // ① 저장본
    const saved = await dbCont(PROC, {
      db_rid: dbCode.value,
      db_pkey: PROP_KEY,
    });
    const first = saved.data?.[0];
    propRowId.value = String(first?.db_prid ?? '');
    const model = parseErdModel(String(first?.db_pvalue ?? ''));

    // ② 대상 DB 의 실제 테이블
    const tables = await jsCont('tablelist', projDbParams(dbItem.value));

    // ③ 합친다. 배치는 저장본 값을 그대로 둔다.
    const known = new Map(model.entities.map((e) => [e.id, e]));
    (tables.data ?? []).forEach((row) => {
      const name = String(row.TableName ?? row.tablename ?? '');
      if (!name) return;
      const desc = String(row.Description ?? row.description ?? '');
      const exist = known.get(name);
      if (exist) {
        exist.desc = desc;
      } else {
        model.entities.push({ id: name, name, desc });
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

  await dbSave(
    PROC,
    {
      db_rid: dbCode.value,
      db_pkey: PROP_KEY,
      db_prid: propRowId.value,
      db_pvalue: JSON.stringify(model, null, 2),
    },
    [
      {
        db_rid: dbCode.value,
        db_pkey: PROP_KEY,
        db_prid: propRowId.value,
        db_pvalue: JSON.stringify(model, null, 2),
        quri_ischange: true,
      },
    ],
  );
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
          icon="vxe-icon-zoom-out"
          title="축소"
          @click="diagram?.zoomOut()"
        />
        <GridIconButton
          icon="vxe-icon-zoom-in"
          title="확대"
          @click="diagram?.zoomIn()"
        />
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
