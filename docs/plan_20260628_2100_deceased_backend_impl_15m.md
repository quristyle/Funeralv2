# 구현 계획서 (Implementation Plan)

- **작성일시**: 2026-06-28 21:00 (23:08 갱신)
- **태스크명**: 고인 정보 생성/수정 관련 백엔드 연동 및 보강 + DateTime 변환 오류 수정 + 밸리데이션 누락 시 입력 필드 포커싱 + 공통 AutoDatePicker 적용 + PostgreSQL DateTime Kind 오류 해결 + 영정사진 업로드 401 Unauthorized 오류 조치 + 목록 그리드 개선 및 수정 팝업 빈값 문제 조치 + Core Icons 패키지 보강 + 업로드 완료 후 파일 정보 언래핑 버그 조치 + 고인사진 전용 업로드 폴더 분리 + 썸네일 전용 URL 호출 연동 + 썸네일 파일 물리 저장 경로 매핑 오류 개선 + 고인 목록 그리드 썸네일 연동 + 고인 목록 조회 시 사진 데이터 누락 해결 + 빈소 배정 화면 Cascading BizSelect 연동 + AutoDatePicker 타입 예외 조치 + GetBizFolder 슬래시 제거에 따른 썸네일 폴더 경로 결합 오류 수정 + 목록 그리드 검색 필터 추가 및 백엔드 다중 조건 동적 쿼리 연동 + 입관 기간 검색 기능 개선 및 백엔드 매핑 정정 + 검색 일시의 시분초 고정 포맷팅 정밀화 + 성별 필터에 DictSelect 연동 (그룹코드: SEX) + DictSelect 공통 컴포넌트 showAll 기능 추가 + 호실 필터의 Cascading params 다차원 조건 바인딩 + 고인 목록 다중 호실 배정 이력 검색 쿼리 고도화 및 건물/층/호실 정보 텍스트 병합 프로젝션 구현 + 고인 목록 그리드에 영정사진 실제 편집(크롭) 및 저장 모달 기능 구현 + 크롭 모달 API 매개변수 및 버튼 타입 오류 조치 + TUI Color Picker CSS Import 에러 수정 + 영정사진 편집 환경의 공간 협소 문제 해소를 위한 새 탭(독립 페이지) 및 부모-자식 창 간 동기화 구현 + 영정사진 편집 탭 레이아웃 뷰포트 극대화 및 세로 낭비 제거 + TUI Image Editor 내장 자르기(Crop) 서브메뉴(UL/DIV 프리셋)에 직접 세로형 비율(3:4, 2:3, 5:7, 9:16) 단축 옵션 동적 주입(DOM Injection) 구현
- **예상 소요 시간**: 189분

---

## 1. 문제 요약 (Problem Summary)
1. **라우팅 경로 오류**: 신규 고인 등록 시 경로 매칭 실패(404 Not Found). (완료)
2. **DateTime 형식 오류**: `JsonException` 발생. (완료)
3. **포커싱 기능 부재**: 필수 항목 누락 시 포커싱 누락. (완료)
4. **날짜 기본값 자동화 요구**: 공통 날짜 컨트롤 요구. (완료)
5. **PostgreSQL DateTime Kind 오류**: `Kind=Unspecified` 에러 발생. (완료)
6. **영정사진 업로드 401 Unauthorized 오류**: 토큰 누락. (완료)
7. **수정/삭제 텍스트 버튼 가독성**: 수정/삭제 버튼이 텍스트 링크로 되어 있음. (완료)
8. **고인사진 미노출**: 영정사진 썸네일을 목록 그리드에서 식별할 수 없음. (완료)
9. **수정 시 고인명 및 데이터 누락**: 수정 팝업이 열릴 때 고인 정보가 비워져 나타남. (완료)
10. **Core Icons 패키지 아이콘 누락**: `Pencil` Lucide 아이콘 누락. (완료)
11. **업로드 완료 후 파일 다운로드 URL undefined 문제**: `id` 필드가 `undefined`로 평가된 버그. (완료)
12. **고인사진 업로드 시 bizType 누락 및 폴더 미분리**: `basecom` 폴더로 저장되는 현상. (완료)
13. **고인사진 폼의 원본 파일 다운로드 과다 호출**: 썸네일 최적화 필요. (완료)
14. **썸네일 물리 저장 경로 매핑 오류**: 고인 영정사진의 썸네일 생성 시 경로 어긋남 문제. (완료)
15. **고인 목록 그리드의 원본 사진 로드 부하**: 목록 화면의 그리드에서도 원본 크기 이미지 로드 문제. (완료)
16. **고인 목록 조회 시 고인사진 정보 누락**: 목록 조회 호출 시 영정사진 필드가 `null`로 반환되던 버그. (완료)
17. **빈소 배정 화면의 사용자 편의성 부족**: 회사-건물-층의 위계와 분리되어 빈소를 할당하기 어려웠던 문제. (완료)
18. **AutoDatePicker 연동 중 date.locale is not a function 런타임 오류**: 날짜 문자열에 대한 Dayjs 객체 변환 픽스 완료. (완료)
19. **GetBizFolder 내 슬래시(/) 제거로 인한 썸네일 저장소 경로 합쳐짐 버그**: 슬래시 필터 완화 완료. (완료)
20. **목록 그리드 검색 기능 부재**: 검색 기능 추가 완료. (완료)
21. **검색 기간 조건 불일치 버그**: 입관/발인일자 필터 맵핑 교정 완료. (완료)
22. **검색 기간 일시의 클라이언트 로컬 기준 시분초 전달 문제**: 포맷 정규화 완료. (완료)
23. **하드코딩된 성별 셀렉트 필터**: 성별 검색 DictSelect 전환 완료. (완료)
24. **DictSelect show-all 미작동 버그**: DictSelect showAll 옵션 기능 추가 완료. (완료)
25. **호실 필터링의 상위 종속성 단일화 현상**: 호실 `BizSelect` params 다차원 조건 바인딩 완료. (완료)
26. **고인 목록 검색 시 호실 배정 조건 미반영 및 정보 출력 오류**: DeceasedRoom 테이블 조인 필터링 및 "건물 층 호실" 목록 결합 합성 완료. (완료)
27. **고인 영정사진 실제 편집 기능의 부재**: 영정사진 크롭 모달 신규 생성 및 이미지 업로드/고인 메타데이터 갱신 연동 완료. (완료)
28. **크롭 모달 컴포넌트 내 API 인수 개수 불일치 및 버튼 타입 미지원 에러**: saveDeceasedDetail 2개 인수로 전달하도록 수정 및 Button type default 값 처리 완료. (완료)
29. **Vite Import Analysis 에러**: tui-color-picker.css 임포트 제거 완료. (완료)
30. **에디터 조작 화면의 공간 협소 결함**: 모달(700px) 내부에서 작업 시 공간 협소 결함 조치 완료. (완료)
31. **편집창 세로 공간의 과도한 낭비**: 세로 화면 공간을 낭비 차단 완료. (완료)
32. **세로 비율 크롭 기능의 제한성**: TUI Image Editor 내장 자르기 서브메뉴(툴바 프리셋)에 가로 비율 위주로 탑재되어 세로 형태의 비율 조절이 불가능한 현상.

## 2. Design Summary (설계 요약)
- **백엔드 (Endpoints)**: `DeceasedEndpoints.cs`에 `/detail`에 대응하는 ID 없는 PUT 엔드포인트를 추가로 정의하여 신규 생성을 유연하게 지원. (완료)
- **백엔드 (Services)**: `DeceasedService.cs`에서 제공하는 `SaveDeceasedDetailAsync`가 빈 ID 문자열이나 존재하지 않는 ID가 들어올 시 새 Guid를 생성하도록 기존 로직 유지. (완료)
- **프론트엔드 (API Client)**: `id` 매개변수의 유무에 따라 URL을 동적으로 정돈해 호출하도록 리팩토링. (완료)
- **프론트엔드 (Validation & Formatting)**: 
  - 필수 값인 "작고 일시"(`deathDate`)에 대해 미입력 시 전송을 차단하는 클라이언트 단 유효성 검사 추가. (완료)
  - 전송 날짜 형식을 표준 ISO 8601(`YYYY-MM-DDTHH:mm:ss`) 규격으로 변경하고, 비어있는 값은 빈 문자열 대신 `null`을 명시적으로 대입하여 역직렬화 오류를 차단. (완료)
- **프론트엔드 (Input Focus)**:
  - `DeceasedBasicForm` 컴포넌트에 `ref="basicFormRef"` 및 `focusName` 메소드를 노출(expose)하여 유효성 검사 오류 시 고인 성명 입력란에 초점을 보냄. (완료)
  - `DatePicker` 컴포넌트에 `ref="deathDatePickerRef"`를 연결하여 작고 일시 누락 시 데이트 피커에 초점을 보냄. (완료)
- **공통 컴포넌트 (`AutoDatePicker`)**:
  - `AutoDatePicker.vue` 공통 컴포넌트 개발 및 발인일시 부분에 `offset-days="3"` 옵션을 적용하여 자동으로 3일 뒤 날짜가 기본 노출되도록 처리. (완료)
- **백엔드 (DateTime Kind 처리)**:
  - `DeceasedService.cs`에 `SpecifyUtc` 헬퍼 메서드 추가 및 모든 날짜 필드를 DB에 저장/수정하기 직전 `DateTime.SpecifyKind(..., DateTimeKind.Utc)`로 변환하여 PostgreSQL 호환성 해결. (완료)
- **프론트엔드 (Upload Headers 바인딩)**:
  - `@vben/stores`의 `useAccessStore` 로부터 헤더 토큰을 추출하여 `<Upload>`에 바인딩. (완료)
- **그리드 및 아이콘 개선**:
  - `index.vue`에서 텍스트 버튼(수정, 삭제)을 `@vben/icons`의 `Pencil`, `Trash2` 아이콘으로 전환. (완료)
  - 그리드에 `memorialPhotoUrl`을 보여주는 `고인사진` 컬럼을 썸네일 구조의 커스텀 슬롯으로 구현. (완료)
- **수정 시 상세 데이터 바인딩 버그 픽스**:
  - `getDeceasedDetail(id)` 조회 시 백엔드 규격에 맞춰 `result?.[0]` DTO 객체를 언래핑하여 `formModel.value`에 바인딩. (완료)
- **Core Icons 패키지 수정**:
  - `packages/@core/base/icons/src/lucide.ts` 내 재배포 목록에 `Pencil` 및 `Image` 아이콘 추가 완료. (완료)
- **파일 업로드 응답 처리 정합성 개선**:
  - `deceased-photo-form.vue` 내 `handlePhotoUpload`에서 `res.data` 뿐만 아니라 `res.data.result` 배열이 존재할 시 첫 번째 인덱스(`result[0]`)를 `fileData` 객체로 추출하도록 로직 보강. (완료)
- **고인사진 전용 업로드 폴더 분리 및 파라미터 보강**:
  - `deceased-photo-form.vue` 의 `<Upload>` 컴포넌트에 `:data` 바인딩 적용 및 `FileService.cs` DECEASED 분기 추가. (완료)
- **썸네일 호출 연동**:
  - `deceased-photo-form.vue` 내에 `thumbnailUrl` computed 속성 추가 및 이미지 태그 바인딩 수정. (완료)
- **썸네일 물리 저장 경로 매핑 정합성 개선**:
  - `FileService.cs` 의 `GetPresetImageAsync` 에 다중 세그먼트 Join 방식 반영. (완료)
- **목록 그리드 썸네일 연동**:
  - `deceased/index.vue` 목록 그리드의 photo 슬롯 이미지 소스를 인라인 템플릿 제어를 통해 썸네일 주소를 사용하게끔 수정. (완료)
- **고인 목록 DTO 매핑 보완**:
  - `DeceasedService.cs` 의 `GetDeceasedListAsync` select new DeceasedDto 내에 3개 필드 매핑 보강. (완료)
- **빈소 배정 화면 개선 (회사-건물-층-호실 다단계 Cascading 구조)**:
  - `deceased-rooms-form.vue` 내에 회사-건물-층-호실 다단계 cascading 선택 및 일시 컴포넌체 교체 완료. (완료)
- **AutoDatePicker 문자열 파싱 및 양방향 반환 포맷팅 개선**:
  - `AutoDatePicker.vue` 문자열 파싱 및 value-format 바인딩 정밀화 완료. (완료)
- **GetBizFolder 하위 슬래시 문자 필터링 완화 (보안 우회 보장)**:
  - `FileService.cs` 내 `GetBizFolder` 의 슬래시 무효화 로직 완화 및 Trim 처리 보정 완료. (완료)
- **고급 검색 필터 백엔드/프론트엔드 연동**:
  - `DeceasedSearchDto` 추가, `DeceasedEndpoints` 매칭 수정, `DeceasedService` Linq 필터 구현, `deceased/index.vue` 검색 폼 UI 마크업 및 proxyConfig 바인딩 연동 완료. (완료)
- **입관 기간 검색 기능 개선 및 백엔드 매핑 정정**:
  - 입관 및 발인 필터 컬럼을 실제 UI의 입관 일시(`FuneralDate`) 및 발인 일시(`BurialDate`)에 대응시켜 정정 완료. (완료)
- **날짜 검색 기간 파라미터 시분초 정규화 및 날짜 전송 구현**:
  - `index.vue` 시작일/종료일에 시분초 정밀 포맷 적용 완료. (완료)
- **성별 필터 DictSelect 전환**:
  - `index.vue` 216라인 성별 검색을 `DictSelect`로 전환 완료. (완료)
- **DictSelect 공통 컴포넌트 내 showAll 기능 도입**:
  - `DictSelect.vue` `showAll` 기능 보강 완료. (완료)
- **호실 필터 Cascading Params 개선**:
  - `index.vue` 204라인 호실 필터 `BizSelect` params 복합 바인딩 완료. (완료)
- **고인 목록 다중 호실 배정 이력 검색 및 텍스트 합성 프로젝션**:
  - `DeceasedService.cs` 내에서 `DeceasedRoom` 테이블 조인 필터링 및 "건물 층 호실" 목록 결합 합성 기능 완료. (완료)
- **고인 영정사진 실제 편집(크롭) 및 저장 모달 기능 구현**:
  - `deceased-photo-crop-modal.vue` 및 크롭 모달 타입 예외 정정 완료. (완료)
- **TUI Color Picker CSS Import 에러 조치**:
  - `tui-color-picker.css` 임포트 코드 제거로 빌드 오류 차단 완료. (완료)
- **영정사진 편집 독립 페이지(새 탭) 구성 및 부모창 동적 동기화**:
  - `core.ts` 단독 라우트 등록, `photo-editor.vue` 화면 개발 및 `window.open` 연동 완료. (완료)
- **영정사진 편집 탭 레이아웃 뷰포트 극대화 및 세로 낭비 제거**:
  - cssMaxWidth/cssMaxHeight 크기 대폭 상향, calc(100vh - 100px) 높이 스타일, inner-container 100% 강제 오버라이딩 적용 완료. (완료)
- **TUI Image Editor 내장 자르기(Crop) 서브메뉴(UL/DIV 프리셋)에 직접 세로형 비율(3:4, 2:3, 5:7, 9:16) 단축 옵션 동적 주입(DOM Injection) 구현**:
  - **툴바 버튼 강제 탐색 및 주입 (`injectCustomCropRatios`)**: TUI includeUI 초기화가 완료된 직후 에디터 하단의 `.tui-image-editor-menu-crop` 엘리먼트 내부에 있는 프리셋 리스트(`.tui-image-editor-submenu-item` 또는 `align` 클래스)를 취득.
  - **버튼 및 라벨 렌더링**: TUI 내부 규격에 준하는 `li` 또는 `div` 엘리먼트 기반 `custom-vertical-ratio` 클래스를 부여하고 `3:4 (영정)`, `2:3 (세로)`, `5:7 (세로)`, `9:16 (세로)` 라벨 주입.
  - **실시간 크롭 이벤트 매칭**: 각 동적 버튼 클릭 시 TUI 기본 프리셋의 active/checked 스타일을 청소하고 자신의 버튼에 active 스타일을 매칭하며, `editor.startDrawingMode('CROPPER')` 및 `editor.setCropzoneAspectRatio(item.ratio)`를 강제 호출하여 화면 하단 툴바에 완벽한 세로 비율 자르기 프리셋 적용.
  - **기본 버튼 클릭 싱크**: 기존 TUI 기본 버튼들이 클릭될 때 우리가 추가한 동적 세로형 버튼들의 active를 자동으로 지워주는 안전 클릭 리스너 결합.

## 3. Implementation Plan (구현 계획)
1. **백엔드 엔드포인트 수정**: `DeceasedEndpoints.cs` 파일에 `group.MapPut("/detail", ...)` 엔드포인트를 추가 매핑. (완료)
2. **프론트엔드 API 클라이언트 수정**: `fronts/apps/funeralv2/src/api/building/index.ts` 파일의 `saveDeceasedDetail`에서 `id` 유무에 따른 동적 URL 라우팅 설정. (완료)
3. **날짜 포맷팅 및 유효성 검사 수정**: `deceased-form-modal.vue` 파일 내 저장 시 필수 밸리데이션 검사 추가 및 ISO 8601 포맷 전환/null 처리 구현. (완료)
4. **필수 필드 포커싱 구현**: `deceased-basic-form.vue` 및 `deceased-form-modal.vue` 내 포커스 이동 처리. (완료)
5. **AutoDatePicker 공통 컴포넌트 구현 및 적용**: `AutoDatePicker.vue` 생성 및 적용. (완료)
6. **DateTime Kind UTC 강제 지정 구현**: `DeceasedService.cs` 내 `SpecifyUtc` 헬퍼 메서드 도입 및 저장 부분 연동. (완료)
7. **파일 업로드 인증 헤더 추가**: `deceased-photo-form.vue`에 `useAccessStore` 적용 및 `<Upload>` 컴포넌트에 `:headers` 바인딩 처리. (완료)
8. **목록 그리드 개선 (아이콘 및 고인사진 컬럼 추가)**: `deceased/index.vue`에 Pencil, Trash2 아이콘 추가 및 photo 슬롯 렌더링 추가. (완료)
9. **상세 데이터 바인딩 버그 수정**: `deceased-form-modal.vue`에서 `getDeceasedDetail` 호출 후 `(detail as any)?.result?.[0]` DTO 객체를 언래핑하여 `formModel.value`에 바인딩. (완료)
10. **Core Icons 패키지 수정**: `packages/@core/base/icons/src/lucide.ts`에 `Pencil` 및 `Image` 아이콘 재수출 추가. (완료)
11. **업로드 응답 래핑 해제 픽스**: `deceased-photo-form.vue`에서 `rawData.result[0]`을 추출하고 `fileData.downloadUrl` 매핑 적용. (완료)
12. **업로드 파라미터 `:data` 바인딩 추가**: `deceased-photo-form.vue` 의 `<Upload>` 컴포넌트에 `:data` 바인딩 적용. (완료)
13. **백엔드 DECEASED 전용 폴더 설정**: `FileService.cs` 의 `GetBizFolder` 에 DECEASED 분기 추가. (완료)
14. **고인사진 썸네일 이미지 적용**: `deceased-photo-form.vue` 내 `thumbnailUrl` computed 속성 추가 및 이미지 태그 바인딩 수정. (완료)
15. **썸네일 물리 저장 경로 매핑 정정**: `FileService.cs` 의 `GetPresetImageAsync` 에 다중 세그먼트 Join 방식 반영. (완료)
16. **그리드 photo 슬롯 썸네일 주소 맵핑**: `deceased/index.vue` 내 이미지 src를 썸네일 엔드포인트로 인라인 적용. (완료)
17. **고인 목록 DTO 매핑 보완**: `DeceasedService.cs` 의 `GetDeceasedListAsync` select new DeceasedDto 내에 3개 필드 매핑 보강. (완료)
18. **빈소 배정 화면 BizSelect 및 AutoDatePicker 연동**: `deceased-rooms-form.vue` 연동 완료. (완료)
19. **AutoDatePicker 날짜 문자열 포맷팅 처리 정교화**: 문자열 입력 시 dayjs 래핑 및 `value-format` 유무 분석 분기 추가. (완료)
20. **GetBizFolder 슬래시 보존 처리**: `FileService.cs` 내 `GetBizFolder` 의 슬래시 무효화 로직 완화 및 Trim 처리 보정 완료. (완료)
21. **검색 기능 관련 백엔드/프론트엔드 연동 구현**: `DeceasedSearchDto` 추가, `DeceasedEndpoints` 매칭 수정, `DeceasedService` Linq 필터 구현, `deceased/index.vue` 검색 폼 UI 마크업 및 proxyConfig 바인딩 연동 완료. (완료)
22. **입관 기간 검색 기능 보완 및 백엔드 매핑 정정**: `index.vue` 249라인 검색 기간 라벨을 `"입관 기간"`으로 변경하고 백엔드 날짜 비교 대상 컬럼을 `FuneralDate` 및 `BurialDate`로 교정 완료. (완료)
23. **날짜 검색 전송 포맷 시분초 고정 정규화**: `index.vue` 내 `format('YYYY-MM-DDT00:00:00')` 및 `format('YYYY-MM-DDT23:59:59')` 전송 처리 완료. (완료)
24. **성별 필터 DictSelect 전환 및 SEX 공통코드 바인딩**: `index.vue` 216라인 성별 검색을 `DictSelect` (dict-code="SEX", show-all)로 전환 완료. (완료)
25. **DictSelect 내 showAll 옵션 기능 추가**: `DictSelect.vue` 파일에 `showAll?: boolean` prop 추가 및 옵션 최상단에 `"전체"` 자동 주입 로직 반영 완료. (완료)
26. **호실 필터 params 다단계 조건 결합**: `index.vue` 204라인 호실 필터 `BizSelect` params에 `companyId`, `buildingId`, `floorId` 복합 바인딩 완료. (완료)
27. **고인 목록 다중 호실 배정 이력 검색 및 텍스트 합성 쿼리 고도화**: `DeceasedService.cs` 의 `GetDeceasedListAsync` 메서드 내 다중 배정 이력 `DeceasedRoom` 매칭 검색 및 `"건물 층 호실"` 목록 결합 합성 로직 구현 완료. (완료)
28. **고인 영정사진 편집(크롭) 모달 컴포넌트 신규 생성 및 그리드 연동**: `deceased-photo-crop-modal.vue` 생성 완료. (완료)
29. **TUI Color Picker CSS Import 에러 제거**: `deceased-photo-crop-modal.vue` 10행의 `tui-color-picker.css` 임포트를 지워 Vite 번들러 Import Analysis 오류 완벽 차단. (완료)
30. **영정사진 편집용 독립 단독 페이지(새 탭) 라우팅 등록 및 뷰 개발**: `core.ts` 단독 라우트 등록, `photo-editor.vue` 신규 구현, `index.vue` window.open 링크 교체 및 message 리스너를 통한 그리드 연쇄 갱신 구현 완료. (완료)
31. **에디터 세로 공간 극대화 및 패딩 정돈**: `photo-editor.vue` 캔버스 해상도 증량 및 calc(100vh - 100px) 스타일 오버라이드 완료. (완료)
32. **TUI Image Editor 내장 자르기(Crop) 서브메뉴 프리셋 버튼 바에 직접 동적 DOM 인젝션 주입**: `photo-editor.vue` 내 `injectCustomCropRatios` 함수 구현 및 에디터 인스턴스 초기화 연동 완료. (완료)
33. **컴파일 검증**: `dotnet build` 및 `pnpm run check:type` 명령어를 실행해 백엔드/프론트엔드 빌드 정상 여부를 체크. (완료)
