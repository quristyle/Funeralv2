# VXE Table Plugin

vxe-table 및 vxe-pc-ui 기반의 테이블 컴포넌트 플러그인입니다.

## 내보내기

| 내보내기              | 타입 | 설명           |
| --------------------- | ---- | -------------- |
| `setupVbenVxeTable`   | 함수 | 초기화 설정 함수 |
| `useVbenVxeGrid`      | 함수 | 테이블 컴포저블 함수 |
| `VbenVxeGrid`         | 컴포넌트 | 테이블 컴포넌트       |
| `VxeTableGridColumns` | 타입 | 테이블 컬럼 타입     |
| `VxeTableGridOptions` | 타입 | 테이블 설정 타입   |
| `VxeGridProps`        | 타입 | 테이블 Props     |
| `VxeGridListeners`    | 타입 | 테이블 이벤트 타입   |

## 사용법

```ts
import {
  setupVbenVxeTable,
  useVbenVxeGrid,
  VbenVxeGrid,
} from '@vben/plugins/vxe-table';
```

## 초기화

애플리케이션 진입점에서 호출:

```ts
import { setupVbenVxeTable } from '@vben/plugins/vxe-table';
import { useVbenForm } from '@vben-core/form-ui';

setupVbenVxeTable({
  configVxeTable: (vxeUI) => {
    // VXE Table 설정
  },
  useVbenForm,
});
```

## 타입

```ts
import type {
  VxeTableGridOptions,
  VxeGridProps,
} from '@vben/plugins/vxe-table';
```
