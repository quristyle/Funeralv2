<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue';
import { Page } from '@vben/common-ui';
import { Select, message } from 'ant-design-vue';
import { getFuneralStatuses } from '#/api/funeral/status';

const list = ref<any[]>([]);
const selectedRoomId = ref<string>('');
const currentData = ref<any>(null);

// 현황 데이터 조회
async function fetchStatuses() {
  try {
    const data = await getFuneralStatuses();
    list.value = data || [];
    if (list.value.length > 0 && list.value[0]?.roomId) {
      selectedRoomId.value = list.value[0].roomId;
    }
  } catch (error) {
    message.error('현황 데이터 연동 실패');
  }
}

watch(selectedRoomId, () => {
  currentData.value = list.value.find(item => item.roomId === selectedRoomId.value) || null;
});

onMounted(() => {
  fetchStatuses();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 flex items-center justify-between bg-card p-4 rounded border">
      <div class="flex items-center gap-2">
        <span class="font-semibold text-sm">모의 송출할 빈소 선택:</span>
        <Select v-model:value="selectedRoomId" style="width: 200px" placeholder="빈소 선택">
          <Select.Option v-for="item in list" :key="item.roomId" :value="item.roomId">
            {{ item.roomName }} ({{ item.status === 'USING' ? '사용중' : '공실' }})
          </Select.Option>
        </Select>
      </div>
      <div class="text-xs text-muted-foreground">
        🖥️ 실제 DID 기기에 표시되는 전광판 화면과 동일한 시뮬레이터입니다.
      </div>
    </div>

    <!-- 가상 DID 렌더링 영역 (실물 블랙보드 고가시성 테마 설계) -->
    <div class="flex justify-center items-center py-6 bg-accent/20 rounded border">
      <div class="w-[800px] h-[450px] bg-neutral-900 border-8 border-neutral-700 shadow-2xl rounded text-white font-sans flex flex-col justify-between p-6">
        
        <!-- DID 상단 헤더 -->
        <div class="flex justify-between items-center border-b border-neutral-700 pb-3">
          <div class="text-2xl font-extrabold tracking-widest text-yellow-400">
            {{ currentData?.roomName || '대기 화면' }}
          </div>
          <div class="text-sm font-semibold text-neutral-400">
            삼가 고인의 명복을 빕니다
          </div>
        </div>

        <!-- DID 바디 콘텐츠 -->
        <div v-if="currentData && currentData.status === 'USING'" class="flex-1 flex gap-6 py-4">
          <!-- 영정사진 플레이스홀더 -->
          <div class="w-[180px] h-[220px] bg-neutral-800 border-2 border-neutral-600 rounded flex flex-col items-center justify-center text-neutral-500 overflow-hidden">
            <span class="text-3xl">🕯️</span>
            <span class="text-[10px] mt-2">영정 사진</span>
          </div>

          <!-- 상 상세 내역 -->
          <div class="flex-1 flex flex-col justify-between text-base">
            <div>
              <span class="text-2xl font-bold text-neutral-100">
                故 {{ currentData.deceasedName }} 님
              </span>
              <span class="text-xs text-neutral-400 ml-2">
                ({{ currentData.deceasedGender === 'MALE' ? '남성' : '여성' }} / {{ currentData.deceasedAge }}세)
              </span>
            </div>

            <div class="grid grid-cols-4 gap-2 text-sm text-neutral-300 border-t border-neutral-800 pt-3">
              <div class="font-bold text-yellow-500">상주</div>
              <div class="col-span-3 font-semibold">{{ currentData.chiefMourner || '유가족 연락 대기' }}</div>

              <div class="font-bold text-yellow-500">입관 일시</div>
              <div class="col-span-3">{{ currentData.coffinTime || '미정' }}</div>

              <div class="font-bold text-yellow-500">발인 일시</div>
              <div class="col-span-3">{{ currentData.dischargeTime || '미정' }}</div>

              <div class="font-bold text-yellow-500">장지</div>
              <div class="col-span-3 text-neutral-100 font-semibold">{{ currentData.burialPlace || '미정' }}</div>
            </div>
          </div>
        </div>

        <!-- 사용 안함 / 대기 상태 일 때 -->
        <div v-else class="flex-1 flex flex-col items-center justify-center text-neutral-400 gap-2">
          <span class="text-5xl">🏢</span>
          <span class="text-lg font-bold tracking-wide mt-2">이용 가능한 빈소 상태입니다.</span>
          <span class="text-xs text-neutral-600">장례가 등록되면 고인 및 상주 정보가 표출됩니다.</span>
        </div>

        <!-- DID 하단 푸터 -->
        <div class="border-t border-neutral-700 pt-2 flex justify-between text-xs text-neutral-500 font-semibold">
          <span>장례식장 통합 시스템 v2.0</span>
          <span>실시간 모니터링 모드</span>
        </div>

      </div>
    </div>
  </Page>
</template>
