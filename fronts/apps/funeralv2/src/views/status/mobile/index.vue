<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Collapse, CollapsePanel, Badge, Spin, Button, message } from 'ant-design-vue';
import { getFuneralStatuses } from '#/api/status';

const list = ref<any[]>([]);
const loading = ref<boolean>(false);
const activeKey = ref<string[]>([]);

async function fetchStatuses() {
  loading.value = true;
  try {
    const data = await getFuneralStatuses();
    list.value = data || [];
    // 기본적으로 첫 번째 빈소 열어두기
    if (list.value.length > 0 && list.value[0]?.roomId) {
      activeKey.value = [list.value[0].roomId];
    }
  } catch (error) {
    message.error('모바일 현황 조회 실패');
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  fetchStatuses();
});
</script>

<template>
  <Page auto-content-height class="p-2">
    <div class="mb-4 flex items-center justify-between p-2 bg-accent rounded border">
      <span class="font-bold text-sm text-foreground">📱 모바일 실시간 빈소 현황</span>
      <Button size="small" type="primary" @click="fetchStatuses">갱신</Button>
    </div>

    <Spin :spinning="loading">
      <Collapse v-model:activeKey="activeKey" accordion class="bg-card">
        <CollapsePanel v-for="item in list" :key="item.roomId">
          <!-- 모바일 헤더 요약 렌더링 -->
          <template #header>
            <div class="flex items-center justify-between w-full pr-4 text-sm">
              <span class="font-bold">{{ item.roomName }}</span>
              <div class="flex items-center gap-2">
                <span v-if="item.status === 'USING'" class="font-semibold text-primary">
                  {{ item.deceasedName }}
                </span>
                <Badge :status="item.status === 'USING' ? 'error' : 'default'" />
              </div>
            </div>
          </template>

          <!-- 아코디언 내용 렌더링 -->
          <div v-if="item.status === 'USING'" class="text-xs space-y-2 p-2">
            <div class="flex justify-between border-b pb-1">
              <span class="font-semibold">상주 성명</span>
              <span>{{ item.chiefMourner || '-' }}</span>
            </div>
            <div class="flex justify-between border-b pb-1">
              <span class="font-semibold">고인 나이</span>
              <span>{{ item.deceasedGender === 'MALE' ? '남성' : '여성' }}, {{ item.deceasedAge }}세</span>
            </div>
            <div class="flex justify-between border-b pb-1">
              <span class="font-semibold">입관 일시</span>
              <span>{{ item.coffinTime || '-' }}</span>
            </div>
            <div class="flex justify-between border-b pb-1">
              <span class="font-semibold">발인 일시</span>
              <span>{{ item.dischargeTime || '-' }}</span>
            </div>
            <div class="flex justify-between border-b pb-1">
              <span class="font-semibold">장지</span>
              <span>{{ item.burialPlace || '-' }}</span>
            </div>
            
            <div class="pt-3 flex gap-2">
              <Button size="small" class="w-full" type="primary" ghost>관내 비상연락</Button>
              <Button size="small" class="w-full">배정 장비 관리</Button>
            </div>
          </div>
          <div v-else class="text-center py-4 text-xs text-muted-foreground">
            빈소가 깨끗하게 비어있습니다. (사용 가능)
          </div>
        </CollapsePanel>
      </Collapse>
    </Spin>
  </Page>
</template>
