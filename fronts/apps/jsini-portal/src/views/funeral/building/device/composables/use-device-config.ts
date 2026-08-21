import { ref } from 'vue';
import { message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { getDeviceConfigs, upsertDeviceConfig } from '#/api/building';
import type { BuildingApi } from '#/api/building';

/** 장비 기본 설정 기본값 팩토리 */
export function defaultDeviceConfig(deviceId: string): BuildingApi.DeviceConfig {
  return {
    id: '',
    deviceId,
    volume: 50,
    brightness: 80,
    rebootTime: '',
    isAutoPower: false,
    powerOnTime: '',
    powerOffTime: '',
  };
}

export function useDeviceConfig() {
  const deviceConfig = ref<BuildingApi.DeviceConfig | null>(null);
  const configLoading = ref(false);
  const configSaving = ref(false);
  const powerOnTimeVal = ref<any>(null);
  const powerOffTimeVal = ref<any>(null);
  const rebootTimeVal = ref<any>(null);

  function applyTimeValues(config: BuildingApi.DeviceConfig) {
    powerOnTimeVal.value = config.powerOnTime ? dayjs(config.powerOnTime, 'HH:mm') : null;
    powerOffTimeVal.value = config.powerOffTime ? dayjs(config.powerOffTime, 'HH:mm') : null;
    rebootTimeVal.value = config.rebootTime ? dayjs(config.rebootTime, 'HH:mm') : null;
  }

  /** 장비 기본 설정 로드 (없으면 기본값으로 초기화) */
  async function loadDeviceConfig(deviceId: string) {
    configLoading.value = true;
    deviceConfig.value = null;
    powerOnTimeVal.value = null;
    powerOffTimeVal.value = null;
    rebootTimeVal.value = null;
    try {
      const res = await getDeviceConfigs({ deviceId });
      const raw = (res as any)?.result ?? res;
      const list: BuildingApi.DeviceConfig[] = Array.isArray(raw) ? raw : [];
      const found = list[0] ?? null;
      deviceConfig.value = found ?? defaultDeviceConfig(deviceId);
      applyTimeValues(deviceConfig.value);
    } catch {
      deviceConfig.value = defaultDeviceConfig(deviceId);
      applyTimeValues(deviceConfig.value);
    } finally {
      configLoading.value = false;
    }
  }

  /** 장비 기본 설정 저장 (Upsert) */
  async function handleConfigSave(deviceId: string) {
    if (!deviceConfig.value) return;
    configSaving.value = true;
    try {
      const payload: Omit<BuildingApi.DeviceConfig, 'id' | 'deviceName'> = {
        deviceId,
        volume: deviceConfig.value.volume,
        brightness: deviceConfig.value.brightness,
        isAutoPower: deviceConfig.value.isAutoPower,
        powerOnTime: powerOnTimeVal.value ? powerOnTimeVal.value.format('HH:mm') : '',
        powerOffTime: powerOffTimeVal.value ? powerOffTimeVal.value.format('HH:mm') : '',
        rebootTime: rebootTimeVal.value ? rebootTimeVal.value.format('HH:mm') : '',
      };
      const result = await upsertDeviceConfig(payload);
      const raw = (result as any)?.result ?? result;
      const saved = Array.isArray(raw) ? (raw[0] ?? null) : raw;
      if (saved && typeof saved === 'object') {
        deviceConfig.value = saved as BuildingApi.DeviceConfig;
        applyTimeValues(deviceConfig.value);
      }
      message.success('장비 설정이 저장되었습니다.');
    } catch {
      message.error('설정 저장에 실패했습니다.');
    } finally {
      configSaving.value = false;
    }
  }

  /** 설정 폼을 기본값으로 초기화 */
  function handleConfigReset(deviceId: string) {
    deviceConfig.value = defaultDeviceConfig(deviceId);
    applyTimeValues(deviceConfig.value);
  }

  /** 설정 상태 초기화 */
  function resetConfig() {
    deviceConfig.value = null;
    powerOnTimeVal.value = null;
    powerOffTimeVal.value = null;
    rebootTimeVal.value = null;
  }

  return {
    deviceConfig,
    configLoading,
    configSaving,
    powerOnTimeVal,
    powerOffTimeVal,
    rebootTimeVal,
    loadDeviceConfig,
    handleConfigSave,
    handleConfigReset,
    resetConfig,
  };
}
