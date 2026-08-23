<script setup lang="ts">
/**
 * [프로젝트 화면 메뉴 관리]
 *
 * 원본: ProjMngWasm `Pages/Comm/MenuMng.razor` (`/menumng`).
 * 프로시저: `sp_dev_menu_exec`, 소스 스캔은 `md_blazor_scan`
 *
 * 관리 대상 프로젝트가 들고 있는 화면 목록을 트리로 편집한다.
 * **포털 메뉴가 아니다** — 포털 메뉴·권한은 AuthServer(`scom.system_menus`)가 관장한다.
 *
 * 원본과 같은 동작을 옮겼다.
 *   · 트리 드래그로 상위 메뉴 변경
 *   · 우클릭 메뉴: 같은 위치 추가 / 하위 추가 / 루트에 추가 / 삭제 / 소스에서 읽기
 *   · 오른쪽 상세 폼에서 값 수정 → 저장 시 변경된 항목만 보낸다
 */
import type { ProjMngRow } from '#/api/projmng';

import type { AntTreeNode, MenuNode } from '../shared';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Dropdown,
  Form,
  FormItem,
  Input,
  InputNumber,
  Menu,
  MenuItem,
  message,
  Modal,
  RadioGroup,
  Spin,
  Textarea,
  Tree,
} from 'ant-design-vue';

import { dbCont, dbDelete, dbSave, mdCont } from '#/api/projmng';

import {
  buildMenuTree,
  findMenuNode,
  flattenMenuTree,
  SearchBar,
  SplitPane,
  toAntTree,
} from '../shared';

const PROC = 'sp_dev_menu_exec';

const loading = ref(false);
/** 평평한 원본 목록. 저장 대상(변경된 항목)을 여기서 찾는다. */
const source = ref<MenuNode[]>([]);
const roots = ref<MenuNode[]>([]);
const keyword = ref('');

const selectedKeys = ref<string[]>([]);
const expandedKeys = ref<string[]>([]);
const selected = ref<null | MenuNode>(null);

/** 우클릭한 노드. 컨텍스트 메뉴 동작의 대상이다. */
const contextNode = ref<null | MenuNode>(null);

const treeData = computed<AntTreeNode[]>(() => toAntTree(roots.value));

/** 검색어에 걸리는 노드 키. 원본의 이름 필터를 같은 자리에 옮겼다. */
const matchedKeys = computed(() => {
  const term = keyword.value.trim().toLowerCase();
  if (!term) return new Set<string>();
  return new Set(
    source.value
      .filter((node) =>
        `${node.mnu_nm ?? ''} ${node.pgm_id ?? ''} ${node.mnu_cd ?? ''}`
          .toLowerCase()
          .includes(term),
      )
      .map((node) => String(node.mnu_id ?? '')),
  );
});

async function load() {
  loading.value = true;
  try {
    const result = await dbCont(PROC, { srch_type: 'main' });
    const rows = result.data ?? [];
    roots.value = buildMenuTree(rows);
    source.value = flattenMenuTree(roots.value);
    // 원본은 전체 펼침 상태였다.
    expandedKeys.value = source.value.map((node) => String(node.mnu_id ?? ''));
    selected.value = null;
    selectedKeys.value = [];
  } finally {
    loading.value = false;
  }
}

function onSelect(keys: (number | string)[]) {
  const key = String(keys[0] ?? '');
  selectedKeys.value = key ? [key] : [];
  selected.value = key ? (findMenuNode(roots.value, key) ?? null) : null;
}

/** 값이 바뀐 항목에만 표시를 남긴다. 저장은 표시된 것만 보낸다. */
function markDirty(node: null | MenuNode) {
  if (node) node.__dirty = true;
}

async function saveAll() {
  const dirty = source.value.filter((node) => node.__dirty);
  if (dirty.length === 0) {
    message.warning('수정대상이 존재하지 않습니다.');
    return;
  }

  for (const node of dirty) {
    // eslint-disable-next-line no-await-in-loop
    const saved = await dbSave(
      PROC,
      {
        mnu_id: node.mnu_id,
        owner_id: node.owner_id,
        mnu_cd: node.mnu_cd,
        mnu_nm: node.mnu_nm,
        mnu_url: node.mnu_url,
        pgm_id: node.pgm_id,
        disp_seq: node.disp_seq,
        use_yn: node.use_yn,
        mnu_desc: node.mnu_desc,
      },
      // 이 화면은 그리드가 아니라 폼이라 변경 행을 직접 만들어 넘긴다.
      [{ ...node, children: undefined, parent: undefined, quri_ischange: true }],
    );
    if (saved.code >= 0) delete node.__dirty;
  }

  await load();
}

// ============================================================
// 트리 조작
// ============================================================

/** 드래그로 상위 메뉴를 바꾼다. 자기 부모/자식으로 떨어지는 것은 막는다. */
function onDrop(info: any) {
  const dragKey = String(info.dragNode.key);
  const dropKey = String(info.node.key);

  const dragged = findMenuNode(roots.value, dragKey);
  const target = findMenuNode(roots.value, dropKey);
  if (!dragged || !target || dragged === target) return;
  if (String(target.owner_id ?? '') === dragKey) return;

  // 원래 자리에서 뗀다.
  const from = dragged.parent ? dragged.parent.children : roots.value;
  const index = from.indexOf(dragged);
  if (index >= 0) from.splice(index, 1);

  dragged.owner_id = target.mnu_id;
  dragged.parent = target;
  target.children.push(dragged);
  markDirty(dragged);

  if (!expandedKeys.value.includes(dropKey)) expandedKeys.value.push(dropKey);
}

function onRightClick({ node }: any) {
  contextNode.value = findMenuNode(roots.value, String(node.key)) ?? null;
}

/** 새 메뉴를 서버에 만들고 트리에 붙인다. */
async function createMenu(ownerId: string, label: string) {
  const saved = await dbSave(
    PROC,
    { mnu_nm: label, owner_id: ownerId },
    [{ mnu_nm: label, owner_id: ownerId, quri_ischange: true }],
  );
  if (saved.code < 0) return;
  await load();
}

async function addSibling() {
  const node = contextNode.value;
  if (!node) return;
  await createMenu(String(node.owner_id ?? 'ROOT'), 'same line new menu');
}

async function addChild() {
  const node = contextNode.value;
  if (!node) return;
  await createMenu(String(node.mnu_id ?? ''), 'new menu');
}

async function addRoot() {
  await createMenu('ROOT', 'new Menu');
}

function removeMenu() {
  const node = contextNode.value;
  if (!node) return;

  Modal.confirm({
    title: '메뉴를 삭제하겠습니까?',
    content: String(node.mnu_nm ?? ''),
    okText: '삭제',
    cancelText: '취소',
    okType: 'danger',
    onOk: async () => {
      const deleted = await dbDelete(PROC, { mnu_id: node.mnu_id });
      if (deleted.code >= 0) await load();
    },
  });
}

/**
 * 소스를 훑어 아직 등록되지 않은 화면을 선택한 메뉴 아래에 넣는다.
 * 원본의 "파일에서 메뉴 읽기"다. 서버가 프로젝트 소스 경로를 훑어 화면 목록을 준다.
 */
async function importFromSource() {
  const owner = contextNode.value;
  if (!owner) return;

  const scanned = await mdCont('md_blazor_scan', { prj_rid: '3' });
  const rows = scanned.data ?? [];
  if (rows.length === 0) {
    message.info('소스에서 찾은 화면이 없습니다.');
    return;
  }

  const known = new Set(source.value.map((node) => String(node.pgm_id ?? '')));
  const news = rows.filter((row) => !known.has(String(row.fullname ?? '')));

  if (news.length === 0) {
    message.info('새로 등록할 화면이 없습니다. 모두 이미 등록되어 있습니다.');
    return;
  }

  for (const row of news) {
    // eslint-disable-next-line no-await-in-loop
    await dbSave(
      PROC,
      {},
      [
        {
          mnu_nm: row.title,
          mnu_url: row.url,
          pgm_id: row.fullname,
          mnu_cd: row.name,
          owner_id: owner.mnu_id,
          quri_ischange: true,
        },
      ],
    );
  }

  message.success(`${news.length}건을 등록했습니다.`);
  await load();
}

onMounted(load);

/** 상세 폼이 쓰는 필드 접근자. 값이 바뀌면 변경 표시를 남긴다. */
function field(name: string) {
  return computed({
    get: () => (selected.value?.[name] ?? '') as any,
    set: (value: any) => {
      if (!selected.value) return;
      (selected.value as ProjMngRow)[name] = value;
      markDirty(selected.value);
    },
  });
}

const mnuCd = field('mnu_cd');
const mnuNm = field('mnu_nm');
const pgmId = field('pgm_id');
const mnuUrl = field('mnu_url');
const dispSeq = field('disp_seq');
const mnuDesc = field('mnu_desc');
const useYn = field('use_yn');
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <SearchBar class="mb-2">
      <Input
        v-model:value="keyword"
        allow-clear
        placeholder="메뉴 이름 · 프로그램 ID 검색"
        size="small"
        style="width: 260px"
      />
      <template #actions>
        <Button v-perm:search size="small" @click="load">조회</Button>
        <Button v-perm:update size="small" type="primary" @click="saveAll">
          저장
        </Button>
      </template>
    </SearchBar>

    <SplitPane :size="45">
      <template #first>
        <div class="border-border h-full overflow-auto rounded-md border p-2">
          <Spin :spinning="loading">
          <Dropdown :trigger="['contextmenu']">
            <Tree
              v-model:expanded-keys="expandedKeys"
              :tree-data="treeData"
              :selected-keys="selectedKeys"
              block-node
              draggable
              @drop="onDrop"
              @select="onSelect"
              @rightclick="onRightClick"
            >
              <template #title="{ title, key }">
                <span
                  :class="
                    matchedKeys.has(String(key))
                      ? 'text-primary font-semibold'
                      : ''
                  "
                >
                  {{ title }}
                </span>
              </template>
            </Tree>

            <template #overlay>
              <Menu>
                <MenuItem key="sibling" @click="addSibling">
                  선택위치 메뉴 추가
                </MenuItem>
                <MenuItem key="child" @click="addChild">하위 메뉴 추가</MenuItem>
                <MenuItem key="scan" @click="importFromSource">
                  파일에서 메뉴 읽기
                </MenuItem>
                <MenuItem key="root" @click="addRoot">Root 에 추가</MenuItem>
                <MenuItem key="remove" danger @click="removeMenu">
                  메뉴 삭제
                </MenuItem>
              </Menu>
            </template>
          </Dropdown>
          </Spin>
        </div>
      </template>

      <template #second>
        <div class="h-full overflow-auto p-3">
          <div
            v-if="!selected"
            class="text-muted-foreground flex h-full items-center justify-center text-sm"
          >
            왼쪽 트리에서 메뉴를 고르세요.
          </div>

          <Form v-else :label-col="{ span: 6 }" size="small">
            <FormItem label="mnu_id">
              <Input :value="String(selected.mnu_id ?? '')" disabled />
            </FormItem>
            <FormItem label="owner_id">
              <Input :value="String(selected.owner_id ?? '')" disabled />
            </FormItem>
            <FormItem label="mnu_cd">
              <Input v-model:value="mnuCd" />
            </FormItem>
            <FormItem label="mnu_nm">
              <Input v-model:value="mnuNm" />
            </FormItem>
            <FormItem label="pgm_id">
              <Input v-model:value="pgmId" />
            </FormItem>
            <FormItem label="url">
              <Input v-model:value="mnuUrl" />
            </FormItem>
            <FormItem label="disp_seq">
              <InputNumber v-model:value="dispSeq" class="w-full" />
            </FormItem>
            <FormItem label="사용여부">
              <RadioGroup
                v-model:value="useYn"
                :options="[
                  { label: '사용', value: 'Y' },
                  { label: '비사용', value: 'N' },
                ]"
              />
            </FormItem>
            <FormItem label="mnu_desc">
              <Textarea v-model:value="mnuDesc" :rows="4" />
            </FormItem>
          </Form>
        </div>
      </template>
    </SplitPane>
  </Page>
</template>
