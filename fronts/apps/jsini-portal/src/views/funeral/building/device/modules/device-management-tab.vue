<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Button, Form, Input, InputNumber, message } from 'ant-design-vue';
import { updateDevice } from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';
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
const debounceTimer = ref<NodeJS.Timeout | null>(null);
/** 서버에서 받아 채운 마지막 모양. 이것과 같으면 사람이 고친 것이 아니다. */
const syncedSnapshot = ref('');

// Props로 받은 device 데이터가 변경될 때마다 formModel을 동기화합니다.
watch(
  () => props.device,
  (newDevice) => {
    if (newDevice) {
      formModel.value = { ...newDevice };
      syncedSnapshot.value = JSON.stringify(formModel.value);
      // 다른 장비로 넘어갔다면 앞 장비를 위해 예약된 자동 저장은 버린다.
      if (debounceTimer.value) {
        clearTimeout(debounceTimer.value);
        debounceTimer.value = null;
      }
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
    // 저장 뒤 서버에서 되돌아온 값이면 다시 저장하지 않는다(무한 왕복 방지).
    if (newValue === syncedSnapshot.value) {
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
        handleSave(true);
      }
    }, 1500);
  },
);

/**
 * 장비 정보 저장 처리
 *
 * 목록의 수정 아이콘이 이 탭으로 오게 되면서 여기가 장비 정보를 고치는 유일한 자리다.
 * 회사를 바꾸면 건물 · 층 · 호실이 한 번 비므로, 그 순간 자동 저장이 나가면
 * 소속이 지워진 채로 저장된다. 필수 값이 채워졌을 때만 보낸다.
 *
 * @param auto 자동 저장(타이핑 멈춤)이면 true. 손으로 누른 것이면 왜 안 나갔는지 알려 준다.
 */
async function handleSave(auto = false) {
  if (!formModel.value.id) return;
  if (!formModel.value.name || !formModel.value.buildingId) {
    if (!auto) {
      message.warning({ content: '장비명과 건물은 비워 둘 수 없습니다.', key: 'device-save' });
    }
    return;
  }
  isSaving.value = true;
  try {
    const dataToSend = {
      ...formModel.value,
      code: formModel.value.code || '',
    };
    await updateDevice(formModel.value.id, dataToSend);
    // 자동 저장이라 타이핑 중에도 뜬다. 같은 key 를 써서 알림이 쌓이지 않게 한다.
    message.success({ content: '장비 정보가 수정되었습니다.', key: 'device-save' });
    // 목록의 그 행과 패널 머리말을 최신 정보로 맞춘다(목록 재조회는 하지 않는다).
    emit('saved');
  } catch {
    message.error({ content: '저장 실패', key: 'device-save' });
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
              type="funeralCompany"
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
    <div class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2">
      <!-- `handleSave()` 로 부른다 — 함수만 넘기면 MouseEvent 가 `auto` 자리에 들어간다. -->
      <Button v-perm:update type="primary" :loading="isSaving" @click="handleSave()">정보 저장</Button>
    </div>
  </div>
</template>