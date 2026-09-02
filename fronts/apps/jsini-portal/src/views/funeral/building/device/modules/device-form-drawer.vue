<script lang="ts" setup>
import { ref } from 'vue';
import { useVbenDrawer } from '@vben/common-ui';
import { message } from 'ant-design-vue';
import { Form, Input, InputNumber } from 'ant-design-vue';
import { createDevice, updateDevice } from '#/api/funeral/building';
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
  shortName: '',
  code: '',
  deviceType: 'FUNERAL_PORTRAIT',
  ipAddress: '',
  macAddress: '',
  status: 'UNKNOWN' as 'ONLINE' | 'OFFLINE' | 'UNKNOWN',
  companyId: '',
  buildingId: '',
  floorId: '',
  roomId: '',
  sortOrder: 0,
});

const [DeviceDrawer, deviceDrawerApi] = useVbenDrawer({
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
    shortName: '',
    code: '',
    deviceType: 'FUNERAL_PORTRAIT',
    ipAddress: '',
    macAddress: '',
    status: 'UNKNOWN',
    companyId: props.selectedCompanyId,
    buildingId: props.selectedBuildingId,
    floorId: props.selectedFloorId,
    roomId: props.selectedRoomId,
    sortOrder: 0,
  };
  deviceDrawerApi.open();
}

/** 수정 모달 열기 */
function openEdit(row: any) {
  formModel.value = {
    sortOrder: 0,
    ...row,
  };
  deviceDrawerApi.open();
}

async function handleSave() {
  try {
    const dataToSend = {
      ...formModel.value,
      code: formModel.value.code || '',
    };

    if (formModel.value.id) {
      await updateDevice(formModel.value.id, dataToSend);
      message.success('장비 정보가 수정되었습니다.');
    } else {
      await createDevice(dataToSend);
      message.success('장비가 성공적으로 등록되었습니다.');
    }
    deviceDrawerApi.close();
    emit('saved');
  } catch {
    message.error('저장 실패');
  }
}

defineExpose({ openCreate, openEdit });
</script>

<template>
  <!--
    `@ok` 를 걸지 않는다. 저장은 위 `onConfirm` 하나가 맡는다 —
    둘 다 걸면 확인 한 번에 저장이 두 번 나갈 수 있다.
  -->
  <DeviceDrawer>
    <div class="p-2">
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
        <Form.Item label="짧은 명칭">
          <Input v-model:value="formModel.shortName" placeholder="예: 로비 DID, 102호 등" />
        </Form.Item>
        <Form.Item label="장비코드">
          <Input
            v-model:value="formModel.code"
            placeholder="저장 시 자동으로 생성됩니다."
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
        <!--
          IP/MAC 은 장비(플레이어)가 접속하면서 스스로 보고하는 값이므로 화면에서는 입력하지 않는다.
          수기로 넣어봐야 장비가 접속하는 순간 실제 값으로 덮어써지기 때문에,
          비활성 상태로 보여주기만 한다. (device-management-tab.vue 와 동일한 처리)
        -->
        <Form.Item label="IP 주소">
          <Input
            v-model:value="formModel.ipAddress"
            :disabled="true"
            placeholder="자동 감지 대기 중..."
          />
        </Form.Item>
        <Form.Item label="MAC 주소">
          <Input
            v-model:value="formModel.macAddress"
            :disabled="true"
            placeholder="자동 감지 대기 중..."
          />
        </Form.Item>
      </Form>
    </div>
  </DeviceDrawer>
</template>
