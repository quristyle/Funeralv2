<script lang="ts" setup>
import type { SystemMenuApi } from '#/api/portal/system/menu';

import { computed } from 'vue';

import { Alert, Checkbox, Input } from 'ant-design-vue';

/**
 * [메뉴가 사용하는 권한 항목]
 *
 * `scom.role_menus` 는 메뉴마다 권한 칸을 15개 들고 있다.
 * 기본 7개(열람·조회·추가·삭제·수정·출력·엑셀)와 사용자 정의 8개다.
 * 그런데 메뉴마다 실제로 의미 있는 항목은 다르다. 조회만 하는 리포트 화면에
 * '삭제' 체크박스가 떠 있어도 쓸모가 없고, 사용자 정의 칸은 이름이 없으면
 * 'C1'~'C8' 로만 보여서 무엇을 켜는 건지 알 수가 없다.
 *
 * 여기서 메뉴별로 쓰는 항목을 정하고 사용자 정의 칸에 이름을 붙이면,
 * 역할 권한 화면이 그대로 따라간다 — 쓰지 않는 항목은 잠기고,
 * 사용자 정의 칸은 붙인 이름으로 나온다.
 */

const props = defineProps<{
  /** 메뉴 유형. 디렉터리·버튼은 화면이 없어 권한 항목을 쓰지 않는다. */
  menuType?: string;
}>();

const model = defineModel<SystemMenuApi.MenuPermissionItems>({
  required: true,
});

/** 기본 권한 7종 */
const BASE_ITEMS = [
  { key: 'useView', label: '열람' },
  { key: 'useSearch', label: '조회' },
  { key: 'useCreate', label: '추가' },
  { key: 'useUpdate', label: '수정' },
  { key: 'useDelete', label: '삭제' },
  { key: 'usePrint', label: '출력' },
  { key: 'useExcel', label: '엑셀' },
] as const;

/** 사용자 정의 권한 8종 */
const CUSTOM_ITEMS = Array.from({ length: 8 }, (_, i) => ({
  nameKey: `cust${i + 1}Name` as keyof SystemMenuApi.MenuPermissionItems,
  no: i + 1,
  useKey: `useCust${i + 1}` as keyof SystemMenuApi.MenuPermissionItems,
}));

/** 화면이 없는 메뉴 유형에서는 권한 항목을 정할 이유가 없다. */
const notApplicable = computed(() =>
  ['BUTTON', 'CATALOG'].includes(props.menuType ?? ''),
);

function toggleAllBase(checked: boolean) {
  BASE_ITEMS.forEach((item) => {
    (model.value as any)[item.key] = checked;
  });
}

const allBaseChecked = computed(() =>
  BASE_ITEMS.every((item) => (model.value as any)[item.key]),
);

/** 사용자 정의 칸을 끄면 이름도 지운다. 껐다 켰을 때 옛 이름이 남지 않게. */
function onCustomToggle(useKey: string, nameKey: string, checked: boolean) {
  (model.value as any)[useKey] = checked;
  if (!checked) (model.value as any)[nameKey] = '';
}
</script>

<template>
  <div class="mx-4 mt-2">
    <div class="mb-2 flex items-center justify-between">
      <span class="text-sm font-semibold">사용 권한 항목</span>
      <Checkbox
        v-if="!notApplicable"
        :checked="allBaseChecked"
        @change="(e: any) => toggleAllBase(e.target.checked)"
      >
        <span class="text-xs">기본 전체</span>
      </Checkbox>
    </div>

    <Alert
      v-if="notApplicable"
      description="디렉터리와 버튼은 화면이 없어 권한 항목을 쓰지 않습니다."
      show-icon
      type="info"
    />

    <template v-else>
      <p class="mb-2 text-xs text-muted-foreground">
        여기서 켜 둔 항목만 역할 권한 화면에 나타납니다. 꺼 둔 항목은 그
        화면에서 지정할 수 없고, 저장 요청이 들어와도 서버가 받지 않습니다.
      </p>

      <!-- 기본 권한 -->
      <div class="mb-3 flex flex-wrap gap-x-4 gap-y-2 rounded border border-border p-3">
        <Checkbox
          v-for="item in BASE_ITEMS"
          :key="item.key"
          v-model:checked="(model as any)[item.key]"
        >
          {{ item.label }}
        </Checkbox>
      </div>

      <!-- 사용자 정의 권한 -->
      <div class="rounded border border-border p-3">
        <div class="mb-2 text-xs text-muted-foreground">
          사용자 정의 권한 — 켠 칸에는 이름을 붙여 주세요. 이름이 역할 권한
          화면의 열 제목이 됩니다.
        </div>
        <div class="grid grid-cols-1 gap-2 sm:grid-cols-2">
          <div
            v-for="item in CUSTOM_ITEMS"
            :key="item.no"
            class="flex items-center gap-2"
          >
            <Checkbox
              :checked="!!(model as any)[item.useKey]"
              @change="
                (e: any) =>
                  onCustomToggle(item.useKey, item.nameKey, e.target.checked)
              "
            >
              <span class="text-xs">C{{ item.no }}</span>
            </Checkbox>
            <Input
              v-model:value="(model as any)[item.nameKey]"
              :disabled="!(model as any)[item.useKey]"
              :maxlength="20"
              :placeholder="`권한명 ${item.no}`"
              size="small"
            />
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
