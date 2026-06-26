<script lang="ts" setup>
import {
  Button, Divider, Form, Input, InputNumber, Select, Slider, Spin, Switch,
} from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
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
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="attrLoading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="장비 속성 불러오는 중..." />
    </div>

    <!-- 속성 폼 -->
    <div v-else-if="deviceAttr" class="flex-1 overflow-auto px-4 py-3">
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
            </div>
            <div class="grid grid-cols-2 gap-3">
              <Form.Item label="고인 이름 표시">
                <Switch
                  v-model:checked="deviceAttr.isDeceasedNameVisible"
                  checked-children="표시"
                  un-checked-children="숨김"
                />
              </Form.Item>
              <Form.Item label="유족 연락처 표시">
                <Switch
                  v-model:checked="deviceAttr.isFamilyContactVisible"
                  checked-children="표시"
                  un-checked-children="숨김"
                />
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
          <div class="grid grid-cols-2 gap-3">
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
