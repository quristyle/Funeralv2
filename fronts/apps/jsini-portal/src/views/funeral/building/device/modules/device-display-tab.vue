<script lang="ts" setup>
import { computed, ref, watch } from 'vue';
import { Alert, Button, Collapse, Form, Spin, Tooltip } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import type { BuildingApi } from '#/api/funeral/building';
import { sectionsForDevice } from '../constants/attribute-sections';
import { getDeviceTypeInfo } from '../constants/device-type';
import { defaultAttr } from '../composables/use-device-attribute';
import DeviceAttributeFields from './device-attribute-fields.vue';
import DeviceDisplayPreview from './device-display-preview.vue';
import DeviceRibbonTab from './device-ribbon-tab.vue';

// ---------------------------------------------------------------------------
// [화면 표시] 탭 — 49번 문서 D-DV2~D-DV5
//
// 속성을 저장하면 곧바로 운영 중인 장비 화면이 다시 그려진다
// (`DeviceAttributeService` 가 `DeviceChanged` 를 방송한다). 그래서 이 탭만
// **초안 방식**이다 — 고친 것은 여기 쌓이고 [장비에 적용] 을 눌러야 나간다.
// 맞추는 동안은 왼쪽 미리보기가 대신 받아 준다.
//
// 볼륨 · 밝기처럼 귀와 눈으로 확인하며 맞추는 값은 [하드웨어] 탭에 두고
// 즉시 반영을 그대로 뒀다 (D-DV5).
// ---------------------------------------------------------------------------

const props = defineProps<{
  /** 서버가 준 현재 값 — '장비에 들어 있는 것'의 기준이다. */
  attr: BuildingApi.DeviceAttribute | null;
  attrLoading: boolean;
  attrSaving: boolean;
  deviceId: string;
  deviceType?: string;
  /** 이 장비가 있는 호실에 배정된 고인. 있으면 조문 중이라 적용 전에 알린다. */
  deceasedName?: string;
}>();

const emit = defineEmits<{
  (e: 'apply', draft: BuildingApi.DeviceAttribute): void;
}>();

/** 편집 중인 초안. 화면의 모든 칸과 미리보기가 이것을 본다. */
const draft = ref<BuildingApi.DeviceAttribute | null>(null);
/** 서버 값의 스냅숏 — 무엇이 바뀌었는지 세는 기준. */
const baseline = ref<BuildingApi.DeviceAttribute | null>(null);
const activeKeys = ref<string[]>([]);
/** 편집기로 들어간 섹션(리본). null 이면 두 단 화면. */
const drillIn = ref<null | string>(null);

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}

watch(
  () => props.attr,
  (next) => {
    draft.value = next ? clone(next) : null;
    baseline.value = next ? clone(next) : null;
  },
  { immediate: true, deep: true },
);

const sections = computed(() => sectionsForDevice(props.deviceType));
const deviceTypeLabel = computed(() => getDeviceTypeInfo(props.deviceType).label);

// 장비가 바뀌면 그 유형에 해당하는 섹션만 펼친 상태로 다시 연다.
watch(
  () => `${props.deviceId}|${props.deviceType}`,
  () => {
    activeKeys.value = sections.value.relevant
      .filter((s) => !s.drillIn && s.key !== 'remark')
      .map((s) => s.key);
    drillIn.value = null;
  },
  { immediate: true },
);

/** 초안과 기준이 다른 칸 이름들. */
const changedFields = computed<string[]>(() => {
  const a = draft.value;
  const b = baseline.value;
  if (!a || !b) return [];
  return Object.keys(a).filter(
    (key) => key !== 'id' && JSON.stringify((a as any)[key]) !== JSON.stringify((b as any)[key]),
  );
});

const isDirty = computed(() => changedFields.value.length > 0);

/** 섹션 머리에 붙일 「N 변경」. */
function changedInSection(fields?: string[]) {
  if (!fields) return 0;
  return fields.filter((f) => changedFields.value.includes(f)).length;
}

function revert() {
  if (baseline.value) draft.value = clone(baseline.value);
}

/** 기본값을 초안에 얹는다. 곧바로 나가지 않으므로 [적용] 전에 되돌릴 수 있다. */
function loadDefaults() {
  if (!draft.value) return;
  draft.value = { id: draft.value.id, ...defaultAttr(props.deviceId) };
}

function apply() {
  if (draft.value && isDirty.value) emit('apply', clone(draft.value));
}
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- 로딩 -->
    <div v-if="attrLoading" class="flex flex-1 items-center justify-center py-16">
      <Spin tip="화면 표시 설정 불러오는 중..." />
    </div>

    <!-- ── 장식 · 문구 편집기 (들어간 상태) ─────────────────────── -->
    <template v-else-if="drillIn === 'ribbon'">
      <div class="flex shrink-0 items-center gap-2 border-b border-border px-3 py-1.5">
        <Button type="text" size="small" @click="drillIn = null">
          <IconifyIcon icon="lucide:arrow-left" class="size-4" />
        </Button>
        <span class="text-sm font-medium">장식 · 문구</span>
        <span class="text-xs text-muted-foreground">
          이 편집기는 [모두 저장] 을 누르는 즉시 장비에 반영됩니다.
        </span>
      </div>
      <DeviceRibbonTab :device-id="deviceId" :display-orientation="draft?.displayOrientation" />
    </template>

    <!-- ── 미리보기 + 속성 ──────────────────────────────────────── -->
    <template v-else-if="draft">
      <div class="flex min-h-0 flex-1">
        <!-- 왼쪽: 미리보기 (고정) -->
        <aside class="flex w-[290px] shrink-0 flex-col gap-2 overflow-auto border-r border-border p-3">
          <DeviceDisplayPreview
            :attr="draft"
            :device-type="deviceType"
            :deceased-name="deceasedName"
          />
          <div
            v-if="isDirty"
            class="flex items-start gap-1.5 rounded-md bg-amber-500/10 px-2 py-1.5 text-xs text-amber-600 dark:text-amber-400"
          >
            <IconifyIcon icon="lucide:eye" class="mt-0.5 size-3.5 shrink-0" />
            <span>미리보기입니다. 아직 장비에는 나가지 않았습니다.</span>
          </div>
          <div v-else class="flex items-start gap-1.5 px-2 text-xs text-muted-foreground">
            <IconifyIcon icon="lucide:check" class="mt-0.5 size-3.5 shrink-0" />
            <span>장비에 들어 있는 값과 같습니다.</span>
          </div>
        </aside>

        <!-- 오른쪽: 섹션 -->
        <div class="min-w-0 flex-1 overflow-auto p-3">
          <Alert
            v-if="!draft.id"
            type="info"
            show-icon
            class="mb-3"
            message="아직 저장된 화면 표시 설정이 없습니다."
            description="항목을 맞춘 뒤 [장비에 적용] 을 누르면 등록됩니다."
          />

          <Form layout="vertical" size="small">
            <!-- 이 장비 유형에 해당하는 섹션 -->
            <Collapse v-model:activeKey="activeKeys" ghost>
              <template v-for="section in sections.relevant" :key="section.key">
                <!-- 편집기로 들어가는 섹션은 접이식이 아니라 줄 하나다 -->
                <div
                  v-if="section.drillIn"
                  class="mb-1 flex cursor-pointer items-center gap-2 rounded-md border border-border px-3 py-2 hover:bg-muted/50"
                  @click="drillIn = section.key"
                >
                  <IconifyIcon :icon="section.icon" class="size-4 text-muted-foreground" />
                  <span class="text-sm">{{ section.label }}</span>
                  <IconifyIcon icon="lucide:chevron-right" class="ml-auto size-4 text-muted-foreground" />
                </div>

                <Collapse.Panel v-else :key="section.key">
                  <template #header>
                    <span class="flex items-center gap-2">
                      <IconifyIcon :icon="section.icon" class="size-4" />
                      <span class="text-sm">{{ section.label }}</span>
                      <span
                        v-if="changedInSection(section.fields)"
                        class="rounded-full bg-amber-500/15 px-2 py-0.5 text-xs text-amber-600 dark:text-amber-400"
                      >
                        {{ changedInSection(section.fields) }} 변경
                      </span>
                    </span>
                  </template>
                  <DeviceAttributeFields :section-key="section.key" :draft="draft" />
                </Collapse.Panel>
              </template>
            </Collapse>

            <!--
              이 장비 유형과 무관한 섹션.
              숨기지 않고 접어서 내려 둔다 — 유형을 잘못 넣은 장비를 고칠 길이 필요하다.
            -->
            <template v-if="sections.others.length">
              <div class="mb-1 mt-4 flex items-center gap-2 px-1">
                <span class="text-xs text-muted-foreground">이 장비 유형과 무관</span>
                <div class="h-px flex-1 bg-border"></div>
              </div>
              <Collapse v-model:activeKey="activeKeys" ghost>
                <Collapse.Panel v-for="section in sections.others" :key="section.key">
                  <template #header>
                    <span class="flex items-center gap-2 text-muted-foreground">
                      <IconifyIcon :icon="section.icon" class="size-4" />
                      <span class="text-sm">{{ section.label }}</span>
                      <span
                        v-if="changedInSection(section.fields)"
                        class="rounded-full bg-amber-500/15 px-2 py-0.5 text-xs text-amber-600 dark:text-amber-400"
                      >
                        {{ changedInSection(section.fields) }} 변경
                      </span>
                    </span>
                  </template>
                  <div class="mb-2 text-xs text-muted-foreground">
                    {{ deviceTypeLabel }} 장비에는 쓰이지 않습니다. 유형을 잘못 지정한 경우에만 손대세요.
                  </div>
                  <DeviceAttributeFields
                    v-if="!section.drillIn"
                    :section-key="section.key"
                    :draft="draft"
                  />
                  <Button v-else size="small" @click="drillIn = section.key">
                    편집기 열기
                  </Button>
                </Collapse.Panel>
              </Collapse>
            </template>
          </Form>
        </div>
      </div>

      <!-- 적용 바 -->
      <div class="flex shrink-0 items-center gap-2 border-t border-border bg-muted/40 px-4 py-2">
        <template v-if="isDirty">
          <span class="text-xs text-muted-foreground">
            바뀐 항목 <span class="font-medium text-foreground">{{ changedFields.length }}</span>
          </span>
          <Button size="small" @click="revert">되돌리기</Button>
        </template>
        <span v-else class="text-xs text-muted-foreground">바뀐 항목 없음</span>

        <Tooltip
          v-if="deceasedName"
          title="이 장비가 있는 호실은 지금 조문 중입니다. 적용하면 화면이 곧바로 다시 그려집니다."
        >
          <span class="ml-2 flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400">
            <IconifyIcon icon="lucide:alert-triangle" class="size-3.5" />
            조문 중 (고 {{ deceasedName }})
          </span>
        </Tooltip>

        <div class="ml-auto flex items-center gap-2">
          <Button size="small" @click="loadDefaults">기본값</Button>
          <Button
            v-perm:update
            type="primary"
            :loading="attrSaving"
            :disabled="!isDirty"
            @click="apply"
          >
            장비에 적용
          </Button>
        </div>
      </div>
    </template>
  </div>
</template>
