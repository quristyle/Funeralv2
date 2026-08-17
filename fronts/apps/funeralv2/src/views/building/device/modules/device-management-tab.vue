<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Button, Form, Input, InputNumber, message } from 'ant-design-vue';
import { setDeviceScreenPower, updateDevice } from '#/api/building';
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
const isPowerSending = ref(false);

/// 원격 모니터 전원 제어.
/// 저장되는 설정이 아니라 즉시 실행 명령이라, 장비가 접속 중일 때만 전달된다.
async function handleScreenPower(state: 'OFF' | 'ON') {
  const code = props.device?.code;
  if (!code) return;

  isPowerSending.value = true;
  try {
    await setDeviceScreenPower(code, state);
    message.success(`화면 ${state === 'ON' ? '켜기' : '끄기'} 명령을 전송했습니다.`);
  } catch {
    message.error('명령 전송에 실패했습니다.');
  } finally {
    isPowerSending.value = false;
  }
}
const debounceTimer = ref<NodeJS.Timeout | null>(null);

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

// formModel의 변경을 감지하여 1.5초 후 자동 저장 실행
watch(
  () => JSON.stringify(formModel.value),
  (newValue, oldValue) => {
    // 초기화 단계이거나, 실제 값의 변경이 없으면 실행하지 않음
    if (newValue === oldValue || oldValue === '{}') {
      return;
    }

    // 기존 디바운스 타이머가 있다면 취소
    if (debounceTimer.value) {
      clearTimeout(debounceTimer.value);
    }

    // 1.5초(1500ms) 후에 handleSave 함수를 호출하는 새로운 타이머 설정
    debounceTimer.value = setTimeout(() => {
      // 수동 저장 버튼이 이미 로딩 중이면 자동 저장 실행 안 함
      if (!isSaving.value) {
        handleSave();
      }
    }, 1500);
  },
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
    //emit('saved');
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
        <Form.Item label="사설 IP 주소">
          <Input v-model:value="formModel.ipAddress" :disabled="true" placeholder="자동 감지 대기 중..." />
        </Form.Item>
        <Form.Item label="공인 IP 주소">
          <Input v-model:value="formModel.publicIpAddress" :disabled="true" placeholder="자동 감지 대기 중..." />
        </Form.Item>
        <Form.Item label="MAC 주소">
          <Input v-model:value="formModel.macAddress" :disabled="true" placeholder="자동 감지 대기 중..." />
        </Form.Item>
      </Form>
    </div>
    <!-- 저장 버튼 -->
    <div class="flex shrink-0 items-center justify-between gap-2 border-t border-border bg-muted/40 px-4 py-2">
      <!--
        원격 모니터 전원 제어. DB 에 저장되지 않는 즉시 실행 명령이며
        장비가 온라인일 때만 전달된다. 재기동하면 화면은 다시 켜진 상태로 뜬다.
      -->
      <div class="flex items-center gap-2">
        <span class="text-xs text-muted-foreground">화면 전원</span>
        <Button size="small" :loading="isPowerSending" @click="handleScreenPower('ON')">켜기</Button>
        <Button size="small" danger :loading="isPowerSending" @click="handleScreenPower('OFF')">끄기</Button>
      </div>
      <Button type="primary" :loading="isSaving" @click="handleSave">정보 저장</Button>
    </div>
  </div>
</template>