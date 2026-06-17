# Motion Plugin

@vueuse/motion 기반의 애니메이션 플러그인입니다.

## 내보내기

| 내보내기           | 타입 | 설명           |
| ----------------- | ---- | -------------- |
| `Motion`          | 컴포넌트 | 애니메이션 컴포넌트   |
| `MotionGroup`     | 컴포넌트 | 애니메이션 그룹 컴포넌트 |
| `MotionDirective` | 디렉티브 | 애니메이션 디렉티브   |
| `MotionPlugin`    | 플러그인 | Vue 플러그인   |

## 사용법

```ts
import { MotionPlugin, Motion, MotionDirective } from '@vben/plugins/motion';

app.use(MotionPlugin);
```

## 타입

```ts
import type { MotionOptions, MotionVariants } from '@vben/plugins/motion';
```
