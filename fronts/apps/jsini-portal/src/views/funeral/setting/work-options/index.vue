<script lang="ts" setup>
/**
 * 장례식장 업무 설정 — 옛 `page/ui_config.jsp` 의 오른쪽 표.
 *
 * 옛 화면은 코드 여덟 개를 체크박스로 켜고 껐다. 그중 넷(탭 숨기기 · 사이드바 접기 ·
 * 하위 메뉴 펼치기 · 사이드바 자동 닫힘)은 vben 개인 환경설정이 이미 하는 일이라
 * 옮기지 않았다 — 같은 것을 두 군데서 켜면 서로 어긋난다.
 * 남은 넷이 장례식장 업무 규칙이다. 어떤 것이 있는지는 백엔드의 SettingCatalog 가 정본이다.
 *
 * **`/setting/environment` 와 다른 화면이다.** 그쪽은 vben 개인 환경설정
 * (테마 · 사이드바 · 탭)이고 여기는 업무 규칙이다. 처음 이식할 때 그 자리를
 * 덮어썼다가 되돌리고 이리로 옮겼다.
 */
import { computed, onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Card, Skeleton, Switch, message } from 'ant-design-vue';
import type { SettingApi } from '#/api/funeral/setting';
import { getEnvironmentSettings, updateEnvironmentSettings } from '#/api/funeral/setting';

const loading = ref(true);
const saving = ref(false);
const settings = ref<SettingApi.EnvironmentSetting[]>([]);

/** 화면에서 바꾼 값. 저장 버튼을 누를 때까지 서버에 보내지 않는다. */
const draft = ref<Record<string, boolean>>({});

/** 묶음별로 나눠 그린다 (빈소 운영 · 장비 · 화면) */
const groups = computed(() => {
  const map = new Map<string, SettingApi.EnvironmentSetting[]>();
  for (const s of settings.value) {
    const list = map.get(s.groupName) ?? [];
    list.push(s);
    map.set(s.groupName, list);
  }
  return [...map.entries()].map(([name, items]) => ({ name, items }));
});

const dirty = computed(() =>
  settings.value.some((s) => draft.value[s.code] !== s.enabled),
);

async function load() {
  loading.value = true;
  try {
    const list = (await getEnvironmentSettings()) || [];
    settings.value = list;
    draft.value = Object.fromEntries(list.map((s) => [s.code, s.enabled]));
  } catch {
    message.error('업무 설정을 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

async function handleSave() {
  saving.value = true;
  try {
    // 바뀐 것만 보낸다.
    const changed: Record<string, boolean> = {};
    for (const s of settings.value) {
      if (draft.value[s.code] !== s.enabled) {
        changed[s.code] = draft.value[s.code]!;
      }
    }

    if (Object.keys(changed).length === 0) {
      message.info('바뀐 설정이 없습니다.');
      return;
    }

    const list = (await updateEnvironmentSettings(changed)) || [];
    settings.value = list;
    draft.value = Object.fromEntries(list.map((s) => [s.code, s.enabled]));
    message.success('업무 설정을 저장했습니다.');
  } catch {
    message.error('저장에 실패했습니다.');
  } finally {
    saving.value = false;
  }
}

function handleReset() {
  draft.value = Object.fromEntries(settings.value.map((s) => [s.code, s.defaultValue]));
}

onMounted(load);
</script>

<template>
  <Page auto-content-height>
    <div class="mx-auto flex h-full w-full max-w-3xl flex-col gap-4">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-lg font-bold">장례식장 업무 설정</h1>
          <p class="text-xs text-muted-foreground">
            이 계정에만 적용되는 장례식장 업무 규칙이다. 화면 테마·사이드바 같은 개인 설정은
            <b>설정 &gt; 환경설정</b> 또는 오른쪽 위 톱니에 있다.
          </p>
        </div>
        <div class="flex gap-2">
          <Button :disabled="saving" @click="handleReset">기본값으로</Button>
          <Button type="primary" :loading="saving" :disabled="!dirty" @click="handleSave">
            저장
          </Button>
        </div>
      </div>

      <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />

      <div v-else class="flex flex-col gap-4 overflow-y-auto pb-2">
        <Card v-for="g in groups" :key="g.name" :title="g.name" size="small">
          <ul class="divide-y">
            <li
              v-for="item in g.items"
              :key="item.code"
              class="flex items-start justify-between gap-4 py-3 first:pt-0 last:pb-0"
            >
              <div class="min-w-0">
                <div class="text-sm font-medium">{{ item.name }}</div>
                <p v-if="item.description" class="mt-0.5 text-xs text-muted-foreground">
                  {{ item.description }}
                </p>
                <p class="mt-0.5 font-mono text-[10px] text-muted-foreground/70">
                  {{ item.code }}
                </p>
              </div>
              <Switch v-model:checked="draft[item.code]" />
            </li>
          </ul>
        </Card>

        <p v-if="groups.length === 0" class="text-sm text-muted-foreground">
          다룰 수 있는 설정이 없습니다.
        </p>
      </div>
    </div>
  </Page>
</template>
