import { ref, computed, watch } from 'vue';
import type { Dayjs } from 'dayjs';
import { getBuildings, getFloors, getRooms, getDevices, getDeceasedList } from '#/api/building';
import type { BuildingApi } from '#/api/building';

export function useStatusData() {
  // ─── 검색 필터 상태 ─────────────────────────────────────────────
  const searchForm = ref({
    companyId: '',
    buildingId: '',
    floorId: '',
    name: '',
  });

  const roomEnterDates = ref<[Dayjs, Dayjs] | undefined>(undefined);
  const funeralDates = ref<[Dayjs, Dayjs] | undefined>(undefined);

  // ─── 데이터 상태 ────────────────────────────────────────────────
  const loading = ref(false);
  const hasLoaded = ref(false);
  const buildings = ref<BuildingApi.Building[]>([]);
  const floors = ref<BuildingApi.Floor[]>([]);
  const roomStatuses = ref<any[]>([]);
  const devices = ref<BuildingApi.Device[]>([]);

  // ─── 아코디언 토글 상태 ──────────────────────────────────────────
  const collapsedBuildings = ref<Record<string, boolean>>({});

  function toggleBuilding(buildingId: string) {
    collapsedBuildings.value[buildingId] = !collapsedBuildings.value[buildingId];
  }

  // ─── Watch: 회사 필터 변경 ─────────────────────────────────────
  watch(
    () => searchForm.value.companyId,
    () => {
      searchForm.value.buildingId = '';
      searchForm.value.floorId = '';
      buildings.value = [];
      floors.value = [];
    }
  );

  // ─── Watch: 건물 필터 변경 ─────────────────────────────────────
  watch(
    () => searchForm.value.buildingId,
    async (newBId) => {
      searchForm.value.floorId = '';
      floors.value = [];
      if (newBId) {
        try {
          const res = await getFloors(newBId);
          floors.value = (res as any)?.result ?? res;
        } catch (err) {
          console.error('층 로드 실패:', err);
        }
      }
    }
  );

  // ─── 데이터 로드 및 구조화 ──────────────────────────────────────
  async function loadData() {
    loading.value = true;
    hasLoaded.value = true;
    try {
      // 1. 건물 목록 로드
      const buildingsRes = await getBuildings(searchForm.value.companyId || undefined);
      buildings.value = (buildingsRes as any)?.result ?? buildingsRes;

      // 2. 호실 목록 로드
      const roomsParams = {
        companyId: searchForm.value.companyId || undefined,
        buildingId: searchForm.value.buildingId || undefined,
        floorId: searchForm.value.floorId || undefined,
      };
      const roomsRes = await getRooms(roomsParams);
      const rooms = (roomsRes as any)?.result ?? roomsRes;

      // 3. 장비 목록 로드
      const devicesParams = {
        companyId: searchForm.value.companyId || undefined,
        buildingId: searchForm.value.buildingId || undefined,
        floorId: searchForm.value.floorId || undefined,
      };
      const devicesRes = await getDevices(devicesParams);
      devices.value = (devicesRes as any)?.result ?? devicesRes;

      // 4. 고인 목록 로드 (검색어 및 기간 포함)
      const deceasedParams: Record<string, any> = {};
      if (searchForm.value.companyId) deceasedParams.companyId = searchForm.value.companyId;
      if (searchForm.value.name) deceasedParams.name = searchForm.value.name;
      if (roomEnterDates.value && roomEnterDates.value.length === 2) {
        deceasedParams.roomEnterStartDate = roomEnterDates.value[0]?.format('YYYY-MM-DDT00:00:00');
        deceasedParams.roomEnterEndDate = roomEnterDates.value[1]?.format('YYYY-MM-DDT23:59:59');
      }
      if (funeralDates.value && funeralDates.value.length === 2) {
        deceasedParams.funeralStartDate = funeralDates.value[0]?.format('YYYY-MM-DDT00:00:00');
        deceasedParams.funeralEndDate = funeralDates.value[1]?.format('YYYY-MM-DDT23:59:59');
      }

      const deceasedRes = await getDeceasedList(deceasedParams);
      const deceasedList = (deceasedRes as any)?.result ?? deceasedRes;

      // 5. 프론트엔드 조인 결합
      roomStatuses.value = rooms.map((room: BuildingApi.Room) => {
        const currentDeceased = deceasedList.find(
          (d: any) => d.roomId === room.id && d.status !== 'COMPLETED'
        );
        const roomDevices = devices.value.filter((d: BuildingApi.Device) => d.roomId === room.id);

        return {
          ...room,
          deceased: currentDeceased,
          devices: roomDevices,
        };
      });
    } catch (err) {
      console.error('빈소현황 조회 실패:', err);
    } finally {
      loading.value = false;
    }
  }

  function onSearch() {
    loadData();
  }

  function onReset() {
    searchForm.value = {
      companyId: searchForm.value.companyId,
      buildingId: '',
      floorId: '',
      name: '',
    };
    roomEnterDates.value = undefined;
    funeralDates.value = undefined;
    loadData();
  }

  // ─── 필터링된 건물 목록 ──────────────────────────────────────────
  const filteredBuildings = computed(() => {
    if (searchForm.value.buildingId) {
      return buildings.value.filter((b) => b.id === searchForm.value.buildingId);
    }
    return buildings.value;
  });

  // ─── 건물별 호실 매핑 ────────────────────────────────────────────
  function getRoomsByBuilding(buildingId: string) {
    return roomStatuses.value.filter((r) => r.buildingId === buildingId);
  }

  // ─── 건물별 현황 통계 요약 계산 ───────────────────────────────────
  function getBuildingSummary(buildingId: string) {
    const rooms = getRoomsByBuilding(buildingId);
    const total = rooms.length;
    const active = rooms.filter((r) => r.deceased).length;
    const empty = total - active;

    const deviceSummary: Record<string, number> = {
      FUNERAL_PORTRAIT: 0,
      MULTIMEDIA: 0,
      ROOM_GUIDE: 0,
      ENTRANCE_GUIDE: 0,
      KIOSK: 0,
    };

    for (const r of rooms) {
      if (Array.isArray(r.devices)) {
        for (const d of r.devices) {
          if (d.deviceType in deviceSummary) {
            deviceSummary[d.deviceType] = (deviceSummary[d.deviceType] ?? 0) + 1;
          }
        }
      }
    }

    return {
      total,
      active,
      empty,
      deviceSummary,
    };
  }

  return {
    searchForm,
    roomEnterDates,
    funeralDates,
    loading,
    hasLoaded,
    buildings,
    floors,
    roomStatuses,
    collapsedBuildings,
    toggleBuilding,
    onSearch,
    onReset,
    filteredBuildings,
    getRoomsByBuilding,
    getBuildingSummary,
    devices,
  };
}
