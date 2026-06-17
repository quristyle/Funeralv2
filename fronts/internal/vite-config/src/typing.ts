import type { PluginVisualizerOptions } from 'rollup-plugin-visualizer';
import type { PluginOptions } from 'unplugin-dts';
import type {
  ConfigEnv,
  PluginOption,
  UserConfig,
  UserConfigFnPromise,
} from 'vite';
import type { Options as PwaPluginOptions } from 'vite-plugin-pwa';

/**
 * ImportMap 설정 인터페이스
 * @description 모듈 가져오기 매핑을 설정하는 데 사용되며, 사용자 정의 가져오기 경로 및 범위를 지원함
 * @example
 * ```typescript
 * {
 *   imports: {
 *     'vue': 'https://unpkg.com/vue@3.2.47/dist/vue.esm-browser.js'
 *   },
 *   scopes: {
 *     'https://site.com/': {
 *       'vue': 'https://unpkg.com/vue@3.2.47/dist/vue.esm-browser.js'
 *     }
 *   }
 * }
 * ```
 */
interface IImportMap {
  /** 모듈 가져오기 매핑 */
  imports?: Record<string, string>;
  /** 범위별 특정 가져오기 매핑 */
  scopes?: {
    [scope: string]: Record<string, string>;
  };
}

/**
 * 출력 플러그인 설정 옵션
 * @description 콘솔 출력 정보를 설정하는 데 사용됨
 */
interface PrintPluginOptions {
  /**
   * 출력 데이터 매핑
   * @description 키-값 쌍 형태의 데이터로, 콘솔에 출력됨
   * @example
   * ```typescript
   * {
   *   'App Version': '1.0.0',
   *   'Build Time': '2024-01-01'
   * }
   * ```
   */
  infoMap?: Record<string, string | undefined>;
}

/**
 * Nitro Mock 플러그인 설정 옵션
 * @description Nitro Mock 서버의 동작을 설정하는 데 사용됨
 */
interface NitroMockPluginOptions {
  /**
   * Mock 서버 패키지명
   * @default '@vbenjs/nitro-mock'
   */
  mockServerPackage?: string;

  /**
   * Mock 서비스 포트
   * @default 3000
   */
  port?: number;

  /**
   * Mock 로그 출력 여부
   * @default false
   */
  verbose?: boolean;
}

/**
 * 아카이브 플러그인 설정 옵션
 * @description 빌드 결과물의 압축 아카이브를 설정하는 데 사용됨
 */
interface ArchiverPluginOptions {
  /**
   * 출력 파일명
   * @default 'dist'
   */
  name?: string;
  /**
   * 출력 디렉토리
   * @default '.'
   */
  outputDir?: string;
}

/**
 * ImportMap 플러그인 설정
 * @description 모듈의 CDN 가져오기를 설정하는 데 사용됨
 */
interface ImportmapPluginOptions {
  /**
   * CDN 공급업체
   * @default 'jspm.io'
   * @description esm.sh 및 jspm.io 두 가지 CDN 공급업체 지원
   */
  defaultProvider?: 'esm.sh' | 'jspm.io';
  /**
   * ImportMap 설정 배열
   * @description CDN에서 가져올 패키지 설정
   * @example
   * ```typescript
   * [
   *   { name: 'vue' },
   *   { name: 'pinia', range: '^2.0.0' }
   * ]
   * ```
   */
  importmap?: Array<{ name: string; range?: string }>;
  /**
   * 수동 ImportMap 설정
   * @description 사용자 정의 ImportMap 설정
   */
  inputMap?: IImportMap;
}

/**
 * 조건부 플러그인 설정
 * @description 조건에 따라 플러그인을 동적으로 로드하는 데 사용됨
 */
interface ConditionPlugin {
  /**
   * 판단 조건
   * @description 조건이 true일 때 플러그인 로드
   */
  condition?: boolean;
  /**
   * 플러그인 객체
   * @description 플러그인 배열 또는 Promise 반환
   */
  plugins: () => PluginOption[] | PromiseLike<PluginOption[]>;
}

/**
 * 공통 플러그인 설정 옵션
 * @description 모든 플러그인이 공유하는 기본 설정
 */
interface CommonPluginOptions {
  /**
   * 개발 도구 활성화 여부
   * @default false
   */
  devtools?: boolean;
  /**
   * 환경 변수
   * @description 사용자 정의 환경 변수
   */
  env?: Record<string, any>;
  /**
   * 메타데이터 주입 여부
   * @default true
   */
  injectMetadata?: boolean;
  /**
   * 빌드 모드 여부
   * @default false
   */
  isBuild?: boolean;
  /**
   * 빌드 모드
   * @default 'development'
   */
  mode?: string;
  /**
   * 의존성 분석 활성화 여부
   * @default false
   * @description rollup-plugin-visualizer를 사용하여 의존성 분석
   */
  visualizer?: boolean | PluginVisualizerOptions;
}

/**
 * 애플리케이션 플러그인 설정 옵션
 * @description 애플리케이션 빌드 시 플러그인 옵션을 설정하는 데 사용됨
 */
interface ApplicationPluginOptions extends CommonPluginOptions {
  /**
   * 압축 아카이브 활성화 여부
   * @default false
   * @description 활성화 시 빌드 디렉토리에 zip 파일 생성
   */
  archiver?: boolean;
  /**
   * 압축 아카이브 플러그인 설정
   * @description 압축 아카이브 동작 설정
   */
  archiverPluginOptions?: ArchiverPluginOptions;
  /**
   * 압축 활성화 여부
   * @default false
   * @description gzip 및 brotli 압축 지원
   */
  compress?: boolean;
  /**
   * 압축 유형
   * @default ['gzip']
   * @description 선택 가능한 압축 유형
   */
  compressTypes?: ('brotli' | 'gzip')[];
  /**
   * 설정 파일 분리 여부
   * @default false
   * @description 빌드 시 설정 파일 분리
   */
  extraAppConfig?: boolean;
  /**
   * HTML 플러그인 활성화 여부
   * @default true
   */
  html?: boolean;
  /**
   * 국제화 활성화 여부
   * @default false
   */
  i18n?: boolean;
  /**
   * ImportMap CDN 활성화 여부
   * @default false
   */
  importmap?: boolean;
  /**
   * ImportMap 플러그인 설정
   */
  importmapOptions?: ImportmapPluginOptions;
  /**
   * 애플리케이션 로딩 애니메이션 주입 여부
   * @default true
   */
  injectAppLoading?: boolean;
  /**
   * 글로벌 SCSS 주입 여부
   * @default true
   */
  injectGlobalScss?: boolean;
  /**
   * 저작권 정보 주입 여부
   * @default true
   */
  license?: boolean;
  /**
   * Nitro Mock 활성화 여부
   * @default false
   */
  nitroMock?: boolean;
  /**
   * Nitro Mock 플러그인 설정
   */
  nitroMockOptions?: NitroMockPluginOptions;
  /**
   * 콘솔 출력 활성화 여부
   * @default false
   */
  print?: boolean;
  /**
   * 출력 플러그인 설정
   */
  printInfoMap?: PrintPluginOptions['infoMap'];
  /**
   * PWA 활성화 여부
   * @default false
   */
  pwa?: boolean;
  /**
   * PWA 플러그인 설정
   */
  pwaOptions?: Partial<PwaPluginOptions>;
  /**
   * VXE Table 지연 로딩 활성화 여부
   * @default false
   */
  vxeTableLazyImport?: boolean;
}

/**
 * 라이브러리 플러그인 설정 옵션
 * @description 라이브러리 빌드 시 플러그인 옵션을 설정하는 데 사용됨
 */
interface LibraryPluginOptions extends CommonPluginOptions {
  /**
   * DTS 출력 활성화 여부
   * @default true
   * @description TypeScript 타입 선언 파일 생성
   */
  dts?: boolean | PluginOptions;
}

/**
 * 애플리케이션 설정 옵션 유형
 */
type ApplicationOptions = ApplicationPluginOptions;

/**
 * 라이브러리 설정 옵션 유형
 */
type LibraryOptions = LibraryPluginOptions;

/**
 * 애플리케이션 설정 정의 함수 유형
 * @description 애플리케이션 빌드 설정을 정의하는 데 사용됨
 */
type DefineApplicationOptions = (config?: ConfigEnv) => Promise<{
  /** 애플리케이션 플러그인 설정 */
  application?: ApplicationOptions;
  /** Vite 설정 */
  vite?: UserConfig;
}>;

/**
 * 라이브러리 설정 정의 함수 유형
 * @description 라이브러리 빌드 설정을 정의하는 데 사용됨
 */
type DefineLibraryOptions = (config?: ConfigEnv) => Promise<{
  /** 라이브러리 플러그인 설정 */
  library?: LibraryOptions;
  /** Vite 설정 */
  vite?: UserConfig;
}>;

/**
 * 설정 정의 유형
 * @description 애플리케이션 또는 라이브러리의 설정 정의
 */
type DefineConfig = DefineApplicationOptions | DefineLibraryOptions;

type VbenViteConfig = Promise<UserConfig> | UserConfig | UserConfigFnPromise;

export type {
  ApplicationPluginOptions,
  ArchiverPluginOptions,
  CommonPluginOptions,
  ConditionPlugin,
  DefineApplicationOptions,
  DefineConfig,
  DefineLibraryOptions,
  IImportMap,
  ImportmapPluginOptions,
  LibraryPluginOptions,
  NitroMockPluginOptions,
  PrintPluginOptions,
  VbenViteConfig,
};
