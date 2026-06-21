<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Row, Col, Card, Spin, message } from 'ant-design-vue';
import { getFuneralStatuses } from '#/api/status';

const list = ref<any[]>([]);
const loading = ref<boolean>(false);

async function fetchStatuses() {
  loading.value = true;
  try {
    const data = await getFuneralStatuses();
    list.value = data || [];
  } catch (error) {
    message.error('현황 조회 실패');
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  fetchStatuses();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between border-b pb-2">
      <div class="text-lg font-bold text-foreground">빈소 이용 상태 전광판 (심플)</div>
      <div class="text-xs text-muted-foreground">실시간 자동 갱신 중</div>
    </div>

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <Col v-for="item in list" :key="item.roomId" :xs="12" :sm="8" :md="6" :lg="4">
          <Card
            :class="['text-center border rounded shadow-none transition-colors duration-200', item.status === 'USING' ? 'bg-red-50/50 border-red-200' : 'bg-green-50/50 border-green-200']"
            :body-style="{ padding: '12px' }"
          >
            <div class="text-sm font-bold truncate text-muted-foreground">{{ item.roomName }}</div>
            
            <div class="my-3">
              <span
                v-if="item.status === 'USING'"
                class="text-lg font-extrabold text-red-600"
              >
                {{ item.deceasedName }}
              </span>
              <span
                v-else
                class="text-sm font-semibold text-green-600"
              >
                공실 (대기중)
              </span>
            </div>

            <div class="text-[10px] text-muted-foreground truncate">
              {{ item.status === 'USING' ? `발인: ${item.dischargeTime?.split(' ')[1] || '미정'}` : '비어있음' }}
            </div>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
