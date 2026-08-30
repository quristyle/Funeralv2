<script lang="ts" setup>
import type {
  AccountMenuItem,
  AccountPick,
  RoleScopeApi,
} from '#/api/portal/system/role-scope';

import { computed, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Avatar, Card, Empty, Input, Radio, Spin, Tag, message } from 'ant-design-vue';

import { getCompanyList } from '#/api/portal/system/company';
import { getDeptList } from '#/api/portal/system/dept';
import { getRoleList } from '#/api/portal/system/role';
import {
  assignRoleScope,
  getAccountMenuAccess,
  getEffectiveRoles,
  getRoleScopeAccounts,
  getRoleScopeTree,
  removeRoleScope,
} from '#/api/portal/system/role-scope';
import { $t } from '#/locales';
import { avatarInitial, avatarStyle, avatarThumbUrl } from '#/utils/avatar';

/**
 * [사람롤 — 이 사람이 무슨 권한을 갖는가]
 *
 * 목적은 **한 대상의 권한을 한눈에 이해하는 것**이다.
 *   왼쪽 : 대상 목록. [사람] / [회사·부서] 로 바꿔 본다. 한 칸에서 바로 검색된다
 *   오른쪽: 역할 → 역할 서랍 → 메뉴 순서
 *
 * 역할은 **회사 + 부서 + 사람을 모두 합쳐** 적용된다. 덮어쓰지 않는다.
 * 그래서 역할마다 어디서 왔는지를 함께 적는다 — 그러지 않으면
 * "사람에서 뺐는데 왜 아직 있지" 를 알 수 없다.
 *
 * <b>역할과 서랍을 붙여 두었다.</b> 사이에 메뉴 목록이 있으면 끌어다 놓는 거리가 멀다.
 * 메뉴는 결과를 확인하는 것이라 아래에 둔다.
 *
 * 스크롤은 **목록 안에서만** 생긴다(준수사항 4). 화면 전체가 늘어나면 왼쪽 목록과
 * 오른쪽 역할이 함께 밀려나 드래그 자체가 불가능해진다.
 */

type Mode = 'account' | 'scope';

/** 왼쪽 목록의 한 칸. 사람과 회사·부서를 같은 모양으로 다룬다. */
interface Target {
  id: string;
  kind: RoleScopeApi.ScopeKind;
  /** 검색에 쓰는 부가 정보 (회사·부서명, 로그인 아이디) */
  meta: string;
  name: string;
  /**
   * 프로필 사진 주소. 사람일 때만 있고, 사진을 올리지 않았으면 없다.
   * 없는 것이 정상이라 화면은 이름 첫 글자로 대신 그린다.
   */
  avatar?: string;
}

const mode = ref<Mode>('account');
const keyword = ref('');
const selected = ref<null | Target>(null);

const accountTargets = ref<Target[]>([]);
const scopeTargets = ref<Target[]>([]);
const roles = ref<{ id: string; name: string }[]>([]);

/** 선택한 대상에 **직접** 걸린 역할 (회사·부서 모드에서 쓴다) */
const directRoles = ref<string[]>([]);
/** 사람 모드에서의 합산 결과 */
const effective = ref<null | RoleScopeApi.EffectiveRoles>(null);
const assigned = ref<AccountMenuItem[]>([]);
const unassigned = ref<AccountMenuItem[]>([]);

const loadingList = ref(false);
const loadingDetail = ref(false);
const saving = ref(false);

const dragging = ref<null | { from: 'drawer' | 'tag'; roleId: string }>(null);
const hoverRoles = ref(false);
const hoverDrawer = ref(false);

const targets = computed(() =>
  mode.value === 'account' ? accountTargets.value : scopeTargets.value,
);

const roleName = computed(() => {
  const map = new Map(roles.value.map((r) => [r.id, r.name || r.id]));
  return (id: string) => map.get(id) ?? id;
});

/**
 * 검색. 이름·아이디·부서·회사를 한 덩어리로 훑는다.
 * 공백으로 나눈 낱말은 **모두** 포함해야 걸린다("개발 김" 처럼 좁혀 갈 수 있게).
 */
const filtered = computed(() => {
  const words = keyword.value.trim().toLowerCase().split(/\s+/).filter(Boolean);
  if (words.length === 0) return targets.value;
  return targets.value.filter((t) => {
    const hay = `${t.name} ${t.meta}`.toLowerCase();
    return words.every((w) => hay.includes(w));
  });
});

/** 사람 모드에서만 합산 역할을 보여준다. 회사·부서는 직접 걸린 것만 있다. */
const shownRoles = computed(() =>
  mode.value === 'account' ? (effective.value?.roleIds ?? []) : directRoles.value,
);

function sourcesOf(roleId: string) {
  return effective.value?.sources?.[roleId] ?? [];
}

/** 이 자리에서 뺄 수 있는 역할인가. 회사·부서 모드는 직접 걸린 것뿐이라 전부 뺄 수 있다. */
function isDirect(roleId: string) {
  if (mode.value === 'scope') return true;
  return sourcesOf(roleId).includes('account');
}

function sourceLabel(roleId: string) {
  if (mode.value === 'scope') return '';
  const s = sourcesOf(roleId);
  return [
    s.includes('account') ? '직접' : '',
    s.includes('department') ? '부서' : '',
    s.includes('company') ? '회사' : '',
  ]
    .filter(Boolean)
    .join(' + ');
}

// ── 목록 ──────────────────────────────────────────────────────

async function loadAccounts() {
  const list = await getRoleScopeAccounts();
  accountTargets.value = list.map((a: AccountPick) => ({
    id: a.id,
    kind: 'account' as const,
    meta: [a.loginId, a.companyName, a.departmentName].filter(Boolean).join(' '),
    name: a.name,
    avatar: avatarThumbUrl(a.avatar),
  }));
}

/**
 * 회사·부서를 한 줄 목록으로 편다.
 * 트리로 보여 주면 접힘 상태를 다루느라 검색이 느려진다 — 여기서는 **찾는 속도**가 우선이다.
 * 대신 부서 앞에 회사명을 붙여 어디 것인지 알 수 있게 한다.
 */
async function loadScopes() {
  const compRes: any = await getCompanyList();
  const companies = compRes?.result ?? compRes?.items ?? compRes ?? [];

  const out: Target[] = [];
  for (const c of companies) {
    out.push({ id: c.id, kind: 'company', meta: '회사', name: c.name });

    try {
      const dRes: any = await getDeptList(c.id);
      const depts = dRes?.result ?? dRes ?? [];
      const walk = (nodes: any[], trail: string[]) => {
        for (const d of nodes) {
          out.push({
            id: d.id,
            kind: 'department',
            meta: `${c.name} 부서 ${trail.join(' ')}`.trim(),
            name: [...trail, d.name].join(' › '),
          });
          if (d.children?.length) walk(d.children, [...trail, d.name]);
        }
      };
      walk(depts, []);
    } catch {
      // 부서를 못 읽어도 회사는 목록에 남는다.
    }
  }
  scopeTargets.value = out;
}

async function loadRoles() {
  const res: any = await getRoleList({});
  const list = res?.result ?? res ?? [];
  roles.value = list
    .filter((r: any) => r.status === 1)
    .map((r: any) => ({ id: r.id, name: r.name || r.id }));
}

// ── 선택한 대상의 상세 ────────────────────────────────────────

async function loadDetail() {
  const t = selected.value;
  if (!t) {
    effective.value = null;
    directRoles.value = [];
    assigned.value = [];
    unassigned.value = [];
    return;
  }

  loadingDetail.value = true;
  try {
    if (t.kind === 'account') {
      const [eff, menus] = await Promise.all([
        getEffectiveRoles(t.id),
        getAccountMenuAccess(t.id),
      ]);
      effective.value = eff;
      directRoles.value = [];
      assigned.value = menus?.assigned ?? [];
      unassigned.value = menus?.unassigned ?? [];
    } else {
      // 회사·부서는 직접 걸린 역할만 있다. 조직 트리에서 그 칸을 찾아 읽는다.
      effective.value = null;
      assigned.value = [];
      unassigned.value = [];
      directRoles.value = await loadScopeRoles(t);
    }
  } catch (error) {
    console.error(error);
    message.error('권한 정보를 불러오지 못했습니다.');
  } finally {
    loadingDetail.value = false;
  }
}

/** 조직 트리에서 이 회사·부서에 직접 걸린 역할을 찾는다. */
async function loadScopeRoles(t: Target): Promise<string[]> {
  const companyId =
    t.kind === 'company'
      ? t.id
      : (scopeTargets.value.find((s) => s.kind === 'company' && t.meta.startsWith(s.name))?.id ??
        '');

  if (!companyId) return [];

  const res = await getRoleScopeTree(companyId);
  const company = res?.company;
  if (!company) return [];
  if (t.kind === 'company') return company.roleIds ?? [];

  let found: string[] = [];
  const walk = (nodes: RoleScopeApi.ScopeNode[]) => {
    for (const n of nodes) {
      if (n.id === t.id) found = n.roleIds ?? [];
      if (n.children?.length) walk(n.children);
    }
  };
  walk(company.children ?? []);
  return found;
}

watch(selected, loadDetail);
watch(mode, () => {
  selected.value = null;
  keyword.value = '';
});

onMounted(async () => {
  loadingList.value = true;
  try {
    await Promise.all([loadAccounts(), loadScopes(), loadRoles()]);
  } catch (error) {
    console.error(error);
    message.error('목록을 불러오지 못했습니다.');
  } finally {
    loadingList.value = false;
  }
});

// ── 드래그드롭 ────────────────────────────────────────────────

function onDrawerDragStart(e: DragEvent, roleId: string) {
  if (!e.dataTransfer) return;
  e.dataTransfer.setData('text/plain', roleId);
  e.dataTransfer.effectAllowed = 'copy';
  dragging.value = { from: 'drawer', roleId };
}

function onTagDragStart(e: DragEvent, roleId: string) {
  if (!isDirect(roleId)) return; // 물려받은 역할은 여기서 못 뺀다
  if (!e.dataTransfer) return;
  e.dataTransfer.setData('text/plain', roleId);
  e.dataTransfer.effectAllowed = 'move';
  dragging.value = { from: 'tag', roleId };
}

function onDragEnd() {
  dragging.value = null;
  hoverRoles.value = false;
  hoverDrawer.value = false;
}

function onRolesDragOver(e: DragEvent) {
  if (!dragging.value || dragging.value.from !== 'drawer' || saving.value) return;
  if (!selected.value) return;
  if (shownRoles.value.includes(dragging.value.roleId) && isDirect(dragging.value.roleId)) return;
  e.preventDefault();
  hoverRoles.value = true;
}

/**
 * 역할 지정 — 드롭(onRolesDrop)과 서랍의 [+] 버튼이 **같은 함수**를 쓴다.
 * 버튼은 HTML5 drag&drop 이 동작하지 않는 터치(모바일)의 대체 경로다.
 */
async function assignRole(roleId: string) {
  const t = selected.value;
  if (!t || saving.value) return;
  // 이미 직접 걸려 있으면 다시 지정할 것이 없다 (드롭 판정과 같다).
  if (shownRoles.value.includes(roleId) && isDirect(roleId)) return;

  saving.value = true;
  try {
    await assignRoleScope(t.kind, t.id, roleId);
    message.success(`[${t.name}] 에 ${roleName.value(roleId)} 을(를) 지정했습니다.`);
    await loadDetail();
  } catch (error) {
    console.error(error);
    message.error('역할을 지정하지 못했습니다.');
  } finally {
    saving.value = false;
  }
}

/**
 * 태그의 × 처리. antd Tag 는 close 때 스스로 사라지므로 preventDefault 로 막고,
 * 서버 처리가 끝나면 loadDetail 이 목록을 다시 그린다.
 */
function onTagClose(e: Event, roleId: string) {
  e.preventDefault();
  removeRole(roleId);
}

/** 역할 해제 — 드롭(onDrawerDrop)과 태그의 × 버튼이 같은 함수를 쓴다. */
async function removeRole(roleId: string) {
  const t = selected.value;
  if (!t || saving.value) return;
  if (!isDirect(roleId)) return; // 물려받은 역할은 여기서 못 뺀다

  saving.value = true;
  try {
    await removeRoleScope(t.kind, t.id, roleId);
    message.success(`${roleName.value(roleId)} 을(를) 해제했습니다.`);
    await loadDetail();
  } catch (error) {
    console.error(error);
    message.error('역할을 해제하지 못했습니다.');
  } finally {
    saving.value = false;
  }
}

async function onRolesDrop(e: DragEvent) {
  if (!dragging.value || dragging.value.from !== 'drawer' || !selected.value) return;
  e.preventDefault();

  const roleId = dragging.value.roleId;
  onDragEnd();
  await assignRole(roleId);
}

function onDrawerDragOver(e: DragEvent) {
  if (!dragging.value || dragging.value.from !== 'tag' || saving.value) return;
  e.preventDefault();
  hoverDrawer.value = true;
}

async function onDrawerDrop(e: DragEvent) {
  if (!dragging.value || dragging.value.from !== 'tag' || !selected.value) return;
  e.preventDefault();

  const roleId = dragging.value.roleId;
  onDragEnd();
  await removeRole(roleId);
}

/** 메뉴 제목은 다국어 키일 수 있다. 다른 화면과 같은 규칙으로 번역한다. */
function menuTitle(m: AccountMenuItem) {
  return $t(m.title ?? m.path);
}

function kindIcon(kind: RoleScopeApi.ScopeKind) {
  if (kind === 'company') return 'lucide:building-2';
  return kind === 'department' ? 'lucide:folder-open' : 'lucide:user';
}
</script>

<template>
  <!--
    스크롤은 목록 안에서만 생긴다. Page 는 내용 높이를 고정해 주고(auto-content-height),
    안쪽은 min-h-0 + overflow-hidden 사슬로 높이를 내려보낸다.
    이 사슬이 한 칸이라도 끊기면 화면 전체가 늘어나 스크롤이 생긴다.
  -->
  <Page auto-content-height>
    <div class="grid h-full min-h-0 grid-cols-12 gap-3">
      <!-- 왼쪽: 대상 목록 -->
      <div class="col-span-12 flex h-full min-h-0 flex-col md:col-span-3">
        <Card
          class="flex h-full min-h-0 flex-1 flex-col overflow-hidden"
          :body-style="{
            flex: 1,
            minHeight: 0,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            padding: '10px 12px',
          }"
        >
          <template #title>
            <span class="text-sm">
              {{ mode === 'account' ? '사람' : '회사 · 부서' }}
              {{ filtered.length }}/{{ targets.length }}
            </span>
          </template>

          <Radio.Group v-model:value="mode" button-style="solid" size="small" class="mb-2 w-full">
            <Radio.Button value="account" class="w-1/2 text-center">사람</Radio.Button>
            <Radio.Button value="scope" class="w-1/2 text-center">회사 · 부서</Radio.Button>
          </Radio.Group>

          <Input
            v-model:value="keyword"
            allow-clear
            :placeholder="mode === 'account' ? '이름 · 아이디 · 부서 · 회사' : '회사 · 부서명'"
            class="mb-2"
          >
            <template #prefix>
              <IconifyIcon icon="lucide:search" class="size-3.5 text-gray-400" />
            </template>
          </Input>

          <!-- 여기서만 스크롤이 생긴다 -->
          <div class="min-h-0 flex-1 overflow-y-auto">
            <Spin :spinning="loadingList">
              <div
                v-for="t in filtered"
                :key="`${t.kind}:${t.id}`"
                class="mb-1 cursor-pointer rounded border px-2 py-1.5 transition-colors"
                :class="
                  selected?.id === t.id && selected?.kind === t.kind
                    ? 'border-primary bg-primary/5'
                    : 'hover:border-primary/40'
                "
                @click="selected = t"
              >
                <div class="flex items-center gap-1.5">
                  <!--
                    사람은 얼굴로 찾는다. 사진이 있으면 사진, 없으면 이름 첫 글자다.
                    (사진이 없는 것은 흔한 일이지 오류가 아니다 — 빈 동그라미 대신
                    글자를 넣어야 목록에서 사람이 구분된다.)

                    크기는 `avatarStyle` 이 rem 으로 준다. antd 가 32px 을 자기
                    클래스로 박아 두어 Tailwind 로는 밀리지 않고, `size` 속성은
                    px 이라 사용자 글꼴 설정을 따라가지 못한다.
                  -->
                  <Avatar
                    v-if="t.kind === 'account'"
                    :src="t.avatar"
                    class="shrink-0"
                    :style="avatarStyle(t.name, !!t.avatar)"
                  >
                    {{ avatarInitial(t.name) }}
                  </Avatar>
                  <IconifyIcon
                    v-else
                    :icon="kindIcon(t.kind)"
                    class="size-3.5 shrink-0 text-gray-400"
                  />
                  <span class="truncate text-sm font-medium">{{ t.name }}</span>
                </div>
                <div
                  class="truncate text-[11px] text-gray-400"
                  :class="t.kind === 'account' ? 'pl-[26px]' : 'pl-5'"
                >
                  {{ t.meta || '소속 없음' }}
                </div>
              </div>
              <Empty v-if="filtered.length === 0" description="검색 결과가 없습니다." />
            </Spin>
          </div>
        </Card>
      </div>

      <!-- 오른쪽 -->
      <div class="col-span-12 flex h-full min-h-0 flex-col gap-3 md:col-span-9">
        <div
          v-if="!selected"
          class="flex flex-1 items-center justify-center text-sm text-gray-400"
        >
          왼쪽에서 대상을 선택해 주세요.
        </div>

        <template v-else>
          <!-- 역할 + 서랍: 붙여 두어야 끌어다 놓는 거리가 짧다 -->
          <Card
            class="shrink-0 transition-colors"
            :class="hoverRoles ? 'ring-2 ring-primary' : ''"
            :body-style="{ padding: '10px 14px' }"
            @dragover="onRolesDragOver"
            @dragleave="hoverRoles = false"
            @drop="onRolesDrop"
          >
            <template #title>
              <span class="flex items-center gap-1.5 text-sm">
                <!-- 왼쪽 목록에서 고른 사람과 같은 얼굴이어야 한다. -->
                <Avatar
                  v-if="selected.kind === 'account'"
                  :src="selected.avatar"
                  class="shrink-0"
                  :style="avatarStyle(selected.name, !!selected.avatar, 1.5)"
                >
                  {{ avatarInitial(selected.name) }}
                </Avatar>
                <IconifyIcon
                  v-else
                  :icon="kindIcon(selected.kind)"
                  class="size-4 text-gray-400"
                />
                {{ selected.name }}
                <span class="text-[11px] font-normal text-gray-400">{{ selected.meta }}</span>
              </span>
            </template>

            <div class="flex flex-wrap items-center gap-1.5">
              <!--
                직접 걸린 역할은 × 로도 해제된다 — 드래그가 동작하지 않는
                터치(모바일)의 대체 경로다.
              -->
              <Tag
                v-for="rid in shownRoles"
                :key="rid"
                :closable="isDirect(rid)"
                :color="isDirect(rid) ? 'blue' : 'default'"
                :draggable="isDirect(rid)"
                :class="isDirect(rid) ? 'cursor-move' : 'cursor-not-allowed'"
                :title="
                  isDirect(rid)
                    ? '아래 서랍으로 끌어다 놓거나 × 를 누르면 해제됩니다'
                    : '회사·부서에서 물려받았습니다. 회사·부서 보기에서 해제하세요'
                "
                @close="onTagClose($event, rid)"
                @dragstart="onTagDragStart($event, rid)"
                @dragend="onDragEnd"
              >
                {{ roleName(rid) }}
                <span v-if="sourceLabel(rid)" class="text-[10px] opacity-70">
                  ({{ sourceLabel(rid) }})
                </span>
              </Tag>
              <span v-if="shownRoles.length === 0" class="text-xs text-gray-400">
                지정된 역할이 없습니다.
              </span>
            </div>
          </Card>

          <!-- 역할 서랍 (해제 자리이기도 하다) -->
          <Card
            class="shrink-0 transition-colors"
            :class="hoverDrawer ? 'ring-2 ring-red-400' : ''"
            :body-style="{ padding: '8px 14px' }"
            @dragover="onDrawerDragOver"
            @dragleave="hoverDrawer = false"
            @drop="onDrawerDrop"
          >
            <div class="flex flex-wrap items-center gap-1.5">
              <span class="mr-1 text-[11px] text-gray-400">
                역할 서랍 — 위로 끌거나 [+] 를 누르면 지정, 여기로 끌거나 태그의 × 를 누르면 해제
              </span>
              <!--
                [+] 는 드래그의 터치(모바일) 대체 경로다. 선택한 대상에 바로 지정한다.
                이미 직접 걸린 역할이면 assignRole 이 조용히 아무 일도 하지 않는다.
              -->
              <div
                v-for="r in roles"
                :key="r.id"
                draggable="true"
                class="flex cursor-move items-center gap-1 rounded border px-2 py-1 text-xs transition-all hover:border-primary hover:shadow-sm"
                @dragstart="onDrawerDragStart($event, r.id)"
                @dragend="onDragEnd"
              >
                {{ r.name }}
                <button
                  type="button"
                  class="text-primary hover:bg-primary/10 -mr-0.5 rounded p-0.5 disabled:cursor-not-allowed disabled:opacity-40"
                  :disabled="!selected || saving"
                  :title="selected ? `[${selected.name}] 에 지정` : '왼쪽에서 대상을 먼저 선택해 주세요'"
                  @click.stop="assignRole(r.id)"
                >
                  <IconifyIcon icon="lucide:plus" class="size-3.5" />
                </button>
              </div>
            </div>
          </Card>

          <!-- 메뉴: 결과 확인용이라 아래에 둔다. 사람일 때만 뜻이 있다 -->
          <div v-if="selected.kind === 'account'" class="grid min-h-0 flex-1 grid-cols-2 gap-3">
            <Card
              class="flex min-h-0 flex-col overflow-hidden"
              :body-style="{ flex: 1, minHeight: 0, overflow: 'auto', padding: '8px 12px' }"
            >
              <template #title>
                <span class="text-sm text-green-700">열린 메뉴 {{ assigned.length }}</span>
              </template>
              <Spin :spinning="loadingDetail">
                <div v-for="m in assigned" :key="m.id" class="border-b py-1 last:border-0">
                  <div class="truncate text-xs">{{ menuTitle(m) }}</div>
                  <div class="truncate text-[10px] text-gray-400">
                    {{ m.breadcrumb || '/' }} · {{ m.path }}
                  </div>
                  <div class="truncate text-[10px] text-green-600">
                    {{ m.grantedBy.map(roleName).join(', ') }}
                  </div>
                </div>
                <Empty v-if="assigned.length === 0" description="열린 메뉴가 없습니다." />
              </Spin>
            </Card>

            <Card
              class="flex min-h-0 flex-col overflow-hidden"
              :body-style="{ flex: 1, minHeight: 0, overflow: 'auto', padding: '8px 12px' }"
            >
              <template #title>
                <span class="text-sm text-gray-500">닫힌 메뉴 {{ unassigned.length }}</span>
              </template>
              <Spin :spinning="loadingDetail">
                <div
                  v-for="m in unassigned"
                  :key="m.id"
                  class="border-b py-1 opacity-70 last:border-0"
                >
                  <div class="truncate text-xs">{{ menuTitle(m) }}</div>
                  <div class="truncate text-[10px] text-gray-400">
                    {{ m.breadcrumb || '/' }} · {{ m.path }}
                  </div>
                </div>
                <Empty v-if="unassigned.length === 0" description="모든 메뉴가 열려 있습니다." />
              </Spin>
            </Card>
          </div>

          <!-- 회사·부서일 때는 메뉴 대신 안내 -->
          <div v-else class="flex flex-1 items-center justify-center px-6 text-center text-xs text-gray-400">
            여기서 지정한 역할은 이 {{ selected.kind === 'company' ? '회사' : '부서' }} 에 속한
            사람 모두에게 더해집니다.<br />
            어떤 메뉴가 열리는지는 [사람] 보기에서 그 사람을 골라 확인하세요.
          </div>
        </template>
      </div>
    </div>
  </Page>
</template>
