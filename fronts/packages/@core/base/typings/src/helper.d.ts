import type { ComputedRef, MaybeRef } from 'vue';

/**
 * 타입 레벨 재귀에서 깊이 카운트 증가
 */
type Increment<A extends unknown[]> = [...A, unknown];
/**
 * 모든 속성을 선택적으로 깊게 재귀
 */
type DeepPartial<
  T,
  D extends number = 10,
  C extends unknown[] = [],
> = C['length'] extends D
  ? T
  : T extends object
    ? {
        [P in keyof T]?: DeepPartial<T[P], D, Increment<C>>;
      }
    : T;

/**
 * 모든 속성을 읽기 전용으로 깊게 재귀
 */
type DeepReadonly<
  T,
  D extends number = 10,
  C extends unknown[] = [],
> = C['length'] extends D
  ? T
  : T extends object
    ? {
        readonly [P in keyof T]: DeepReadonly<T[P], D, Increment<C>>;
      }
    : T;

/**
 * 임의 타입의 비동기 함수
 */

type AnyPromiseFunction<T extends any[] = any[], R = void> = (
  ...arg: T
) => PromiseLike<R>;

/**
 * 임의 타입의 일반 함수
 */
type AnyNormalFunction<T extends any[] = any[], R = void> = (...arg: T) => R;

/**
 * 임의 타입의 함수
 */
type AnyFunction<T extends any[] = any[], R = void> =
  | AnyNormalFunction<T, R>
  | AnyPromiseFunction<T, R>;

/**
 *  T | null 래퍼
 */
type Nullable<T> = null | T;

/**
 * T | Not null 래퍼
 */
type NonNullable<T> = T extends null | undefined ? never : T;

/**
 * 문자열 타입 객체
 */
type Recordable<T> = Record<string, T>;

/**
 * 문자열 타입 객체(읽기 전용)
 */
interface ReadonlyRecordable<T = any> {
  readonly [key: string]: T;
}

/**
 * setTimeout 반환 값 타입
 */
type TimeoutHandle = ReturnType<typeof setTimeout>;

/**
 * setInterval 반환 값 타입
 */
type IntervalHandle = ReturnType<typeof setInterval>;

/**
 * 계산된 ref이거나 getter 함수일 수 있음
 *
 */
type MaybeReadonlyRef<T> = (() => T) | ComputedRef<T>;

/**
 * ref, 일반 값 또는 getter 함수일 수 있음
 *
 */
type MaybeComputedRef<T> = MaybeReadonlyRef<T> | MaybeRef<T>;

type Merge<O extends object, T extends object> = {
  [K in keyof O | keyof T]: K extends keyof T
    ? T[K]
    : K extends keyof O
      ? O[K]
      : never;
};

/**
 * T = [
 *  { name: string; age: number; },
 *  { sex: 'male' | 'female'; age: string }
 * ]
 * =>
 * MergeAll<T> = {
 *  name: string;
 *  sex: 'male' | 'female';
 *  age: string
 * }
 */
type MergeAll<
  T extends object[],
  R extends object = Record<string, any>,
> = T extends [infer F extends object, ...infer Rest extends object[]]
  ? MergeAll<Rest, Merge<R, F>>
  : R;

type EmitType = (name: Name, ...args: any[]) => void;

type MaybePromise<T> = Promise<T> | T;

export type {
  AnyFunction,
  AnyNormalFunction,
  AnyPromiseFunction,
  DeepPartial,
  DeepReadonly,
  EmitType,
  IntervalHandle,
  MaybeComputedRef,
  MaybePromise,
  MaybeReadonlyRef,
  Merge,
  MergeAll,
  NonNullable,
  Nullable,
  ReadonlyRecordable,
  Recordable,
  TimeoutHandle,
};
