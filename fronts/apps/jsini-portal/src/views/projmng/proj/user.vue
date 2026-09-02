<script setup lang="ts">
/**
 * [프로젝트 참여자]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjUser.razor` (`/proj-user`).
 * 프로시저: `sp_proj_user_map_list`(참여 현황), `sp_dev_proj_user_map_exec`(참여 편집)
 *
 * 왼쪽에서 사람을 고르면 오른쪽에 그 사람이 참여한 프로젝트가 나온다.
 *
 * **사람 목록은 포털 계정이다.** 이식 전에는 프로젝트관리가 자기 사용자 테이블
 * (`projmng.dev_user`)을 들고 있었지만, 사용자의 정본은 포털 한 곳이라 그쪽에서 읽는다.
 * 계정을 만들고 고치는 일도 포털이 단독으로 맡는다 — 이 화면에 남은 편집 기능은
 * '누가 어느 프로젝트에 참여하는가'(오른쪽) 뿐이고, 그건 계정이 아니라 업무 자료다.
 *
 * 왼쪽에 붙는 `inv_cnt`(참여 프로젝트 수)는 이식 전 `sp_dev_user_exec` 가 주던 값과
 * 같은 뜻이다. 프로젝트로 걸러도 전체 기준으로 센다.
 */
import type { ProjMngResult, ProjMngRow } from '#/api/projmng';

import { onMounted, ref, shallowRef } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert } from 'ant-design-vue';

import { fetchBizOptions } from '#/api/biz-select';
import { dbCont } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import {
  CodeSelect,
  DynamicGrid,
  SearchBar,
  SplitPane,
  useProcGrid,
} from '../shared';

/** 왼쪽 그리드의 컬럼 메타. 서버가 주는 것이 아니라 여기서 만든다 —
 *  포털 계정과 프로젝트관리 참여 현황을 합쳐 그리기 때문이다. */
const USER_COLS = {
  user_id: 'System.String',
  user_name: 'System.String',
  inv_cnt: 'System.Int32',
};

const projectCode = ref('');
const selectedUser = ref<null | ProjMngRow>(null);

const userResult = shallowRef<null | ProjMngResult>(null);
const userLoading = ref(false);

const {
  result: mapResult,
  loading: mapLoading,
  load: loadMap,
  save: saveMap,
} = useProcGrid('sp_dev_proj_user_map_exec');

async function search() {
  selectedUser.value = null;
  mapResult.value = null;
  userLoading.value = true;

  try {
    const [accounts, map] = await Promise.all([
      fetchBizOptions('portal_account'),
      dbCont('sp_proj_user_map_list', { prj_rid: projectCode.value }),
    ]);

    // 아이디 → 참여 프로젝트 수. 프로젝트를 골랐다면 이 map 에 있는 사람이
    // 곧 그 프로젝트의 참여자다.
    const counts = new Map<string, number>();
    (map.data ?? []).forEach((row) => {
      counts.set(String(row.user_id ?? ''), Number(row.inv_cnt ?? 0));
    });

    const people = projectCode.value
      ? accounts.items.filter((a) => counts.has(String(a.loginId ?? '')))
      : accounts.items;

    userResult.value = {
      code: 0,
      message: '',
      cols: USER_COLS,
      data: people.map((a) => ({
        user_id: a.loginId,
        user_name: a.userName,
        inv_cnt: counts.get(String(a.loginId ?? '')) ?? 0,
      })),
    };
  } finally {
    userLoading.value = false;
  }
}

async function onUserSelect(row: null | ProjMngRow) {
  selectedUser.value = row;
  if (!row) return;
  await loadMap({ user_id: String(row.user_id ?? '') });
}

function saveMapRows() {
  return saveMap({ user_id: String(selectedUser.value?.user_id ?? '') });
}

onMounted(search);
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <Alert
      class="mb-2"
      message="왼쪽은 포털 계정 목록입니다. 계정 등록·수정은 시스템 관리 › 계정 관리에서 하고, 여기서는 프로젝트 참여만 정합니다."
      show-icon
      type="info"
    />

    <SearchBar class="mb-2">
      <CodeSelect
        v-model="projectCode"
        code-id="projlist"
        show-all
        @change="search"
      />
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
      </template>
    </SearchBar>

    <SplitPane :size="40">
      <template #first>
        <!-- @save·@delete 를 붙이지 않으면 DynamicGrid 가 편집·삭제·행추가 버튼을 아예 그리지 않는다 -->
        <DynamicGrid
          :result="userResult"
          :loading="userLoading"
          export-name="프로젝트참여자"
          @row-click="onUserSelect"
        />
      </template>
      <template #second>
        <DynamicGrid
          :result="mapResult"
          :loading="mapLoading"
          export-name="사용자-프로젝트"
          @save="saveMapRows"
        />
      </template>
    </SplitPane>
  </Page>
</template>
