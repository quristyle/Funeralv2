import { ref, watch } from 'vue';
import { useDebounceFn } from '@vueuse/core';
import { message } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDevices, createDevice, updateDevice, deleteDevice } from '#/api/building';
import type { BuildingApi } from '#/api/building';

export function useDeviceGrid(
  selectedDevice: ReturnType<typeof ref<BuildingApi.Device | null>>,
  onRowClick: (row: BuildingApi.Device) => void,
  onPanelClose: () => void,
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

            selectedDevice.value = null;
            return await getDevices(params);
          },
        },
      },
    },
    gridEvents: {
      cellClick: ({ row }: { row: BuildingApi.Device }) => {
        onRowClick(row);
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
      if (selectedDevice.value?.id === row.id) {
        onPanelClose();
      }
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

  async function handleSaveDevice(
    formModel: {
      id: string;
      name: string;
      shortName: string;
      code: string;
      deviceType: string;
      ipAddress: string;
      macAddress: string;
      status: 'ONLINE' | 'OFFLINE' | 'UNKNOWN';
      companyId: string;
      buildingId: string;
      floorId: string;
      roomId: string;
      sortOrder: number;
    },
    onSuccess: () => void,
  ) {
    try {
      if (formModel.id) {
        await updateDevice(formModel.id, formModel);
        message.success('장비 정보가 수정되었습니다.');
      } else {
        await createDevice(formModel);
        message.success('장비가 성공적으로 등록되었습니다.');
      }
      onSuccess();
      gridApi.query();
    } catch {
      message.error('저장 실패');
    }
  }

  function setCurrentRow(row: BuildingApi.Device) {
    gridApi.grid?.setCurrentRow?.(row);
  }

  function clearCurrentRow() {
    gridApi.grid?.clearCurrentRow?.();
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
    handleSaveDevice,
    setCurrentRow,
    clearCurrentRow,
  };
}
