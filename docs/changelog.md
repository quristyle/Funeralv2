# Changelog

All notable changes to this project will be documented in this file.

## v0.1.2 (2026-06-28)

### feat/fix
- **deceased-backend**: 고인 정보 신규 등록 시 빈 ID로 인한 라우팅 404 에러 및 경로 오류 처리 보강
  - 백엔드 `DeceasedEndpoints.cs`에 `/detail` PUT 엔드포인트를 추가로 정의하여 신규 등록(ID 없음) 시에 매칭되도록 보강
  - 프론트엔드 `fronts/apps/funeralv2/src/api/building/index.ts` 내 `saveDeceasedDetail` 호출 시 ID 파라미터 유무에 따라 `/funeral/building/deceased/detail` 또는 `/funeral/building/deceased/{id}/detail` 엔드포인트를 호출하도록 클라이언트 로직 개선
  - 프론트엔드 `deceased-form-modal.vue`에서 필수값인 "작고 일시"(`deathDate`)의 미입력에 대한 클라이언트 단 밸리데이션 체크를 추가하고, 날짜 데이터를 표준 ISO 8601(`YYYY-MM-DDTHH:mm:ss`) 포맷으로 변경 및 빈 값 시 `null`을 명시적으로 대입하여 C# `System.Text.Json` 역직렬화 시 `JsonException` 발생 문제를 완벽히 해결
  - 프론트엔드 저장 시 필수 항목("고인 성명" 또는 "작고 일시") 누락 시 알림 경고와 함께 사용자 편의성을 위해 해당 필드(인풋/데이트피커)에 자동으로 초점이 맞춰지도록 포커싱(`focus`) 로직 구현
  - 프론트엔드 작고/입관/발인일시 날짜 컨트롤을 바인딩 값이 없을 때 오늘 일시를 노출해주는 공통 컴포넌트 `AutoDatePicker.vue`로 교체 및 발인일시에 +3일 오프셋 적용
  - 백엔드 `DeceasedService.cs`에서 고인 정보 및 관계 테이블(시설 이용, 호실 지정) 정보 저장/수정 시점에 날짜 데이터(`DateTime`, `DateTime?`)의 `Kind` 속성이 `Unspecified` 인 경우 `DateTime.SpecifyKind(dt, DateTimeKind.Utc)` 변환 처리를 강제하여 PostgreSQL `timestamp with time zone` 저장 시 발생하는 `DbUpdateException` 오류 원천 차단
  - 프론트엔드 `deceased-photo-form.vue`에서 영정사진 파일 업로드 시 Ant Design Vue `<Upload>` 컴포넌트 호출에 `@vben/stores`의 `useAccessStore`에서 조회한 Bearer 인증 토큰 헤더(`Authorization`)를 강제 바인딩 처리하여 401 Unauthorized 오류 전면 해소
  - 프론트엔드 `deceased/index.vue` 목록 그리드의 수정, 삭제 버튼을 텍스트 링크에서 `Pencil`, `Trash2` 아이콘으로 전환하여 시각적 직관성 개선
  - 프론트엔드 고인 목록 그리드에 썸네일 형태의 영정사진 이미지를 렌더링하는 `고인사진` 컬럼을 맨 처음에 노출하도록 신규 추가
  - 백엔드 API에서 단일 데이터 조회 결과를 DTO 배열 구조(`result: [data]`)로 넘겨주는 패턴에 조응하여, `deceased-form-modal.vue`에서 상세 정보 로딩 시 `(detail as any)?.result?.[0]` 형태로 DTO 객체를 해체(언래핑)하여 바인딩하도록 수정함으로써 팝업 오픈 시 고인명 및 입력 폼 정보가 빈 값으로 채워지던 연동 버그 해결
  - `packages/@core/base/icons/src/lucide.ts` 내 재배포(re-export) 목록에 `Pencil` 및 `Image` 아이콘을 추가하여 프론트엔드 컴포넌트에서 수정 및 영정사진 편집 용도로 사용할 수 있게 코어 아이콘 모듈 보강
  - `deceased-photo-form.vue` 내의 파일 업로드 완료 핸들러(`handlePhotoUpload`)에서 백엔드 업로드 응답의 `result` 배열 래핑 패턴을 적용하여, 첫 번째 파일 객체(id, downloadUrl)가 올바르게 검출되도록 파싱 로직을 개선함으로써 이미지 다운로드 URL이 `/api/file/download/undefined`로 호출되던 문제 완전 해결
  - 프론트엔드 `deceased-photo-form.vue` 내의 파일 업로드 컴포넌트에 `:data="{ bizType: 'DECEASED' }"`를 바인딩하여 백엔드로 영정사진의 업무 영역 식별자를 동봉해 전송하게 개선
  - 백엔드 `FileService.cs` 의 `GetBizFolder` 메서드 내에 `bizType`이 `"DECEASED"`로 유입될 때 `"funeralv2/deceased"` 폴더명을 반환하게 하여 영정사진 데이터가 `basecom`이 아닌 `funeralv2/deceased` 디렉토리로 격리 저장되도록 경로 매핑 고도화
  - 프론트엔드 `deceased-photo-form.vue` 내에 `thumbnailUrl` computed 프로퍼티를 구현하고 영정사진 이미지 태그에 연결함으로써, 원본 이미지 대신 파일 서버의 경량화 썸네일 API(`/api/file/thumbnail/{id}`)를 통해 최적화된 사진 이미지를 노출하도록 개선
  - 백엔드 `FileService.cs` 의 `GetPresetImageAsync` 메서드 내에서 썸네일 물리 보관 경로를 산출할 때, 원본 파일의 다중 디렉토리 세그먼트(`funeralv2/deceased`)를 완전히 병합하여 `bizType`으로 인식하도록 경로 파싱 코드를 개선함으로써, 영정사진의 썸네일이 `c:\funeralv2_storage\funeralv2\deceased\Thumbnail` 하위 디렉토리에 정확히 분류되어 생성 및 보관되도록 개선
  - 프론트엔드 `deceased/index.vue` 고인 목록 그리드의 photo 슬롯에도 인라인 삼항연산자 바인딩 제어를 통해 파일 서버의 썸네일 생성 API(`/api/file/thumbnail/{id}`) 주소를 우선 적용함으로써 목록 전반의 이미지 로딩 트래픽 최적화 달성
  - 백엔드 `DeceasedService.cs` 내의 `GetDeceasedListAsync()` 메서드에서 `DeceasedDto` 객체를 데이터베이스 엔티티로부터 매핑해 투영할 때, 누락되어 있던 고인 영정사진 및 가족사진 식별자 필드(`MemorialPhotoUrl`, `MemorialPhotoFileId`, `FamilyPhotoGroupId`)를 명시적으로 매핑해주도록 추가 보강하여 목록 조회 시에도 사진 메타데이터가 정상 반환되도록 버그 해결
  - 프론트엔드 고인의 빈소 배정 이력 관리 화면인 `deceased-rooms-form.vue`를 개선하여 기존의 단일 Select 구성을 회사-건물-층-호실(빈소)의 4단계 Cascading `BizSelect` 체인 형태로 리팩토링함으로써 직관적 위계 선택 실현
  - 기존 저장 이력 로드 시 `roomId` 데이터만 존재하는 문제를 해결하고자, 마운트 시점에 건물 및 호실의 마스터 정보를 일괄 사전 로드하여 기존 `roomId`로부터 `floorId`, `buildingId`, `companyId` to roomId 역추적해 Select 상태값으로 기바인딩하는 역매핑 로직 반영
  - 빈소 배정의 시작/종료 일시 컨트롤에 대해 사용자가 작성했던 공통 `AutoDatePicker` 컴포넌트를 연동하여 일관성 있는 날짜 세팅 및 UX 통일성 강화
  - 공통 날짜 선택 컴포넌트인 `AutoDatePicker.vue` 에서 부모가 전달하는 `value` 값이 `Dayjs` 객체가 아닌 문자열(`string`)일 경우 런타임에 발생하던 `date.locale is not a function` 예외를 해결하기 위해, `value` 문자열 유입 시 `dayjs(value)`를 거쳐 `Dayjs` 인스턴스로 자동 파싱 후 하위 컴포넌트에 공급하도록 수정하고 `value-format` 속성이 주어지면 반대로 부모에 포맷된 문자열을 돌려주도록 양방향 입출력 구조 개선
  - 파일 서버 `FileService.cs` 의 `GetBizFolder` 메서드 내에서 경로 순회 위협(`..`)만 무효화하고 다중 계층 서브 폴더를 생성할 수 있게 하는 슬래시 문자의 무효화 처리를 필터에서 배제하고 양 끝 슬래시만 `Trim` 처리하도록 개선하여, `bizType`이 `"DECEASED"` 이거나 `"funeralv2/deceased"` 일 경우 `c:\funeralv2_storage\funeralv2\deceased\Thumbnail\` 하위에 썸네일 폴더 및 파일이 정확히 생성 및 저장되도록 정정
  - 고인 목록 화면(`deceased/index.vue`)에 회사, 건물, 층, 호실, 고인명, 성별, 나이 최소/최대 범위, 종교, 입관 기간 범위, 발인 기간 범위, 장례 상태 등을 포함한 다차원 고급 검색 폼(2행 6열 그리드)을 상단에 구성하고, 각 필터 변경 시 하위 목록이 반응형으로 초기화되는 Cascading logic 추가
  - 백엔드에 `DeceasedSearchDto` 매개변수를 신규 도입하고 `DeceasedEndpoints` 및 `DeceasedService.GetDeceasedListAsync` Linq 쿼리에 3단 테이블 조인과 다중 `Where` 동적 필터 쿼리 연동을 추가하여 고급 통합 검색 기능 완성
  - `deceased/index.vue` 249라인의 `"입실(작고) 기간"` 라벨을 `"입관 기간"`으로 변경하고 백엔드 날짜 비교 대상 컬럼을 실제 UI 바인딩 의미와 일치시키기 위해 `RoomEnterStartDate/EndDate` 조건에 대응하는 컬럼을 `Deceased.FuneralDate`(입관 일시)로, `FuneralStartDate/EndDate` 조건에 대응하는 컬럼을 `Deceased.BurialDate`(발인 일시)로 교정 완료
  - `deceased/index.vue` 내 날짜 기간 검색 시 브라우저 시분초(`toISOString()`)가 연동되는 현상을 배제하고, 시작일시는 `00:00:00`, 종료일시는 `23:59:59`로 시분초를 정밀 리포팅하도록 `format('YYYY-MM-DDT00:00:00')` 포맷을 적용하여 당일 경계 데이터의 검색 유실 원천 해소
  - `deceased/index.vue` 216라인의 성별 검색 필터를 하드코딩된 Select 구조에서 공통코드 `SEX`를 참조하는 공통 컴포넌트 `DictSelect` (dict-code="SEX", show-all)로 전환하여 공통코드 체계와 동적 매핑 완료
  - `DictSelect.vue` 공통 컴포넌트에 `showAll?: boolean` props 정의 및 `showAll` 활성화 시 옵션 리스트 최상단에 `{ label: '전체', value: '' }` 항목을 동적으로 결합해 반환하도록 구현하여 성별, 종교, 장례상태 등의 필터 콤보박스에 전체 검색 옵션 노출 활성화 완료
  - `deceased/index.vue` 204라인의 호실 `BizSelect` 컴포넌트의 `:params` 속성을 `{ companyId, buildingId, floorId }`의 다중 계층 조건으로 바인딩하여, 회사, 건물, 층 중에서 선택된 상위 값이 존재할 경우 해당 소속의 호실만 드롭다운에 유기적으로 조회되도록 개선
  - `DeceasedService.cs` 의 `GetDeceasedListAsync` 목록 쿼리를 개선하여 회사, 건물, 층, 호실 조건 적용 시 `DeceasedRoom` 이력 테이블과 조인 검색되도록 조치하고, 각 고인별 다중 배정 호실 데이터를 `"건물 층 호실"` 목록(콤마 결합)으로 변환 후 `RoomName` DTO 필드에 투영함으로써 목록 그리드에 배정 정보가 정확하고 상세하게 노출되도록 개선 완료
  - `deceased-photo-crop-modal.vue` 팝업 컴포넌트를 신규 개발하고, `index.vue` 그리드 작업 영역에 영정사진 편집용 `Image` 아이콘을 배치하여, 사용자가 크롭한 이미지를 파일 서버에 즉시 전송(업로드)한 후 고인 정보(`memorialPhotoFileId`, `memorialPhotoUrl`)와 유기적으로 동기화 저장되도록 개선 완료
  - `DeceasedPhotoCropModal` 컴포넌트 내부에서 `saveDeceasedDetail` 호출 인수에 `id`를 명시하고 `Button` 컴포넌트의 타입 정의를 안트디자인 규격(`dashed`)에 맞게 정정하여 컴파일 타입 오류 완벽 해소
  - `deceased-photo-crop-modal.vue` 에서 로드할 수 없었던 외부 CSS 파일 `tui-color-picker.css` 임포트 코드를 안전하게 제거함으로써 Vite 빌드 시 분석 실패 import-analysis 오류 해소 완료
  - 영정사진 편집 환경의 공간 협소로 인한 사용자 불편을 해소하기 위해, LNB/GNB 등의 레이아웃을 타지 않는 단독 Blank Page(`/building/deceased/photo-editor`)를 라우터(`core.ts`)에 신규 등록하고, 널찍한 뷰포트 크기 하에서 TUI Image Editor의 풍부한 크롭/회전/반전/필터 등의 조작 도구를 활용할 수 있는 `photo-editor.vue` 화면 개발 및 `window.open` 연동 완료
  - 편집기에서 영정사진 가공 및 저장 성공 시, `postMessage` 이벤트를 송출하여 부모 목록 창(`index.vue`)이 이벤트를 가로채 데이터를 자동 새로고침(`gridApi.query()`)하고 스스로 창을 닫도록 실시간 유기적 창 통신 파이프라인 구현 완료
  - 영정사진 편집 페이지(`photo-editor.vue`)의 세로 여백 낭비를 원천적으로 걷어내기 위해, CSS 오버라이드를 통해 TUI Image Editor의 메인/루트 컨테이너 높이를 100%로 강제 고정하고 뷰포트 반응형 칼큘레이션(`calc(100vh - 140px)`) 높이를 마운트 영역에 주입하여 브라우저 가용 세로 높이를 남김없이 채운 초광폭 편집 레이아웃 최적화 완료
  - 영정사진 편집기 상단 영역에 가로 비율 위주의 기본 옵션 외에 세로 자르기에 필수적인 3:4(영정 권장 비율), 2:3, 5:7, 9:16 크롭 Aspect Ratio 조절용 단축 버튼 바를 추가 배치하고 클릭 시 드로잉 크로퍼 모드로 즉시 진입하는 `changeCropRatio` 액션 API 결합 구현 완료

## v0.1.1 (2026-06-28)

### feat/fix
- **deceased**: 고인 종합 정보 폼 모달에서 우측 내용 영역 스크롤 시 좌측 앵커 바로가기 버튼의 활성화 상태(`activeSection`)가 자동으로 동기화되도록 개선
  - 모달의 라이프사이클에 맞추어 `deceasedModalApi.open()` 호출 직후 `nextTick` 내부에서 `IntersectionObserver` 관찰자를 바인딩
  - 좌측 바로가기 클릭 이동 시 활성화 상태가 다른 영역을 지나가며 일시적으로 오작동하는 것을 방지하기 위해 `isScrollingByClick` 플래그 추가
  - 모달을 다시 열 때 혹은 이전 observer가 존재할 때 `observer.disconnect()`를 호출하여 중복 관찰로 인한 누수 방지
