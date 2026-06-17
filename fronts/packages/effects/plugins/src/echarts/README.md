# ECharts Plugin

ECharts 차트 플러그인으로, 자주 사용하는 컴포넌트와 차트 유형이 사전 설정되어 있습니다.

## 내보내기

| 내보내기      | 타입 | 설명              |
| ------------ | ---- | ---------------- |
| `default`    | 객체 | echarts 인스턴스   |
| `EchartsUI`  | 컴포넌트 | 차트 컨테이너 컴포넌트 |
| `ECOption`   | 타입 | 차트 설정 타입      |
| `useEcharts` | 함수 | 컴포저블 함수      |

## 사용법

```ts
import { EchartsUI, useEcharts, ECOption } from '@vben/plugins/echarts';
```

## 타입

```ts
import type { ECOption } from '@vben/plugins/echarts';
```

## 사전 설정 컴포넌트

- TitleComponent
- TooltipComponent
- GridComponent
- LegendComponent
- ToolboxComponent
- DatasetComponent
- TransformComponent

## 사전 설정 차트

- BarChart
- LineChart
- PieChart
- RadarChart
