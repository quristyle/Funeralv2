- 모든 설명과 결과는 한글을 사용하라.
- authServer 에 사용하는 dbconnectionstring은 환경변수 jsinicore 에 정의된 값을 사용하라.
- funeralv2Api 에 사용하는 dbconnectionstring은 환경변수 funeralv2 에 정의된 값을 사용하라.
- 코드에는 상세하고 친절한 설명을 주석으로 작성하라.
- 다국어를 지원하는 시스템이다. 영어와 한국어를 지원한다.
- 프론트엔드 에서 api 호출시에 authServer 로 호출은 /auth/ 로 시작하여 호출 하도록 작성한다..
- 프론트엔드 에서 api 호출시에 funeralv2Api 로 호출은 /funeralv2/ 로 시작하여 호출 하도록 작성한다..

- 시스템의 구성은 frontend, apiGateway, authServer, microService 의 구성을 가지고 있다.



💡 Vben Admin의 일반적인 Import 정렬 순서
type 임포트 그룹: import type { ... }
프레임워크 라이브러리: vue, vue-router 등
내부/공통 패키지: @vben/icons, @vben/locales 등 (@vben/* 접두사)
외부 서드파티 라이브러리: ant-design-vue, dayjs 등
프로젝트 내부 모듈: #/adapter/..., #/api/... 등 (#/* 접두사)
상대 경로 모듈: ./data, ../components/... 등
이 순서대로 배치하고 각 그룹 사이에 빈 줄을 넣어주시면 Lint 에러가 깔끔하게 해결


vben-admin 의 crud 화면의 구성.

views/system/company/
├── list.vue           # 1. 뼈대: 메인 테이블 컴포넌트 및 이벤트 핸들링
├── data.ts            # 2. 살: 테이블 컬럼 및 폼 스키마 (정적/동적 설정)
└── modules/
    └── form.vue       # 3. 추가 동작: 데이터 등록/수정을 처리하는 팝업 레이어
동작 흐름 (Data Flow):

사용자가 list.vue 페이지에 진입합니다.
list.vue는 data.ts에서 컬럼 정보를 읽어와 테이블을 그리고 API를 호출해 데이터를 채웁니다.
사용자가 "추가" 버튼 또는 "수정" 버튼을 클릭합니다.
list.vue는 modules/form.vue 컴포넌트(Drawer)를 엽니다. 수정의 경우 해당 행의 데이터를 함께 넘겨줍니다.
form.vue는 data.ts에서 폼 스키마를 읽어 입력칸을 구성하고, 전달받은 데이터가 있다면 값을 채워 넣습니다.
폼 작성 후 완료(Submit) 시, form.vue에서 서버로 저장 API를 날리고 서랍을 닫으며 success 이벤트를 발생시킵니다.
이벤트를 감지한 list.vue는 테이블을 새로고침하여 최신 데이터를 보여줍니다.
이 구조를 잘 따르면, 나중에 컬럼을 추가하거나 입력 항목을 변경해야 할 때 .vue 파일의 로직을 건드릴 필요 없이 data.ts 파일만 수정하면 되기 때문에 유지보수성이 극대화됩니다.

