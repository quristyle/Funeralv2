<script lang="ts" setup>
import { ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { message, Select } from 'ant-design-vue';

import { getAvailableRooms, moveDeceasedRoom } from '#/api/funeral/building';

/**
 * [호실 변경 팝업] — 옛 빈소현황의 '호실변경' 드롭다운 (p_room_get_other_room).
 *
 * 이동 가능한 호실(ACTIVE + 미점유)만 서버가 골라 준다. 이동은 배정만 바꾸는
 * 전용 API 라 고인 인적 사항은 건드리지 않는다 (47번 문서 2단계).
 */

interface MoveData {
  deceasedId: string;
  deceasedName: string;
  /** 현재 호실 — 목록에서 뺀다 */
  roomId: string;
  buildingId?: string;
}

const emit = defineEmits<{ (e: 'moved'): void }>();

const data = ref<MoveData | null>(null);
const options = ref<{ label: string; value: string }[]>([]);
const targetRoomId = ref<string>();
const loading = ref(false);
const moving = ref(false);

const [Modal, modalApi] = useVbenModal<MoveData>({
  destroyOnClose: true,
  onConfirm: onMove,
  async onOpenChange(isOpen) {
    if (!isOpen) return;
    data.value = modalApi.getData() ?? null;
    targetRoomId.value = undefined;
    options.value = [];
    if (!data.value) return;

    loading.value = true;
    try {
      const rooms = await getAvailableRooms({
        buildingId: data.value.buildingId || undefined,
        excludeRoomId: data.value.roomId,
      });
      options.value = rooms.map((r) => ({
        label: r.floorName ? `${r.floorName} · ${r.name}` : r.name,
        value: r.id,
      }));
    } catch {
      message.error('이동 가능한 호실 목록을 불러오지 못했습니다.');
    } finally {
      loading.value = false;
    }
  },
});

/** 부모가 카드 메뉴에서 부른다. */
function open(payload: MoveData) {
  modalApi.setData(payload);
  modalApi.open();
}

defineExpose({ open });

async function onMove() {
  if (!data.value) return;
  if (!targetRoomId.value) {
    message.warning('옮길 호실을 선택하세요.');
    return;
  }

  moving.value = true;
  modalApi.lock();
  try {
    await moveDeceasedRoom(data.value.deceasedId, targetRoomId.value);
    message.success('호실을 변경했습니다.');
    modalApi.close();
    emit('moved');
  } catch (err: any) {
    // 배정 검증(점유·중지 호실)에 걸리면 서버가 사유를 400 으로 준다.
    const reason = err?.response?.data?.message || err?.message;
    message.error(reason || '호실 변경 중 오류가 발생했습니다.');
  } finally {
    moving.value = false;
    modalApi.lock(false);
  }
}
</script>

<template>
  <Modal :confirm-loading="moving" title="호실 변경" class="w-[420px]">
    <div class="px-4 py-2 space-y-3">
      <p v-if="data" class="text-sm">
        고인 <b>故 {{ data.deceasedName }}</b> 님을 옮길 호실을 선택하세요.
      </p>
      <Select
        v-model:value="targetRoomId"
        :loading="loading"
        :options="options"
        class="w-full"
        placeholder="이동 가능한 호실 (같은 건물)"
        show-search
        option-filter-prop="label"
      />
      <p v-if="!loading && options.length === 0" class="text-xs text-muted-foreground">
        이동 가능한 빈 호실이 없습니다.
      </p>
    </div>
  </Modal>
</template>
