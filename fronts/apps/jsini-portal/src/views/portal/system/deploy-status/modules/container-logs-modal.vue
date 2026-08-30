<script lang="ts" setup>
import { nextTick, ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { Button, Select, Spin } from 'ant-design-vue';

import { getContainerLogs } from '#/api/portal/system/deploy-status';

/**
 * [컨테이너 로그 팝업]
 *
 * 운영서버 컨테이너에 들어가지 않고 로그를 본다. 팝업 데이터로
 * { service } 를 받아 열릴 때 최근 로그를 당겨 오고, 줄 수를 바꾸거나
 * 새로고침할 수 있다. 관리자 계열만 서버가 허용한다.
 */

interface LogsData {
  service: string;
}

const service = ref('');
const logText = ref('');
const tail = ref(200);
const loading = ref(false);
const errorMsg = ref('');
const logBox = ref<HTMLElement | null>(null);

const tailOptions = [
  { label: '200줄', value: 200 },
  { label: '500줄', value: 500 },
  { label: '1000줄', value: 1000 },
  { label: '2000줄', value: 2000 },
];

const [Modal, modalApi] = useVbenModal<LogsData>({
  destroyOnClose: true,
  onOpenChange(isOpen) {
    if (!isOpen) return;
    service.value = modalApi.getData()?.service ?? '';
    logText.value = '';
    errorMsg.value = '';
    fetchLogs();
  },
});

async function fetchLogs() {
  if (!service.value) return;
  loading.value = true;
  try {
    const r = await getContainerLogs(service.value, tail.value);
    logText.value = r.log || '(로그가 비어 있다)';
    errorMsg.value = '';
    // 최신 로그가 아래에 있으므로 바닥으로 내려 준다
    await nextTick();
    logBox.value?.scrollTo({ top: logBox.value.scrollHeight });
  } catch (error: any) {
    errorMsg.value = error?.message ?? '로그를 불러오지 못했습니다.';
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <Modal :footer="false" :title="`${service} 컨테이너 로그`" class="w-[900px]">
    <div class="flex flex-col gap-2 px-2 pb-2">
      <div class="flex items-center justify-between">
        <span v-if="errorMsg" class="text-destructive text-xs">{{ errorMsg }}</span>
        <span v-else class="text-muted-foreground text-xs">
          최근 {{ tail }}줄 · 시간은 UTC 타임스탬프다
        </span>
        <div class="flex items-center gap-2">
          <Select
            v-model:value="tail"
            :options="tailOptions"
            size="small"
            style="width: 100px"
            @change="fetchLogs"
          />
          <Button :loading="loading" size="small" @click="fetchLogs">
            새로고침
          </Button>
        </div>
      </div>
      <Spin :spinning="loading">
        <div
          ref="logBox"
          class="bg-accent overflow-auto rounded-md p-3 font-mono text-xs leading-5"
          style="height: 480px; white-space: pre-wrap; word-break: break-all"
        >{{ logText }}</div>
      </Spin>
    </div>
  </Modal>
</template>
