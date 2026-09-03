<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, deleteMediaSource } from '#/api/funeral/building';
import ImagePreview from '#/components/ImagePreview.vue';
import DecorationUploadDrawer from './modules/decoration-upload-drawer.vue';

const uploadDrawerRef = ref<InstanceType<typeof DecorationUploadDrawer> | null>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'thumbnailUrl',
        title: '미리보기',
        width: 110,
        // 이미지를 칸에 빈틈없이 채운다 (`styles/index.css` 의 `.jsini-fillcell`).
        // 크기·모양은 배경이미지 관리 화면과 같게 맞췄다.
        className: 'jsini-fillcell',
        slots: { default: 'preview' },
      },
      { field: 'name', title: '장식/리본 명칭', minWidth: 180 },
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
    height: 'auto',
    /**
     * 미리보기를 칸에 꽉 채우므로 줄 높이가 곧 이미지 높이다.
     *
     * 전역이 `showOverflow: true` 라 vxe 가 줄 높이를 고정한다 — 여기서 정하지
     * 않으면 기본 높이(작은 크기)에 맞춰 이미지가 납작해진다.
     * 배경이미지 관리 화면과 같은 값으로 맞췄다.
     */
    rowConfig: { height: 64 },
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
        <GridIconButton
          icon="vxe-icon-add"
          title="신규 장식 등록"
          @click="openUpload"
        />
      </template>

      <!--
        바탕의 격자무늬는 투명 PNG 의 투명한 부분을 알아보게 하려는 것이다.
        `fit="cover"` — 칸을 꽉 채운다. 비율은 그대로고 넘치는 가장자리만 잘린다.
      -->
      <template #preview="{ row }">
        <div class="size-full bg-[url('data:image/svg+xml;utf8,<svg xmlns=%22http://www.w3.org/2000/svg%22 width=%228%22 height=%228%22 viewBox=%220 0 8 8%22><rect width=%224%22 height=%224%22 fill=%22%23ccc%22/><rect x=%224%22 y=%224%22 width=%224%22 height=%224%22 fill=%22%23ccc%22/></svg>')] bg-white overflow-hidden">
          <ImagePreview
            :src="row.thumbnailUrl || row.url"
            :preview-src="row.url"
            width="100%"
            height="100%"
            fit="cover"
            frameless
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
    <DecorationUploadDrawer ref="uploadDrawerRef" @saved="gridApi.query()" />
  </Page>
</template>
