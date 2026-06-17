# @vben/plugins

이 디렉토리는 프로젝트에 통합된 타사 라이브러리 및 관련 플러그인을 저장하는 데 사용됩니다. 각 플러그인에는 재사용 가능한 로직, 설정 및 컴포넌트가 포함되어 있어 프로젝트에서 통합 관리 및 호출이 편리합니다.

## 주의

모든 타사 플러그인은 `subpath` 형식으로 도입해야 합니다. 예:

`echarts`를 예로 들면, 다음과 같이 도입합니다:

**packages.json**

```json
"exports": {
    "./echarts": {
      "types": "./src/echarts/index.ts",
      "default": "./src/echarts/index.ts"
    }
  }
```

**사용 방법**

```ts
import { useEcharts } from '@vben/plugins/echarts';
```

이렇게 하면 애플리케이션에서 플러그인 사용 여부를 직접 선택할 수 있으며, 플러그인 도입 및 부작용으로 인해 번들 크기가 커지는 것을 방지하고 필요한 플러그인만 도입할 수 있다는 장점이 있습니다.
