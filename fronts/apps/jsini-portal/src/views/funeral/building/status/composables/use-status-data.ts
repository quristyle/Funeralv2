import type { Dayjs } from 'dayjs';

import type { BuildingApi } from '#/api/funeral/building';
import type { StatusApi } from '#/api/funeral/status';

import { computed, ref, watch } from 'vue';

import { useIsMobile } from '@vben/hooks';

import dayjs from 'dayjs';

import { fetchBizOptions } from '#/api/biz-select';
import { getBuildings } from '#/api/funeral/building';
import { getRoomBoard } from '#/api/funeral/status';

/**
 * 밀도 — 한 화면에 몇 곳을 담느냐로 배치가 갈린다 (47번 문서 5단계).
 *
 * | 값 | 이름 | 쓰는 때 | 조작 |
 * |---|---|---|---|
 * | `manage` | 운영 | 시설 1곳 | 화면에서 직접 |
 * | `watch` | 감시 | 시설 2~4곳 | 타일을 눌러 상세 패널에서 |
 * | `board` | 상황판 | 시설 5곳 이상 | 시설로 내려가서 |
 *
 * 셋은 **같은 데이터와 같은 액션**을 쓴다. 그리는 크기만 다르다.
 */
export type Density = 'board' | 'manage' | 'watch';

/** 호실 한 칸의 상태 — 색 하나로 줄인 것. 우선순위는 위에서부터다. */
export type RoomState = 'empty' | 'offline' | 'soon' | 'using';

/** 발인이 이 시간 안에 남았으면 '임박'으로 본다. */
const DISCHARGE_SOON_HOURS = 3;

const STORAGE_KEY = 'jsini.room-status.view';

/** 호실 카드 하나가 그리는 모양 — 호실 + 현재 고인 + 장비 */
export interface RoomStatusRow {
  id: string;
  name: string;
  shortName?: string;
  floorId?: string;
  floorName?: string;
  buildingId?: string;
  buildingName?: string;
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
  /** 장비 수 — `detail=summary` 라 목록이 비어 와도 서버가 센 값은 있다 */
  deviceCount: number;
  onlineDeviceCount: number;
}

/** 시설(=건물) 하나. 관제의 기본 단위다. */
export interface FacilityGroup {
  id: string;
  name: string;
  companyId: string;
  companyName: string;
  rooms: RoomStatusRow[];
  /** 운영 밀도에서만 쓰는 층 묶음 */
  floors: { floorId: string; floorName: string; rooms: RoomStatusRow[] }[];
  commonDevices: BuildingApi.Device[];
  summary: FacilitySummary;
}

export interface FacilitySummary {
  total: number;
  using: number;
  empty: number;
  deviceTotal: number;
  deviceOffline: number;
  /** 오늘 발인 */
  dischargeToday: number;
  /** 오늘 입관 */
  coffinToday: number;
  /** 발인 임박(3시간 내) */
  dischargeSoon: number;
  /** 눈에 띄어야 하는 것의 합 — 시설 정렬 기준 */
  alertCount: number;
}

/** 회사 하나. 여러 운영사를 한 계정이 볼 때만 겉으로 드러난다. */
export interface CompanyGroup {
  id: string;
  name: string;
  facilities: FacilityGroup[];
}


interface PersistedView {
  density?: Density | 'auto';
  facilityIds?: string[];
  collapsed?: Record<string, boolean>;
  sortByAlert?: boolean;
}

function readPersisted(): PersistedView {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as PersistedView) : {};
  } catch {
    return {};
  }
}

export function useStatusData() {
  const saved = readPersisted();
  const { isMobile } = useIsMobile();

  // ─── 검색 필터 상태 ─────────────────────────────────────────────
  const searchForm = ref({
    companyId: '',
    floorId: '',
    name: '',
  });

  /**
   * 보고 있는 시설들. 비어 있으면 '전체'다.
   *
   * 예전에는 건물 셀렉트 하나여서 '전체' 아니면 한 곳뿐이었다. 여러 곳을 위탁
   * 운영하면 "다섯 중 관심 있는 둘" 이 안 됐다. 거르기는 화면에서 한다 —
   * 조회는 어차피 회사 범위로 한 번 나가고, 그래야 시설을 껐다 켤 때
   * 서버를 다시 부르지 않는다.
   */
  const selectedFacilityIds = ref<string[]>(saved.facilityIds ?? []);

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
  /** 회사 id → 이름. 회사 표는 AuthServer 것이라 장례 API 가 붙여 줄 수 없다. */
  const companyNames = ref<Record<string, string>>({});
  /** 서버가 센 전체 합계. 화면에서 다시 세지 않는다. */
  const serverSummary = ref<StatusApi.Summary | null>(null);

  /** 남은 시간 표시가 멈추지 않도록 폴링과 같이 밀어 준다. */
  const now = ref(dayjs());
  function touchNow() {
    now.value = dayjs();
  }

  // ─── 보기 상태 (브라우저에 남는다) ────────────────────────────────
  const densityOverride = ref<Density | 'auto'>(saved.density ?? 'auto');
  const collapsedFacilities = ref<Record<string, boolean>>(saved.collapsed ?? {});
  const sortByAlert = ref<boolean>(saved.sortByAlert ?? false);

  function persist() {
    try {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          density: densityOverride.value,
          facilityIds: selectedFacilityIds.value,
          collapsed: collapsedFacilities.value,
          sortByAlert: sortByAlert.value,
        } satisfies PersistedView),
      );
    } catch {
      // 사파리 프라이빗 등 저장이 막힌 환경 — 보기 상태일 뿐이라 그냥 넘어간다.
    }
  }

  watch([densityOverride, selectedFacilityIds, collapsedFacilities, sortByAlert], persist, {
    deep: true,
  });

  function toggleFacility(facilityId: string) {
    collapsedFacilities.value[facilityId] = !collapsedFacilities.value[facilityId];
  }

  // ─── Watch: 회사 필터 변경 ─────────────────────────────────────
  watch(
    () => searchForm.value.companyId,
    () => {
      searchForm.value.floorId = '';
      selectedFacilityIds.value = [];
      buildings.value = [];
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

  /**
   * 회사 id → 이름.
   *
   * 장례 API 는 회사 이름을 모른다 — `scom.companies` 는 AuthServer 것이고
   * `smfr.buildings` 에는 `company_id` 만 있다. 화면이 회사 셀렉트를 이미
   * 같은 통로로 받으므로 그것을 한 번 더 쓴다. MSA 경계를 넘는 조인을
   * 백엔드에 새로 만드는 것보다 싸다.
   */
  async function loadCompanyNames() {
    if (Object.keys(companyNames.value).length > 0) return;
    try {
      const { options } = await fetchBizOptions('funeralCompany');
      const map: Record<string, string> = {};
      for (const o of options) {
        if (o.value) map[String(o.value)] = o.label;
      }
      companyNames.value = map;
    } catch (err) {
      console.error('회사명 매핑 로드 실패:', err);
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
      buildingName: row.buildingName,
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
      deviceCount: row.deviceCount,
      onlineDeviceCount: row.onlineDeviceCount,
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
      await Promise.all([loadMultimediaOptions(), loadCompanyNames()]);

      // 건물 목록은 회사 소속을 알아내는 데 쓴다 — 호실은 회사를 모르고
      // 건물만 안다. 조회 자체는 회사 범위로 한 번만 나간다.
      const [buildingList, board] = await Promise.all([
        getBuildings(searchForm.value.companyId || undefined),
        getRoomBoard({
          companyId: searchForm.value.companyId || undefined,
          floorId: searchForm.value.floorId || undefined,
          name: searchForm.value.name || undefined,
          // 화면 라벨은 '입실 기간'이지만 거르는 컬럼은 입관 일시다 (기존 동작 유지).
          coffinStartDate: roomEnterDates.value?.[0]?.format('YYYY-MM-DDT00:00:00'),
          coffinEndDate: roomEnterDates.value?.[1]?.format('YYYY-MM-DDT23:59:59'),
          burialStartDate: funeralDates.value?.[0]?.format('YYYY-MM-DDT00:00:00'),
          burialEndDate: funeralDates.value?.[1]?.format('YYYY-MM-DDT23:59:59'),
          // 상황판만 줄여 받는다.
          //
          // 감시 밀도도 타일만 그리지만, **타일을 누르면 옆에 RoomColumn 이 통째로
          // 열린다** — 거기에 영정·장지·상주·장비 미디어가 다 필요하다. 줄여 받으면
          // 그 패널이 빈 껍데기가 된다. 감시는 시설 넷까지라 전량이어도 부담이 작고,
          // 페이로드가 문제 되는 것은 시설 수십 곳을 60초마다 다시 받는 상황판이다.
          detail: density.value === 'board' ? 'summary' : 'full',
        }),
      ]);

      buildings.value = buildingList;
      const rows = board?.rooms ?? [];
      roomStatuses.value = rows.map(toRow);
      devices.value = [
        ...(board?.commonDevices ?? []),
        ...rows.flatMap((r) => r.devices ?? []),
      ];
      serverSummary.value = board?.summary ?? null;
      touchNow();
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
      floorId: '',
      name: '',
    };
    selectedFacilityIds.value = [];
    roomEnterDates.value = undefined;
    funeralDates.value = undefined;
    await loadData();
  }

  // ─── 시설 · 회사 묶기 ────────────────────────────────────────────

  /** 건물 id → 회사 id. 호실은 건물만 알기 때문에 한 번 거친다. */
  const companyIdByBuilding = computed(() => {
    const map: Record<string, string> = {};
    for (const b of buildings.value) map[b.id] = b.companyId;
    return map;
  });

  function isDischargeSoon(row: RoomStatusRow) {
    const t = row.deceased?.dischargeTime;
    if (!t) return false;
    const diff = dayjs(t).diff(now.value, 'minute');
    return diff >= 0 && diff <= DISCHARGE_SOON_HOURS * 60;
  }

  function isSameDay(value: string | undefined) {
    return value ? dayjs(value).isSame(now.value, 'day') : false;
  }

  /** 호실 한 칸의 색 하나. 급한 것이 이긴다. */
  function roomStateOf(row: RoomStatusRow): RoomState {
    if (row.deviceCount > 0 && row.onlineDeviceCount < row.deviceCount) return 'offline';
    if (!row.deceased) return 'empty';
    if (isDischargeSoon(row)) return 'soon';
    return 'using';
  }

  /**
   * 시설 하나의 숫자.
   *
   * 장비는 **호실 배정분 + 그 시설의 공용분**이다. 공용분을 빼면 시설을 하나로
   * 좁혔을 때의 합계가 전체 배너(서버가 센 것)와 어긋난다 — 입구 안내처럼
   * 호실에 안 매인 장비가 그때만 사라져 보인다.
   */
  function summarize(
    rooms: RoomStatusRow[],
    commonDevices: BuildingApi.Device[],
  ): FacilitySummary {
    const using = rooms.filter((r) => r.deceased).length;
    const deviceTotal =
      rooms.reduce((n, r) => n + r.deviceCount, 0) + commonDevices.length;
    const deviceOnline =
      rooms.reduce((n, r) => n + r.onlineDeviceCount, 0) +
      commonDevices.filter((d) => d.status === 'ONLINE').length;
    const deviceOffline = deviceTotal - deviceOnline;
    const dischargeToday = rooms.filter((r) => isSameDay(r.deceased?.dischargeTime)).length;
    const coffinToday = rooms.filter((r) => isSameDay(r.deceased?.coffinTime)).length;
    const dischargeSoon = rooms.filter((r) => isDischargeSoon(r)).length;

    return {
      total: rooms.length,
      using,
      empty: rooms.length - using,
      deviceTotal,
      deviceOffline,
      dischargeToday,
      coffinToday,
      dischargeSoon,
      alertCount: deviceOffline + dischargeSoon,
    };
  }

  /**
   * 조회 결과에 실제로 호실이 있는 시설만 만든다.
   *
   * 예전에는 건물 목록 전량을 섹션으로 폈다 — 호실이 없는 건물도
   * "등록된 호실이 없습니다" 로 자리를 먹었다. 시설이 여럿이면 그만큼
   * 관제 화면이 밀려난다.
   */
  const allFacilities = computed<FacilityGroup[]>(() => {
    const byBuilding = new Map<string, RoomStatusRow[]>();
    for (const room of roomStatuses.value) {
      const key = room.buildingId ?? 'unknown';
      const list = byBuilding.get(key) ?? [];
      list.push(room);
      byBuilding.set(key, list);
    }

    const result: FacilityGroup[] = [];
    for (const [buildingId, rooms] of byBuilding) {
      const companyId = companyIdByBuilding.value[buildingId] ?? '';
      const floorMap = new Map<string, { floorId: string; floorName: string; rooms: RoomStatusRow[] }>();
      for (const room of rooms) {
        const floorId = room.floorId || 'unknown';
        if (!floorMap.has(floorId)) {
          floorMap.set(floorId, { floorId, floorName: room.floorName || '기타', rooms: [] });
        }
        floorMap.get(floorId)!.rooms.push(room);
      }

      const commonDevices = devices.value.filter(
        (d) => d.buildingId === buildingId && !d.roomId,
      );

      result.push({
        id: buildingId,
        name: rooms[0]?.buildingName || '시설 미지정',
        companyId,
        companyName: companyNames.value[companyId] ?? '',
        rooms,
        floors: [...floorMap.values()],
        commonDevices,
        summary: summarize(rooms, commonDevices),
      });
    }

    return result.sort((a, b) => {
      const byCompany = a.companyName.localeCompare(b.companyName);
      return byCompany === 0 ? a.name.localeCompare(b.name) : byCompany;
    });
  });

  /**
   * 브라우저에 남아 있던 시설 선택 중 **지금 조회 결과에 없는 것을 걷어낸다.**
   *
   * 선택은 localStorage 에 남는데 회사를 바꾸거나 시설이 지워지면 그 id 는
   * 어디에도 없다. 그대로 두면 필터가 아무것도 못 골라 화면이 빈 채로 뜨고,
   * 사용자는 왜 비었는지 알 길이 없다 — 선택 칩조차 이름 대신 UUID 로 보인다.
   */
  watch(allFacilities, (list) => {
    if (!hasLoaded.value || list.length === 0) return;
    const known = new Set(list.map((f) => f.id));
    const kept = selectedFacilityIds.value.filter((id) => known.has(id));
    if (kept.length !== selectedFacilityIds.value.length) {
      selectedFacilityIds.value = kept;
    }
  });

  /** 시설 선택을 반영한 것. 화면은 전부 이것을 본다. */
  const facilities = computed(() => {
    const picked = new Set(selectedFacilityIds.value);
    const list =
      picked.size === 0
        ? allFacilities.value
        : allFacilities.value.filter((f) => picked.has(f.id));

    if (!sortByAlert.value) return list;
    // 이상 많은 시설을 위로. 같으면 이름순이라 순서가 튀지 않는다.
    return [...list].sort(
      (a, b) =>
        b.summary.alertCount - a.summary.alertCount || a.name.localeCompare(b.name),
    );
  });

  /**
   * 회사 > 시설 2단.
   *
   * 회사가 하나면 화면은 이 단을 접는다 — 한 곳만 위탁 운영하는 대부분의
   * 경우에 쓸데없는 머리글이 생기지 않게.
   */
  const companyGroups = computed<CompanyGroup[]>(() => {
    const map = new Map<string, CompanyGroup>();
    for (const f of facilities.value) {
      if (!map.has(f.companyId)) {
        map.set(f.companyId, {
          id: f.companyId,
          name: f.companyName || '회사 미지정',
          facilities: [],
        });
      }
      map.get(f.companyId)!.facilities.push(f);
    }
    return [...map.values()];
  });

  const multiCompany = computed(() => companyGroups.value.length > 1);

  // ─── 밀도 ────────────────────────────────────────────────────────

  /**
   * 조회 결과가 정하는 기본 밀도 (47번 문서 5단계의 표).
   *
   * **문턱이 화면 폭에 따라 다르다.** 같은 일곱 호실이라도 1920px 에서는 컬럼
   * 일곱이 한 줄에 들어가지만 휴대폰에서는 한 줄에 하나다 — 운영 밀도로 그리면
   * 세로 2,000px 을 넘겨 관제가 아니라 스크롤이 된다. 그래서 모바일에서는
   * 같은 수라도 한 단계 촘촘한 쪽을 고른다.
   */
  const autoDensity = computed<Density>(() => {
    const facilityCount = facilities.value.length;
    const roomCount = facilities.value.reduce((n, f) => n + f.rooms.length, 0);

    if (isMobile.value) {
      if (facilityCount >= 3 || roomCount > 18) return 'board';
      if (facilityCount >= 2 || roomCount > 6) return 'watch';
      return 'manage';
    }

    if (facilityCount >= 5 || roomCount > 40) return 'board';
    if (facilityCount >= 2 || roomCount > 12) return 'watch';
    return 'manage';
  });

  const density = computed<Density>(() =>
    densityOverride.value === 'auto' ? autoDensity.value : densityOverride.value,
  );

  function setDensity(value: Density | 'auto') {
    densityOverride.value = value;
  }

  // 상황판만 줄여 받으므로, 그 경계를 넘을 때만 다시 받는다 — 안 그러면
  // 상황판에서 올라왔을 때 영정과 장비가 비어 보인다.
  watch(
    () => density.value === 'board',
    () => {
      if (hasLoaded.value) void loadData(true);
    },
  );

  /**
   * 시설 하나로 내려간다 — 레인·스트립 머리글의 진입점.
   * 자동 규칙이 곧바로 '운영'을 고르므로 밀도는 다시 자동으로 되돌린다.
   */
  function drillIntoFacility(facilityId: string) {
    selectedFacilityIds.value = [facilityId];
    densityOverride.value = 'auto';
  }

  /** 드릴다운 해제 — 전체 시설로 돌아간다. */
  function clearFacilityFilter() {
    selectedFacilityIds.value = [];
  }

  // ─── 전역 요약 ──────────────────────────────────────────────────

  /**
   * 맨 위 배너.
   *
   * 시설을 고르지 않았으면 서버가 센 값(`RoomBoard.summary`)을 그대로 쓴다 —
   * 예전에는 이 값을 받아 놓고 버린 뒤 화면에서 다시 셌다. 시설을 골랐을
   * 때만 고른 것들로 합친다.
   */
  const globalSummary = computed(() => {
    const picked = selectedFacilityIds.value.length > 0;
    const s = serverSummary.value;
    if (!picked && s) {
      return {
        total: s.totalRooms,
        using: s.usingRooms,
        empty: s.emptyRooms,
        deviceTotal: s.totalDevices,
        deviceOffline: s.totalDevices - s.onlineDevices,
        dischargeToday: facilities.value.reduce((n, f) => n + f.summary.dischargeToday, 0),
        coffinToday: facilities.value.reduce((n, f) => n + f.summary.coffinToday, 0),
        dischargeSoon: facilities.value.reduce((n, f) => n + f.summary.dischargeSoon, 0),
        facilityCount: facilities.value.length,
        companyCount: companyGroups.value.length,
      };
    }

    const acc = facilities.value.reduce(
      (a, f) => ({
        total: a.total + f.summary.total,
        using: a.using + f.summary.using,
        empty: a.empty + f.summary.empty,
        deviceTotal: a.deviceTotal + f.summary.deviceTotal,
        deviceOffline: a.deviceOffline + f.summary.deviceOffline,
        dischargeToday: a.dischargeToday + f.summary.dischargeToday,
        coffinToday: a.coffinToday + f.summary.coffinToday,
        dischargeSoon: a.dischargeSoon + f.summary.dischargeSoon,
      }),
      {
        total: 0,
        using: 0,
        empty: 0,
        deviceTotal: 0,
        deviceOffline: 0,
        dischargeToday: 0,
        coffinToday: 0,
        dischargeSoon: 0,
      },
    );
    return {
      ...acc,
      facilityCount: facilities.value.length,
      companyCount: companyGroups.value.length,
    };
  });


  // ─── 장비 상태 로컬 갱신 ──────────────────────────────────────────

  /** devices 갱신분을 호실 카드에도 반영한다 (전체 재조회 깜빡임 방지). */
  function syncRoomDevices() {
    roomStatuses.value = roomStatuses.value.map((room) => {
      const list = devices.value
        .filter((d) => d.roomId === room.id)
        .sort((a: any, b: any) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
      // `detail=summary` 로 받은 화면은 장비 목록이 비어 있다. 그때는 서버가
      // 센 수를 그대로 둔다 — 빈 목록으로 덮으면 오프라인 표시가 사라진다.
      if (list.length === 0 && room.devices.length === 0) return room;
      return {
        ...room,
        devices: list,
        deviceCount: list.length,
        onlineDeviceCount: list.filter((d: any) => d.status === 'ONLINE').length,
      };
    });
  }

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

  function updateDeviceStatusState(deviceCode: string, status: string) {
    devices.value = devices.value.map((d) =>
      d.code === deviceCode ? ({ ...d, status } as any) : d,
    );
    syncRoomDevices();
  }

  return {
    // 필터
    searchForm,
    selectedFacilityIds,
    roomEnterDates,
    funeralDates,
    onSearch,
    onReset,
    reloadSilently,
    // 조회 상태
    loading,
    hasLoaded,
    buildings,
    roomStatuses,
    devices,
    videos,
    musics,
    companyNames,
    now,
    touchNow,
    isMobile,
    // 묶음
    allFacilities,
    facilities,
    companyGroups,
    multiCompany,
    globalSummary,
    roomStateOf,
    isDischargeSoon,
    // 보기 상태
    density,
    autoDensity,
    densityOverride,
    setDensity,
    collapsedFacilities,
    toggleFacility,
    sortByAlert,
    drillIntoFacility,
    clearFacilityFilter,
    // 장비 로컬 갱신
    updateDeviceMediaState,
    updateDeviceStatusState,
  };
}
