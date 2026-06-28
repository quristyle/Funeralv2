<script lang="ts" setup>
import { computed, ref, onMounted } from 'vue';
import { Button, Form, Select, DatePicker, message } from 'ant-design-vue';
import { Plus, Trash2 } from '@vben/icons';
import { getRooms } from '#/api/building';

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

async function fetchRoomList() {
  try {
    const list = await getRooms({});
    roomList.value = list || [];
  } catch (error) {
    message.error('호실 목록을 가져올 수 없습니다.');
  }
}

function addRoomHistory() {
  rooms.value = [
    ...rooms.value,
    {
      id: '',
      roomId: '',
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

onMounted(() => {
  fetchRoomList();
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

    <div v-if="rooms.length === 0" class="text-center py-6 text-gray-400  rounded border border-dashed">
      배정된 호실 이력이 없습니다. 빈소 배정을 추가하십시오.
    </div>

    <div v-else class="space-y-3">
      <div
        v-for="(r, index) in rooms"
        :key="index"
        class="p-4  rounded border border-gray-150 relative space-y-3"
      >
        <div class="absolute top-2 right-2 z-10">
          <Button type="text" size="small" danger @click="removeRoomHistory(index)">
            <Trash2 class="size-4" />
          </Button>
        </div>

        <div class="grid grid-cols-3 gap-4 pt-4">
          <Form.Item label="배정 빈소" required>
            <Select v-model:value="r.roomId" placeholder="빈소 선택">
              <Select.Option v-for="rm in roomList" :key="rm.id" :value="rm.id">{{ rm.name }}</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="배정 시작일시" required>
            <DatePicker
              v-model:value="r.startTime"
              show-time
              format="YYYY-MM-DD HH:mm:ss"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
            />
          </Form.Item>
          <Form.Item label="배정 종료(발인)일시">
            <DatePicker
              v-model:value="r.endTime"
              show-time
              format="YYYY-MM-DD HH:mm:ss"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
            />
          </Form.Item>
        </div>
      </div>
    </div>
  </div>
</template>
