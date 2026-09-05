<script lang="ts" setup>
import { computed } from 'vue';
import { Form, Input, InputNumber, Select, Slider, Switch } from 'ant-design-vue';
import BizSelect from '#/components/BizSelect.vue';
import type { BuildingApi } from '#/api/funeral/building';

/**
 * 속성 섹션 하나의 입력 칸들. `device-display-tab.vue` 가 섹션 키로 골라 쓴다.
 *
 * 유형에 맞는 섹션과 무관한 섹션을 같은 마크업으로 그려야 해서 따로 뺐다
 * (49번 문서 D-DV3 — 무관한 섹션도 숨기지 않고 접어서 보여 준다).
 *
 * `draft` 는 부모의 객체를 그대로 받아 안쪽 칸을 고친다. 부모가 이미 서버 값의
 * 복제본을 들고 있으므로, 여기서 고쳐도 [적용] 전까지는 장비로 나가지 않는다.
 */
const props = defineProps<{
  sectionKey: string;
  draft: BuildingApi.DeviceAttribute;
}>();

// ant 의 입력 칸은 null 을 받지 않지만 이 셋은 DB 에서 null 이 「설정 안 함」이다.
// (음악 볼륨 null = 장비 기본값을 따른다.) 가운데서 바꿔 준다 — 빈 값은 null 로
// 되돌려 보내야 기본값 동작이 유지된다.
const musicVolume = computed<number | undefined>({
  get: () => props.draft.musicVolume ?? undefined,
  set: (v) => {
    props.draft.musicVolume = v ?? null;
  },
});

const entranceGreeting = computed<string>({
  get: () => props.draft.entranceGreeting ?? '',
  set: (v) => {
    props.draft.entranceGreeting = v || null;
  },
});

const remark = computed<string>({
  get: () => props.draft.remark ?? '',
  set: (v) => {
    props.draft.remark = v || null;
  },
});
</script>

<template>
  <!-- ① 화면 배치 -->
  <template v-if="sectionKey === 'layout'">
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="화면 방향">
        <Select v-model:value="draft.displayOrientation" style="width: 100%">
          <Select.Option value="LANDSCAPE">가로 (Landscape)</Select.Option>
          <Select.Option value="PORTRAIT">세로 (Portrait)</Select.Option>
        </Select>
      </Form.Item>
      <Form.Item label="화면 표현">
        <Select v-model:value="draft.portraitOrientation" style="width: 100%">
          <Select.Option value="HORIZONTAL">가로형 모습</Select.Option>
          <Select.Option value="VERTICAL_LEFT">좌90도 세로형 모습</Select.Option>
          <Select.Option value="VERTICAL_RIGHT">우90도 세로형 모습</Select.Option>
          <Select.Option value="INVERTED">뒤집기 모습</Select.Option>
        </Select>
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="화면 여백 (위)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="draft.displayPaddingTop" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="draft.displayPaddingTop" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
      <Form.Item label="화면 여백 (아래)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="draft.displayPaddingBottom" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="draft.displayPaddingBottom" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
      <Form.Item label="화면 여백 (좌)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="draft.displayPaddingLeft" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="draft.displayPaddingLeft" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
      <Form.Item label="화면 여백 (우)">
        <div class="flex items-center gap-3">
          <Slider v-model:value="draft.displayPaddingRight" :min="0" :max="40" class="flex-1" />
          <InputNumber v-model:value="draft.displayPaddingRight" :min="0" :max="40" addon-after="%" style="width: 85px" />
        </div>
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="콘텐츠 전환 간격">
        <InputNumber v-model:value="draft.contentIntervalSec" :min="3" :max="300" style="width: 100%" addon-after="초" />
      </Form.Item>
      <Form.Item label="대기 화면(스크린세이버)">
        <Switch v-model:checked="draft.isScreensaverEnabled" checked-children="사용" un-checked-children="사용안함" />
      </Form.Item>
      <Form.Item v-if="draft.isScreensaverEnabled" label="대기 전환 시간">
        <InputNumber v-model:value="draft.screensaverTimeoutSec" :min="30" :max="3600" style="width: 100%" addon-after="초" />
      </Form.Item>
    </div>
  </template>

  <!-- ② 영정사진 · 추모 -->
  <template v-else-if="sectionKey === 'memorial'">
    <Form.Item label="영정사진 표시">
      <Switch v-model:checked="draft.isMemorialPhotoEnabled" checked-children="사용" un-checked-children="사용안함" />
    </Form.Item>
    <template v-if="draft.isMemorialPhotoEnabled">
      <div class="grid grid-cols-2 gap-3">
        <Form.Item label="사진 전환 효과">
          <Select v-model:value="draft.memorialPhotoEffect" style="width: 100%">
            <Select.Option value="FADE">페이드 (Fade)</Select.Option>
            <Select.Option value="SLIDE">슬라이드 (Slide)</Select.Option>
            <Select.Option value="NONE">효과 없음</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="영정사진 비율 유지">
          <Switch v-model:checked="draft.isMemorialPhotoKeepAspectRatio" checked-children="유지" un-checked-children="늘림" />
        </Form.Item>
        <Form.Item label="사진 세로 정렬">
          <Select v-model:value="draft.photoVerticalAlignment" style="width: 100%">
            <Select.Option value="TOP">상단</Select.Option>
            <Select.Option value="CENTER">중앙</Select.Option>
            <Select.Option value="BOTTOM">하단</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="사진 가로 정렬">
          <Select v-model:value="draft.photoHorizontalAlignment" style="width: 100%">
            <Select.Option value="LEFT">좌측</Select.Option>
            <Select.Option value="CENTER">중앙</Select.Option>
            <Select.Option value="RIGHT">우측</Select.Option>
          </Select>
        </Form.Item>
        <Form.Item label="고인 이름 표시">
          <Switch v-model:checked="draft.isDeceasedNameVisible" checked-children="표시" un-checked-children="숨김" />
        </Form.Item>
        <Form.Item label="유족 연락처 표시">
          <Switch v-model:checked="draft.isFamilyContactVisible" checked-children="표시" un-checked-children="숨김" />
        </Form.Item>
      </div>
      <div class="grid grid-cols-2 gap-3">
        <Form.Item label="사진 여백 (위)">
          <div class="flex items-center gap-3">
            <Slider v-model:value="draft.memorialPaddingTop" :min="0" :max="40" class="flex-1" />
            <InputNumber v-model:value="draft.memorialPaddingTop" :min="0" :max="40" addon-after="%" style="width: 85px" />
          </div>
        </Form.Item>
        <Form.Item label="사진 여백 (아래)">
          <div class="flex items-center gap-3">
            <Slider v-model:value="draft.memorialPaddingBottom" :min="0" :max="40" class="flex-1" />
            <InputNumber v-model:value="draft.memorialPaddingBottom" :min="0" :max="40" addon-after="%" style="width: 85px" />
          </div>
        </Form.Item>
        <Form.Item label="사진 여백 (좌)">
          <div class="flex items-center gap-3">
            <Slider v-model:value="draft.memorialPaddingLeft" :min="0" :max="40" class="flex-1" />
            <InputNumber v-model:value="draft.memorialPaddingLeft" :min="0" :max="40" addon-after="%" style="width: 85px" />
          </div>
        </Form.Item>
        <Form.Item label="사진 여백 (우)">
          <div class="flex items-center gap-3">
            <Slider v-model:value="draft.memorialPaddingRight" :min="0" :max="40" class="flex-1" />
            <InputNumber v-model:value="draft.memorialPaddingRight" :min="0" :max="40" addon-after="%" style="width: 85px" />
          </div>
        </Form.Item>
      </div>
    </template>
  </template>

  <!-- ③ 사진 · 영상 · 음악 -->
  <template v-else-if="sectionKey === 'media'">
    <div class="grid grid-cols-3 gap-3">
      <Form.Item label="동영상 재생">
        <Switch v-model:checked="draft.isVideoEnabled" checked-children="사용" un-checked-children="사용안함" />
      </Form.Item>
      <Form.Item label="음악 재생">
        <Switch v-model:checked="draft.isMusicEnabled" checked-children="사용" un-checked-children="사용안함" />
      </Form.Item>
      <Form.Item label="배경 이미지 사용">
        <Switch v-model:checked="draft.isBackgroundImageEnabled" checked-children="사용" un-checked-children="사용안함" />
      </Form.Item>
    </div>
    <div class="grid grid-cols-2 gap-3">
      <Form.Item v-if="draft.isVideoEnabled" label="동영상 선택">
        <BizSelect type="video" v-model:value="draft.videoId" placeholder="동영상을 선택하세요" style="width: 100%" />
      </Form.Item>
      <Form.Item v-if="draft.isVideoEnabled" label="영상 표현">
        <Select v-model:value="draft.videoOrientation" style="width: 100%">
          <Select.Option value="HORIZONTAL">가로형 모습 (0도)</Select.Option>
          <Select.Option value="VERTICAL_LEFT">세로형 모습 (좌로 90도)</Select.Option>
          <Select.Option value="VERTICAL_RIGHT">세로형 모습 (우로 90도)</Select.Option>
          <Select.Option value="INVERTED">가로형 모습 (180도 회전)</Select.Option>
        </Select>
      </Form.Item>
      <Form.Item v-if="draft.isBackgroundImageEnabled" label="배경 이미지 선택">
        <BizSelect type="background" v-model:value="draft.backgroundImageId" placeholder="배경 이미지를 선택하세요" style="width: 100%" />
      </Form.Item>
      <Form.Item v-if="draft.isBackgroundImageEnabled" label="배경 이미지 표현">
        <Select v-model:value="draft.backgroundOrientation" style="width: 100%">
          <Select.Option value="HORIZONTAL">가로형 모습 (0도)</Select.Option>
          <Select.Option value="VERTICAL_LEFT">세로형 모습 (좌로 90도)</Select.Option>
          <Select.Option value="VERTICAL_RIGHT">세로형 모습 (우로 90도)</Select.Option>
          <Select.Option value="INVERTED">가로형 모습 (180도 회전)</Select.Option>
        </Select>
      </Form.Item>
      <Form.Item v-if="draft.isMusicEnabled" label="음악 선택">
        <BizSelect type="music" v-model:value="draft.musicId" placeholder="음악을 선택하세요" style="width: 100%" />
      </Form.Item>
      <Form.Item v-if="draft.isMusicEnabled" label="음소거">
        <Switch v-model:checked="draft.isMuted" checked-children="음소거" un-checked-children="소리 켜짐" />
      </Form.Item>
      <Form.Item v-if="draft.isMusicEnabled" label="음악 볼륨 (별도 설정)">
        <InputNumber
          v-model:value="musicVolume"
          :min="0"
          :max="100"
          :disabled="draft.isMuted"
          placeholder="장비 기본값"
          style="width: 100%"
          addon-after="%"
        />
      </Form.Item>
      <Form.Item label="반복 재생">
        <Switch v-model:checked="draft.isMediaLoop" checked-children="반복" un-checked-children="1회" />
      </Form.Item>
    </div>
  </template>

  <!-- ④ 층별 안내판 -->
  <template v-else-if="sectionKey === 'floorGuide'">
    <Form.Item label="층별 안내판 표시">
      <Switch v-model:checked="draft.isFloorGuideEnabled" checked-children="사용" un-checked-children="사용안함" />
    </Form.Item>
    <template v-if="draft.isFloorGuideEnabled">
      <div class="grid grid-cols-2 gap-3">
        <Form.Item label="빈소 배정 현황 표시">
          <Switch v-model:checked="draft.isRoomAssignmentVisible" checked-children="표시" un-checked-children="숨김" />
        </Form.Item>
        <Form.Item label="진행 중 빈소만 표시">
          <Switch v-model:checked="draft.isActiveRoomsOnly" checked-children="진행 중만" un-checked-children="전체" />
        </Form.Item>
      </div>
      <Form.Item label="안내 새로고침 간격">
        <InputNumber v-model:value="draft.floorGuideRefreshSec" :min="10" :max="600" style="width: 100%" addon-after="초" />
      </Form.Item>
    </template>
  </template>

  <!-- ⑤ 입구 정보 · 키오스크 -->
  <template v-else-if="sectionKey === 'kiosk'">
    <div class="grid grid-cols-2 gap-3">
      <Form.Item label="터치 인터랙션">
        <Switch v-model:checked="draft.isTouchEnabled" checked-children="사용" un-checked-children="사용안함" />
      </Form.Item>
      <Form.Item label="QR코드 표시">
        <Switch v-model:checked="draft.isQrCodeVisible" checked-children="표시" un-checked-children="숨김" />
      </Form.Item>
      <Form.Item label="건물 안내도 표시">
        <Switch v-model:checked="draft.isBuildingMapVisible" checked-children="표시" un-checked-children="숨김" />
      </Form.Item>
      <Form.Item label="공지사항 표시">
        <Switch v-model:checked="draft.isNoticeVisible" checked-children="표시" un-checked-children="숨김" />
      </Form.Item>
    </div>
    <Form.Item v-if="draft.isNoticeVisible" label="공지 스크롤 속도 (1=느림, 5=빠름)">
      <Slider v-model:value="draft.noticeScrollSpeed" :min="1" :max="5" :marks="{ 1: '느림', 3: '보통', 5: '빠름' }" />
    </Form.Item>
    <Form.Item label="입구 인사말 메시지">
      <Input.TextArea
        v-model:value="entranceGreeting"
        :rows="2"
        :maxlength="200"
        show-count
        placeholder="예: 삼가 고인의 명복을 빕니다."
      />
    </Form.Item>
  </template>

  <!-- ⑥ 비고 -->
  <template v-else-if="sectionKey === 'remark'">
    <Form.Item>
      <Input.TextArea
        v-model:value="remark"
        :rows="2"
        :maxlength="500"
        show-count
        placeholder="기타 메모 사항을 입력하세요."
      />
    </Form.Item>
  </template>
</template>
