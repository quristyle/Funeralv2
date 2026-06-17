/**
 * @ko_KR 스택 데이터 구조
 */
export class Stack<T> {
  /**
   * @ko_KR 스택 내 요소 수
   */
  get size() {
    return this.items.length;
  }
  /**
   * @ko_KR 중복 제거 여부
   */
  private readonly dedup: boolean;
  /**
   * @ko_KR 스택 내 요소
   */
  private items: T[] = [];

  /**
   * @ko_KR 스택의 최대 용량
   */
  private readonly maxSize?: number;

  constructor(dedup = true, maxSize?: number) {
    this.maxSize = maxSize;
    this.dedup = dedup;
  }

  /**
   * @ko_KR 스택 내 요소 비우기
   */
  clear() {
    this.items.length = 0;
  }

  /**
   * @ko_KR 스택 최상단 요소 확인
   * @returns 스택 최상단 요소
   */
  peek(): T | undefined {
    return this.items[this.items.length - 1];
  }

  /**
   * @ko_KR pop (요소 꺼내기)
   * @returns 스택 최상단 요소
   */
  pop(): T | undefined {
    return this.items.pop();
  }

  /**
   * @ko_KR push (요소 넣기)
   * @param items 스택에 넣을 요소
   */
  push(...items: T[]) {
    items.forEach((item) => {
      // 중복 제거
      if (this.dedup) {
        const index = this.items.indexOf(item);
        if (index !== -1) {
          this.items.splice(index, 1);
        }
      }
      this.items.push(item);
      if (this.maxSize && this.items.length > this.maxSize) {
        this.items.splice(0, this.items.length - this.maxSize);
      }
    });
  }
  /**
   * @ko_KR 스택 내 요소 제거
   * @param itemList 제거할 요소 목록
   */
  remove(...itemList: T[]) {
    this.items = this.items.filter((i) => !itemList.includes(i));
  }
  /**
   * @ko_KR 스택 내 요소 유지
   * @param itemList 유지할 요소 목록
   */
  retain(itemList: T[]) {
    this.items = this.items.filter((i) => itemList.includes(i));
  }

  /**
   * @ko_KR 배열로 변환
   * @returns 스택 내 요소 배열
   */
  toArray(): T[] {
    return [...this.items];
  }
}

/**
 * @ko_KR 스택 인스턴스 생성
 * @param dedup 중복 제거 여부
 * @param maxSize 스택의 최대 용량
 * @returns 스택 인스턴스
 */
export const createStack = <T>(dedup = true, maxSize?: number) =>
  new Stack<T>(dedup, maxSize);
