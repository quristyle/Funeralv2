# Changelog

## v5.7.13 (2026-06-21)
* **feat**: 공통코드 그룹 관리(SortOrder) 정렬순서 항목 추가 및 DB 마이그레이션 적용
  * **변경 사항 요약**:
    * 백엔드 [CommonCodeGroup.cs](file:///C:/Funeralv2/microservices/AuthServer/Entities/CommonCodeGroup.cs) 엔티티 및 [CommonCodeGroupDto.cs](file:///C:/Funeralv2/microservices/AuthServer/DTOs/CommonCodeGroupDto.cs) DTO 파일에 `SortOrder` 속성을 추가함.
    * `dotnet-ef` 마이그레이션 코드를 가공하여 기존 테이블 충돌을 차단하고 `common_code_groups` 테이블에만 `sort_order` 컬럼이 추가되도록 수동 최적화 후 `database update` 반영 완료.
    * 프론트엔드 API 타입 정의 및 [data.ts](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/common-code/data.ts) 테이블 컬럼/폼 스키마와 [group-form.vue](file:///C:/Funeralv2/fronts/apps/funeralv2/src/views/system/common-code/modules/group-form.vue) 폼 정의에 정수 입력 제어가 가능한 `InputNumber` 기반 `sortOrder` 정렬 순서 필드 추가.
  * **영향 범위**: 공통코드 그룹 데이터 모델 및 UI 컴포넌트

## v5.7.12 (2026-06-21)
* **feat**: 공통코드 다국어 번역 리소스(ko-KR, en-US) DB 일괄 적재
  * **변경 사항 요약**:
    * 데이터베이스 `scom.common_codes` 에 설정된 `i18n_key`를 기반으로, `scom.i18n_resources`에 존재하지 않는 번역 자원 누락분을 자동 조회하여 총 140건의 번역 데이터 동기화 완료.
    * 한국어(`ko-KR`) 로케일에는 공통코드 한글 명칭을 바인딩하고, 영어(`en-US`) 로케일에는 첫 글자를 대문자화한 영문 코드값과 성별 명칭(`Male`/`Female` 고도화)을 매핑하여 다국어 데이터 정합성 보장.
  * **영향 범위**: 다국어 번역 데이터베이스 리소스

## v5.7.11 (2026-06-20)
* **docs**: 그리드(VXE Grid) 데이터 뱃지 렌더링 AI 지침 추가
  * **변경 사항 요약**:
    * Boolean 값 및 상태 코드를 표기할 때 Ant Design Vue의 `Tag`를 활용한 뱃지 형식으로 변환하여 렌더링하도록 강제하는 프롬프트 가이드 문서 [2.AI.md](file:///C:/Funeralv2/docs/prompts/2.AI.md)를 신규 정의.
    * 엄격한 타입 안정성 유지를 위해 `slots.default` 및 `h()` 렌더링 패턴을 사용하는 구체적인 구현 예제 가이드 포함.
  * **영향 범위**: 프로젝트 AI 가이드라인 문서

## v5.7.10 (2026-06-20)
* **feat**: 마이그레이션된 공통코드 및 그룹 코드값을 대문자로 일괄 변환
  * **변경 사항 요약**:
    * 사용자의 요청에 따라 `scom.common_code_groups` 및 `scom.common_codes`에 삽입된 데이터 중 변환 대상(`created_by = 'MigrationSystem'`)에 대해 `UPPER()` SQL 함수를 적용하여 대문자 처리 완료.
    * 대문자 변환된 코드값에 발맞추어 `i18n_key` 데이터도 `'common.code.대문자값'` 포맷으로 일치시킴으로써 데이터 무결성 보장.
  * **영향 범위**: 공통코드 데이터베이스 적재 데이터

## v5.7.9 (2026-06-20)
* **feat**: 임의의 공통코드 대량 데이터를 DB 스키마 구조에 맞춰 변환 및 직접 인서트 수행
  * **변경 사항 요약**:
    * 사용자가 제공한 86개의 공통코드(그룹 및 코드 포함)를 정제하고, 한글 코드값 및 오타(`complate` -> `complete`, `quetion` -> `question` 등)를 식별 표준 코드로 변환 및 매핑하는 스크립트 작성.
    * `scom.common_code_groups` 및 `scom.common_codes` 테이블에 UUID를 자동 생성 및 결합하여 중복 체크 가드 하에 트랜잭션 단위로 총 15개 그룹과 71개 코드 적재 성공.
    * Windows 11 환경에서 PostgreSQL 드라이버(`psycopg2-binary`)를 활용한 데이터 적재 및 DB 직접 검증(Verification) 쿼리 테스트 완료.
  * **영향 범위**: 공통코드 데이터베이스 적재 데이터

## v5.7.8 (2026-06-20)
* **fix**: 공통코드 그룹 폼 및 코드 폼 모달에서 비고(remark) 입력란 미노출 오류 해결
  * **변경 사항 요약**: 
    * `data.ts`의 `groupFormSchema` 및 `codeFormSchema` 내의 비고 필드 component 속성이 존재하지 않는 명칭인 `'InputTextArea'`로 정의되어 있던 버그를 발견하여 Vben Form 표준 식별자인 `'Textarea'`로 치환 수정.
  * **영향 범위**: 공통코드 관리 모듈 (그룹 추가/수정 팝업, 코드 추가/수정 팝업)

## v5.7.7 (2026-06-20)
* **feat**: 공통코드 상태 입력 방식을 스위치(Switch) 버튼으로 교체 및 양방향 형변환 적용
  * **변경 사항 요약**: 
    * `data.ts`의 `codeFormSchema` 중 `status` 컴포넌트를 라디오박스에서 `'Switch'` 컴포넌트로 변경하고 '사용/미사용' 라벨 바인딩.
    * 백엔드의 `1/0` 정수 데이터 형식과 프론트엔드 스위치의 `true/false` 불리언 데이터 형식 사이의 데이터 정합성을 확보하기 위해 `code-form.vue` 내 양방향 데이터 매핑(In/Out) 로직 추가.
    * 컴포넌트 호출 시 타입 안정성을 위해 `CommonCodeParams` 캐스팅 처리 보완.
  * **영향 범위**: 공통코드 관리 모듈 (코드 추가/수정 팝업)

## v5.7.6 (2026-06-20)
* **feat**: 공통코드 수정 시 입력 제한 및 정렬순서/상태 컴포넌트 개선
  * **변경 사항 요약**: 
    * `code-form.vue`에서 수정(Edit) 모드일 때 데이터 정합성 유지를 위해 `codeValue` 필드를 `disabled: true` 설정을 적용하여 수정을 차단함.
    * 모달 닫힘 및 추가 모드 진입 시점에 스키마를 원래 상태(`disabled: false`)로 복구하여 다음 사용 시 오작동 방지.
    * 수정 모드일 때는 코드 영문명 자동 추천이 불필요하므로 `<AiCodeSuggester>` 컴포넌트에 `v-if="!isUpdate"` 속성을 바인딩하여 감춤.
    * `data.ts`의 `codeFormSchema` 중 `sortOrder`를 `step: 1`, `precision: 0` 속성을 추가하여 확실히 정수를 제어하는 `InputNumber`로 스펙 강화.
    * `status` (상태) 컴포넌트를 기존의 드롭다운(Select)에서 라디오 그룹(RadioGroup) 형태로 변경하여 직관성 높은 UI로 개선.
  * **영향 범위**: 공통코드 관리 모듈 (코드 추가/수정 팝업)

## v5.7.5 (2026-06-20)
* **feat**: 코드 그룹 수정 시 그룹코드 입력 차단 및 AI 추천 제거
  * **변경 사항 요약**: 
    * `group-form.vue`에서 수정(Edit) 모드일 때 데이터 정합성 유지를 위해 `groupCode` 필드의 `disabled: true` 설정을 적용하여 수정을 차단함.
    * 추가(Create) 모드 진입 및 모달 닫힘 시점에 스키마를 원래 상태(`disabled: false`)로 동적 복구하여 UI 복원.
    * 수정 모드일 때는 코드 영문명을 자동 추천받을 필요가 없으므로 `<AiCodeSuggester>` 컴포넌트에 `v-if="!isEdit"` 속성을 지정하여 비노출 처리함.
  * **영향 범위**: 공통코드 관리 모듈 (그룹 추가/수정 팝업)

## v5.7.4 (2026-06-20)
* **fix**: 공통코드 그룹 수정(편집) 버튼 클릭 시 추가 모달창으로 뜨는 오류 해결
  * **변경 사항 요약**: 
    * 백엔드 `CommonCodeEndpoints.cs`에 누락되어 있던 코드 그룹 수정 라우팅(`PUT /system/common-code/groups/{id}`)을 추가 매핑하여 서비스의 `UpdateGroupAsync` 메서드 연동.
    * 프론트엔드 API 클라이언트(`common-code.ts`)에 `updateCommonCodeGroup` 함수 선언 및 호출 연동.
    * `group-form.vue` 모달창 오픈 시 파라미터(`record`) 존재 여부에 따라 수정/등록 모드(`isEdit`)를 판별하여 데이터 바인딩 및 모달 제목이 동적으로 노출되도록 보완.
    * 모달창이 닫힐 때(`onOpenChange` false) 폼 필드와 상태 값을 완전 리셋 처리하여 사이드이펙트 방지.
  * **영향 범위**: 공통코드 관리 모듈 (그룹 추가/수정 팝업) 및 백엔드 API 라우팅

## v5.7.3 (2026-06-20)
* **feat**: AI 채팅 사이드바 너비를 브라우저의 localStorage에 영속화
  * **변경 사항 요약**: 
    * `localStorage` 키 `vben_ai_chat_sidebar_width`를 활용하여 사용자가 조정한 마지막 사이드바 너비를 영속 저장.
    * 드래그 조절이 완료되어 마우스 업(`stopResize`) 되는 순간 최종 너비를 1회 동기화하여 드래그 중 I/O 오버헤드로 인한 프레임 드롭 방지.
    * 새로고침 시 이전에 조정한 유효 범위(280px ~ 800px)의 값을 로컬 저장소로부터 복구하고, 없을 경우에만 디폴트값(384px)을 복구하는 안전성 가드 확보.
  * **영향 범위**: 레이아웃 및 영속화 레이어

## v5.7.2 (2026-06-20)
* **feat**: 메인 화면과 AI 채팅 고정 사이드바 사이에 드래그 조절용 스플리터 바 추가
  * **변경 사항 요약**: 
    * `layout.vue`에 반응형 너비 상태 `aiChatWidth` 변수와 전역 마우스 드래그 리스너(`mousemove`, `mouseup`) 관리 구현.
    * 마우스로 드래그 가능한 `w-1 hover:w-1.5 cursor-col-resize` 스플리터 구분선 바를 추가하고, 테마 색상(`bg-primary`)으로 시각적 피드백 제공.
    * 드래그 조절 상태일 때 드래그 랙(Lag)을 방지하기 위해 사이드바에서 CSS `transition` 애니메이션 제외 및 `select-none` 클래스를 적용하여 사용자 경험(UX) 최적화.
    * 최소 280px ~ 최대 800px의 안전 너비 가드 범위 설정.
  * **영향 범위**: 사이드바 레이아웃 시스템

## v5.7.1 (2026-06-20)
* **feat**: AI 어시스턴트 아이콘 클릭 시 Drawer 오버레이를 거치지 않고 즉시 핀 고정 사이드바(`ai-chat-content.vue`)로 토글 노출되도록 개선
  * **변경 사항 요약**: 
    * 헤더의 AI 아이콘 클릭 시 Drawer 오버레이 실행 단계를 제거하고, 레이아웃 단의 `isAiChatPinned` 전역 상태를 토글하여 즉시 우측 영역을 점유하도록 설계 개선.
    * 사용하지 않는 레거시 컴포넌트인 `ai-chat.vue` 및 `ai-chat-drawer.vue` 파일 삭제.
    * `widgets/ai-chat/index.ts`에서 미사용 컴포넌트의 수출(export) 정의를 제거하여 청결한 의존성 관리.
  * **영향 범위**: 레이아웃 헤더 및 사이드바 레이아웃 시스템
  * **마이그레이션 가이드**: 기존 `AiChat` 컴포넌트는 더 이상 사용되지 않으므로 헤더 등 레이아웃 영역에서 `AiChatButton`을 직접 호출하여 상태를 전역적으로 토글합니다.

## v5.7.0 (이전 이력)
* **feat**: AI 채팅을 위한 핀 고정 기능 도입
  * `isAiChatPinned` 상태에 따라 화면 우측에 정적으로 레이아웃 영역을 384px(w-96) 만큼 점유하는 고정 사이드바 구조 구현.
  * 레이아웃 및 탭바 높이를 감산한 `contentHeightStyle`을 제공하여 화면 세로 길이를 초과하는 스크롤 팽창 버그 수정.
  * 전체화면 AI 채팅 API(`streamChatMessage`)를 Layout 패키지로 주입하기 위해 Vue `provide/inject` 의존성 주입 패턴 적용.
