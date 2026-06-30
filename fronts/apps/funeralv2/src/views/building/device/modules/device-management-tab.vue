<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Button, Form, Input, InputNumber, message } from 'ant-design-vue';
import { updateDevice } from '#/api/building';
import type { BuildingApi } from '#/api/building';
import BizSelect from '#/components/BizSelect.vue';
import DictSelect from '#/components/DictSelect.vue';

const props = defineProps<{
  device: BuildingApi.Device;
}>();

const emit = defineEmits<{
  (e: 'saved'): void;
}>();

const formModel = ref<Partial<BuildingApi.Device>>({});
const isSaving = ref(false);

// Props로 받은 device 데이터가 변경될 때마다 formModel을 동기화합니다.
watch(
  () => props.device,
  (newDevice) => {
    if (newDevice) {
      formModel.value = { ...newDevice };
    }
  },
  { immediate: true, deep: true },
);

/**
 * 장비 정보 저장 처리
 */
async function handleSave() {
  if (!formModel.value.id) return;
  isSaving.value = true;
  try {
    const dataToSend = {
      ...formModel.value,
      code: formModel.value.code || '',
    };
    await updateDevice(formModel.value.id, dataToSend);
    message.success('장비 정보가 수정되었습니다.');
    emit('saved');
  } catch {
    message.error('저장 실패');
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- 설정 폼 -->
    <div class="flex-1 overflow-auto px-4 py-3">
      <Form layout="vertical" :model="formModel">
        <Form.Item label="장비 소속 위치" required>
          <div class="grid grid-cols-2 gap-4">
            <BizSelect
              v-model:value="formModel.companyId"
              type="company"
              placeholder="회사 선택"
              @change="
                () => {
                  formModel.buildingId = undefined;
                  formModel.floorId = undefined;
                  formModel.roomId = undefined;
                }
              "
            />
            <BizSelect
              v-model:value="formModel.buildingId"
              type="building"
              :params="{ companyId: formModel.companyId }"
              placeholder="건물 선택"
              @change="
                () => {
                  formModel.floorId = undefined;
                  formModel.roomId = undefined;
                }
              "
            />
            <BizSelect
              v-model:value="formModel.floorId"
              type="floor"
              :params="{ buildingId: formModel.buildingId }"
              placeholder="층 선택"
              allow-clear
              @change="
                () => {
                  formModel.roomId = undefined;
                }
              "
            />
            <BizSelect
              v-model:value="formModel.roomId"
              type="room"
              :params="{ floorId: formModel.floorId }"
              placeholder="호실 선택"
              allow-clear
            />
          </div>
        </Form.Item>
        <Form.Item label="장비명" required>
          <Input v-model:value="formModel.name" placeholder="예: 로비 대형 DID, 102호 현판" />
        </Form.Item>
        <Form.Item label="짧은 명칭">
          <Input v-model:value="formModel.shortName" placeholder="예: 로비 DID, 102호 등" />
        </Form.Item>
        <Form.Item label="장비코드">
          <Input
            v-model:value="formModel.code"
            placeholder="자동 생성된 고유 코드"
            :disabled="true"
          />
        </Form.Item>
        <Form.Item label="장비 유형">
          <DictSelect
            v-model:value="formModel.deviceType"
            dict-code="EQUIPMENT_TYPE"
            placeholder="장비 유형 선택"
            style="width: 100%"
          />
        </Form.Item>
        <Form.Item label="정렬 순서">
          <InputNumber
            v-model:value="formModel.sortOrder"
            :min="0"
            :precision="0"
            style="width: 100%"
          />
        </Form.Item>
        <Form.Item label="IP 주소">
          <Input v-model:value="formModel.ipAddress" placeholder="예: 192.168.1.100" />
        </Form.Item>
        <Form.Item label="MAC 주소">
          <Input v-model:value="formModel.macAddress" placeholder="예: 00:0a:95:9d:68:16" />
        </Form.Item>
      </Form>
    </div>
    <!-- 저장 버튼 -->
    <div class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2">
      <Button type="primary" :loading="isSaving" @click="handleSave">정보 저장</Button>
    </div>
  </div>
</template>