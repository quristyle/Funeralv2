import type {
  CustomPreferencesRecord,
  Preferences,
  PreferencesExtension,
} from '@vben-core/preferences';
import type { DeepPartial } from '@vben-core/typings';

/**
 * 모든 앱에서 동일한 기본 설정(Preferences)을 사용하려면 여기서 정의할 수 있습니다.
 * @vben-core/preferences의 기본 설정을 직접 수정하는 대신 사용하십시오.
 * @param preferences
 * @returns
 */

function defineOverridesPreferences(preferences: DeepPartial<Preferences>) {
  return preferences;
}

function definePreferencesExtension<
  TCustomPreferences extends object = CustomPreferencesRecord,
>(extension: PreferencesExtension<TCustomPreferences>) {
  return extension;
}

export {
  defineOverridesPreferences,
  definePreferencesExtension,
};

export * from '@vben-core/preferences';
