import type { Dayjs } from 'dayjs';

import type { BuildingApi } from '#/api/funeral/building';
import type { StatusApi } from '#/api/funeral/status';

import { computed, ref, watch } from 'vue';

import { fetchBizOptions } from '#/api/biz-select';
import { getBuildings } from '#/api/funeral/building';
import { getRoomBoard } from '#/api/funeral/status';

/** 호실 카드 하나가 그리는 모양 — 호실 + 현재 고인 + 장비 */
export interface RoomStatusRow {
  id: string;
  name: string;
  shortName?: string;
  floorId?: string;
  floorName?: string;
  buildingId?: string;
  /** 빈 호실의 마지막 퇴실 일시 */
  lastVacatedAt?: string;
  /** 빈 호실에서 마지막으로 출상한 고인 — 출상 취소 진입점 */
  lastDepartedDeceasedId?: string;
  lastDepartedDeceasedName?: string;
  deceased?: {
    id: string;
    name?: string;
    gender?: string;
    age?: number;
    religion?: string;
    /** DeceasedStatus 정본 셋 중 하나 */
    status?: string;
    deathDate?: string;
    /** 입관 일시 */
    coffinTime?: string;
    /** 발인 일시 */
    dischargeTime?: string;
    burialPlace?: string;
    chiefMourner?: string;
    /** 영정 — 서버가 보정본 우선으로 골라 준 것 */
    photoFileId?: string;
    photoUrl?: string;
  };
  devices: BuildingApi.Device[];
}

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
  const roomStatuses = ref<RoomStatusRow[]>([]);
  /** 조회 범위의 모든 장비 — 호실 배정분 + 건물 공용분 */
  const devices = ref<BuildingApi.Device[]>([]);
  const videos = ref<{ label: string; value: any }[]>([]);
  const musics = ref<{ label: string; value: any }[]>([]);

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
    },
  );

  // ─── Watch: 건물 필터 변경 (층 선택 초기화 — 목록은 BizSelect 가 받아 온다) ──
  watch(
    () => searchForm.value.buildingId,
    () => {
      searchForm.value.floorId = '';
    },
  );

  /**
   * 영상 · 음원 드롭다운 목록.
   *
   * **`fetchBizOptions` 하나만 부른다.** 예전에는 설정에서 `apiUrl` 만 꺼내
   * `requestClient` 로 직접 불렀는데, 그러면 업무 선택 설정 표의 나머지 칸을
   * 전부 무시한다.
   *
   * | 무시했던 칸 | 그래서 생긴 일 |
   * |---|---|
   * | `serviceCode` | 서비스 접두사가 안 붙어 `/api/building/source/list` 로 나가 **404** |
   * | `staticParams` | 조건을 못 보내 `?type=VIDEO` 를 `apiUrl` 안에 박아 둬야 했다 |
   * | `resultPath` | 목록 위치를 손으로 더듬었다 (`res?.result ?? res`) |
   *
   * 접두사 조립은 `api/biz-select.ts` 의 `call()` 이 `serviceCode` 로 한다
   * (헬프데스크 · 프로젝트관리는 봉투가 달라 전용 클라이언트로 갈라진다 —
   * URL 만 맞춰서는 안 되는 이유다).
   */
  async function loadMultimediaOptions() {
    if (videos.value.length > 0 && musics.value.length > 0) return;
    try {
      const [video, music] = await Promise.all([
        fetchBizOptions('video'),
        fetchBizOptions('music'),
      ]);
      videos.value = video.options;
      musics.value = music.options;
    } catch (err) {
      console.error('멀티미디어 드롭다운 로드 실패:', err);
    }
  }

  /** 서버 조인 응답 한 행을 카드가 그리는 모양으로 옮긴다. */
  function toRow(row: StatusApi.FuneralStatus): RoomStatusRow {
    return {
      id: row.roomId,
      name: row.roomName,
      shortName: row.roomShortName ?? undefined,
      floorId: row.floorId,
      floorName: row.floorName,
      buildingId: row.buildingId,
      lastVacatedAt: row.lastVacatedAt,
      lastDepartedDeceasedId: row.lastDepartedDeceasedId,
      lastDepartedDeceasedName: row.lastDepartedDeceasedName,
      deceased: row.deceasedId
        ? {
            id: row.deceasedId,
            name: row.deceasedName,
            gender: row.deceasedGender,
            age: row.deceasedAge,
            religion: row.religion,
            status: row.deceasedStatus,
            deathDate: row.deathDate,
            coffinTime: row.coffinTime,
            dischargeTime: row.dischargeTime,
            burialPlace: row.burialPlace,
            chiefMourner: row.chiefMourner,
            photoFileId: row.photoFileId,
            photoUrl: row.photoUrl,
          }
        : undefined,
      devices: row.devices ?? [],
    };
  }

  // ─── 데이터 로드 ────────────────────────────────────────────────
  // 서버 조인 API 하나(`/status/room-board`)로 받는다. 예전에는 건물·호실·
  // 장비·고인 네 목록을 받아 브라우저에서 조인했다 (47번 문서 0단계).
  // silent 면 스피너 없이 갱신한다 — 자동 폴링·SignalR 재조회용 (4단계).
  async function loadData(silent = false) {
    if (!silent) loading.value = true;
    hasLoaded.value = true;
    try {
      await loadMultimediaOptions();

      // 건물 목록은 섹션 골격이다 — 호실이 없는 건물도 섹션으로 보여야 해서 따로 받는다.
      const [buildingList, board] = await Promise.all([
        getBuildings(searchForm.value.companyId || undefined),
        getRoomBoard({
          companyId: searchForm.value.companyId || undefined,
          buildingId: searchForm.value.buildingId || undefined,
          floorId: searchForm.value.floorId || undefined,
          name: searchForm.value.name || undefined,
          // 화면 라벨은 '입실 기간'이지만 거르는 컬럼은 입관 일시다 (기존 동작 유지).
          coffinStartDate: roomEnterDates.value?.[0]?.format('YYYY-MM-DDT00:00:00'),
          coffinEndDate: roomEnterDates.value?.[1]?.format('YYYY-MM-DDT23:59:59'),
          burialStartDate: funeralDates.value?.[0]?.format('YYYY-MM-DDT00:00:00'),
          burialEndDate: funeralDates.value?.[1]?.format('YYYY-MM-DDT23:59:59'),
        }),
      ]);

      buildings.value = buildingList;
      const rows = board?.rooms ?? [];
      roomStatuses.value = rows.map(toRow);
      devices.value = [
        ...(board?.commonDevices ?? []),
        ...rows.flatMap((r) => r.devices ?? []),
      ];
    } catch (err) {
      console.error('빈소현황 조회 실패:', err);
    } finally {
      loading.value = false;
    }
  }

  async function onSearch() {
    await loadData();
  }

  /** 스피너 없는 재조회 — 폴링·SignalR·탭 복귀가 쓴다. */
  async function reloadSilently() {
    if (!hasLoaded.value || loading.value) return;
    await loadData(true);
  }

  async function onReset() {
    searchForm.value = {
      companyId: searchForm.value.companyId,
      buildingId: '',
      floorId: '',
      name: '',
    };
    roomEnterDates.value = undefined;
    funeralDates.value = undefined;
    await loadData();
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
      for (const d of r.devices) {
        if (d.deviceType in deviceSummary) {
          deviceSummary[d.deviceType] = (deviceSummary[d.deviceType] ?? 0) + 1;
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

  /** devices 갱신분을 호실 카드에도 반영한다 (전체 재조회 깜빡임 방지). */
  function syncRoomDevices() {
    roomStatuses.value = roomStatuses.value.map((room) => ({
      ...room,
      devices: devices.value
        .filter((d) => d.roomId === room.id)
        .sort((a: any, b: any) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)),
    }));
  }

  // ─── 장비 미디어 상태 로컬 갱신 ───────────────────────────────────
  function updateDeviceMediaState(
    deviceId: string,
    type: 'music' | 'video',
    mediaId: null | string,
    mediaName: null | string,
  ) {
    devices.value = devices.value.map((d) => {
      if (d.id !== deviceId) return d;
      return type === 'video'
        ? ({ ...d, videoId: mediaId || null, videoName: mediaName || null, isVideoEnabled: !!mediaId } as any)
        : ({ ...d, musicId: mediaId || null, musicName: mediaName || null, isMusicEnabled: !!mediaId } as any);
    });
    syncRoomDevices();
  }

  // ─── 장비 온라인 상태 실시간 로컬 갱신 ───────────────────────────────
  function updateDeviceStatusState(deviceCode: string, status: string) {
    devices.value = devices.value.map((d) =>
      d.code === deviceCode ? ({ ...d, status } as any) : d,
    );
    syncRoomDevices();
  }

  return {
    searchForm,
    roomEnterDates,
    funeralDates,
    loading,
    hasLoaded,
    buildings,
    roomStatuses,
    collapsedBuildings,
    toggleBuilding,
    onSearch,
    onReset,
    reloadSilently,
    filteredBuildings,
    getRoomsByBuilding,
    getBuildingSummary,
    devices,
    videos,
    musics,
    updateDeviceMediaState,
    updateDeviceStatusState,
  };
}
