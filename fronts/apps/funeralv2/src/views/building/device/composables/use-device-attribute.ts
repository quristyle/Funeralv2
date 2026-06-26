import { ref } from 'vue';
import { message } from 'ant-design-vue';
import { getDeviceAttribute, upsertDeviceAttribute } from '#/api/building';
import type { BuildingApi } from '#/api/building';

/** 장비 속성 기본값 팩토리 */
export function defaultAttr(deviceId: string): Omit<BuildingApi.DeviceAttribute, 'id'> {
  return {
    deviceId,
    displayOrientation: 'LANDSCAPE',
    contentIntervalSec: 10,
    isScreensaverEnabled: false,
    screensaverTimeoutSec: 300,
    isMemorialPhotoEnabled: false,
    memorialPhotoEffect: 'FADE',
    isDeceasedNameVisible: true,
    isFamilyContactVisible: false,
    isVideoEnabled: false,
    isMusicEnabled: false,
    musicVolume: null,
    isMediaLoop: true,
    isMuted: false,
    isFloorGuideEnabled: false,
    isRoomAssignmentVisible: true,
    isActiveRoomsOnly: true,
    floorGuideRefreshSec: 30,
    isTouchEnabled: false,
    isQrCodeVisible: false,
    isBuildingMapVisible: true,
    entranceGreeting: null,
    isNoticeVisible: true,
    noticeScrollSpeed: 2,
    remark: null,
  };
}

export function useDeviceAttribute() {
  const deviceAttr = ref<BuildingApi.DeviceAttribute | null>(null);
  const attrLoading = ref(false);
  const attrSaving = ref(false);

  /** 장비 속성 로드 (없으면 기본값으로 초기화) */
  async function loadDeviceAttribute(deviceId: string) {
    attrLoading.value = true;
    deviceAttr.value = null;
    try {
      const res = await getDeviceAttribute(deviceId);
      const raw = (res as any)?.result?.[0] ?? (res as any)?.result ?? res;
      deviceAttr.value = raw ?? null;
      if (!deviceAttr.value) {
        deviceAttr.value = { id: '', ...defaultAttr(deviceId) };
      }
    } catch {
      // 404인 경우 기본값으로 초기화 (신규 장비는 속성이 없을 수 있음)
      deviceAttr.value = { id: '', ...defaultAttr(deviceId) };
    } finally {
      attrLoading.value = false;
    }
  }

  /** 장비 속성 저장 (Upsert) */
  async function handleAttrSave(deviceId: string) {
    if (!deviceAttr.value) return;
    attrSaving.value = true;
    try {
      const payload: Omit<BuildingApi.DeviceAttribute, 'id'> = {
        deviceId,
        displayOrientation: deviceAttr.value.displayOrientation,
        contentIntervalSec: deviceAttr.value.contentIntervalSec,
        isScreensaverEnabled: deviceAttr.value.isScreensaverEnabled,
        screensaverTimeoutSec: deviceAttr.value.screensaverTimeoutSec,
        isMemorialPhotoEnabled: deviceAttr.value.isMemorialPhotoEnabled,
        memorialPhotoEffect: deviceAttr.value.memorialPhotoEffect,
        isDeceasedNameVisible: deviceAttr.value.isDeceasedNameVisible,
        isFamilyContactVisible: deviceAttr.value.isFamilyContactVisible,
        isVideoEnabled: deviceAttr.value.isVideoEnabled,
        isMusicEnabled: deviceAttr.value.isMusicEnabled,
        musicVolume: deviceAttr.value.musicVolume,
        isMediaLoop: deviceAttr.value.isMediaLoop,
        isMuted: deviceAttr.value.isMuted,
        isFloorGuideEnabled: deviceAttr.value.isFloorGuideEnabled,
        isRoomAssignmentVisible: deviceAttr.value.isRoomAssignmentVisible,
        isActiveRoomsOnly: deviceAttr.value.isActiveRoomsOnly,
        floorGuideRefreshSec: deviceAttr.value.floorGuideRefreshSec,
        isTouchEnabled: deviceAttr.value.isTouchEnabled,
        isQrCodeVisible: deviceAttr.value.isQrCodeVisible,
        isBuildingMapVisible: deviceAttr.value.isBuildingMapVisible,
        entranceGreeting: deviceAttr.value.entranceGreeting,
        isNoticeVisible: deviceAttr.value.isNoticeVisible,
        noticeScrollSpeed: deviceAttr.value.noticeScrollSpeed,
        remark: deviceAttr.value.remark,
      };
      const result = await upsertDeviceAttribute(payload);
      const saved = (result as any)?.result?.[0] ?? (result as any)?.result ?? result;
      if (saved && typeof saved === 'object') {
        deviceAttr.value = saved as BuildingApi.DeviceAttribute;
      }
      message.success('장비 속성이 저장되었습니다.');
    } catch {
      message.error('장비 속성 저장에 실패했습니다.');
    } finally {
      attrSaving.value = false;
    }
  }

  /** 장비 속성 기본값으로 초기화 */
  function handleAttrReset(deviceId: string) {
    deviceAttr.value = { id: deviceAttr.value?.id ?? '', ...defaultAttr(deviceId) };
  }

  /** 속성 상태 초기화 */
  function resetAttr() {
    deviceAttr.value = null;
  }

  return {
    deviceAttr,
    attrLoading,
    attrSaving,
    loadDeviceAttribute,
    handleAttrSave,
    handleAttrReset,
    resetAttr,
  };
}
