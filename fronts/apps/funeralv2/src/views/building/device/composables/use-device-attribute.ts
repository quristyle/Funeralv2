import { ref } from 'vue';
import { message } from 'ant-design-vue';
import { getDeviceAttribute, upsertDeviceAttribute } from '#/api/building';
import type { BuildingApi } from '#/api/building';

/** 장비 속성 기본값 팩토리 */
export function defaultAttr(deviceId: string): Omit<BuildingApi.DeviceAttribute, 'id'> {
  return {
    deviceId,
    displayOrientation: 'LANDSCAPE',
    portraitOrientation: 'HORIZONTAL',
    videoOrientation: 'HORIZONTAL',
    displayPaddingTop: 0,
    displayPaddingLeft: 0,
    displayPaddingRight: 0,
    displayPaddingBottom: 0,
    contentIntervalSec: 10,
    isScreensaverEnabled: false,
    screensaverTimeoutSec: 300,
    isMemorialPhotoEnabled: false,
    memorialPhotoEffect: 'FADE',
    photoVerticalAlignment: 'TOP',    // 기본값: 상단
    photoHorizontalAlignment: 'CENTER', // 기본값: 중앙
    isDeceasedNameVisible: true,
    isFamilyContactVisible: false,
    memorialPaddingTop: 0,
    memorialPaddingLeft: 0,
    memorialPaddingRight: 0,
    memorialPaddingBottom: 0,
    isVideoEnabled: false,
    isMusicEnabled: false,
    videoId: null,
    musicId: null,
    musicVolume: null,
    isMediaLoop: true,
    isMuted: false,
    isBackgroundImageEnabled: false,
    backgroundImageId: null,
    backgroundOrientation: 'HORIZONTAL',
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
    isMemorialPhotoKeepAspectRatio: true,
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
      } else {
        // 기존 DB 레코드에 신규 필드가 없는 경우(null/undefined) 기본값으로 폴백
        if (!deviceAttr.value.photoVerticalAlignment) {
          deviceAttr.value.photoVerticalAlignment = 'TOP';
        }
        if (!deviceAttr.value.photoHorizontalAlignment) {
          deviceAttr.value.photoHorizontalAlignment = 'CENTER';
        }
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
        portraitOrientation: deviceAttr.value.portraitOrientation,
        videoOrientation: deviceAttr.value.videoOrientation,
        displayPaddingTop: deviceAttr.value.displayPaddingTop,
        displayPaddingLeft: deviceAttr.value.displayPaddingLeft,
        displayPaddingRight: deviceAttr.value.displayPaddingRight,
        displayPaddingBottom: deviceAttr.value.displayPaddingBottom,
        contentIntervalSec: deviceAttr.value.contentIntervalSec,
        isScreensaverEnabled: deviceAttr.value.isScreensaverEnabled,
        screensaverTimeoutSec: deviceAttr.value.screensaverTimeoutSec,
        isMemorialPhotoEnabled: deviceAttr.value.isMemorialPhotoEnabled,
        memorialPhotoEffect: deviceAttr.value.memorialPhotoEffect,
        photoVerticalAlignment: deviceAttr.value.photoVerticalAlignment,
        photoHorizontalAlignment: deviceAttr.value.photoHorizontalAlignment,
        isDeceasedNameVisible: deviceAttr.value.isDeceasedNameVisible,
        isFamilyContactVisible: deviceAttr.value.isFamilyContactVisible,
        memorialPaddingTop: deviceAttr.value.memorialPaddingTop,
        memorialPaddingLeft: deviceAttr.value.memorialPaddingLeft,
        memorialPaddingRight: deviceAttr.value.memorialPaddingRight,
        memorialPaddingBottom: deviceAttr.value.memorialPaddingBottom,
        isVideoEnabled: deviceAttr.value.isVideoEnabled,
        isMusicEnabled: deviceAttr.value.isMusicEnabled,
        videoId: deviceAttr.value.videoId,
        musicId: deviceAttr.value.musicId,
        musicVolume: deviceAttr.value.musicVolume,
        isMediaLoop: deviceAttr.value.isMediaLoop,
        isMuted: deviceAttr.value.isMuted,
        isBackgroundImageEnabled: deviceAttr.value.isBackgroundImageEnabled,
        backgroundImageId: deviceAttr.value.backgroundImageId,
        backgroundOrientation: deviceAttr.value.backgroundOrientation ?? 'HORIZONTAL',
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
        isMemorialPhotoKeepAspectRatio: deviceAttr.value.isMemorialPhotoKeepAspectRatio,
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
