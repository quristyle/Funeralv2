# 헬프데스크 첨부파일을 FileServer 로 (결정 D5-B)

FileServer 라는 전용 서비스가 있는데도 헬프데스크가 첨부파일을 **따로** 관리하고 있었다.
같은 일을 두 곳에서 다르게 하니 백업 대상도 둘, 용량 관리도 둘이었다.

| | FileServer | HelpDeskServer (예전) |
|---|---|---|
| 표 | `scom.filemetadatas` | `jsini.attachment` |
| 저장 위치 | `/home/quri/goldb_storage` | `/home/lee/jinAttachment` · `/home/quri/jinAttachment` |
| API | `/api/file/*` | `/api/attachments`, `/api/files` |

## 실제 데이터 (2026-08-26 확인)

**37건 · 합계 21.6MB · 최대 6.6MB.** `entitytype` 은 전부 `ImprovementRequest` 하나다.

```
xlsx 15 · pdf 6 · xls 5 · jpeg 5 · png 2 · ico 1 · pptx 1 · mp4 1
```

**저장 경로가 하나가 아니다.** 문서에는 `/home/quri/jinAttachment` 로 적혀 있었지만
실제로는 둘로 갈려 있다.

```
/home/lee/jinAttachment    35건
/home/quri/jinAttachment    2건
```

`FileUploadEndpoints` 가 환경변수 `FileStorage_BasePath` 를 쓰고 기본값이
`/home/lee/jinAttachment` 였다. 장비마다 값이 달랐던 흔적이다.
**그래서 이 도구는 디렉터리를 가정하지 않고 행마다 `filepath` 를 읽는다** —
가정하면 2건을 놓친다.

## 이미 끝난 것 (코드·스키마)

| | 내용 |
|---|---|
| 스키마 | `docs/sql/attachment_to_fileserver.sql` — `fileid` · `migratedat` 컬럼 추가. **실행 완료** |
| 새 업로드 | `/api/files/upload` 가 이제 **로컬 디스크에 쓰지 않는다.** FileServer 로 보내고 파일 아이디만 받아 적는다 |
| 내려받기 | `fileid` 가 있으면 FileServer 로 302. 없으면 예전 로컬 경로 (옮기기 전까지만) |

즉 **지금부터 새로 올라오는 파일은 FileServer 로 간다.** 로컬에 더 쌓이지 않는다.

## 남은 것 — 이 도구를 배포 장비에서 돌린다

옮길 파일의 **바이트가 배포 장비 디스크에만** 있다. 개발 PC 에서는 DB 는 보이지만
파일은 보이지 않으므로, 바이트를 옮기는 일만 여기로 분리했다.

### 1. 먼저 확인만 (아무것도 바꾸지 않는다)

```bash
pip3 install psycopg2-binary

export HD_DB="host=localhost port=5432 dbname=jinrecept user=jsini password=..."
export FILE_UPLOAD_URL="http://localhost:5265/api/file/upload"
python3 migrate.py --dry-run
```

37건이 모두 `[옮길것]` 으로 나와야 한다. `[없음]` 이 있으면 DB 행만 남고 파일이
사라진 것이다 — 그 행은 옮겨지지 않고 마지막에 목록으로 보고된다.

### 2. 한 건만 시험

```bash
export JSINI_TOKEN="eyJ..."      # 포털에 관리자로 로그인해 받은 JWT
python3 migrate.py --limit 1
```

포털의 개선요청 화면에서 그 첨부를 실제로 내려받아 열어 본다.

### 3. 전부 옮긴다

```bash
python3 migrate.py
```

반복 실행해도 안전하다. 이미 `fileid` 가 있는 행은 건너뛰고, 한 건씩 커밋하므로
중간에 끊겨도 다시 돌리면 남은 것만 이어서 옮긴다.

### 4. 확인

```sql
SELECT count(*) AS 전체, count(fileid) AS 옮김, count(*) - count(fileid) AS 남음
FROM jsini.attachment;
```

## 그 다음 (사람이 확인한 뒤)

옮기기가 끝나야 할 수 있는 일들이다. **옮기기 전에 하면 기존 첨부를 못 받는다.**

1. `AttachmentEndpoints.cs` 의 내려받기에서 **로컬 경로 분기를 지운다**
   (주석에 `여기부터는 옮기기 전의 경로다 (없어질 코드)` 로 표시해 두었다).
2. 원본 파일(`/home/lee/jinAttachment` · `/home/quri/jinAttachment`)을 지운다.
   **`filepath`·`storedfilename` 컬럼은 되돌릴 근거로 남겨 둔다.**
3. `FileStorage_BasePath` 환경변수를 걷어낸다.

> 왜 원본 삭제를 도구가 하지 않나 — 되돌릴 수 없는 동작이고, FileServer 쪽에 정말
> 다 올라갔는지는 사람이 한 번 보는 편이 낫다. 21.6MB 라 급하게 지울 이유도 없다.

## JinReception 은 어떻게 되나

**이미 동작하지 않는다.** D10 으로 게이트웨이가 `/api/helpdesk/**` 에 인증을 요구하게
되면서 *자체 토큰을 서버가 믿어 주던* 전제가 사라졌고, D11 로 헬프데스크 자체 로그인이
닫혀(`LocalLogin:Enabled` 기본 false) 로그인 자체가 되지 않는다.
그래서 이 정리로 새로 깨지는 것은 없다.
