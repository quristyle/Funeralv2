import { requestClient } from '#/api/request';

/**
 * 건물별 음원 배정 API (옛 `page/rsrc/music_build.jsp`).
 *
 * 음원 목록은 모든 건물이 공유하지만 실제로 트는 것은 건물마다 다르다.
 * 옛 화면은 위에 음원, 아래에 건물을 두고 건물 줄의 체크박스로 연결을 켰다.
 */
export namespace MusicBuildApi {
  /** 건물 한 줄. 고른 음원이 이 건물에 배정돼 있는지가 함께 온다. */
  export interface BuildingMapping {
    buildingId: string;
    buildingName: string;
    buildingShortName?: string;
    address?: string;
    sortOrder: number;
    mapped: boolean;
    mappingId?: string;
  }
}

/** 음원 하나를 고르면 건물 목록에 배정 여부가 붙어 온다. */
export async function getBuildingsForMusic(mediaSourceId: string) {
  return requestClient.get<MusicBuildApi.BuildingMapping[]>(
    `/funeral/building/music/${mediaSourceId}/buildings`,
  );
}

/** 음원 하나의 배정을 통째로 바꾼다. 목록에 없는 건물은 배정이 풀린다. */
export async function saveBuildingsForMusic(
  mediaSourceId: string,
  buildingIds: string[],
) {
  return requestClient.put<MusicBuildApi.BuildingMapping[]>(
    `/funeral/building/music/${mediaSourceId}/buildings`,
    { buildingIds },
  );
}

/** 한 건물에 배정된 음원 아이디 목록 */
export async function getMusicIdsForBuilding(buildingId: string) {
  return requestClient.get<string[]>(
    `/funeral/building/music/building/${buildingId}`,
  );
}
