-- 요청 상세에서 「수정」을 걷어내고 「종료(최종확인)」을 붙인다 (2026-09-28)
--
-- 화면 쪽 변경은 `web/src/Apps/JSini.Web.HelpDesk/Components/Pages/RequestDetail.razor`
-- 이고, 여기 적는 것은 **코드로는 못 가는 일 둘**이다. DB 가 둘이라 한 번에
-- 돌지 않으므로 나누어 적는다.
--
-- 2026-09-28 운영 반영·확인 완료.


-- ① jsiniportal DB (`jsiniportal` / 스키마 `scom`) — 「요청 수정」 메뉴를 재운다
--
-- 상태를 고르고 저장하는 것 하나뿐이던 화면(`RequestEdit.razor`)을 지웠다.
-- 같은 일을 이제 상세가 「접수」·「완료」 단추로 하고, 남겨 두면 상태를
-- 바꾸는 길이 두 벌이 되어 그중 한 벌은 접수자를 안 정한 채 상태만 갈아
-- **접수자 없는 「진행」**을 만든다.
--
-- 지우지 않고 재운다 — 사이드바에 죽은 줄이 남는 것만 막으면 되고,
-- 메뉴 조회는 `status = 1` 만 본다(`AuthServer/Services/MenuService.cs`).
-- 되돌리려면 `status = 1, hide_in_menu = false, is_deleted = false` 로
-- 되돌리고 `role_menus` 도 함께 푼다.

UPDATE scom.system_menus
   SET status       = 0,
       hide_in_menu = true,
       is_deleted   = true,
       updated_at   = now(),
       updated_by   = 'ai/229-349'
 WHERE id = 'HD_REQ_EDIT';

UPDATE scom.role_menus
   SET is_deleted = true,
       updated_at = now(),
       updated_by = 'ai/229-349'
 WHERE menu_id = 'HD_REQ_EDIT';


-- ② helpdesk DB (`helpdesk` / 스키마 `helpdesk`) — 종료일 칸의 시간대
--
-- `usercompletededat` 만 **시간대 없는 칸**이었다. 이 표의 다른 날짜 칸
-- 여섯은 전부 `timestamp with time zone` 이고, EF 모델도 이 칸을 그렇게
-- 적고 있다(`AppDbContextModelSnapshot` — `timestamp with time zone`).
-- 운영 DB 에만 어긋나 있던 것이다.
--
-- 값을 쓸 때 서버는 `DateTime.UtcNow` 를 주는데, 칸에 시간대가 없으면
-- PostgreSQL 이 **접속 시간대(Asia/Seoul)의 벽시계**로 눌러 담는다. 그래서
-- 08:27Z 가 `17:27` 로 굳고, 돌려받은 글자에 `Z` 가 없어 화면이 그것을 다시
-- UTC 로 읽어 **아홉 시간 뒤(다음 날 02:27)** 로 적었다. 종료일을 화면에
-- 내놓기 시작하면서 드러났다.
--
-- 들어 있던 값은 서울 지역시로 굳어 있으므로 그렇게 읽어 옮긴다.

ALTER TABLE helpdesk.improvementrequest
  ALTER COLUMN usercompletededat TYPE timestamp with time zone
  USING usercompletededat AT TIME ZONE 'Asia/Seoul';


-- 반영 확인
--
--   jsiniportal: select id, path, status, hide_in_menu, is_deleted
--                  from scom.system_menus where pid = 'HD_REQ' order by order_no;
--                -- HD_REQ_EDIT 만 status 0
--
--   helpdesk:    select column_name, data_type
--                  from information_schema.columns
--                 where table_schema = 'helpdesk'
--                   and table_name = 'improvementrequest'
--                   and column_name like '%at';
--                -- 여섯 칸이 모두 timestamp with time zone
