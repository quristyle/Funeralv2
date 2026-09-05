import { ref } from 'vue';
import { message } from 'ant-design-vue';
import { getDeviceAttribute, upsertDeviceAttribute } from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';

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

  /**
   * 장비 속성 저장 (Upsert)
   *
   * `source` 를 주면 그것을 보낸다 — [화면 표시] 탭이 초안을 따로 들고 있어서다
   * (49번 문서 D-DV5). 주지 않으면 지금 불러 둔 값을 그대로 보낸다.
   *
   * 저장은 곧바로 장비로 방송된다(`DeviceAttributeService`). 그래서 부르는 쪽이
   * '언제 나갈지'를 정할 수 있어야 한다.
   */
  async function handleAttrSave(
    deviceId: string,
    source?: BuildingApi.DeviceAttribute,
  ) {
    const value = source ?? deviceAttr.value;
    if (!value) return;
    attrSaving.value = true;
    try {
      const payload: Omit<BuildingApi.DeviceAttribute, 'id'> = {
        deviceId,
        displayOrientation: value.displayOrientation,
        portraitOrientation: value.portraitOrientation,
        videoOrientation: value.videoOrientation,
        displayPaddingTop: value.displayPaddingTop,
        displayPaddingLeft: value.displayPaddingLeft,
        displayPaddingRight: value.displayPaddingRight,
        displayPaddingBottom: value.displayPaddingBottom,
        contentIntervalSec: value.contentIntervalSec,
        isScreensaverEnabled: value.isScreensaverEnabled,
        screensaverTimeoutSec: value.screensaverTimeoutSec,
        isMemorialPhotoEnabled: value.isMemorialPhotoEnabled,
        memorialPhotoEffect: value.memorialPhotoEffect,
        photoVerticalAlignment: value.photoVerticalAlignment,
        photoHorizontalAlignment: value.photoHorizontalAlignment,
        isDeceasedNameVisible: value.isDeceasedNameVisible,
        isFamilyContactVisible: value.isFamilyContactVisible,
        memorialPaddingTop: value.memorialPaddingTop,
        memorialPaddingLeft: value.memorialPaddingLeft,
        memorialPaddingRight: value.memorialPaddingRight,
        memorialPaddingBottom: value.memorialPaddingBottom,
        isVideoEnabled: value.isVideoEnabled,
        isMusicEnabled: value.isMusicEnabled,
        videoId: value.videoId,
        musicId: value.musicId,
        musicVolume: value.musicVolume,
        isMediaLoop: value.isMediaLoop,
        isMuted: value.isMuted,
        isBackgroundImageEnabled: value.isBackgroundImageEnabled,
        backgroundImageId: value.backgroundImageId,
        backgroundOrientation: value.backgroundOrientation ?? 'HORIZONTAL',
        isFloorGuideEnabled: value.isFloorGuideEnabled,
        isRoomAssignmentVisible: value.isRoomAssignmentVisible,
        isActiveRoomsOnly: value.isActiveRoomsOnly,
        floorGuideRefreshSec: value.floorGuideRefreshSec,
        isTouchEnabled: value.isTouchEnabled,
        isQrCodeVisible: value.isQrCodeVisible,
        isBuildingMapVisible: value.isBuildingMapVisible,
        entranceGreeting: value.entranceGreeting,
        isNoticeVisible: value.isNoticeVisible,
        noticeScrollSpeed: value.noticeScrollSpeed,
        isMemorialPhotoKeepAspectRatio: value.isMemorialPhotoKeepAspectRatio,
        remark: value.remark,
      };
      const result = await upsertDeviceAttribute(payload);
      const saved = (result as any)?.result?.[0] ?? (result as any)?.result ?? result;
      if (saved && typeof saved === 'object') {
        deviceAttr.value = saved as BuildingApi.DeviceAttribute;
      }
      // 저장이 곧 장비 반영이다. 「저장됐다」가 아니라 어디까지 갔는지 말한다.
      message.success('장비에 적용했습니다.');
    } catch {
      message.error('적용에 실패했습니다.');
    } finally {
      attrSaving.value = false;
    }
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
    resetAttr,
  };
}
