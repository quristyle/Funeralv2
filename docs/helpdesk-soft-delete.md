# 헬프데스크 요청글 삭제 — 지움 표시와 완전삭제

요청글을 지우면 **줄을 지우지 않고 지움 표시만 세운다**(`helpdesk.improvementrequest.isdel` ·
`deletedat` · `deletedby`). 댓글은 전부터 그랬다(`improvementcomment.isdel`).
실제로 지우는 것은 **시스템관리자의 완전삭제** 하나뿐이다.

| | 누가 | 길 |
|---|---|---|
| 삭제(지움 표시) | 글 주인 · 관리자 — 전과 같다 | `DELETE /api/requests/{id}` · `DELETE /api/comments/{id}` |
| 지운 글 보기 | **시스템관리자** | 요청 처리의 「삭제된 글」(`srch` 에 `trash=true`) · 상세 |
| 복원 | **시스템관리자** | `PUT /api/requests/{id}/restore` |
| 완전삭제 | **시스템관리자**, 지움 표시가 선 것만 | `DELETE /api/requests/{id}/purge` · `DELETE /api/comments/{id}/purge` |

## 가리는 자리는 하나다 — 전역 걸름

`AppDbContext` 가 `ImprovementRequest` 에 `!IsDel`, `ImprovementComment` 에
`!Request.IsDel` 걸름을 건다. 목록 · 현황판 · 보고서 · 알림 · 업무 비서가 **따로
거르지 않아도** 지운 글이 안 보인다. 시스템관리자의 길만 `IgnoreQueryFilters()` 로 연다.
새 조회를 짤 때 거를 것이 없다 — 거꾸로, 지운 글이 필요한 자리에서만 걸름을 푼다.

지운 글에 댓글을 달려 하면 「요청글을 찾을 수 없습니다」다(걸름 때문에 `FindAsync` 가 비운다).

댓글을 지워도 이제 그림 폴더(`cmt_{id}`)를 지우지 않는다 — 지움 표시만 세운다.

## 완전삭제는 파일부터 지운다 (`RequestPurger`)

지우는 것: 요청 줄 · 댓글 전부 · 첨부 줄(요청 · 댓글) · **FileServer 의 파일**(첨부의
`fileid` + 본문·댓글 HTML 에 박힌 `/api/file/download/id/{guid}`) · 옛 로컬 첨부와
그림 폴더(`reqs/{id}` · `reqs/cmt_{id}`).

- **파일을 먼저, 줄을 나중에.** 파일 하나라도 못 지우면 줄을 남긴 채 멈춘다 — 줄이 있어야
  다시 눌러 마저 지울 수 있다. FileServer 의 404 는 「이미 없음」으로 보고 넘어간다.
- FileServer 는 **게이트웨이로** 부른다(`FileServer:UploadUrl` 에서 `/upload` 를 뗀 주소).
  부른 사람의 토큰을 그대로 실으므로 FileServer 가 시스템관리자를 관리자로 보고 지운다.
- 댓글끼리의 부모 외래키가 `RESTRICT` 라, 요청을 지울 때는 부모를 먼저 끊고 지운다.
  댓글 하나만 지울 때는 **답글을 그 댓글의 부모로 옮겨 단다**(끊으면 뿌리로 튀어 오른다).

2026-11-25 시험 인스턴스(:55400) + FileServer 대역(:55999)으로 몰아 확인했다 — 지움 → 남에게
404 · 휴지통에만 보임 · 복원 · 댓글 완전삭제(답글 옮겨 달림) · 요청 완전삭제(파일 셋 DELETE,
줄 0) · FileServer 가 죽어 있으면 멈추고 줄이 남음 → 살아나면 다시 눌러 끝남.

DB: 마이그레이션 `20261125112211_AddRequestSoftDelete` (운영 반영 완료 — 이력 줄까지 넣었다).
