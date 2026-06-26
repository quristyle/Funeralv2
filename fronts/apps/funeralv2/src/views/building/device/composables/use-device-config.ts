import { ref } from 'vue';
import { message } from 'ant-design-vue';
import dayjs from 'dayjs';
import { getDeviceConfigs, updateDeviceConfig } from '#/api/building';
import type { BuildingApi } from '#/api/building';

export function useDeviceConfig() {
  const deviceConfig = ref<BuildingApi.DeviceConfig | null>(null);
  const configLoading = ref(false);
  const configSaving = ref(false);
  const powerOnTimeVal = ref<any>(null);
  const powerOffTimeVal = ref<any>(null);
  const rebootTimeVal = ref<any>(null);

  /** 장비 기본 설정 로드 */
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
      deviceConfig.value = found;
      if (found) {
        powerOnTimeVal.value = found.powerOnTime ? dayjs(found.powerOnTime, 'HH:mm') : null;
        powerOffTimeVal.value = found.powerOffTime ? dayjs(found.powerOffTime, 'HH:mm') : null;
        rebootTimeVal.value = found.rebootTime ? dayjs(found.rebootTime, 'HH:mm') : null;
      }
    } catch {
      message.error('장비 설정을 불러오는 데 실패했습니다.');
    } finally {
      configLoading.value = false;
    }
  }

  /** 장비 기본 설정 저장 */
  async function handleConfigSave() {
    if (!deviceConfig.value) return;
    configSaving.value = true;
    try {
      const payload: Partial<BuildingApi.DeviceConfig> = {
        ...deviceConfig.value,
        powerOnTime: powerOnTimeVal.value ? powerOnTimeVal.value.format('HH:mm') : '',
        powerOffTime: powerOffTimeVal.value ? powerOffTimeVal.value.format('HH:mm') : '',
        rebootTime: rebootTimeVal.value ? rebootTimeVal.value.format('HH:mm') : '',
      };
      await updateDeviceConfig(deviceConfig.value.id, payload);
      message.success('장비 설정이 저장되었습니다.');
    } catch {
      message.error('설정 저장에 실패했습니다.');
    } finally {
      configSaving.value = false;
    }
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
    resetConfig,
  };
}
