<script lang="ts" setup>
import { computed, ref, onMounted } from 'vue';
import { Button, Form, message } from 'ant-design-vue';
import { Plus, Trash2 } from '@vben/icons';
import { getRooms, getBuildings } from '#/api/building';
import BizSelect from '#/components/BizSelect.vue';
import AutoDatePicker from '#/components/AutoDatePicker.vue';

const props = defineProps({
  modelValue: {
    type: Array,
    required: true
  }
});

const emit = defineEmits(['update:modelValue']);

const rooms = computed({
  get: () => props.modelValue as any[],
  set: (val) => emit('update:modelValue', val)
});

const roomList = ref<any[]>([]);
const buildingList = ref<any[]>([]);

async function fetchInitialData() {
  try {
    const [roomsRes, buildingsRes] = await Promise.all([
      getRooms({}),
      getBuildings()
    ]);
    
    const rawRooms = (roomsRes as any)?.result ?? roomsRes;
    roomList.value = Array.isArray(rawRooms) ? rawRooms : [];

    const rawBuildings = (buildingsRes as any)?.result ?? buildingsRes;
    buildingList.value = Array.isArray(rawBuildings) ? rawBuildings : [];

    initializeHistoricalSelections();
  } catch (error) {
    message.error('기초 정보 목록을 가져올 수 없습니다.');
  }
}

function initializeHistoricalSelections() {
  for (const r of rooms.value) {
    if (r.roomId && !r.floorId) {
      const rm = roomList.value.find((x) => x.id === r.roomId);
      if (rm) {
        r.floorId = rm.floorId;
        r.buildingId = rm.buildingId;
        const bld = buildingList.value.find((x) => x.id === rm.buildingId);
        if (bld) {
          r.companyId = bld.companyId;
        }
      }
    }
  }
}

function addRoomHistory() {
  rooms.value = [
    ...rooms.value,
    {
      id: '',
      roomId: '',
      companyId: '',
      buildingId: '',
      floorId: '',
      startTime: '',
      endTime: ''
    }
  ];
}

function removeRoomHistory(index: number) {
  const list = [...rooms.value];
  list.splice(index, 1);
  rooms.value = list;
}

function onCompanyChange(r: any) {
  r.buildingId = '';
  r.floorId = '';
  r.roomId = '';
}

function onBuildingChange(r: any) {
  r.floorId = '';
  r.roomId = '';
}

function onFloorChange(r: any) {
  r.roomId = '';
}

onMounted(() => {
  fetchInitialData();
});
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-between items-center mb-2">
      <span class="text-xs text-gray-500">* 고인이 배정받은 빈소/호실의 배정 시작일과 만료일을 기록합니다.</span>
      <Button type="dashed" size="small" @click="addRoomHistory">
        <Plus class="size-4 mr-1 inline-block" /> 호실 배정 추가
      </Button>
    </div>

    <div v-if="rooms.length === 0" class="text-center py-6 text-gray-400 rounded border border-dashed">
      배정된 호실 이력이 없습니다. 빈소 배정을 추가하십시오.
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="(r, index) in rooms"
        :key="index"
        class="p-4 rounded border border-gray-150 relative space-y-3"
      >
        <div class="absolute top-2 right-2 z-10">
          <Button type="text" size="small" danger @click="removeRoomHistory(index)">
            <Trash2 class="size-4" />
          </Button>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 pt-4">
          <!-- 1. 회사 선택 -->
          <Form.Item label="소속 회사" required>
            <BizSelect
              v-model:value="r.companyId"
              type="company"
              placeholder="회사 선택"
              @change="onCompanyChange(r)"
            />
          </Form.Item>

          <!-- 2. 건물 선택 -->
          <Form.Item label="소속 건물" required>
            <BizSelect
              v-model:value="r.buildingId"
              type="building"
              :params="{ companyId: r.companyId }"
              placeholder="건물 선택"
              @change="onBuildingChange(r)"
            />
          </Form.Item>

          <!-- 3. 층 선택 -->
          <Form.Item label="배정 층" required>
            <BizSelect
              v-model:value="r.floorId"
              type="floor"
              :params="{ buildingId: r.buildingId }"
              placeholder="층 선택"
              @change="onFloorChange(r)"
            />
          </Form.Item>

          <!-- 4. 호실(빈소) 선택 -->
          <Form.Item label="호실(빈소)" required>
            <BizSelect
              v-model:value="r.roomId"
              type="room"
              :params="{ floorId: r.floorId }"
              placeholder="호실 선택"
            />
          </Form.Item>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <!-- 5. 시작일시 -->
          <Form.Item label="배정 시작일시" required>
            <AutoDatePicker
              v-model:value="r.startTime"
              show-time
              style="width: 100%"
            />
          </Form.Item>

          <!-- 6. 종료일시 -->
          <Form.Item label="배정 종료(발인)일시">
            <AutoDatePicker
              v-model:value="r.endTime"
              show-time
              style="width: 100%"
            />
          </Form.Item>
        </div>
      </div>
    </div>
  </div>
</template>
