<script lang="ts" setup>
import type { MenuRecordRaw } from '@vben/types';

import { computed, onMounted, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { useAccessStore } from '@vben/stores';

import { Button, message, Spin } from 'ant-design-vue';

import { $tIfKey } from '#/locales';
import { useMenuFavoriteStore } from '#/store/menu-favorite';

/**
 * [고정탭(즐겨찾기 메뉴) 관리]
 *
 * 할당받은 메뉴를 '고정된 메뉴'와 '고정되지 않은 메뉴' 두 칸에 나눠 보여 준다.
 * 항목을 반대쪽 칸으로 끌어다 놓으면 고정/해제되고, 고정 칸 안에서 끌어
 * 순서를 바꾸면 사이드바 즐겨찾기 순서로 저장된다. 끌기 어려운 환경(터치)을
 * 위해 항목마다 고정/해제 단추와 ↑/↓ 순서 단추도 함께 둔다.
 *
 * 고정의 실체는 사용자별 즐겨찾기(scom.menu_favorites)다 — 탭 우클릭
 * 메뉴의 '즐겨찾기 추가'와 같은 데이터를 쓴다.
 */

const accessStore = useAccessStore();
const favoriteStore = useMenuFavoriteStore();

const loading = ref(true);

onMounted(async () => {
  await favoriteStore.load();
  loading.value = false;
});

/** 트리를 펼쳐 '열 수 있는 화면 메뉴' 만 남긴다 (묶음·숨김 제외). */
interface FlatMenu {
  icon?: null | string;
  path: string;
  title: string;
}

function flatten(menus: MenuRecordRaw[], out: FlatMenu[] = []): FlatMenu[] {
  for (const m of menus) {
    const children = m.children ?? [];
    if (children.length > 0) {
      flatten(children, out);
      continue;
    }
    if (!m.path || m.path.startsWith('__') || m.meta?.hideInMenu) continue;
    out.push({
      path: m.path,
      title: $tIfKey((m.meta?.title as string) ?? m.name ?? m.path),
      icon: (m.meta?.icon as string) ?? null,
    });
  }
  return out;
}

const allMenus = computed(() => flatten(accessStore.accessMenus ?? []));

/** 고정된 메뉴 — 즐겨찾기 순서대로 */
const pinned = computed(() =>
  favoriteStore.favorites.map((f) => ({
    path: f.path,
    title: $tIfKey(f.title ?? f.name),
    icon: f.icon,
  })),
);

/** 고정되지 않은 메뉴 */
const unpinned = computed(() =>
  allMenus.value.filter((m) => !favoriteStore.isFavorite(m.path)),
);

// ── 드래그 처리 ─────────────────────────────────────────

const dragging = ref<null | { from: 'pinned' | 'unpinned'; path: string }>(
  null,
);
const overZone = ref<'' | 'pinned' | 'unpinned'>('');

function onDragStart(from: 'pinned' | 'unpinned', path: string, e: DragEvent) {
  dragging.value = { from, path };
  e.dataTransfer?.setData('text/plain', path);
  if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
}

function onDragEnd() {
  dragging.value = null;
  overZone.value = '';
}

/** 고정 칸 안의 항목 위에 놓았을 때 — 그 자리로 끼워 넣는다. */
async function dropOnPinnedItem(targetIndex: number) {
  const d = dragging.value;
  onDragEnd();
  if (!d) return;
  try {
    if (d.from === 'unpinned') {
      await favoriteStore.add(d.path);
    }
    const paths = pinned.value.map((p) => p.path).filter((p) => p !== d.path);
    paths.splice(targetIndex, 0, d.path);
    await favoriteStore.reorder(paths);
  } catch (error: any) {
    message.error(error?.message ?? '저장하지 못했습니다.');
  }
}

/** 고정 칸의 빈 곳에 놓았을 때 — 맨 뒤에 붙인다. */
async function dropOnPinnedZone() {
  const d = dragging.value;
  onDragEnd();
  if (!d || d.from === 'pinned') return;
  try {
    await favoriteStore.add(d.path);
  } catch (error: any) {
    message.error(error?.message ?? '고정하지 못했습니다.');
  }
}

/** 미고정 칸에 놓았을 때 — 고정을 푼다. */
async function dropOnUnpinnedZone() {
  const d = dragging.value;
  onDragEnd();
  if (!d || d.from === 'unpinned') return;
  try {
    await favoriteStore.remove(d.path);
  } catch (error: any) {
    message.error(error?.message ?? '해제하지 못했습니다.');
  }
}

async function pin(path: string) {
  try {
    await favoriteStore.add(path);
  } catch (error: any) {
    message.error(error?.message ?? '고정하지 못했습니다.');
  }
}

async function unpin(path: string) {
  try {
    await favoriteStore.remove(path);
  } catch (error: any) {
    message.error(error?.message ?? '해제하지 못했습니다.');
  }
}

/**
 * 고정 칸 안에서 한 칸 위/아래로 옮긴다.
 * HTML5 드래그가 뜨지 않는 터치 환경을 위한 길이다 — 저장은 드래그와
 * 같은 reorder 를 그대로 쓴다.
 */
async function move(index: number, delta: -1 | 1) {
  const target = index + delta;
  if (target < 0 || target >= pinned.value.length) return;
  const paths = pinned.value.map((p) => p.path);
  const [moved] = paths.splice(index, 1);
  if (!moved) return;
  paths.splice(target, 0, moved);
  try {
    await favoriteStore.reorder(paths);
  } catch (error: any) {
    message.error(error?.message ?? '저장하지 못했습니다.');
  }
}
</script>

<template>
  <Spin :spinning="loading || favoriteStore.saving">
    <p class="text-muted-foreground mb-4 text-sm">
      항목을 반대쪽으로 끌어다 놓으면 고정/해제된다. 고정된 메뉴 안에서 끌면
      순서가 바뀐다 — 사이드바 즐겨찾기 묶음에 이 순서대로 나타난다.
      끌기 어려운 환경에서는 각 항목의 단추(↑ · ↓ · 고정/해제)를 쓴다.
    </p>
    <div class="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <!-- 고정된 메뉴 -->
      <div
        :class="overZone === 'pinned' ? 'border-primary' : 'border-border'"
        class="rounded-md border-2 border-dashed p-3"
        @dragleave.self="overZone = ''"
        @dragover.prevent="overZone = 'pinned'"
        @drop.prevent="dropOnPinnedZone"
      >
        <div class="mb-2 flex items-center justify-between">
          <span class="font-medium">고정된 메뉴</span>
          <span class="text-muted-foreground text-xs">{{ pinned.length }}개</span>
        </div>
        <div v-if="pinned.length === 0" class="text-muted-foreground py-8 text-center text-sm">
          오른쪽에서 메뉴를 끌어다 놓으면 고정된다.
        </div>
        <div class="flex flex-col gap-1.5">
          <div
            v-for="(item, index) in pinned"
            :key="item.path"
            class="bg-card hover:border-primary flex cursor-grab items-center gap-2 rounded-md border px-3 py-2 active:cursor-grabbing"
            draggable="true"
            @dragend="onDragEnd"
            @dragstart="onDragStart('pinned', item.path, $event)"
            @drop.prevent.stop="dropOnPinnedItem(index)"
            @dragover.prevent
          >
            <IconifyIcon
              v-if="item.icon"
              :icon="item.icon"
              class="text-muted-foreground size-4 flex-none"
            />
            <span class="flex-1 truncate text-sm">{{ item.title }}</span>
            <span class="text-muted-foreground font-mono text-xs">{{ index + 1 }}</span>
            <!-- 끌기 어려운 환경(터치)용 순서 단추 -->
            <Button
              :disabled="index === 0"
              size="small"
              title="위로"
              type="text"
              @click="move(index, -1)"
            >
              <IconifyIcon icon="lucide:chevron-up" class="size-4" />
            </Button>
            <Button
              :disabled="index === pinned.length - 1"
              size="small"
              title="아래로"
              type="text"
              @click="move(index, 1)"
            >
              <IconifyIcon icon="lucide:chevron-down" class="size-4" />
            </Button>
            <Button size="small" type="text" @click="unpin(item.path)">
              해제
            </Button>
          </div>
        </div>
      </div>

      <!-- 고정되지 않은 메뉴 -->
      <div
        :class="overZone === 'unpinned' ? 'border-primary' : 'border-border'"
        class="rounded-md border-2 border-dashed p-3"
        @dragleave.self="overZone = ''"
        @dragover.prevent="overZone = 'unpinned'"
        @drop.prevent="dropOnUnpinnedZone"
      >
        <div class="mb-2 flex items-center justify-between">
          <span class="font-medium">고정되지 않은 메뉴</span>
          <span class="text-muted-foreground text-xs">{{ unpinned.length }}개</span>
        </div>
        <div class="flex max-h-[520px] flex-col gap-1.5 overflow-auto pr-1">
          <div
            v-for="item in unpinned"
            :key="item.path"
            class="bg-card hover:border-primary flex cursor-grab items-center gap-2 rounded-md border px-3 py-2 active:cursor-grabbing"
            draggable="true"
            @dragend="onDragEnd"
            @dragstart="onDragStart('unpinned', item.path, $event)"
          >
            <IconifyIcon
              v-if="item.icon"
              :icon="item.icon"
              class="text-muted-foreground size-4 flex-none"
            />
            <span class="flex-1 truncate text-sm">{{ item.title }}</span>
            <Button size="small" type="text" @click="pin(item.path)">
              고정
            </Button>
          </div>
        </div>
      </div>
    </div>
  </Spin>
</template>
