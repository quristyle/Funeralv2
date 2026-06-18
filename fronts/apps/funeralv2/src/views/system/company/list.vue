<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions, } from '#/adapter/vxe-table';
import type { SystemCompanyApi } from '#/api/system/company';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { Plus } from '@vben/icons';

import { Button, message } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteCompany, getCompanyList, updateCompany } from '#/api/system/company';
import { $t } from '#/locales';

import { useColumns } from './data';
import Form from './modules/form.vue';

/**
 * [회사 관리 - 목록 화면]
 * VbenVxeGrid를 사용하여 페이징 및 CRUD 기능을 제공합니다.
 */

/** 등록/수정 팝업 레이어(Drawer) 설정 */
const [FormDrawer, formDrawerApi] = useVbenDrawer({ connectedComponent: Form, destroyOnClose: true, });

/**
 * 상세 수정을 위한 Drawer 열기
 * @param row 선택된 행 데이터
 */
function onEdit(row: SystemCompanyApi.SystemCompany) { 
  formDrawerApi.setData(row).open(); 
}

/**
 * 신규 등록을 위한 Drawer 열기
 */
function onCreate() { 
  formDrawerApi.setData({}).open(); 
}

/**
 * 회사 정보를 삭제합니다.
 * @param row 삭제할 행 데이터
 */
function onDelete(row: SystemCompanyApi.SystemCompany) {
  deleteCompany(row.id)
    .then(() => {
      message.success($t('ui.actionMessage.deleteSuccess', [row.name]));
      refreshGrid();
    })
    .catch((error) => {
      console.error('[Delete Error]', error);
    });
}

/**
 * 테이블 행에서 직접 데이터를 수정한 후 셀 포커스가 빠졌을 때 호출됩니다. (In-cell editing)
 * @param row 수정된 행 데이터
 */
async function onEditClosed({ row }: any) {
  const grid = gridApi.grid;
  if (!grid) return;

  // 행의 데이터 변경 여부 확인 (VXETable 내장 기능)
  if (!grid.isUpdateByRow(row)) { return; }

  const { id, name, businessNumber, representative, status, remark } = row;
  try {
    // 서버에 업데이트 요청 전송
    await updateCompany(id, { name, businessNumber, representative, status, remark, });
    message.success($t('ui.actionMessage.updateSuccess', [name]));
    // 저장 성공 시 변경 상태 마크(삼각형) 제거를 위해 행 데이터 동기화
    grid.reloadRow(row, null);
  } catch (error) {
    console.error('[Cell Edit Error]', error);
    // 에러 발생 시 변경 전의 원본 데이터로 복원
    grid.revertData(row);
  }
}

/**
 * 테이블 내 버튼(수정/삭제) 클릭 시 발생하는 이벤트를 분기 처리합니다.
 * data.ts의 columns 정의에서 호출됩니다.
 */
function onActionClick({ code, row, }: OnActionClickParams<SystemCompanyApi.SystemCompany>) {
  switch (code) {
    case 'delete': { onDelete(row); break; }
    case 'edit': { onEdit(row); break; }
  }
}

/**
 * VXETable 그리드 설정 및 API 연결
 */
const [Grid, gridApi] = useVbenVxeGrid({
  gridEvents: { 
    /** 셀 편집 종료 이벤트 핸들러 등록 */
    editClosed: onEditClosed, 
  },
  gridOptions: {
    /** 컬럼 정의 주입 */
    columns: useColumns(onActionClick),
    /** 데이터 로드 프록시 설정 */
    proxyConfig: {
      ajax: {
        query: async (_params) => {
          // 서버에서 페이징된 회사 목록을 가져옵니다.
          return await getCompanyList();
        },
      },
    }
  } as VxeTableGridOptions,
});

/**
 * 테이블 데이터를 최신 상태로 새로고침합니다.
 */
function refreshGrid() { gridApi.query(); }
</script>

<template>
  <Page auto-content-height>
    <!-- 등록/수정용 팝업 레이어 -->
    <FormDrawer @success="refreshGrid" />
    
    <!-- VXETable 그리드 본체 -->
    <Grid :table-title="$t('system.company.title')" >
      <template #toolbar-tools>
        <!-- 툴바 영역의 신규 등록 버튼 -->
        <Button type="primary" @click="onCreate">
          <Plus class="size-5" />
          {{ $t('ui.actionTitle.create', [$t('system.company.name')]) }}
        </Button>
      </template>
    </Grid>
  </Page>
</template>
