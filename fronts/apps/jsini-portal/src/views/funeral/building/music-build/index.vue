<script lang="ts" setup>
/**
 * 건물별 음원 배정 — 옛 `page/rsrc/music_build.jsp`.
 *
 * 음원 목록은 모든 건물이 함께 쓰지만 실제로 트는 것은 건물마다 다르다.
 * 옛 화면은 위에 음원 표, 아래에 건물 표를 두고 건물 줄의 `mapping` 체크박스로
 * 연결을 켰다. 그 구조를 그대로 옮긴다 — 왼쪽에서 음원을 고르면 오른쪽에 건물이 뜬다.
 */
import { onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Checkbox, Empty, Spin, Tag, message } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import ImagePreview from '#/components/ImagePreview.vue';
import { getMediaSources } from '#/api/funeral/building';
import type { MusicBuildApi } from '#/api/funeral/music-build';
import { getBuildingsForMusic, saveBuildingsForMusic } from '#/api/funeral/music-build';

/** 지금 고른 음원 */
const selected = ref<any | null>(null);

const buildings = ref<MusicBuildApi.BuildingMapping[]>([]);
const checked = ref<Record<string, boolean>>({});
const loading = ref(false);
const saving = ref(false);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'thumbnail', title: '커버', width: 70, slots: { default: 'thumbnail' } },
      { field: 'name', title: '음원명', minWidth: 180 },
      { field: 'shortName', title: '짧은 명칭', width: 120 },
      { field: 'sortOrder', title: '순서', width: 70, align: 'right' },
      { field: 'action', title: '', width: 90, fixed: 'right', slots: { default: 'action' } },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => await getMediaSources('AUDIO'),
      },
    },
  },
});

async function selectMusic(row: any) {
  selected.value = row;
  loading.value = true;
  try {
    const list = (await getBuildingsForMusic(row.id)) || [];
    buildings.value = list;
    checked.value = Object.fromEntries(list.map((b) => [b.buildingId, b.mapped]));
  } catch {
    message.error('건물 목록을 불러오지 못했습니다.');
    buildings.value = [];
  } finally {
    loading.value = false;
  }
}

async function handleSave() {
  if (!selected.value) return;

  saving.value = true;
  try {
    const ids = buildings.value
      .filter((b) => checked.value[b.buildingId])
      .map((b) => b.buildingId);

    const list = (await saveBuildingsForMusic(selected.value.id, ids)) || [];
    buildings.value = list;
    checked.value = Object.fromEntries(list.map((b) => [b.buildingId, b.mapped]));
    message.success('배정을 저장했습니다.');
  } catch {
    message.error('저장에 실패했습니다.');
  } finally {
    saving.value = false;
  }
}

function toggleAll(on: boolean) {
  checked.value = Object.fromEntries(buildings.value.map((b) => [b.buildingId, on]));
}

onMounted(() => {
  // 첫 음원을 자동으로 골라 준다 — 빈 화면부터 보이면 무엇을 해야 할지 모른다.
  setTimeout(async () => {
    const rows = gridApi.grid?.getData?.() ?? [];
    if (rows.length > 0) await selectMusic(rows[0]);
  }, 300);
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full gap-3">
      <!-- 왼쪽: 음원 -->
      <div class="flex min-w-0 flex-1 flex-col">
        <Grid table-title="음원 목록">
          <template #thumbnail="{ row }">
            <ImagePreview
              :src="row.thumbnailFileId ? `/api/file/thumbnail/${row.thumbnailFileId}` : row.thumbnailUrl"
              :fallback-src="row.thumbnailUrl"
              :width="36"
              :height="36"
              fallback-text="🎵"
            />
          </template>

          <template #action="{ row }">
            <Button
              type="link"
              size="small"
              :class="selected?.id === row.id ? 'font-bold' : ''"
              @click="selectMusic(row)"
            >
              {{ selected?.id === row.id ? '선택됨' : '건물 배정' }}
            </Button>
          </template>
        </Grid>
      </div>

      <!-- 오른쪽: 건물 배정 -->
      <div class="flex w-[380px] shrink-0 flex-col rounded-lg border">
        <div class="flex items-center justify-between border-b px-3 py-2">
          <div class="min-w-0">
            <div class="truncate text-sm font-semibold">
              {{ selected ? selected.name : '음원을 고르세요' }}
            </div>
            <div class="text-[11px] text-muted-foreground">이 음원을 틀 건물</div>
          </div>
          <Button
            type="primary"
            size="small"
            :loading="saving"
            :disabled="!selected"
            @click="handleSave"
          >
            저장
          </Button>
        </div>

        <div v-if="selected" class="flex items-center gap-2 border-b px-3 py-1.5 text-xs">
          <Button size="small" type="link" class="px-0" @click="toggleAll(true)">전체 선택</Button>
          <span class="text-muted-foreground">·</span>
          <Button size="small" type="link" class="px-0" @click="toggleAll(false)">전체 해제</Button>
          <Tag class="ml-auto" color="blue">
            {{ buildings.filter((b) => checked[b.buildingId]).length }} / {{ buildings.length }}
          </Tag>
        </div>

        <Spin :spinning="loading" class="flex-1 overflow-y-auto">
          <ul v-if="selected" class="divide-y">
            <li
              v-for="b in buildings"
              :key="b.buildingId"
              class="flex items-start gap-2 px-3 py-2"
            >
              <Checkbox v-model:checked="checked[b.buildingId]" class="mt-0.5" />
              <div class="min-w-0">
                <div class="truncate text-sm">
                  {{ b.buildingName }}
                  <span v-if="b.buildingShortName" class="text-xs text-muted-foreground">
                    ({{ b.buildingShortName }})
                  </span>
                </div>
                <div v-if="b.address" class="truncate text-[11px] text-muted-foreground">
                  {{ b.address }}
                </div>
              </div>
            </li>
          </ul>

          <Empty
            v-else
            description="왼쪽에서 음원을 고르면 건물이 나옵니다."
            class="py-10"
          />
        </Spin>
      </div>
    </div>
  </Page>
</template>
