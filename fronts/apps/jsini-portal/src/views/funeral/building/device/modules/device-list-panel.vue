<script lang="ts" setup>
import { Badge, Button, Popconfirm, Tooltip } from 'ant-design-vue';
import { IconifyIcon, Plus } from '@vben/icons';
import BizSelect from '#/components/BizSelect.vue';
import type { BuildingApi } from '#/api/funeral/building';

const props = defineProps<{
  Grid: any;
  selectedCompanyId: string;
  selectedBuildingId: string;
  selectedFloorId: string;
  selectedRoomId: string;
  showConfigPanel: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:selectedCompanyId', val: string): void;
  (e: 'update:selectedBuildingId', val: string): void;
  (e: 'update:selectedFloorId', val: string): void;
  (e: 'update:selectedRoomId', val: string): void;
  (e: 'create'): void;
  (e: 'edit', row: BuildingApi.Device): void;
  (e: 'delete', row: BuildingApi.Device): void;
  (e: 'reboot', row: BuildingApi.Device): void;
}>();
</script>

<template>
  <div class="flex h-full flex-col gap-3">
    <!-- 상단 필터 바 -->
    <div class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-card p-3 shadow-sm shrink-0">
      <div class="flex flex-wrap items-center gap-3">
        <div class="flex items-center gap-2">
          <span class="whitespace-nowrap text-xs font-semibold text-muted-foreground">회사</span>
          <BizSelect
            :value="selectedCompanyId"
            type="company"
            auto-select-first
            placeholder="회사 선택"
            class="w-40"
            show-search
            option-filter-prop="label"
            @update:value="(val) => emit('update:selectedCompanyId', val as string)"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="whitespace-nowrap text-xs font-semibold text-muted-foreground">건물</span>
          <BizSelect
            :value="selectedBuildingId"
            type="building"
            :params="{ companyId: selectedCompanyId }"
            auto-select-first
            placeholder="건물 선택"
            class="w-40"
            show-search
            option-filter-prop="label"
            @update:value="(val) => emit('update:selectedBuildingId', val as string)"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="whitespace-nowrap text-xs font-semibold text-muted-foreground">층</span>
          <BizSelect
            :value="selectedFloorId"
            type="floor"
            :params="{ buildingId: selectedBuildingId }"
            auto-select-first
            allow-clear
            placeholder="층 선택"
            class="w-32"
            show-search
            option-filter-prop="label"
            @update:value="(val) => emit('update:selectedFloorId', val as string)"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="whitespace-nowrap text-xs font-semibold text-muted-foreground">호실</span>
          <BizSelect
            :value="selectedRoomId"
            type="room"
            :params="{ floorId: selectedFloorId }"
            allow-clear
            placeholder="전체 호실"
            class="w-32"
            show-search
            option-filter-prop="label"
            @update:value="(val) => emit('update:selectedRoomId', val as string)"
          />
        </div>
      </div>
      <Button type="primary" size="small" @click="emit('create')">
        <Plus class="mr-1 size-4" />
        장비 등록
      </Button>
    </div>

    <!-- 장비 목록 그리드 -->
    <div class="flex min-h-0 flex-1 flex-col">
      <component :is="Grid" table-title="장비 목록">
        <template #status-badge="{ row }">
          <Badge v-if="row.status === 'ONLINE'" status="success" text="온라인" />
          <Badge v-else-if="row.status === 'OFFLINE'" status="error" text="오프라인" />
          <Badge v-else status="default" text="미확인" />
        </template>

        <template #action="{ row }">
          <div class="flex gap-1">
            <Tooltip v-perm:update title="수정">
              <Button type="link" size="small" @click.stop="emit('edit', row)">
                <IconifyIcon icon="lucide:edit" class="size-4" />
              </Button>
            </Tooltip>
            <Tooltip title="원격 재부팅">
              <Button type="link" size="small" @click.stop="emit('reboot', row)">
                <IconifyIcon icon="lucide:power-off" class="size-4" />
              </Button>
            </Tooltip>
            <Popconfirm title="삭제하시겠습니까?" @confirm="emit('delete', row)">
              <Tooltip v-perm:delete title="삭제">
                <Button type="link" size="small" danger @click.stop>
                  <IconifyIcon icon="lucide:trash-2" class="size-4" />
                </Button>
              </Tooltip>
            </Popconfirm>
          </div>
        </template>
      </component>
      <div v-if="!showConfigPanel" class="mt-2 text-center text-xs text-muted-foreground">
        장비를 클릭하면 오른쪽에 설정 패널이 표시됩니다.
      </div>
    </div>
  </div>
</template>
