<script lang="ts" setup>
import { ref, watch } from 'vue';
import {
  Alert, Button, Divider, Form, Input, InputNumber, Select, Slider, Spin, Switch,
} from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import BizSelect from '#/components/BizSelect.vue';
import type { BuildingApi } from '#/api/building';

const props = defineProps<{
  deviceAttr: BuildingApi.DeviceAttribute | null;
  attrLoading: boolean;
  attrSaving: boolean;
}>();

const emit = defineEmits<{
  (e: 'save'): void;
  (e: 'reset'): void;
}>();

// 자동 저장을 위한 디바운스 타이머
const debounceTimer = ref<NodeJS.Timeout | null>(null);

// deviceAttr 객체의 모든 속성 변경을 감지하여 자동 저장 실행
watch(
  // 객체를 문자열로 변환하여 실제 값의 변경을 감지 (참조 문제 회피)
  () => JSON.stringify(props.deviceAttr),
  (newValue, oldValue) => {
    // 컴포넌트 마운트 초기, 데이터 로딩 중, 또는 부모로부터 새 데이터를 받는 시점에는 실행하지 않음
    const oldData = oldValue ? JSON.parse(oldValue) : null;
    if (!newValue || !oldValue || !oldData?.id) {
      return;
    }

    // 문자열로 직접 비교하여 변경 여부 확인
    if (newValue === oldValue) {
      return;
    }

    // 기존에 설정된 타이머가 있다면 취소 (연속 변경 시 마지막 변경만 저장하기 위함)
    if (debounceTimer.value) {
      clearTimeout(debounceTimer.value);
    }

    // 1초(1000ms) 후에 'save' 이벤트를 발생시키는 새로운 타이머 설정
    debounceTimer.value = setTimeout(() => {
      emit('save');
    }, 1000);
  },
);
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="attrLoading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="장비 속성 불러오는 중..." />
    </div>

    <!-- 속성 폼 -->
    <div v-else-if="deviceAttr" class="flex-1 overflow-auto px-4 py-3">
      <Alert
        v-if="!deviceAttr.id"
        type="info"
        show-icon
        class="mb-4"
        message="아직 저장된 장비 속성이 없습니다."
        description="아래 항목을 설정한 뒤 저장하면 장비의 고유 속성이 등록됩니다."
      />

      <Form layout="vertical" size="small">

        <!-- ① 공통 표시 설정 -->
        <div class="attr-section">
          <div class="attr-section-title">
            <IconifyIcon icon="lucide:monitor-cog" class="size-4" />
            <span>공통 표시 설정</span>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="화면 방향">
              <Select v-model:value="deviceAttr.displayOrientation" style="width: 100%">
                <Select.Option value="LANDSCAPE">가로 (Landscape)</Select.Option>
                <Select.Option value="PORTRAIT">세로 (Portrait)</Select.Option>
              </Select>
            </Form.Item>
            <Form.Item label="화면 표현">
              <Select v-model:value="deviceAttr.portraitOrientation" style="width: 100%">
                <Select.Option value="HORIZONTAL">가로형 모습</Select.Option>
                <Select.Option value="VERTICAL_LEFT">좌90도 세로형 모습</Select.Option>
                <Select.Option value="VERTICAL_RIGHT">우90도 세로형 모습</Select.Option>
                <Select.Option value="INVERTED">뒤집기 모습</Select.Option>
              </Select>
            </Form.Item>
          </div>
          <div class="grid grid-cols-2 gap-3 mt-2">
            <Form.Item label="전체 화면 여백 (위)">
              <div class="flex items-center gap-3">
                <Slider v-model:value="deviceAttr.displayPaddingTop" :min="0" :max="40" class="flex-1" />
                <InputNumber v-model:value="deviceAttr.displayPaddingTop" :min="0" :max="40" addon-after="%" style="width: 85px" />
              </div>
            </Form.Item>
            <Form.Item label="전체 화면 여백 (아래)">
              <div class="flex items-center gap-3">
                <Slider v-model:value="deviceAttr.displayPaddingBottom" :min="0" :max="40" class="flex-1" />
                <InputNumber v-model:value="deviceAttr.displayPaddingBottom" :min="0" :max="40" addon-after="%" style="width: 85px" />
              </div>
            </Form.Item>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="전체 화면 여백 (좌)">
              <div class="flex items-center gap-3">
                <Slider v-model:value="deviceAttr.displayPaddingLeft" :min="0" :max="40" class="flex-1" />
                <InputNumber v-model:value="deviceAttr.displayPaddingLeft" :min="0" :max="40" addon-after="%" style="width: 85px" />
              </div>
            </Form.Item>
            <Form.Item label="전체 화면 여백 (우)">
              <div class="flex items-center gap-3">
                <Slider v-model:value="deviceAttr.displayPaddingRight" :min="0" :max="40" class="flex-1" />
                <InputNumber v-model:value="deviceAttr.displayPaddingRight" :min="0" :max="40" addon-after="%" style="width: 85px" />
              </div>
            </Form.Item>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="콘텐츠 전환 간격(초)">
              <InputNumber
                v-model:value="deviceAttr.contentIntervalSec"
                :min="3"
                :max="300"
                style="width: 100%"
                addon-after="초"
              />
            </Form.Item>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="대기 화면(스크린세이버)">
              <Switch
                v-model:checked="deviceAttr.isScreensaverEnabled"
                checked-children="사용"
                un-checked-children="사용안함"
              />
            </Form.Item>
            <Form.Item v-if="deviceAttr.isScreensaverEnabled" label="대기 전환 시간(초)">
              <InputNumber
                v-model:value="deviceAttr.screensaverTimeoutSec"
                :min="30"
                :max="3600"
                style="width: 100%"
                addon-after="초"
              />
            </Form.Item>
          </div>
        </div>

        <Divider />

        <!-- ② 영정사진/추모 콘텐츠 설정 -->
        <div class="attr-section">
          <div class="attr-section-title">
            <IconifyIcon icon="lucide:image" class="size-4" />
            <span>영정사진 · 추모 콘텐츠</span>
            <span class="attr-device-tag">영정사진 DID</span>
          </div>
          <Form.Item label="영정사진 표시">
            <Switch
              v-model:checked="deviceAttr.isMemorialPhotoEnabled"
              checked-children="사용"
              un-checked-children="사용안함"
            />
          </Form.Item>
          <template v-if="deviceAttr.isMemorialPhotoEnabled">
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="사진 전환 효과">
                <Select v-model:value="deviceAttr.memorialPhotoEffect" style="width: 100%">
                  <Select.Option value="FADE">페이드 (Fade)</Select.Option>
                  <Select.Option value="SLIDE">슬라이드 (Slide)</Select.Option>
                  <Select.Option value="NONE">효과 없음</Select.Option>
                </Select>
              </Form.Item>
              <Form.Item label="사진 세로 정렬">
                <Select v-model:value="deviceAttr.photoVerticalAlignment" style="width: 100%">
                  <Select.Option value="TOP">상단</Select.Option>
                  <Select.Option value="CENTER">중앙</Select.Option>
                  <Select.Option value="BOTTOM">하단</Select.Option>
                </Select>
              </Form.Item>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="사진 가로 정렬">
                <Select v-model:value="deviceAttr.photoHorizontalAlignment" style="width: 100%">
                  <Select.Option value="LEFT">좌측</Select.Option>
                  <Select.Option value="CENTER">중앙</Select.Option>
                  <Select.Option value="RIGHT">우측</Select.Option>
                </Select>
              </Form.Item>
              <Form.Item label="고인 이름 표시">
                <Switch
                  v-model:checked="deviceAttr.isDeceasedNameVisible"
                  checked-children="표시"
                  un-checked-children="숨김"
                />
              </Form.Item>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="유족 연락처 표시">
                <Switch
                  v-model:checked="deviceAttr.isFamilyContactVisible"
                  checked-children="표시"
                  un-checked-children="숨김"
                />
              </Form.Item>
              <Form.Item label="영정사진 비율 유지">
                <Switch
                  v-model:checked="deviceAttr.isMemorialPhotoKeepAspectRatio"
                  checked-children="유지"
                  un-checked-children="늘림"
                />
              </Form.Item>
            </div>
            <div class="grid grid-cols-2 gap-3 mt-2">
              <Form.Item label="영정사진 여백 (위)">
                <div class="flex items-center gap-3">
                  <Slider v-model:value="deviceAttr.memorialPaddingTop" :min="0" :max="40" class="flex-1" />
                  <InputNumber v-model:value="deviceAttr.memorialPaddingTop" :min="0" :max="40" addon-after="%" style="width: 85px" />
                </div>
              </Form.Item>
              <Form.Item label="영정사진 여백 (아래)">
                <div class="flex items-center gap-3">
                  <Slider v-model:value="deviceAttr.memorialPaddingBottom" :min="0" :max="40" class="flex-1" />
                  <InputNumber v-model:value="deviceAttr.memorialPaddingBottom" :min="0" :max="40" addon-after="%" style="width: 85px" />
                </div>
              </Form.Item>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="영정사진 여백 (좌)">
                <div class="flex items-center gap-3">
                  <Slider v-model:value="deviceAttr.memorialPaddingLeft" :min="0" :max="40" class="flex-1" />
                  <InputNumber v-model:value="deviceAttr.memorialPaddingLeft" :min="0" :max="40" addon-after="%" style="width: 85px" />
                </div>
              </Form.Item>
              <Form.Item label="영정사진 여백 (우)">
                <div class="flex items-center gap-3">
                  <Slider v-model:value="deviceAttr.memorialPaddingRight" :min="0" :max="40" class="flex-1" />
                  <InputNumber v-model:value="deviceAttr.memorialPaddingRight" :min="0" :max="40" addon-after="%" style="width: 85px" />
                </div>
              </Form.Item>
            </div>
          </template>
        </div>

        <Divider />

        <!-- ③ 멀티미디어 콘텐츠 설정 -->
        <div class="attr-section">
          <div class="attr-section-title">
            <IconifyIcon icon="lucide:play-circle" class="size-4" />
            <span>사진 · 영상 · 음악</span>
            <span class="attr-device-tag">멀티미디어 DID</span>
          </div>
          <div class="grid grid-cols-3 gap-3">
            <Form.Item label="동영상 재생">
              <Switch
                v-model:checked="deviceAttr.isVideoEnabled"
                checked-children="사용"
                un-checked-children="사용안함"
              />
            </Form.Item>
            <Form.Item label="음악 재생">
              <Switch
                v-model:checked="deviceAttr.isMusicEnabled"
                checked-children="사용"
                un-checked-children="사용안함"
              />
            </Form.Item>
            <Form.Item label="배경 이미지 사용">
              <Switch
                v-model:checked="deviceAttr.isBackgroundImageEnabled"
                checked-children="사용"
                un-checked-children="사용안함"
              />
            </Form.Item>
          </div>
          <div
            v-if="deviceAttr.isVideoEnabled || deviceAttr.isMusicEnabled || deviceAttr.isBackgroundImageEnabled"
            class="grid grid-cols-3 gap-3"
          >
            <Form.Item v-if="deviceAttr.isVideoEnabled" label="동영상 선택">
              <BizSelect
                type="video"
                v-model:value="deviceAttr.videoId"
                placeholder="동영상을 선택하세요"
                style="width: 100%"
              />
            </Form.Item>
            <Form.Item v-if="deviceAttr.isMusicEnabled" label="음악 선택">
              <BizSelect
                type="music"
                v-model:value="deviceAttr.musicId"
                placeholder="음악을 선택하세요"
                style="width: 100%"
              />
            </Form.Item>
            <Form.Item v-if="deviceAttr.isBackgroundImageEnabled" label="배경 이미지 선택">
              <BizSelect
                type="background"
                v-model:value="deviceAttr.backgroundImageId"
                placeholder="배경 이미지를 선택하세요"
                style="width: 100%"
              />
            </Form.Item>
          </div>
          <div
            v-if="deviceAttr.isVideoEnabled || deviceAttr.isBackgroundImageEnabled"
            class="grid grid-cols-2 gap-3"
          >
            <Form.Item v-if="deviceAttr.isVideoEnabled" label="영상 표현">
              <Select v-model:value="deviceAttr.videoOrientation" style="width: 100%">
                <Select.Option value="HORIZONTAL">가로형 모습 (0도)</Select.Option>
                <Select.Option value="VERTICAL_LEFT">세로형 모습 (좌로 90도)</Select.Option>
                <Select.Option value="VERTICAL_RIGHT">세로형 모습 (우로 90도)</Select.Option>
                <Select.Option value="INVERTED">가로형 모습 (180도 회전)</Select.Option>
              </Select>
            </Form.Item>
            <Form.Item v-if="deviceAttr.isBackgroundImageEnabled" label="배경 이미지 표현">
              <Select v-model:value="deviceAttr.backgroundOrientation" style="width: 100%">
                <Select.Option value="HORIZONTAL">가로형 모습 (0도)</Select.Option>
                <Select.Option value="VERTICAL_LEFT">세로형 모습 (좌로 90도)</Select.Option>
                <Select.Option value="VERTICAL_RIGHT">세로형 모습 (우로 90도)</Select.Option>
                <Select.Option value="INVERTED">가로형 모습 (180도 회전)</Select.Option>
              </Select>
            </Form.Item>
          </div>
          <template v-if="deviceAttr.isMusicEnabled">
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="음소거">
                <Switch
                  v-model:checked="deviceAttr.isMuted"
                  checked-children="음소거"
                  un-checked-children="소리 켜짐"
                />
              </Form.Item>
              <Form.Item label="음악 볼륨 (별도 설정)">
                <InputNumber
                  v-model:value="deviceAttr.musicVolume"
                  :min="0"
                  :max="100"
                  :disabled="deviceAttr.isMuted"
                  placeholder="장비 기본값"
                  style="width: 100%"
                  addon-after="%"
                />
              </Form.Item>
            </div>
          </template>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="반복 재생">
              <Switch
                v-model:checked="deviceAttr.isMediaLoop"
                checked-children="반복"
                un-checked-children="1회"
              />
            </Form.Item>
          </div>
        </div>

        <Divider />

        <!-- ④ 층별 안내 설정 -->
        <div class="attr-section">
          <div class="attr-section-title">
            <IconifyIcon icon="lucide:layout-list" class="size-4" />
            <span>층별 안내판</span>
            <span class="attr-device-tag">호실 안내 (ROOM_GUIDE)</span>
          </div>
          <Form.Item label="층별 안내판 표시">
            <Switch
              v-model:checked="deviceAttr.isFloorGuideEnabled"
              checked-children="사용"
              un-checked-children="사용안함"
            />
          </Form.Item>
          <template v-if="deviceAttr.isFloorGuideEnabled">
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="빈소 배정 현황 표시">
                <Switch
                  v-model:checked="deviceAttr.isRoomAssignmentVisible"
                  checked-children="표시"
                  un-checked-children="숨김"
                />
              </Form.Item>
              <Form.Item label="진행 중 빈소만 표시">
                <Switch
                  v-model:checked="deviceAttr.isActiveRoomsOnly"
                  checked-children="진행 중만"
                  un-checked-children="전체"
                />
              </Form.Item>
            </div>
            <Form.Item label="안내 새로고침 간격(초)">
              <InputNumber
                v-model:value="deviceAttr.floorGuideRefreshSec"
                :min="10"
                :max="600"
                style="width: 100%"
                addon-after="초"
              />
            </Form.Item>
          </template>
        </div>

        <Divider />

        <!-- ⑤ 입구 정보/키오스크 설정 -->
        <div class="attr-section">
          <div class="attr-section-title">
            <IconifyIcon icon="lucide:door-open" class="size-4" />
            <span>입구 정보 · 키오스크</span>
            <span class="attr-device-tag">키오스크 (KIOSK)</span>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="터치 인터랙션">
              <Switch
                v-model:checked="deviceAttr.isTouchEnabled"
                checked-children="사용"
                un-checked-children="사용안함"
              />
            </Form.Item>
            <Form.Item label="QR코드 표시">
              <Switch
                v-model:checked="deviceAttr.isQrCodeVisible"
                checked-children="표시"
                un-checked-children="숨김"
              />
            </Form.Item>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <Form.Item label="건물 안내도 표시">
              <Switch
                v-model:checked="deviceAttr.isBuildingMapVisible"
                checked-children="표시"
                un-checked-children="숨김"
              />
            </Form.Item>
            <Form.Item label="공지사항 표시">
              <Switch
                v-model:checked="deviceAttr.isNoticeVisible"
                checked-children="표시"
                un-checked-children="숨김"
              />
            </Form.Item>
          </div>
          <template v-if="deviceAttr.isNoticeVisible">
            <Form.Item label="공지 스크롤 속도 (1=느림, 5=빠름)">
              <Slider
                v-model:value="deviceAttr.noticeScrollSpeed"
                :min="1"
                :max="5"
                :marks="{ 1: '느림', 3: '보통', 5: '빠름' }"
              />
            </Form.Item>
          </template>
          <Form.Item label="입구 인사말 메시지">
            <Input.TextArea
              v-model:value="deviceAttr.entranceGreeting"
              :rows="2"
              :maxlength="200"
              show-count
              placeholder="예: 삼가 고인의 명복을 빕니다."
            />
          </Form.Item>
        </div>

        <Divider />

        <!-- ⑥ 비고 -->
        <div class="attr-section">
          <div class="attr-section-title">
            <IconifyIcon icon="lucide:file-text" class="size-4" />
            <span>비고</span>
          </div>
          <Form.Item>
            <Input.TextArea
              v-model:value="deviceAttr.remark"
              :rows="2"
              :maxlength="500"
              show-count
              placeholder="기타 메모 사항을 입력하세요."
            />
          </Form.Item>
        </div>

      </Form>
    </div>

    <!-- 저장 버튼 -->
    <div
      v-if="deviceAttr && !attrLoading"
      class="flex shrink-0 justify-end gap-2 border-t border-border bg-muted/40 px-4 py-2"
    >
      <Button @click="emit('reset')">초기화</Button>
      <Button type="primary" :loading="attrSaving" @click="emit('save')">
        속성 저장
      </Button>
    </div>

  </div>
</template>

<style scoped>
.attr-section {
  margin-bottom: 4px;
}

.attr-section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.8125rem;
  font-weight: 600;
  color: hsl(var(--foreground));
  margin-bottom: 12px;
}

.attr-device-tag {
  margin-left: auto;
  font-size: 0.7rem;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 9999px;
  background: hsl(var(--primary) / 0.1);
  color: hsl(var(--primary));
  white-space: nowrap;
}
</style>
