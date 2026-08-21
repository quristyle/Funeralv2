<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus, IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource } from '#/api/funeral/building';
import ImagePreview from '#/components/ImagePreview.vue';
import DecorationUploadModal from './modules/decoration-upload-modal.vue';

const uploadModalRef = ref<InstanceType<typeof DecorationUploadModal> | null>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'thumbnailUrl', title: '미리보기', width: 100, slots: { default: 'preview' } },
      { field: 'name', title: '장식/리본 명칭', minWidth: 180 },
      { field: 'shortName', title: '짧은 명칭', width: 120 },
      { field: 'sortOrder', title: '순서', width: 80 },
      { field: 'url', title: '이미지 URL 경로', minWidth: 280 },
      { field: 'remark', title: '설명', minWidth: 200 },
      {
        field: 'action',
        title: '관리',
        width: 120,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          // IMAGE 타입 소스 조회
          return await getMediaSources('IMAGE');
        },
      },
    },
  },
});

function openUpload() {
  if (uploadModalRef.value) {
    uploadModalRef.value.open();
  }
}

function handleEdit(row: any) {
  if (uploadModalRef.value) {
    uploadModalRef.value.open(row);
  }
}

async function handleDelete(row: any) {
  try {
    await deleteMediaSource(row.id);
    message.success('장식 이미지 리소스가 성공적으로 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="영정사진용 투명 근조리본 및 장식 이미지 리소스 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="openUpload">
          <Plus class="size-5 mr-1" />
          신규 장식 등록
        </Button>
      </template>

      <!-- 미리보기 슬롯 정의 (배경을 격자패턴 또는 반투명 어두운색으로 주어 투명 PNG가 잘 보이도록 함) -->
      <template #preview="{ row }">
        <div class="size-10 bg-[url('data:image/svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%228%22 height=%228%22 viewBox=%220 0 8 8%22><rect width=%224%22 height=%224%22 fill=%22%23ccc%22/><rect x=%224%22 y=%224%22 width=%224%22 height=%224%22 fill=%22%23ccc%22/></svg>')] bg-white rounded border overflow-hidden flex items-center justify-center">
          <ImagePreview
            :src="row.thumbnailUrl || row.url"
            :preview-src="row.url"
            :width="36"
            :height="36"
            fallback-text="🎀"
          />
        </div>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handleEdit(row)" title="장식 정보 수정">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Popconfirm title="해당 장식 리소스를 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>
              <IconifyIcon icon="lucide:trash-2" class="size-4" />
            </Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 장식 등록/업로드 모달 컴포넌트 -->
    <DecorationUploadModal ref="uploadModalRef" @saved="gridApi.query()" />
  </Page>
</template>
