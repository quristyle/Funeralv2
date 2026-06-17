# @vben/constants

여러 앱에서 공통으로 사용하는 상수 파일로, `@vben-core/shared/constants`의 모든 기능을 상속받습니다. 업무상 공통 상수가 있다면 여기에 둘 수 있습니다.

## 사용법

### 의존성 추가

```bash
# 대상 애플리케이션 디렉토리로 이동합니다. 예: apps/xxxx-app
# cd apps/xxxx-app
pnpm add @vben/constants
```

### 사용

```ts
import { LOGIN_PATH } from '@vben/constants';
```
