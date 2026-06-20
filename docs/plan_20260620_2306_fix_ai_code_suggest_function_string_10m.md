# 작업 계획서: AI 코드 추천 기능에서 함수 소스 코드 문자열 노출 버그 수정

## 1) 문제 요약
* **현상**: `AiCodeSuggester` 컴포넌트 호출 시 한글명에 매핑되는 추천 코드 문자열 대신 `function suggestCommonCodeByAI(word) { ... }`와 같이 API 클라이언트 함수의 자바스크립트 코드 소스 자체가 추천 뱃지 영역에 렌더링되는 현상이 발견되었습니다.
* **원인**:
  * `withDefaults`에서 팩토리 함수인 `suggestApi: () => suggestCommonCodeByAI`를 제공함으로써 Vue 3 Prop Compiler가 이 람다 자체를 기본값으로 주입했습니다.
  * 결과적으로 `props.suggestApi(queryText)`를 호출했을 때, 람다 함수가 매개변수를 무시하고 단순히 `suggestCommonCodeByAI` 함수 자체를 반환하였으며, 이 함수가 string으로 형변환되면서 UI에 소스코드가 렌더링되었습니다.

---

## 2) 설계 요약
* **수정 대상 파일**: 
  * `fronts/apps/funeralv2/src/components/ai-code-suggester/ai-code-suggester.vue`
* **설계 상세**:
  * `withDefaults` 셋업 시 `suggestApi` 의 기본값 지정을 람다 팩토리가 아닌 `suggestCommonCodeByAI` 함수 자체로 치환합니다.
  * 이를 통해 `props.suggestApi(queryText)` 실행 시 람다 가로채기 없이 정상적으로 Axios 호출이 수행되며 백엔드 추천 결과(string)를 온전하게 전달받게 됩니다.

---

## 3) 구현 계획
* **Task 1: prop 기본값 설정 수정** (완료)
* **Task 2: 컴파일 및 컴포넌트 검증** (진행 중)

---

## 4) 자가 코드 리뷰 계획
* 타입스크립트 타입 체크 검사를 수행하여 Prop 형식 및 API 결과값 바인딩에 에러가 없는지 체크합니다.
* 팩토리 함수 래핑이 제거된 후 `props.suggestApi`가 온전히 API 클라이언트 함수 객체를 가리키는 지 확인합니다.
