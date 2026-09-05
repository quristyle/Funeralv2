<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource } from '#/api/funeral/building';
import ImagePreview from '#/components/ImagePreview.vue';
import BackgroundUploadDrawer from './modules/background-upload-drawer.vue';

const uploadDrawerRef = ref<InstanceType<typeof BackgroundUploadDrawer> | null>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'thumbnailUrl',
        title: '미리보기',
        width: 110,
        // 이미지를 칸에 빈틈없이 채운다 (`styles/index.css` 의 `.jsini-fillcell`).
        className: 'jsini-fillcell',
        slots: { default: 'preview' },
      },
      { field: 'name', title: '배경 이미지 명칭', minWidth: 180 },
      { field: 'shortName', title: '짧은 명칭', width: 120 },
      { field: 'sortOrder', title: '순서', width: 80 },
      // 이미지 URL 경로는 목록에 두지 않는다 — 사람이 읽을 것이 아니고 폭만 먹는다.
      // 자료(`row.url`)는 그대로 있어서 미리보기 슬롯이 계속 쓴다.
      { field: 'remark', title: '설명', minWidth: 200 },
      {
        field: 'action',
        title: '관리',
        width: 120,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    gridFeatures: { onCreate: () => openUpload() },
    height: 'auto',
    /**
     * 미리보기를 80px 폭으로 키웠으므로 줄도 그만큼 높여야 한다.
     *
     * 전역이 `showOverflow: true` 라 vxe 가 줄 높이를 고정하고 넘치는 것을
     * 잘라 낸다 — 그대로 두면 이미지 아래쪽이 잘린다.
     * 16:9 이미지가 80×45 이고 위아래 여백까지 64 면 넉넉하다.
     */
    rowConfig: { height: 64 },
    proxyConfig: {
      ajax: {
        query: async () => {
          // BACKGROUND 타입 소스 조회
          return await getMediaSources('BACKGROUND');
        },
      },
    },
  },
});

function openUpload() {
  if (uploadDrawerRef.value) {
    uploadDrawerRef.value.open();
  }
}

function handleEdit(row: any) {
  if (uploadDrawerRef.value) {
    uploadDrawerRef.value.open(row);
  }
}

async function handleDelete(row: any) {
  try {
    await deleteMediaSource(row.id);
    message.success('배경 이미지 리소스가 성공적으로 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid >


      <!-- 미리보기 슬롯 정의 -->
      <template #preview="{ row }">
        <!--
          바탕의 격자무늬는 투명한 부분을 알아보게 하려는 것이다.
          `fit="cover"` — 칸을 꽉 채운다. 비율은 그대로고 넘치는 가장자리만 잘린다
          (`contain` 으로는 칸과 이미지의 비율 차이만큼 반드시 여백이 남는다).
        -->
        <div class="size-full bg-[url('data:image/svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%228%22 height=%228%22 viewBox=%220 0 8 8%22><rect width=%224%22 height=%224%22 fill=%22%23ccc%22/><rect x=%224%22 y=%224%22 width=%224%22 height=%224%22 fill=%22%23ccc%22/></svg>')] bg-white overflow-hidden">
          <ImagePreview
            :src="row.thumbnailUrl || row.url"
            :preview-src="row.url"
            width="100%"
            height="100%"
            fit="cover"
            frameless
            fallback-text="🖼️"
          />
        </div>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handleEdit(row)" title="배경 정보 수정">
            <IconifyIcon icon="lucide:edit-3" class="size-4" />
          </Button>
          <Popconfirm title="해당 배경 리소스를 삭제하시겠습니까?" @confirm="handleDelete(row)">
            <Button type="link" size="small" danger>
              <IconifyIcon icon="lucide:trash-2" class="size-4" />
            </Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 배경 등록/업로드 모달 컴포넌트 -->
    <BackgroundUploadDrawer ref="uploadDrawerRef" @saved="gridApi.query()" />
  </Page>
</template>
