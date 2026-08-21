<script lang="ts" setup>
import { ref, watch } from 'vue';
import {
  Alert, Button, Divider, Form, message, Slider, Spin, Switch, TimePicker,
} from 'ant-design-vue';
import { setDeviceScreenPower } from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';

const props = defineProps<{
  deviceConfig: BuildingApi.DeviceConfig | null;
  configLoading: boolean;
  configSaving: boolean;
  powerOnTimeVal: any;
  powerOffTimeVal: any;
  rebootTimeVal: any;
  deviceId: string;
  deviceCode: string;
}>();

const emit = defineEmits<{
  (e: 'save'): void;
  (e: 'reset'): void;
  (e: 'update:powerOnTimeVal', val: any): void;
  (e: 'update:powerOffTimeVal', val: any): void;
  (e: 'update:rebootTimeVal', val: any): void;
}>();

// 원격 화면 전원 명령 전송 중 여부
const isPowerSending = ref(false);

/// [원격 화면 전원 제어]
/// 아래의 '자동 전원 제어'가 예약(시각) 기반인 것과 달리, 지금 즉시 화면을 끄고 켠다.
/// DB 에 저장되지 않는 일회성 명령이라 장비가 온라인일 때만 전달되고,
/// 장비가 재기동되면 화면은 항상 켜진 상태로 뜬다.
async function handleScreenPower(state: 'OFF' | 'ON') {
  if (!props.deviceCode || isPowerSending.value) return;

  isPowerSending.value = true;
  try {
    await setDeviceScreenPower(props.deviceCode, state);
    message.success(`화면 ${state === 'ON' ? '켜기' : '끄기'} 명령을 전송했습니다.`);
  } catch {
    message.error('명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.');
  } finally {
    isPowerSending.value = false;
  }
}

// 자동 저장을 위한 디바운스 타이머
const debounceTimer = ref<NodeJS.Timeout | null>(null);
const lastSavedData = ref<string>('');

// 장비가 변경되면 기존 타이머를 즉시 취소하여 잘못된 자동 저장 방지
watch(
  () => props.deviceId,
  () => {
    if (debounceTimer.value) {
      clearTimeout(debounceTimer.value);
      debounceTimer.value = null;
    }
  },
);

// 설정 값 및 로딩/저장 상태 변경을 하나의 watch에서 일관되게 관리
watch(
  () => [
    props.configLoading,
    props.configSaving,
    JSON.stringify([
      props.deviceConfig,
      props.powerOnTimeVal,
      props.powerOffTimeVal,
      props.rebootTimeVal,
    ]),
  ] as const,
  ([loading, saving, currentDataJson], oldState) => {
    // 최초 실행 시 안전 처리
    if (!oldState) {
      if (loading || saving) {
        return;
      }
      if (props.deviceConfig) {
        lastSavedData.value = currentDataJson;
      }
      return;
    }

    const [oldLoading, oldSaving, oldDataJson] = oldState;

    // 1. 로딩 중이거나 저장 중일 때는 기존 타이머를 취소하고 자동 저장을 하지 않음
    if (loading || saving) {
      if (debounceTimer.value) {
        clearTimeout(debounceTimer.value);
        debounceTimer.value = null;
      }
      return;
    }

    // 2. 로딩 완료 또는 저장 완료 시점에는 최종 데이터를 원본 기준으로 설정하고 타이머 취소
    const justFinishedLoading = oldLoading && !loading;
    const justFinishedSaving = oldSaving && !saving;

    if (justFinishedLoading || justFinishedSaving) {
      lastSavedData.value = currentDataJson;
      if (debounceTimer.value) {
        clearTimeout(debounceTimer.value);
        debounceTimer.value = null;
      }
      return;
    }

    // 3. 실제 설정 값이 변경된 경우
    if (currentDataJson !== oldDataJson) {
      // 백업된 기준 데이터가 없거나, 현재 데이터가 기준 데이터와 일치하면 타이머 취소 후 종료
      if (!lastSavedData.value || currentDataJson === lastSavedData.value) {
        if (debounceTimer.value) {
          clearTimeout(debounceTimer.value);
          debounceTimer.value = null;
        }
        return;
      }

      // 기존에 설정된 타이머 취소
      if (debounceTimer.value) {
        clearTimeout(debounceTimer.value);
      }

      // 1.5초(1500ms) 후에 'save' 이벤트를 발생시키는 새로운 타이머 설정
      debounceTimer.value = setTimeout(() => {
        if (!props.configSaving) {
          emit('save');
        }
      }, 1500);
    }
  },
  { immediate: true },
);
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="configLoading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="설정 불러오는 중..." />
    </div>

    <!-- 설정 폼 -->
    <div v-else-if="deviceConfig" class="flex-1 overflow-auto px-4 py-3">
      <Alert
        v-if="!deviceConfig.id"
        type="info"
        show-icon
        class="mb-4"
        message="아직 저장된 기본 설정이 없습니다."
        description="아래 항목을 설정한 뒤 저장하면 장비 기본 설정이 등록됩니다."
      />

      <Form layout="vertical" size="small">
        <Form.Item label="기기 음량 (Volume)">
          <Slider v-model:value="deviceConfig.volume" :min="0" :max="100" />
          <div class="text-right text-xs text-muted-foreground">{{ deviceConfig.volume }}%</div>
        </Form.Item>
        <Form.Item label="화면 밝기 (Brightness)">
          <Slider v-model:value="deviceConfig.brightness" :min="0" :max="100" />
          <div class="text-right text-xs text-muted-foreground">{{ deviceConfig.brightness }}%</div>
        </Form.Item>
        <Divider />
        <!--
          즉시 실행되는 원격 화면 전원 제어.
          아래 '자동 전원 제어'는 시각 예약이고, 이쪽은 지금 바로 끄고 켜는 명령이다.
        -->
        <Form.Item label="원격 화면 전원 (즉시 실행)">
          <div class="flex items-center gap-2">
            <Button :loading="isPowerSending" @click="handleScreenPower('ON')">화면 켜기</Button>
            <Button danger :loading="isPowerSending" @click="handleScreenPower('OFF')">화면 끄기</Button>
          </div>
          <div class="mt-1 text-xs text-muted-foreground">
            장비가 온라인일 때만 전달되며 저장되지 않습니다. 재기동 시 화면은 다시 켜집니다.
          </div>
        </Form.Item>
        <Divider />
        <Form.Item label="자동 전원 제어">
          <Switch
            v-model:checked="deviceConfig.isAutoPower"
            checked-children="사용"
            un-checked-children="사용안함"
          />
        </Form.Item>
        <template v-if="deviceConfig.isAutoPower">
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="자동 켜짐 시각">
              <TimePicker
                :value="powerOnTimeVal"
                format="HH:mm"
                style="width: 100%"
                @change="(val) => emit('update:powerOnTimeVal', val)"
              />
            </Form.Item>
            <Form.Item label="자동 꺼짐 시각">
              <TimePicker
                :value="powerOffTimeVal"
                format="HH:mm"
                style="width: 100%"
                @change="(val) => emit('update:powerOffTimeVal', val)"
              />
            </Form.Item>
          </div>
        </template>
        <Divider />
        <Form.Item label="일일 자동 재시작 시각">
          <TimePicker
            :value="rebootTimeVal"
            format="HH:mm"
            style="width: 100%"
            @change="(val) => emit('update:rebootTimeVal', val)"
          />
        </Form.Item>
      </Form>
    </div>

    <!-- 저장 버튼 -->
    <div
      v-if="deviceConfig && !configLoading"
      class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2"
    >
      <Button @click="emit('reset')">기본값으로 초기화</Button>
      <Button type="primary" :loading="configSaving" @click="emit('save')">
        설정 저장
      </Button>
    </div>
  </div>
</template>
