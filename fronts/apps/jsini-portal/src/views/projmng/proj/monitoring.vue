<script setup lang="ts">
/**
 * [프로젝트 모니터링]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjMonitoring.razor` (`/proj-monitoring`).
 * 프로시저: `sp_proj_user_map_list`(참여자), `sp_dev_srcinfo_exec`(소스),
 *           `sp_projdblist`(DB), `sp_proj_wbs_exec`(WBS), `md_blazor_scan`(소스 스캔)
 *
 * 프로젝트 하나의 상태를 한 화면에 모은다 — 참여자·소스·DB·WBS 진행.
 * 원본이 카드로 늘어놓던 것을 같은 순서의 카드로 옮겼다.
 */
import type { ProjMngRow } from '#/api/projmng';

import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, Col, Progress, Row, Spin, Statistic } from 'ant-design-vue';

import { dbCont, mdCont } from '#/api/projmng';

import GridIconButton from '#/components/GridIconButton.vue';
import { CodeSelect, DynamicGrid, SearchBar } from '../shared';

const projectCode = ref('');
const loading = ref(false);

const users = ref<ProjMngRow[]>([]);
const sources = ref<ProjMngRow[]>([]);
const databases = ref<ProjMngRow[]>([]);
const scanned = ref<ProjMngRow[]>([]);
const wbsResult = ref<any>(null);

/** WBS 진행률. 상태가 COMP 인 항목의 비율이다. */
const progress = computed(() => {
  const rows = wbsResult.value?.data ?? [];
  if (rows.length === 0) return 0;
  const done = rows.filter(
    (row: ProjMngRow) => String(row.wbs_state ?? '') === 'COMP',
  ).length;
  return Math.round((done / rows.length) * 100);
});

async function search() {
  if (!projectCode.value) return;

  loading.value = true;
  try {
    const [userData, srcData, dbData, wbsData] = await Promise.all([
      // 참여자는 `dev_user`(자체 사용자 테이블)가 아니라 참여 현황에서 센다.
      // 사람의 정본은 포털 계정이고, 프로젝트관리가 아는 것은 참여 여부뿐이다.
      dbCont('sp_proj_user_map_list', { prj_rid: projectCode.value }),
      dbCont('sp_dev_srcinfo_exec', { prj_rid: projectCode.value }),
      dbCont('sp_projdblist', { proj_rid: projectCode.value }),
      dbCont('sp_proj_wbs_exec', {
        prj_rid: projectCode.value,
        schedule_type: 'WBS',
      }),
    ]);

    users.value = userData.data ?? [];
    sources.value = srcData.data ?? [];
    databases.value = dbData.data ?? [];
    wbsResult.value = wbsData;
  } finally {
    loading.value = false;
  }
}

/** 서버가 소스 경로를 훑어 화면 목록을 센다. 요청이 길어질 수 있어 따로 눌러 실행한다. */
async function scanSource() {
  loading.value = true;
  try {
    const result = await mdCont('md_blazor_scan', {
      prj_rid: projectCode.value,
    });
    scanned.value = result.data ?? [];
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <Page auto-content-height>
    <SearchBar class="mb-2">
      <CodeSelect v-model="projectCode" code-id="projlist" @change="search" />
      <template #actions>
        <GridIconButton
          v-perm:search
          icon="vxe-icon-search"
          title="조회"
          @click="search"
        />
        <GridIconButton
          icon="vxe-icon-search-zoom-in"
          title="소스 스캔"
          @click="scanSource"
        />
      </template>
    </SearchBar>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]" class="mb-2">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="users.length" title="참여자" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="sources.length" title="소스 묶음" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="databases.length" title="연결 DB" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="scanned.length" title="스캔된 화면" />
          </Card>
        </Col>
      </Row>

      <Card class="mb-2" size="small" title="WBS 진행">
        <Progress :percent="progress" />
        <span class="text-muted-foreground text-xs">
          전체 {{ wbsResult?.data?.length ?? 0 }} 건
        </span>
      </Card>

      <Card :body-style="{ padding: 0, height: '360px' }" size="small" title="WBS 목록">
        <DynamicGrid
          :result="wbsResult"
          hidden-cols="prj_rid,proc_lvl,build_user,build_status,build_chk_dt,qc_chk_dt,cre_user,cre_dt,mod_user,mod_dt"
          export-name="프로젝트모니터링-WBS"
          height="330"
        />
      </Card>
    </Spin>
  </Page>
</template>
