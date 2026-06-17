import type { FieldOptions, FormContext, GenericObject } from 'vee-validate';
import type { ZodTypeAny } from 'zod';

import type { Component, HtmlHTMLAttributes, Ref } from 'vue';

import type { VbenButtonProps } from '@vben-core/shadcn-ui';
import type { ClassType, MaybeComputedRef } from '@vben-core/typings';

import type { FormApi } from './form-api';

export type FormLayout = 'horizontal' | 'inline' | 'vertical';

export type BaseFormComponentType =
  | 'DefaultButton'
  | 'PrimaryButton'
  | 'VbenCheckbox'
  | 'VbenInput'
  | 'VbenInputPassword'
  | 'VbenPinInput'
  | 'VbenSelect'
  | (Record<never, never> & string);

type Breakpoints = '2xl:' | '3xl:' | '' | 'lg:' | 'md:' | 'sm:' | 'xl:';

type GridCols = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13;

export type WrapperClassType =
  | `${Breakpoints}grid-cols-${GridCols}`
  | (Record<never, never> & string);

export type FormItemClassType =
  | `${Breakpoints}cols-end-${'auto' | GridCols}`
  | `${Breakpoints}cols-span-${'auto' | 'full' | GridCols}`
  | `${Breakpoints}cols-start-${'auto' | GridCols}`
  | (Record<never, never> & string)
  | WrapperClassType;

export type FormFieldOptions = Partial<
  FieldOptions & {
    validateOnBlur?: boolean;
    validateOnChange?: boolean;
    validateOnInput?: boolean;
    validateOnModelUpdate?: boolean;
  }
>;

export interface FormShape {
  /** 기본값 */
  default?: any;
  /** 필드명 */
  fieldName: string;
  /** 필수 여부 */
  required?: boolean;
  rules?: ZodTypeAny;
}

export type MaybeComponentPropKey =
  | 'options'
  | 'placeholder'
  | 'title'
  | keyof HtmlHTMLAttributes
  | (Record<never, never> & string);

export type MaybeComponentProps = { [K in MaybeComponentPropKey]?: any };

export type FormActions = FormContext<GenericObject>;

export type CustomRenderType = (() => Component | string) | string;

// 동적 렌더링 파라미터
export type CustomParamsRenderType =
  | ((
      value: Partial<Record<string, any>>,
      actions: FormActions,
    ) => Component | string)
  | string;

export type FormSchemaRuleType =
  | 'required'
  | 'selectRequired'
  | null
  | (Record<never, never> & string)
  | ZodTypeAny;

type FormItemDependenciesCondition<T = boolean | PromiseLike<boolean>> = (
  value: Partial<Record<string, any>>,
  actions: FormActions,
) => T;

type FormItemDependenciesConditionWithRules = (
  value: Partial<Record<string, any>>,
  actions: FormActions,
) => FormSchemaRuleType | PromiseLike<FormSchemaRuleType>;

type FormItemDependenciesConditionWithProps = (
  value: Partial<Record<string, any>>,
  actions: FormActions,
) => MaybeComponentProps | PromiseLike<MaybeComponentProps>;

export interface FormItemDependencies {
  /**
   * 컴포넌트 props
   * @returns 컴포넌트 props
   */
  componentProps?: FormItemDependenciesConditionWithProps;
  /**
   * 비활성화 여부
   * @returns 비활성화 여부
   */
  disabled?: boolean | FormItemDependenciesCondition;
  /**
   * 렌더링 여부 (DOM 삭제)
   * @returns 렌더링 여부
   */
  if?: boolean | FormItemDependenciesCondition;
  /**
   * 필수 여부
   * @returns 필수 여부
   */
  required?: FormItemDependenciesCondition;
  /**
   * 필드 규칙
   */
  rules?: FormItemDependenciesConditionWithRules;
  /**
   * 숨김 여부 (CSS)
   * @returns 숨김 여부
   */
  show?: boolean | FormItemDependenciesCondition;
  /**
   * 어떤 트리거든 실행됨
   */
  trigger?: FormItemDependenciesCondition<void>;
  /**
   * 트리거 필드
   */
  triggerFields: string[];
}

type ComponentProps =
  | ((
      value: Partial<Record<string, any>>,
      actions: FormActions,
    ) => MaybeComponentProps)
  | MaybeComponentProps;

export interface FormCommonConfig {
  /**
   * Label 뒤에 콜론 표시
   */
  colon?: boolean;
  /**
   * 모든 폼 항목의 props
   */
  componentProps?: ComponentProps;
  /**
   * 모든 폼 항목의 컨트롤 스타일
   */
  controlClass?: string;
  /**
   * 모든 폼 항목의 비활성화 상태
   * @default false
   */
  disabled?: boolean;
  /**
   * 모든 폼 항목의 change 이벤트 리스너 비활성화 여부
   * @default true
   */
  disabledOnChangeListener?: boolean;
  /**
   * 모든 폼 항목의 input 이벤트 리스너 비활성화 여부
   * @default true
   */
  disabledOnInputListener?: boolean;
  /**
   * 모든 폼 항목의 빈 상태 값 (기본값: undefined, naive-ui는 null)
   */
  emptyStateValue?: null | undefined;
  /**
   * 모든 폼 항목의 컨트롤 스타일
   * @default {}
   */
  formFieldProps?: FormFieldOptions;
  /**
   * 모든 폼 항목의 그리드 레이아웃, 함수 형식 지원
   * @default ""
   */
  formItemClass?: (() => string) | string;
  /**
   * 모든 폼 항목의 label 숨기기
   * @default false
   */
  hideLabel?: boolean;
  /**
   * 필수 표시 숨김 여부
   * @default false
   */
  hideRequiredMark?: boolean;
  /**
   * 모든 폼 항목의 label 스타일
   * @default ""
   */
  labelClass?: string;
  /**
   * 모든 폼 항목의 label 너비
   */
  labelWidth?: number;
  /**
   * 모든 폼 항목의 model 속성명
   * @default "modelValue"
   */
  modelPropName?: string;
  /**
   * 모든 폼 항목의 wrapper 스타일
   */
  wrapperClass?: string;
}

type RenderComponentContentType = (
  value: Partial<Record<string, any>>,
  api: FormActions,
) => Record<string, any>;

export type HandleSubmitFn = (
  values: Record<string, any>,
) => Promise<void> | void;

export type HandleResetFn = (
  values: Record<string, any>,
) => Promise<void> | void;

export type FieldMappingTime = [
  string,
  [string, string],
  (
    | ((value: any, fieldName: string) => any)
    | [string, string]
    | null
    | string
  )?,
][];

export type ArrayToStringFields = Array<
  | [string[], string?] // 중첩 배열 형식, 선택적 구분 기호
  | string // 단일 필드, 기본 구분 기호 사용
  | string[] // 단순 배열 형식, 마지막 요소가 구분 기호일 수 있음
>;

export interface FormSchema<
  T extends BaseFormComponentType = BaseFormComponentType,
> extends FormCommonConfig {
  /** 컴포넌트 */
  component: Component | T;
  /** 컴포넌트 props */
  componentProps?: ComponentProps;
  /** 기본값 */
  defaultValue?: any;
  /** 의존성 */
  dependencies?: FormItemDependencies;
  /** 설명 */
  description?: CustomRenderType;
  /** 필드명 */
  fieldName: string;
  /** 도움말 정보 */
  help?: CustomParamsRenderType;
  /** 폼 항목 숨김 여부 */
  hide?: boolean;
  /** 폼 항목 label */
  label?: CustomRenderType;
  // 커스텀 컴포넌트 내부 렌더링
  renderComponentContent?: RenderComponentContentType;
  /** 필드 규칙 */
  rules?: FormSchemaRuleType;
  /** 접미사 */
  suffix?: CustomRenderType;
}

export interface FormFieldProps extends FormSchema {
  required?: boolean;
}

export interface FormRenderProps<
  T extends BaseFormComponentType = BaseFormComponentType,
> {
  /**
   * 폼 필드 배열을 문자열로 매핑하는 설정 (기본값: ",")
   */
  arrayToStringFields?: ArrayToStringFields;
  /**
   * 접힘 여부 (showCollapseButton=true일 때 유효)
   * true: 접힘, false: 펼쳐짐
   */
  collapsed?: boolean;
  /**
   * 접혔을 때 유지할 행 수
   * @default 1
   */
  collapsedRows?: number;
  /**
   * resize 이벤트 트리거 여부
   * @default false
   */
  collapseTriggerResize?: boolean;
  /**
   * 폼 항목 공통 백업 설정. 하위 항목에 설정이 없을 때 사용되며, 하위 항목의 설정이 우선순위가 더 높습니다.
   */
  commonConfig?: FormCommonConfig;
  /**
   * 컴팩트 모드 (각 폼 항목 하단의 유효성 검사 메시지용 여백 제거)
   */
  compact?: boolean;
  /**
   * 컴포넌트 v-model 이벤트 바인딩
   */
  componentBindEventMap?: Partial<Record<BaseFormComponentType, string>>;
  /**
   * 컴포넌트 모음
   */
  componentMap: Record<BaseFormComponentType, Component>;
  /**
   * 폼 필드를 시간 형식으로 매핑
   */
  fieldMappingTime?: FieldMappingTime;
  /**
   * 폼 인스턴스
   */
  form?: FormContext<GenericObject>;
  /**
   * 폼 항목 레이아웃
   */
  layout?: FormLayout;
  /**
   * 폼 정의
   */
  schema?: FormSchema<T>[];

  /**
   * 펼치기/접기 버튼 표시 여부
   */
  showCollapseButton?: boolean;
  /**
   * 날짜 형식 지정
   */

  /**
   * 폼 그리드 레이아웃
   * @default "grid-cols-1"
   */
  wrapperClass?: WrapperClassType;
}

export interface ActionButtonOptions extends VbenButtonProps {
  [key: string]: any;
  content?: MaybeComputedRef<string>;
  show?: boolean;
}

export interface VbenFormProps<
  T extends BaseFormComponentType = BaseFormComponentType,
> extends Omit<
  FormRenderProps<T>,
  'componentBindEventMap' | 'componentMap' | 'form'
> {
  /**
   * 작업 버튼 반전 여부 (제출 버튼을 앞으로)
   */
  actionButtonsReverse?: boolean;
  /**
   * 작업 버튼 그룹 스타일
   * newLine: 새 행에 표시. rowEnd: 행 내에 표시, 우측 정렬 (기본값). inline: grid 기본 스타일 사용
   */
  actionLayout?: 'inline' | 'newLine' | 'rowEnd';
  /**
   * 작업 버튼 그룹 표시 위치 (기본값: 우측)
   */
  actionPosition?: 'center' | 'left' | 'right';
  /**
   * 폼 작업 영역 클래스
   */
  actionWrapperClass?: ClassType;
  /**
   * 폼 필드 배열을 문자열로 매핑하는 설정 (기본값: ",")
   */
  arrayToStringFields?: ArrayToStringFields;

  /**
   * 폼 필드 매핑
   */
  fieldMappingTime?: FieldMappingTime;
  /**
   * 폼 접힘/펼침 상태 변경 콜백
   */
  handleCollapsedChange?: (collapsed: boolean) => void;
  /**
   * 폼 초기화 콜백
   */
  handleReset?: HandleResetFn;
  /**
   * 폼 제출 콜백
   */
  handleSubmit?: HandleSubmitFn;
  /**
   * 폼 값 변경 콜백
   */
  handleValuesChange?: (
    values: Record<string, any>,
    fieldsChanged: string[],
  ) => void;
  /**
   * 초기화 버튼 옵션
   */
  resetButtonOptions?: ActionButtonOptions;

  /**
   * 유효성 검사 실패 시 첫 번째 오류 필드로 자동 스크롤 여부
   * @default false
   */
  scrollToFirstError?: boolean;

  /**
   * 기본 작업 버튼 표시 여부
   * @default true
   */
  showDefaultActions?: boolean;

  /**
   * 제출 버튼 옵션
   */
  submitButtonOptions?: ActionButtonOptions;

  /**
   * 필드 값 변경 시 폼 제출 여부
   * @default false
   */
  submitOnChange?: boolean;

  /**
   * 엔터 키 입력 시 폼 제출 여부
   * @default false
   */
  submitOnEnter?: boolean;
}

export type ExtendedFormApi = FormApi & {
  useStore: <T = NoInfer<VbenFormProps>>(
    selector?: (state: NoInfer<VbenFormProps>) => T,
  ) => Readonly<Ref<T>>;
};

export interface VbenFormAdapterOptions<
  T extends BaseFormComponentType = BaseFormComponentType,
> {
  config?: {
    baseModelPropName?: string;
    disabledOnChangeListener?: boolean;
    disabledOnInputListener?: boolean;
    emptyStateValue?: null | undefined;
    modelPropNameMap?: Partial<Record<T, string>>;
  };
  defineRules?: {
    required?: (
      value: any,
      params: any,
      ctx: Record<string, any>,
    ) => boolean | string;
    selectRequired?: (
      value: any,
      params: any,
      ctx: Record<string, any>,
    ) => boolean | string;
  };
}
