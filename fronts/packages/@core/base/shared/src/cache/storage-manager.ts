type StorageType = 'localStorage' | 'sessionStorage';

interface StorageManagerOptions {
  prefix?: string;
  storageType?: StorageType;
}

interface StorageItem<T> {
  expiry?: number;
  value: T;
}

class StorageManager {
  private prefix: string;
  private storage: Storage;

  constructor({
    prefix = '',
    storageType = 'localStorage',
  }: StorageManagerOptions = {}) {
    this.prefix = prefix;
    this.storage =
      storageType === 'localStorage'
        ? window.localStorage
        : window.sessionStorage;
  }

  /**
   * 접두사가 있는 모든 저장 항목 삭제
   */
  clear(): void {
    const keysToRemove: string[] = [];
    for (let i = 0; i < this.storage.length; i++) {
      const key = this.storage.key(i);
      if (key && key.startsWith(this.prefix)) {
        keysToRemove.push(key);
      }
    }
    keysToRemove.forEach((key) => this.storage.removeItem(key));
  }

  /**
   * 만료된 모든 저장 항목 삭제
   */
  clearExpiredItems(): void {
    for (let i = 0; i < this.storage.length; i++) {
      const key = this.storage.key(i);
      if (key && key.startsWith(this.prefix)) {
        const shortKey = key.replace(this.prefix, '');
        this.getItem(shortKey); // getItem 메서드를 호출하여 만료된 항목을 확인하고 제거
      }
    }
  }

  /**
   * 저장 항목 가져오기
   * @param key 키
   * @param defaultValue 항목이 존재하지 않거나 만료되었을 때 반환할 기본값
   * @returns 값, 항목이 만료되었거나 파싱 오류가 발생하면 기본값 반환
   */
  getItem<T>(key: string, defaultValue: null | T = null): null | T {
    const fullKey = this.getFullKey(key);
    const itemStr = this.storage.getItem(fullKey);
    if (!itemStr) {
      return defaultValue;
    }

    try {
      const item: StorageItem<T> = JSON.parse(itemStr);
      if (item.expiry && Date.now() > item.expiry) {
        this.storage.removeItem(fullKey);
        return defaultValue;
      }
      return item.value;
    } catch (error) {
      console.error(`Error parsing item with key "${fullKey}":`, error);
      this.storage.removeItem(fullKey); // 파싱에 실패하면 해당 항목 삭제
      return defaultValue;
    }
  }

  /**
   * 저장 항목 제거
   * @param key 키
   */
  removeItem(key: string): void {
    const fullKey = this.getFullKey(key);
    this.storage.removeItem(fullKey);
  }

  /**
   * 저장 항목 설정
   * @param key 키
   * @param value 값
   * @param ttl 유효 시간(밀리초)
   */
  setItem<T>(key: string, value: T, ttl?: number): void {
    const fullKey = this.getFullKey(key);
    const expiry = ttl ? Date.now() + ttl : undefined;
    const item: StorageItem<T> = { expiry, value };
    try {
      this.storage.setItem(fullKey, JSON.stringify(item));
    } catch (error) {
      console.error(`Error setting item with key "${fullKey}":`, error);
    }
  }

  /**
   * 전체 저장 키 가져오기
   * @param key 원본 키
   * @returns 접두사가 포함된 전체 키
   */
  private getFullKey(key: string): string {
    return `${this.prefix}-${key}`;
  }
}

export { StorageManager };
