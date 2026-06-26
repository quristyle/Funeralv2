<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenModal } from '@vben/common-ui';
import { message } from 'ant-design-vue';
import { Button, Form, Input } from 'ant-design-vue';
import { createDevice, updateDevice } from '#/api/building';
import BizSelect from '#/components/BizSelect.vue';
import DictSelect from '#/components/DictSelect.vue';

const props = defineProps<{
  selectedCompanyId: string;
  selectedBuildingId: string;
  selectedFloorId: string;
  selectedRoomId: string;
}>();

const emit = defineEmits<{
  (e: 'saved'): void;
}>();

const formModel = ref({
  id: '',
  name: '',
  code: '',
  deviceType: 'FUNERAL_PORTRAIT',
  ipAddress: '',
  macAddress: '',
  status: 'UNKNOWN' as 'ONLINE' | 'OFFLINE' | 'UNKNOWN',
  companyId: '',
  buildingId: '',
  floorId: '',
  roomId: '',
});

const [DeviceModal, deviceModalApi] = useVbenModal({
  title: '장비 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  },
});

/** 등록 모달 열기 */
function openCreate() {
  formModel.value = {
    id: '',
    name: '',
    code: '',
    deviceType: 'FUNERAL_PORTRAIT',
    ipAddress: '',
    macAddress: '',
    status: 'UNKNOWN',
    companyId: props.selectedCompanyId,
    buildingId: props.selectedBuildingId,
    floorId: props.selectedFloorId,
    roomId: props.selectedRoomId,
  };
  deviceModalApi.open();
}

/** 수정 모달 열기 */
function openEdit(row: any) {
  formModel.value = { ...row };
  deviceModalApi.open();
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateDevice(formModel.value.id, formModel.value);
      message.success('장비 정보가 수정되었습니다.');
    } else {
      await createDevice(formModel.value);
      message.success('장비가 성공적으로 등록되었습니다.');
    }
    deviceModalApi.close();
    emit('saved');
  } catch {
    message.error('저장 실패');
  }
}

defineExpose({ openCreate, openEdit });
</script>

<template>
  <DeviceModal @ok="handleSave">
    <div class="p-6">
      <Form layout="vertical">
        <Form.Item label="장비 소속 위치" required>
          <div class="grid grid-cols-2 gap-4">
            <BizSelect
              v-model:value="formModel.buildingId"
              type="building"
              :params="{ companyId: formModel.companyId || selectedCompanyId }"
              placeholder="건물 선택"
            />
            <BizSelect
              v-model:value="formModel.floorId"
              type="floor"
              :params="{ buildingId: formModel.buildingId }"
              placeholder="층 선택"
            />
            <BizSelect
              v-model:value="formModel.roomId"
              type="room"
              :params="{ floorId: formModel.floorId }"
              placeholder="호실 선택"
            />
          </div>
        </Form.Item>
        <Form.Item label="장비명" required>
          <Input v-model:value="formModel.name" placeholder="예: 로비 대형 DID, 102호 현판" />
        </Form.Item>
        <Form.Item label="장비코드" required>
          <Input
            v-model:value="formModel.code"
            placeholder="예: DID_LOBBY_01"
            :disabled="!!formModel.id"
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
        <Form.Item label="IP 주소">
          <Input v-model:value="formModel.ipAddress" placeholder="예: 192.168.1.100" />
        </Form.Item>
        <Form.Item label="MAC 주소">
          <Input v-model:value="formModel.macAddress" placeholder="예: 00:0a:95:9d:68:16" />
        </Form.Item>
      </Form>
    </div>
  </DeviceModal>
</template>
