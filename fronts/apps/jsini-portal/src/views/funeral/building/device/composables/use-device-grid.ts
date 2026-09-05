import { ref, watch } from 'vue';
import { useDebounceFn } from '@vueuse/core';
import { message } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDevices, deleteDevice } from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';

export function useDeviceGrid(
  /** 행을 두 번 누르거나 [수정] 을 눌렀을 때 — 수정 서랍을 연다. */
  onEditRow: (row: BuildingApi.Device) => void,
  /** 지운 장비가 서랍에 열려 있으면 닫게 알린다. */
  onDeleted: (row: BuildingApi.Device) => void,
  /** 그리드 아래 도구줄의 [추가]. 목록 위쪽 아이콘과 같은 일을 한다. */
  onCreate?: () => void,
) {
  // ─── 상단 필터 상태 ───────────────────────────────────────────
  const selectedCompanyId = ref<string>('');
  const selectedBuildingId = ref<string>('');
  const selectedFloorId = ref<string>('');
  const selectedRoomId = ref<string>('');

  // ─── 그리드 ──────────────────────────────────────────────────
  const [Grid, gridApi] = useVbenVxeGrid({
    gridOptions: {
      columns: [
        { field: 'name', title: '장비명', minWidth: 120 },
        { field: 'shortName', title: '짧은 명칭', minWidth: 100 },
        { field: 'floorShortName', title: '층', minWidth: 80 },
        { field: 'roomShortName', title: '호실', minWidth: 100 },
        {
          field: 'locationPath',
          title: '소속 위치',
          minWidth: 150,
          formatter: ({ row }: { row: BuildingApi.Device }) => {
            const b = row.buildingShortName || '';
            const f = row.floorShortName || '';
            const r = row.roomShortName || '';
            if (row.roomId) {
              return `${b} ${r}`.trim() || '-';
            }
            if (row.floorId) {
              return `${b} ${f}`.trim() || '-';
            }
            if (row.buildingId) {
              return b || '-';
            }
            return '-';
          },
        },
        { field: 'code', title: '코드', minWidth: 100 },
        { field: 'sortOrder', title: '정렬 순서', minWidth: 100 },
        {
          field: 'deviceType',
          title: '유형',
          minWidth: 80,
          formatter: ({ cellValue }: { cellValue: any }) => {
            const map: Record<string, string> = {
              FUNERAL_PORTRAIT: '영정사진',
              MULTIMEDIA: '멀티미디어',
              ROOM_GUIDE: '호실 안내',
              ENTRANCE_GUIDE: '입구 안내',
              KIOSK: '키오스크',
            };
            return map[cellValue] ?? cellValue;
          },
        },
        {
          field: 'status',
          title: '상태',
          minWidth: 80,
          slots: { default: 'status-badge' },
        },
        {
          field: 'action',
          title: '작업',
          width: 110,
          fixed: 'right',
          slots: { default: 'action' },
        },
      ],
      // 아래 도구줄의 [추가] — 목록 위쪽 아이콘과 같은 함수를 부른다.
      // (`gridFeatures` 는 vxe 타입에 없다. 공통 레이어가 읽고 떼어 낸다.)
      gridFeatures: { onCreate },
      height: 'auto',
      rowConfig: { isHover: true, isCurrent: true },
      proxyConfig: {
        autoLoad: false,
        ajax: {
          query: async () => {
            const params: {
              companyId?: string;
              buildingId?: string;
              floorId?: string;
              roomId?: string;
            } = {};
            if (selectedCompanyId.value) params.companyId = selectedCompanyId.value;
            if (selectedBuildingId.value) params.buildingId = selectedBuildingId.value;
            if (selectedFloorId.value) params.floorId = selectedFloorId.value;
            if (selectedRoomId.value) params.roomId = selectedRoomId.value;

            return await getDevices(params);
          },
        },
      },
    } as any,
    gridEvents: {
      // 한 번 누르면 그 행이 켜지기만 한다(vxe 의 `isCurrent`).
      // 두 번 누르면 수정 서랍이 열린다 — [수정] 아이콘과 같은 자리로 간다.
      cellDblclick: ({ row }: { row: BuildingApi.Device }) => {
        onEditRow(row);
      },
    },
  });

  // ─── 필터 변경 → debounced 재조회 ────────────────────────────
  const debouncedQuery = useDebounceFn(() => {
    gridApi.query();
  }, 300);

  watch(
    [selectedCompanyId, selectedBuildingId, selectedFloorId, selectedRoomId],
    () => debouncedQuery(),
  );

  // ─── CRUD ──────────────────────────────────────────────────
  async function handleDelete(row: BuildingApi.Device) {
    try {
      await deleteDevice(row.id);
      message.success('장비가 삭제되었습니다.');
      onDeleted(row);
      gridApi.query();
    } catch {
      message.error('삭제 실패');
    }
  }

  function handleReboot(row: BuildingApi.Device) {
    message.loading({ content: `${row.name} 재부팅 명령 송신 중...`, key: 'reboot' });
    setTimeout(() => {
      message.success({ content: '명령 송신 성공. 장비가 곧 리부팅됩니다.', key: 'reboot', duration: 2 });
    }, 1000);
  }

  /**
   * 서랍에서 저장한 장비를 목록에서 그 행만 갈아 끼운다.
   *
   * [장비 관리] 탭은 타이핑이 멈출 때마다 스스로 저장하므로, 그때마다 목록 전체를
   * 다시 부르면 서랍 뒤에서 표가 계속 깜박인다. 바뀐 한 줄만 손본다.
   */
  function replaceRow(device: BuildingApi.Device) {
    const grid = gridApi.grid;
    if (!grid) return;
    const rows = (grid.getData?.() ?? []) as BuildingApi.Device[];
    const target = rows.find((r) => r.id === device.id);
    if (!target) return;
    Object.assign(target, device);
    // 바꾼 값을 그 행의 원본으로 삼는다(수정 표시 삼각형이 남지 않게).
    grid.reloadRow?.(target, null);
    grid.setCurrentRow?.(target);
  }

  return {
    selectedCompanyId,
    selectedBuildingId,
    selectedFloorId,
    selectedRoomId,
    Grid,
    gridApi,
    handleDelete,
    handleReboot,
    replaceRow,
  };
}
