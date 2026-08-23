import type { ZodType } from 'zod';

import type { Component, HtmlHTMLAttributes, Ref, UnwrapNestedRefs } from 'vue';

import type { VbenButtonProps } from '@vben-core/shadcn-ui';
import type { ClassType, MaybeComputedRef } from '@vben-core/typings';

import type { FormApi } from './form-api';
import type { useFormLabelWidth } from './form-render/utils';

export type FormLabelWidthContext = UnwrapNestedRefs<
  ReturnType<typeof useFormLabelWidth>
>;

export type FormValues = Record<string, any>;

export interface FormCodec<
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
> {
  /** 将提交值转换为表单组件值。 */
  decode: (values: Readonly<TSubmitValues>) => TFormValues;
  /** 将表单组件值转换为提交值。 */
  encode: (values: Readonly<TFormValues>) => TSubmitValues;
}

export type FormFieldName<TValues extends FormValues = FormValues> =
  | Extract<keyof TValues, string>
  | (Record<never, never> & string);

export type FormFieldValue<
  TValues extends FormValues,
  TFieldName extends string,
> = TFieldName extends keyof TValues ? TValues[TFieldName] : unknown;

export type FormLayout = 'horizontal' | 'inline' | 'vertical';

export type BaseFormComponentType =
  | 'DefaultButton'
  | 'PrimaryButton'
  | 'VbenCheckbox'
  | 'VbenFormFieldArray'
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

export interface FormFieldOptions {
  asyncDebounceMs?: number;
  validateOn?: readonly FormValidationTrigger[];
}

export type FormValidationTrigger = 'blur' | 'change';

export interface FormShape {
  /** 기본값 */
  default?: any;
  /** 필드명 */
  fieldName: string;
  /** 필수 여부 */
  required?: boolean;
  rules?: ZodType;
}

export interface FormRuntimeField<TValue = unknown> {
  handleBlur: () => void;
  handleChange: (value: TValue) => void;
  state: {
    meta: {
      errors: unknown[];
      isDirty: boolean;
      isTouched: boolean;
      isValid: boolean;
    };
    value: TValue;
  };
}

export interface FormComponentField<
  TValue = unknown,
  TFieldName extends string = string,
> {
  modelValue: TValue;
  name: TFieldName;
  onBlur: () => void;
  onChange: (value: TValue) => void;
  onInput: (value: TValue) => void;
  'onUpdate:modelValue': (value: TValue) => void;
}

export type MaybeComponentPropKey =
  | 'options'
  | 'placeholder'
  | 'title'
  | keyof HtmlHTMLAttributes
  | (Record<never, never> & string);

export type MaybeComponentProps = { [K in MaybeComponentPropKey]?: any };

export interface FormMeta {
  dirty: boolean;
  submitting: boolean;
  valid: boolean;
  validating: boolean;
}

export interface FormRuntimeState<TValues extends FormValues = FormValues> {
  errors: Record<string, string>;
  meta: FormMeta;
  values: TValues;
}

export interface FormValidationResult {
  errors: Record<string, string>;
  valid: boolean;
}

export interface FormValueSnapshot<
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
> {
  rawValues: Readonly<TFormValues>;
  values: TSubmitValues;
}

export interface FormResetState<TValues extends FormValues = FormValues> {
  values?: Partial<TValues>;
}

export interface FormResetOptions {
  force?: boolean;
  keepDefaultValues?: boolean;
}

export interface FormContextApi<TValues extends FormValues = FormValues> {
  clearValidation: (
    fieldNames?: FormFieldName<TValues> | FormFieldName<TValues>[],
  ) => void;
  readonly errors: Record<string, string>;
  readonly fieldComponent: Component;
  getFieldError: (fieldName: string) => string | undefined;
  getFieldValue: <TFieldName extends FormFieldName<TValues>>(
    fieldName: TFieldName,
  ) => FormFieldValue<TValues, TFieldName>;
  handleSubmit: (
    callback?: (values: TValues) => Promise<void> | void,
  ) => (event?: Event) => Promise<void>;
  isFieldValid: (fieldName: string) => boolean;
  readonly meta: FormMeta;
  pushFieldValue: (fieldName: string, value: any) => void;
  removeFieldValue: (fieldName: string, index: number) => Promise<void>;
  reset: (
    state?: FormResetState<TValues>,
    options?: FormResetOptions,
  ) => Promise<void>;
  /** @deprecated Use `reset` instead. */
  resetForm: (
    state?: FormResetState<TValues>,
    options?: FormResetOptions,
  ) => Promise<void>;
  setFieldError: (fieldName: string, error?: string) => void;
  setFieldValue: <TFieldName extends FormFieldName<TValues>>(
    fieldName: TFieldName,
    value: FormFieldValue<TValues, NoInfer<TFieldName>>,
    shouldValidate?: boolean,
  ) => Promise<void>;
  setValues: (
    values: Partial<TValues>,
    shouldValidate?: boolean,
  ) => Promise<void>;
  submit: () => Promise<void>;
  /** @deprecated Use `submit` instead. */
  submitForm: () => Promise<void>;
  useFieldError: (fieldName: string) => Readonly<Ref<string | undefined>>;
  useFieldValue: <TFieldName extends FormFieldName<TValues>>(
    fieldName: TFieldName,
  ) => Readonly<Ref<FormFieldValue<TValues, TFieldName>>>;
  useFieldValues: <TFieldName extends FormFieldName<TValues>>(
    fieldNames: readonly TFieldName[],
  ) => Readonly<Ref<FormFieldValue<TValues, TFieldName>[]>>;
  useSelector: <T>(
    selector: (state: FormRuntimeState<TValues>) => T,
  ) => Readonly<Ref<T>>;
  useValues: () => Readonly<Ref<TValues>>;
  validate: () => Promise<FormValidationResult>;
  validateField: (fieldName: string) => Promise<FormValidationResult>;
  readonly values: TValues;
}

/** @deprecated Use `FormContextApi` instead. */
export type FormActions<TValues extends FormValues = FormValues> =
  FormContextApi<TValues>;

type ReservedFormSlotName =
  | 'default'
  | 'expand-after'
  | 'expand-before'
  | 'reset-before'
  | 'submit-before';

type KnownFormFieldName<TValues extends FormValues> =
  string extends Extract<keyof TValues, string>
    ? never
    : Exclude<Extract<keyof TValues, string>, ReservedFormSlotName>;

export interface VbenFormActionSlotProps<
  TValues extends FormValues = FormValues,
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TSubmitValues extends FormValues = TValues,
> {
  formApi: ExtendedFormApi<TValues, T, P, TSubmitValues>;
  values: TValues;
}

export interface VbenFormDefaultSlotProps<
  TValues extends FormValues = FormValues,
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TSubmitValues extends FormValues = TValues,
> extends VbenFormActionSlotProps<TValues, T, P, TSubmitValues> {
  shapes: FormShape[];
}

export interface VbenFormFieldSlotProps<
  TValues extends FormValues = FormValues,
  TFieldName extends FormFieldName<TValues> = FormFieldName<TValues>,
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TSubmitValues extends FormValues = TValues,
> extends VbenFormActionSlotProps<TValues, T, P, TSubmitValues> {
  componentField: FormComponentField<
    FormFieldValue<TValues, TFieldName>,
    TFieldName
  >;
  componentProps: VbenFormResolvedComponentProps<
    FormFieldValue<TValues, TFieldName>,
    TFieldName
  >;
  disabled: boolean;
  field: FormRuntimeField<FormFieldValue<TValues, TFieldName>>;
  isInValid: boolean;
  modelValue: FormFieldValue<TValues, TFieldName>;
  name: TFieldName;
}

export type VbenFormResolvedComponentProps<
  TValue = unknown,
  TFieldName extends string = string,
> = MaybeComponentProps & {
  disabled: boolean;
  modelValue?: TValue;
  name: TFieldName;
  'onUpdate:modelValue'?: (value: TValue) => void;
};

type VbenFormFieldSlots<
  TValues extends FormValues,
  T extends BaseFormComponentType,
  P extends Record<string, any>,
  TSubmitValues extends FormValues,
> =
  string extends Extract<keyof TValues, string>
    ? Record<
        string,
        | ((
            props: VbenFormFieldSlotProps<
              TValues,
              FormFieldName<TValues>,
              T,
              P,
              TSubmitValues
            >,
          ) => any)
        | undefined
      >
    : {
        [TFieldName in KnownFormFieldName<TValues>]?: (
          props: VbenFormFieldSlotProps<
            TValues,
            TFieldName,
            T,
            P,
            TSubmitValues
          >,
        ) => any;
      };

export type VbenFormSlots<
  TValues extends FormValues = FormValues,
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TSubmitValues extends FormValues = TValues,
> = VbenFormFieldSlots<TValues, T, P, TSubmitValues> & {
  default?: (
    props: VbenFormDefaultSlotProps<TValues, T, P, TSubmitValues>,
  ) => any;
  'expand-after'?: (
    props: VbenFormActionSlotProps<TValues, T, P, TSubmitValues>,
  ) => any;
  'expand-before'?: (
    props: VbenFormActionSlotProps<TValues, T, P, TSubmitValues>,
  ) => any;
  'reset-before'?: (
    props: VbenFormActionSlotProps<TValues, T, P, TSubmitValues>,
  ) => any;
  'submit-before'?: (
    props: VbenFormActionSlotProps<TValues, T, P, TSubmitValues>,
  ) => any;
};

export type VbenFormComponent<
  TValues extends FormValues = FormValues,
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TSubmitValues extends FormValues = TValues,
> = new () => {
  $props: VbenFormProps<T, P, TValues, TSubmitValues>;
  $slots: VbenFormSlots<TValues, T, P, TSubmitValues>;
};

export interface FormSchemaContext<TValues extends FormValues = FormValues> {
  /** 数组字段名，例如 contacts */
  arrayField?: string;
  /** 当前真实字段名，例如 contacts[0].name */
  fieldName?: string;
  /** 原始 schema 字段名，例如 name */
  originalFieldName?: string;
  /** 表单完整值 */
  rootValues?: TValues;
  /** 当前行数据 */
  row?: Record<string, any>;
  /** 当前行索引 */
  rowIndex?: number;
  /** 当前行路径，例如 contacts[0] */
  rowPath?: string;
}

export type CustomRenderType = (() => Component | string) | string;

// 동적 렌더링 파라미터
export type CustomParamsRenderType<TValues extends FormValues = FormValues> =
  | ((ctx: FormSchemaContext<TValues>) => Component | string)
  | string;

export type FormSchemaRuleType =
  | 'required'
  | 'selectRequired'
  | null
  | (Record<never, never> & string)
  | ZodType;

type FormItemDependenciesCondition<
  TValues extends FormValues,
  TResult = boolean | PromiseLike<boolean>,
> = (
  value: Partial<TValues>,
  actions: FormActions<TValues>,
  controller: ExtendedFormApi<TValues>, // dependencies 안에서 extendApi 에 접근할 수 있게 한다
  ctx?: FormSchemaContext<TValues>,
) => TResult;

type FormItemDependenciesConditionWithRules<TValues extends FormValues> = (
  value: Partial<TValues>,
  actions: FormActions<TValues>,
  controller: ExtendedFormApi<TValues>, // dependencies 안에서 extendApi 에 접근할 수 있게 한다
  ctx?: FormSchemaContext<TValues>,
) => FormSchemaRuleType | PromiseLike<FormSchemaRuleType>;

type FormItemDependenciesConditionWithProps<TValues extends FormValues> = (
  value: Partial<TValues>,
  actions: FormActions<TValues>,
  controller: ExtendedFormApi<TValues>, // dependencies 안에서 extendApi 에 접근할 수 있게 한다
  ctx?: FormSchemaContext<TValues>,
) => MaybeComponentProps | PromiseLike<MaybeComponentProps>;

interface FormItemDependenciesBase {
  /**
   * 트리거 필드
   */
  triggerFields: string[];
}

export interface FormDependenciesResolveContext<
  TValues extends FormValues = FormValues,
> {
  actions: FormActions<TValues>;
  controller: ExtendedFormApi<TValues>;
  schema: FormSchemaContext<TValues>;
  values: Readonly<TValues>;
}

export interface FormDependenciesResolvedState {
  componentProps?: MaybeComponentProps;
  disabled?: boolean;
  help?: CustomRenderType;
  if?: boolean;
  renderComponentContent?: Record<string, any>;
  required?: boolean;
  rules?: FormSchemaRuleType;
  show?: boolean;
}

export interface FormItemDependenciesLegacy<
  TValues extends FormValues = FormValues,
> extends FormItemDependenciesBase {
  /**
   * 컴포넌트 props
   * @returns 컴포넌트 props
   * @deprecated Use `dependencies.resolve` instead.
   */
  componentProps?: FormItemDependenciesConditionWithProps<TValues>;
  /**
   * 비활성화 여부
   * @returns 비활성화 여부
   * @deprecated Use `dependencies.resolve` instead.
   */
  disabled?: boolean | FormItemDependenciesCondition<TValues>;
  /**
   * 렌더링 여부 (DOM 삭제)
   * @returns 렌더링 여부
   * @deprecated Use `dependencies.resolve` instead.
   */
  if?: boolean | FormItemDependenciesCondition<TValues>;
  /**
   * 필수 여부
   * @returns 필수 여부
   * @deprecated Use `dependencies.resolve` instead.
   */
  required?: FormItemDependenciesCondition<TValues>;
  resolve?: never;
  /**
   * 필드 규칙
   * @deprecated Use `dependencies.resolve` instead.
   */
  rules?: FormItemDependenciesConditionWithRules<TValues>;
  /**
   * 숨김 여부 (CSS)
   * @returns 숨김 여부
   * @deprecated Use `dependencies.resolve` instead.
   */
  show?: boolean | FormItemDependenciesCondition<TValues>;
  /**
   * 어떤 트리거든 실행됨
   * @deprecated Use `dependencies.resolve` instead.
   */
  trigger?: FormItemDependenciesCondition<TValues, void>;
}

export interface FormItemDependenciesResolve<
  TValues extends FormValues = FormValues,
> extends FormItemDependenciesBase {
  componentProps?: never;
  disabled?: never;
  if?: never;
  required?: never;
  resolve: (
    context: FormDependenciesResolveContext<TValues>,
  ) =>
    | FormDependenciesResolvedState
    | PromiseLike<FormDependenciesResolvedState | undefined>
    | undefined;
  rules?: never;
  show?: never;
  trigger?: never;
}

export type FormItemDependencies<TValues extends FormValues = FormValues> =
  | FormItemDependenciesLegacy<TValues>
  | FormItemDependenciesResolve<TValues>;

type ComponentProps<TValues extends FormValues = FormValues> =
  | ((ctx: FormSchemaContext<TValues>) => MaybeComponentProps)
  | MaybeComponentProps;

export interface FormCommonConfig<TValues extends FormValues = FormValues> {
  /**
   * 是否启用 change 事件兼容回退。
   * 仅当组件不发送 update:*、只发送 change 时启用。
   * @default false
   */
  changeEventFallback?: boolean;
  /**
   * 是否可折叠的
   * @default false
   */
  collapsible?: boolean;
  /**
   * Label 뒤에 콜론 표시
   */
  colon?: boolean;
  /**
   * 모든 폼 항목의 props
   */
  componentProps?: ComponentProps<TValues>;
  /**
   * 모든 폼 항목의 컨트롤 스타일
   */
  controlClass?: string;
  /**
   * 默认折叠
   * @default false
   */
  defaultCollapsed?: boolean;
  /**
   * 모든 폼 항목의 비활성화 상태
   * @default false
   */
  disabled?: boolean;
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
   * 设置为 `auto` 时，水平布局下会按当前表单可见 label 的最大宽度自动对齐
   */
  labelWidth?: number | string;
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

type RenderComponentContentType<TValues extends FormValues = FormValues> = (
  ctx: FormSchemaContext<TValues>,
) => Record<string, any>;

type MappedComponentProps<P, TValues extends FormValues = FormValues> =
  | ((ctx: FormSchemaContext<TValues>) => P & Record<string, any>)
  | (P & Record<string, any>);

/**
 * 格式化 `getValues()` 输出中的当前字段值。
 * - 返回 `undefined`：保留当前字段已被移除的状态，通常配合 `setValue(key, nextValue)`
 *   把一个字段拆分写入到其他字段，例如 `startTime` / `endTime`
 * - 返回其他值：会将当前字段恢复/写回为该返回值
 * - `setValue` 回调签名为 `(key, nextValue) => void`
 * @deprecated Use the form-level `codec` instead.
 */
export type FormValueFormat<TValues extends FormValues = FormValues> = (
  value: any,
  setValue: (fieldName: string, value: any) => void,
  values: TValues,
  ctx?: FormSchemaContext<TValues>,
) => any;

interface FormSchemaBody<TValues extends FormValues = FormValues> extends Omit<
  FormCommonConfig<TValues>,
  'componentProps'
> {
  /** 기본값 */
  defaultValue?: any;
  /** 依赖 */
  dependencies?: FormItemDependencies<TValues>;
  /** 描述 */
  description?: CustomRenderType;
  /** 필드명 */
  fieldName: string;
  /** 帮助信息 */
  help?: CustomParamsRenderType<TValues>;
  /** 是否隐藏表单项 */
  hide?: boolean;
  /** 表单项 */
  label?: CustomRenderType;
  // 自定义组件内部渲染
  renderComponentContent?: RenderComponentContentType<TValues>;
  /** 字段规则 */
  rules?: FormSchemaRuleType;
  /** 后缀 */
  suffix?: CustomRenderType;
  /**
   * 获取表单值时格式化当前字段。
   * - 返回值不为 `undefined` 时，会回写到当前 fieldName
   * - 返回值为 `undefined` 时，可通过 setValue 写入一个或多个目标字段
   * @deprecated Use the form-level `codec` instead.
   */
  valueFormat?: FormValueFormat<TValues>;
}

type FormSchemaDiscriminated<
  T extends BaseFormComponentType,
  P extends Record<string, any>,
  TValues extends FormValues,
> = {
  [K in Extract<keyof P, T>]: {
    /** 组件 */
    component: K;
    /** 组件参数 */
    componentProps?: MappedComponentProps<P[K], TValues>;
  } & FormSchemaBody<TValues>;
}[Extract<keyof P, T>];

type FormSchemaFallback<
  T extends BaseFormComponentType,
  TValues extends FormValues,
> = {
  /** 컴포넌트 */
  component: Component | T;
  /** 컴포넌트 props */
  componentProps?: ComponentProps<TValues>;
} & FormSchemaBody<TValues>;

type FormArraySchema<
  T extends BaseFormComponentType,
  P extends Record<string, any>,
  TValues extends FormValues,
> = {
  /** 内置数组编辑器参数 */
  arrayProps?: Omit<
    VbenFormFieldArrayProps<T, P, TValues>,
    'disabled' | 'globalCommonConfig' | 'name' | 'schema'
  >;
  /** 数组子字段定义 */
  children: FormSchema<T, P, TValues>[];
  /** 兼容显式指定内置数组编辑器 */
  component?: Component | T;
  /** 兼容通过 componentProps 传递数组编辑器参数 */
  componentProps?: ComponentProps<TValues>;
  /** 数组字段标记 */
  type: 'array';
} & FormSchemaBody<TValues>;

export type FormSchema<
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TValues extends FormValues = FormValues,
> =
  | FormArraySchema<T, P, TValues>
  | FormSchemaDiscriminated<T, P, TValues>
  | FormSchemaFallback<T, TValues>;

/**
 * 数组编辑器（VbenFormFieldArray）的组件参数
 */
export interface VbenFormFieldArrayProps<
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TValues extends FormValues = FormValues,
> {
  /** 操作列表头文案 */
  actionText?: string;
  /** 「添加」按钮文案 */
  addButtonText?: string;
  /** 子字段通用配置 */
  commonConfig?: FormCommonConfig<TValues>;
  /** 新增一行时生成的默认数据；缺省时按列定义的 fieldName 生成空对象 */
  createRow?: () => Record<string, any>;
  disabled?: boolean;
  /** 空数据文案 */
  emptyText?: string;
  /** 子字段全局通用配置 */
  globalCommonConfig?: FormCommonConfig<TValues>;
  /** 最多行数 */
  max?: number;
  /** 最少行数 */
  min?: number;
  /** 数组字段路径，由外层 FormField 透传 */
  name?: string;
  /** 列定义，每一列是一个子字段（复用 FormSchema） */
  schema?: FormSchema<T, P, TValues>[];
  /** 是否显示序号列 */
  showIndex?: boolean;
}

export type HandleSubmitFn<
  TFormValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TFormValues,
> = (
  values: NoInfer<TSubmitValues>,
  rawValues: Readonly<TFormValues>,
) => Promise<void> | void;

export type HandleResetFn<TSubmitValues extends FormValues = FormValues> = (
  values: TSubmitValues,
) => Promise<void> | void;

/** @deprecated Use the form-level `codec` instead. */
export type FieldMappingTimeItem = [
  string,
  [string, string],
  (
    | ((value: any, fieldName: string) => any)
    | [string, string]
    | null
    | string
  )?,
];

/** @deprecated Use the form-level `codec` instead. */
export type FieldMappingTime = FieldMappingTimeItem[];

/** @deprecated Use the form-level `codec` instead. */
export type ArrayToStringFields = Array<
  | [string[], string?] // 중첩 배열 형식, 선택적 구분 기호
  | string // 단일 필드, 기본 구분 기호 사용
  | string[] // 단순 배열 형식, 마지막 요소가 구분 기호일 수 있음
>;

export interface FormFieldProps<
  T extends BaseFormComponentType = BaseFormComponentType,
  TValues extends FormValues = FormValues,
> extends FormSchemaBody<TValues> {
  /** 컴포넌트 */
  component: Component | T;
  /** 컴포넌트 props */
  componentProps?: ComponentProps<TValues>;
}

export interface FormRenderProps<
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TValues extends FormValues = FormValues,
> {
  /**
   * 폼 필드 배열을 문자열로 매핑하는 설정 (기본값: ",")
   * @deprecated Use the form-level `codec` instead.
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
  commonConfig?: FormCommonConfig<TValues>;
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
   * @deprecated Use the form-level `codec` instead.
   */
  fieldMappingTime?: FieldMappingTime;
  /**
   * 폼 인스턴스
   */
  form?: FormActions<TValues>;
  /**
   * 폼 항목 레이아웃
   */
  layout?: FormLayout;
  /**
   * 폼 정의
   */
  schema?: FormSchema<T, P, TValues>[];

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
  P extends Record<string, any> = Record<never, never>,
  TValues extends FormValues = FormValues,
  TSubmitValues extends FormValues = TValues,
> extends Omit<
  FormRenderProps<T, P, TValues>,
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
   * @deprecated Use the form-level `codec` instead.
   */
  arrayToStringFields?: ArrayToStringFields;

  /**
   * submitOnChange改变时防抖时间 | 默认300ms
   */
  changeDebouncedTime?: number;
  /** 表单组件值与提交值之间的双向编解码器。 */
  codec?: FormCodec<TValues, TSubmitValues>;
  /**
   * 폼 필드 매핑
   * @deprecated Use the form-level `codec` instead.
   */
  fieldMappingTime?: FieldMappingTime;
  /**
   * 폼 접힘/펼침 상태 변경 콜백
   */
  handleCollapsedChange?: (collapsed: boolean) => void;
  /**
   * 폼 초기화 콜백
   */
  handleReset?: HandleResetFn<NoInfer<TSubmitValues>>;
  /**
   * 폼 제출 콜백
   */
  handleSubmit?: HandleSubmitFn<TValues, TSubmitValues>;
  /**
   * 폼 값 변경 콜백
   */
  handleValuesChange?: (
    values: Readonly<TValues>,
    fieldsChanged: string[],
    getFormattedValues: () => TSubmitValues,
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

export type ExtendedFormApi<
  TValues extends FormValues = FormValues,
  T extends BaseFormComponentType = BaseFormComponentType,
  P extends Record<string, any> = Record<never, never>,
  TSubmitValues extends FormValues = TValues,
> = FormApi<TValues, T, P, TSubmitValues> & {
  useStore: <TResult = NoInfer<VbenFormProps<T, P, TValues, TSubmitValues>>>(
    selector?: (
      state: NoInfer<VbenFormProps<T, P, TValues, TSubmitValues>>,
    ) => TResult,
  ) => Readonly<Ref<TResult>>;
};

export interface VbenFormAdapterOptions<
  T extends BaseFormComponentType = BaseFormComponentType,
> {
  config?: {
    baseModelPropName?: string;
    /**
     * 是否启用 change 事件兼容回退。
     * 仅用于只发送 change 的兼容组件。
     * @default false
     */
    changeEventFallback?: boolean;
    emptyStateValue?: null | undefined;
    modelPropNameMap?: Partial<Record<T, string>>;
  };
  /** @deprecated Use `rules` instead. */
  defineRules?: Partial<Record<string, FormRuleValidator>>;
  rules?: Partial<Record<string, FormRuleValidator>>;
}

export interface FormRuleContext {
  field: {
    label?: string;
    name: string;
  };
  label?: string;
  name: string;
}

export type FormRuleValidator = (
  value: any,
  params: any,
  context: FormRuleContext,
) => boolean | Promise<boolean | string> | string;
