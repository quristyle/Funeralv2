<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getMediaSources, createMediaSource, deleteMediaSource } from '#/api/building';

const [UploadModal, uploadModalApi] = useVbenModal({
  title: '새 미디어 리소스 등록',
  destroyOnClose: true,
});

const filterType = ref<string>('');
const formModel = ref({
  name: '',
  sourceType: 'IMAGE' as 'VIDEO' | 'AUDIO' | 'IMAGE',
  url: '',
  remark: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '소스명', minWidth: 150 },
      {
        field: 'sourceType',
        title: '구분',
        minWidth: 100,
        formatter: ({ cellValue }: { cellValue: any }) => {
          if (cellValue === 'IMAGE') return '이미지';
          if (cellValue === 'VIDEO') return '동영상';
          if (cellValue === 'AUDIO') return '오디오/음원';
          return cellValue;
        }
      },
      {
        field: 'url',
        title: '미디어 미리보기/링크',
        minWidth: 250,
        slots: { default: 'media-preview' }
      },
      { field: 'remark', title: '비고', minWidth: 200 },
      {
        field: 'action',
        title: '작업',
        width: 100,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          const typeParam = filterType.value === '' ? undefined : (filterType.value as 'VIDEO' | 'AUDIO' | 'IMAGE');
          return await getMediaSources(typeParam);
        },
      },
    },
  },
});

watch(filterType, () => {
  gridApi.query();
});

function openUpload() {
  formModel.value = {
    name: '',
    sourceType: 'IMAGE',
    url: '',
    remark: ''
  };
  uploadModalApi.open();
}

async function handleSave() {
  try {
    if (!formModel.value.name || !formModel.value.url) {
      message.warning('소스명과 미디어 URL 주소는 필수 기입 사항입니다.');
      return;
    }
    await createMediaSource(formModel.value);
    message.success('미디어 리소스가 등록되었습니다.');
    uploadModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('리소스 추가 실패');
  }
}

async function handleDelete(row: any) {
  try {
    await deleteMediaSource(row.id);
    message.success('미디어가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">구분 필터:</span>
        <Select v-model:value="filterType" style="width: 150px">
          <Select.Option value="">전체 보기</Select.Option>
          <Select.Option value="IMAGE">이미지</Select.Option>
          <Select.Option value="VIDEO">동영상</Select.Option>
          <Select.Option value="AUDIO">오디오/음원</Select.Option>
        </Select>
      </div>
      <Button type="primary" @click="openUpload">
        <Plus class="size-5 mr-1" />
        미디어 등록
      </Button>
    </div>

    <Grid table-title="미디어 소스 데이터 보관소">
      <template #media-preview="{ row }">
        <div class="flex items-center gap-2 py-1">
          <img
            v-if="row.sourceType === 'IMAGE'"
            :src="row.url"
            class="h-10 w-16 object-cover rounded border bg-muted"
            alt="미리보기"
          />
          <span class="text-xs font-mono text-primary truncate max-w-[200px]">{{ row.url }}</span>
        </div>
      </template>

      <template #action="{ row }">
        <Popconfirm title="해당 미디어 소스를 보관소에서 영구 삭제하시겠습니까?" @confirm="handleDelete(row)">
          <Button type="link" size="small" danger>삭제</Button>
        </Popconfirm>
      </template>
    </Grid>

    <UploadModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="미디어 소스 구분" required>
            <Select v-model:value="formModel.sourceType">
              <Select.Option value="IMAGE">이미지 파일 (.png, .jpg)</Select.Option>
              <Select.Option value="VIDEO">동영상 파일 (.mp4)</Select.Option>
              <Select.Option value="AUDIO">오디오/음원 파일 (.mp3, .wav)</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="소스명" required>
            <Input v-model:value="formModel.name" placeholder="예: 메인 로비 광고 이미지, 배경 음악 등" />
          </Form.Item>
          <Form.Item label="미디어 파일 URL" required>
            <Input v-model:value="formModel.url" placeholder="예: https://cdn.funeralv2.com/media/ad.png" />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="간략한 설명 및 용도 입력" />
          </Form.Item>
        </Form>
      </div>
    </UploadModal>
  </Page>
</template>
