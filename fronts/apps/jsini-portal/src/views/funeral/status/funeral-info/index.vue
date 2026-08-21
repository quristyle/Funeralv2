<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, Badge, Spin, message, Button } from 'ant-design-vue';
import { getFuneralStatuses } from '#/api/funeral/status';

const loading = ref<boolean>(false);
const list = ref<any[]>([]);

async function fetchStatuses() {
  loading.value = true;
  try {
    const data = await getFuneralStatuses();
    list.value = data || [];
  } catch (error) {
    message.error('빈소 현황 데이터를 조회할 수 없습니다.');
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
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-lg font-bold">빈소별 상세 정보 현황</h2>
      <Button type="primary" @click="fetchStatuses">실시간 새로고침</Button>
    </div>

    <Spin :spinning="loading">
      <Row :gutter="[16, 16]">
        <Col v-for="item in list" :key="item.roomId" :xs="24" :sm="12" :md="8" :lg="6">
          <Card
            :class="['h-full border transition-all hover:shadow-md', item.status === 'USING' ? 'border-primary/50 bg-primary/5' : 'border-muted']"
            :body-style="{ padding: '16px' }"
          >
            <template #title>
              <div class="flex items-center justify-between">
                <span class="font-bold text-lg">{{ item.roomName }}</span>
                <Badge
                  :status="item.status === 'USING' ? 'error' : 'default'"
                  :text="item.status === 'USING' ? '사용중' : '비어있음'"
                />
              </div>
            </template>

            <!-- 사용 중인 경우 고인 및 장례 상세 정보 노출 -->
            <div v-if="item.status === 'USING'" class="flex flex-col gap-2 text-sm">
              <div class="flex items-center justify-between border-b pb-2">
                <span class="font-semibold text-base text-foreground">
                  고인: {{ item.deceasedName }}
                  <span class="text-xs text-muted-foreground ml-1">
                    ({{ item.deceasedGender === 'MALE' ? '남' : '여' }}, {{ item.deceasedAge }}세)
                  </span>
                </span>
              </div>
              <div class="grid grid-cols-3 gap-1 text-xs text-muted-foreground">
                <div class="font-semibold text-foreground">상주</div>
                <div class="col-span-2 truncate text-foreground font-medium">{{ item.chiefMourner || '-' }}</div>

                <div class="font-semibold">입관</div>
                <div class="col-span-2">{{ item.coffinTime || '-' }}</div>

                <div class="font-semibold">발인</div>
                <div class="col-span-2">{{ item.dischargeTime || '-' }}</div>

                <div class="font-semibold">장지</div>
                <div class="col-span-2 truncate">{{ item.burialPlace || '-' }}</div>
              </div>
            </div>

            <!-- 비어 있는 경우 -->
            <div v-else class="flex h-32 items-center justify-center text-muted-foreground text-xs">
              현재 이용 가능한 빈소 상태입니다.
            </div>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
