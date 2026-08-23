export class StateHandler {
  private condition: boolean = false;
  private rejectCondition: ((reason?: Error) => void) | null = null;
  private resolveCondition: (() => void) | null = null;

  isConditionTrue(): boolean {
    return this.condition;
  }

  reset() {
    this.condition = false;
    this.clearPromises();
  }

  // 트리거 상태가 false일 때 reject
  setConditionFalse() {
    this.condition = false;
    if (this.rejectCondition) {
      this.rejectCondition(new Error('Condition was set to false'));
      this.clearPromises();
    }
  }

  // 트리거 상태가 true일 때 resolve
  setConditionTrue() {
    this.condition = true;
    if (this.resolveCondition) {
      this.resolveCondition();
      this.clearPromises();
    }
  }

  // condition이 true가 될 때까지 기다리는 Promise 반환
  waitForCondition(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (this.condition) {
        resolve(); // condition이 이미 true인 경우 즉시 resolve
      } else {
        this.resolveCondition = resolve;
        this.rejectCondition = reject;
      }
    });
  }

  // resolve/reject 함수 정리
  private clearPromises() {
    this.resolveCondition = null;
    this.rejectCondition = null;
  }
}
