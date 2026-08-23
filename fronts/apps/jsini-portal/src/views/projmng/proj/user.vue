<script setup lang="ts">
/**
 * [프로젝트 사용자]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjUser.razor` (`/proj-user`).
 * 프로시저: `sp_dev_user_exec`(사용자), `sp_dev_proj_user_map_exec`(사용자-프로젝트)
 *
 * 왼쪽에서 사용자를 고르면 오른쪽에 그 사용자가 참여한 프로젝트가 나온다.
 *
 * **사용자 목록은 읽기 전용이다**(결정 Q4). 사용자를 만들고 고치는 일은
 * JSini 관리 포털의 계정 관리가 단독으로 맡는다. 이 화면에 남은 편집 기능은
 * '누가 어느 프로젝트에 참여하는가'(오른쪽) 뿐이다 — 그건 계정이 아니라 업무 데이터다.
 *
 * 왼쪽 목록은 프로젝트관리 자체 사용자 테이블(`projmng.dev_user`)을 그대로 보여 준다.
 * 이 사람들은 포털 계정(`pm_*`)으로도 옮겨져 있다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Button } from 'ant-design-vue';

import {
  CodeSelect,
  DynamicGrid,
  SearchBar,
  SplitPane,
  useProcGrid,
} from '../shared';

const projectCode = ref('');
const selectedUser = ref<null | ProjMngRow>(null);

// 조회만 쓴다. save/remove 는 일부러 꺼내지 않는다 — 계정 편집은 포털 소관이다.
const {
  result: userResult,
  loading: userLoading,
  load: loadUsers,
} = useProcGrid('sp_dev_user_exec');

const {
  result: mapResult,
  loading: mapLoading,
  load: loadMap,
  save: saveMap,
} = useProcGrid('sp_dev_proj_user_map_exec');

async function search() {
  selectedUser.value = null;
  mapResult.value = null;
  await loadUsers({ user_name: '', prj_rid: projectCode.value });
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
      message="사용자 목록은 읽기 전용입니다. 계정 등록·수정은 시스템 관리 › 계정 관리에서 합니다."
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
        <Button v-perm:search size="small" type="primary" @click="search">
          조회
        </Button>
      </template>
    </SearchBar>

    <SplitPane :size="40">
      <template #first>
        <!-- @save·@delete 를 붙이지 않으면 DynamicGrid 가 편집·삭제·행추가 버튼을 아예 그리지 않는다 -->
        <DynamicGrid
          :result="userResult"
          :loading="userLoading"
          export-name="프로젝트사용자"
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
