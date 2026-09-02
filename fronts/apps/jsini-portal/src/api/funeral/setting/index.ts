import { unwrapList, unwrapOne } from '#/api/envelope';
import { requestClient } from '#/api/request';

/**
 * 환경설정 API — 계정별 장례식장 업무 설정.
 *
 * 옛 `page/ui_config.jsp` 의 오른쪽 표에 해당한다. 옛 시스템은 코드 여덟 개를
 * 두었는데, 그중 넷(탭 숨기기 · 사이드바 접기 등)은 vben 개인 환경설정이 이미
 * 하는 일이라 옮기지 않았다. 자세한 것은 docs/analysis/40-old-funeral-migration.md.
 */
export namespace SettingApi {
  /** 설정 한 줄 */
  export interface EnvironmentSetting {
    /** 설정 코드 (옛 conf_cd) */
    code: string;
    name: string;
    description?: string;
    /** 화면에서 구역을 나누는 묶음 이름 */
    groupName: string;
    enabled: boolean;
    /** 저장한 적 없을 때 쓰는 값 */
    defaultValue: boolean;
    updatedAt?: string;
  }
}

export async function getEnvironmentSettings() {
  return unwrapList<SettingApi.EnvironmentSetting>(
    await requestClient.get('/funeral/setting/environment/list'),
  );
}

/** 한 줄만 바꾼다 (스위치를 누르는 즉시 저장하는 화면용). */
export async function updateEnvironmentSetting(code: string, enabled: boolean) {
  return unwrapOne<SettingApi.EnvironmentSetting>(
    await requestClient.put(`/funeral/setting/environment/${code}`, { enabled }),
  );
}

/** 여러 줄을 한 번에 바꾼다 (저장 버튼 하나로 끝내는 화면용). */
export async function updateEnvironmentSettings(
  settings: Record<string, boolean>,
) {
  return unwrapList<SettingApi.EnvironmentSetting>(
    await requestClient.put('/funeral/setting/environment', { settings }),
  );
}
