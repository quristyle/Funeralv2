import type { DeepPartial } from '@vben-core/typings';

import type { InitialOptions, Preferences } from './types';

import { markRaw, reactive, readonly, watch } from 'vue';

import { StorageManager } from '@vben-core/shared/cache';
import { isMacOs, merge } from '@vben-core/shared/utils';

import {
  breakpointsTailwind,
  useBreakpoints,
  useDebounceFn,
} from '@vueuse/core';

import { defaultPreferences } from './config';
import { updateCSSVariables } from './update-css-variables';

const STORAGE_KEYS = {
  MAIN: 'preferences',
  LOCALE: 'preferences-locale',
  THEME: 'preferences-theme',
} as const;

class PreferenceManager {
  private cache: StorageManager;
  private debouncedSave: (preference: Preferences) => void;
  private initialPreferences: Preferences = defaultPreferences;
  private isInitialized = false;
  private state: Preferences;

  constructor() {
    this.cache = new StorageManager();
    this.state = reactive<Preferences>(
      this.loadFromCache() || { ...defaultPreferences },
    );
    this.debouncedSave = useDebounceFn(
      (preference) => this.saveToCache(preference),
      150,
    );
  }

  /**
   * 캐시된 모든 환경 설정을 삭제합니다.
   */
  clearCache = () => {
    Object.values(STORAGE_KEYS).forEach((key) => this.cache.removeItem(key));
  };

  /**
   * 초기 환경 설정을 가져옵니다.
   */
  getInitialPreferences = () => {
    return this.initialPreferences;
  };

  /**
   * 현재 환경 설정을 가져옵니다. (읽기 전용)
   */
  getPreferences = () => {
    return readonly(this.state);
  };

  /**
   * 환경 설정을 초기화합니다.
   * @param options - 초기화 설정 항목
   * @param options.namespace - 여러 애플리케이션의 설정을 격리하기 위한 네임스페이스
   * @param options.overrides - 덮어쓸 환경 설정
   */
  initPreferences = async ({ namespace, overrides }: InitialOptions) => {
    // 중복 초기화 방지
    if (this.isInitialized) {
      return;
    }

    // 네임스페이스를 사용하여 스토리지 관리자 초기화
    this.cache = new StorageManager({ prefix: namespace });

    // 초기 환경 설정 병합
    this.initialPreferences = merge({}, overrides, defaultPreferences);

    // 캐시된 환경 설정을 로드하고 초기 설정과 병합
    const cachedPreferences = this.loadFromCache() || {};
    const mergedPreference = merge(
      {},
      cachedPreferences,
      this.initialPreferences,
    );

    // 환경 설정 업데이트
    this.updatePreferences(mergedPreference);

    // 리스너 설정
    this.setupWatcher();

    // 플랫폼 식별자 초기화
    this.initPlatform();

    this.isInitialized = true;
  };

  /**
   * 환경 설정을 초기 상태로 재설정합니다.
   */
  resetPreferences = () => {
    // 상태를 초기 환경 설정으로 재설정
    Object.assign(this.state, this.initialPreferences);

    // 환경 설정을 캐시에 저장
    this.saveToCache(this.state);

    // 즉시 UI 업데이트 트리거
    this.handleUpdates(this.state);
  };

  /**
   * 환경 설정 업데이트
   * @param updates - 업데이트할 환경 설정
   */
  updatePreferences = (updates: DeepPartial<Preferences>) => {
    // 업데이트 내용과 현재 상태를 딥 병합
    const mergedState = merge({}, updates, markRaw(this.state));
    Object.assign(this.state, mergedState);

    // 업데이트된 값에 따라 업데이트 실행
    this.handleUpdates(updates);

    // 캐시에 저장
    this.debouncedSave(this.state);
  };

  /**
   * 업데이트 처리
   * @param updates - 업데이트할 환경 설정
   */
  private handleUpdates(updates: DeepPartial<Preferences>) {
    const { theme, app } = updates;

    if (
      theme &&
      (Object.keys(theme).length > 0 || Reflect.has(theme, 'fontSize'))
    ) {
      updateCSSVariables(this.state);
    }

    if (
      app &&
      (Reflect.has(app, 'colorGrayMode') || Reflect.has(app, 'colorWeakMode'))
    ) {
      this.updateColorMode(this.state);
    }
  }

  /**
   * 플랫폼 식별자 초기화
   */
  private initPlatform() {
    document.documentElement.dataset.platform = isMacOs() ? 'macOs' : 'window';
  }

  /**
   * 캐시에서 환경 설정 로드
   * @returns 캐시된 환경 설정, 존재하지 않으면 null 반환
   */
  private loadFromCache(): null | Preferences {
    return this.cache.getItem<Preferences>(STORAGE_KEYS.MAIN);
  }

  /**
   * 환경 설정을 캐시에 저장
   * @param preference - 저장할 환경 설정
   */
  private saveToCache(preference: Preferences) {
    this.cache.setItem(STORAGE_KEYS.MAIN, preference);
    this.cache.setItem(STORAGE_KEYS.LOCALE, preference.app.locale);
    this.cache.setItem(STORAGE_KEYS.THEME, preference.theme.mode);
  }

  /**
   * 상태 및 시스템 환경 설정의 변경 사항 감시
   */
  private setupWatcher() {
    if (this.isInitialized) {
      return;
    }

    // 브레이크포인트를 감시하여 모바일 여부 판단
    const breakpoints = useBreakpoints(breakpointsTailwind);
    const isMobile = breakpoints.smaller('md');

    watch(
      () => isMobile.value,
      (val) => {
        this.updatePreferences({
          app: { isMobile: val },
        });
      },
      { immediate: true },
    );

    // 시스템 테마 환경 설정 변경 사항 감시
    window
      .matchMedia('(prefers-color-scheme: dark)')
      .addEventListener('change', ({ matches: isDark }) => {
        // 자동 모드에서만 시스템 테마를 따름
        if (this.state.theme.mode === 'auto') {
          // 실제 테마 먼저 적용
          this.updatePreferences({
            theme: { mode: isDark ? 'dark' : 'light' },
          });
          // 다시 auto 모드로 복구하여 시스템 상태를 따르도록 유지
          this.updatePreferences({
            theme: { mode: 'auto' },
          });
        }
      });
  }

  /**
   * 페이지 색상 모드 업데이트 (그레이스케일, 색약)
   * @param preference - 환경 설정
   */
  private updateColorMode(preference: Preferences) {
    const { colorGrayMode, colorWeakMode } = preference.app;
    const dom = document.documentElement;

    dom.classList.toggle('invert-mode', colorWeakMode);
    dom.classList.toggle('grayscale-mode', colorGrayMode);
  }
}

const preferencesManager = new PreferenceManager();

export { PreferenceManager, preferencesManager };
