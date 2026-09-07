-- 메뉴에 route_key 를 채운다 (백필)
--
-- 생성 근거는 코드다 — 화면의 [RouteKey] 155건과 RouteAliases 의 옛 경로 69건을
-- 맞춘 결과다. 손으로 적은 표가 아니므로 코드가 바뀌면 다시 만든다.
--
-- [이 파일이 하는 일]
--
-- 지금 DB 의 path 는 두 가지가 섞여 있다 — Vue 시절 옛 경로(/room_status)와
-- 이미 새 경로인 것(/helpdesk/...). 어느 쪽이든 같은 화면을 가리키므로 둘 다
-- 같은 열쇠로 채운다. 매개변수 화면 두 건은 DB 가 vben 표기(:id)를 들고 있어
-- 그 형태도 함께 받는다.
--
-- **path 는 건드리지 않는다.** 권한표와 즐겨찾기의 열쇠라 바꾸면 권한이 조용히
-- 끊긴다 — 그것이 애초에 열쇠를 따로 둔 이유다.
--
-- [운영 DB 실측 (2026-09-07, jsiniportal.scom.system_menus 179건)]
--
--   · 열쇠가 붙는 메뉴          153건
--   · 안 붙는 메뉴               26건 = 묶음(CATALOG) 24 + vben 대시보드 잔재 2
--                                      (/analytics · /workspace — 갈 화면이 없다)
--   · DB 메뉴가 없는 화면         2건 (admin.system.signup · projmng._smoke —
--                                      메뉴에 안 걸린 화면이라 정상이다)
--
-- [안전한 성질]
--
--   · 멱등하다. 여러 번 돌려도 결과가 같다.
--   · path 를 바꾸지 않으므로 권한·즐겨찾기가 그대로다.
--   · 되돌리려면 route_key 를 NULL 로 지우면 된다. 프론트가 옛 방식
--     (RouteAliases + path)으로 그대로 떨어지므로 화면이 안 깨진다.
--   · 그래서 배포 순서를 안 탄다 — 먼저 돌려도, 나중에 돌려도 된다.
--     menu-path-cutover.sql 이 순서를 타던 것과 다른 점이 이것이다.

BEGIN;

UPDATE scom.system_menus SET route_key = 'admin'
 WHERE path IN ('/admin', '/system') AND route_key IS DISTINCT FROM 'admin';
UPDATE scom.system_menus SET route_key = 'admin.auth'
 WHERE path IN ('/admin/auth', '/auth') AND route_key IS DISTINCT FROM 'admin.auth';
UPDATE scom.system_menus SET route_key = 'admin.auth.menu-role'
 WHERE path IN ('/admin/auth/menu-role', '/auth/menu-role') AND route_key IS DISTINCT FROM 'admin.auth.menu-role';
UPDATE scom.system_menus SET route_key = 'admin.auth.role'
 WHERE path IN ('/admin/auth/role', '/system/role-map') AND route_key IS DISTINCT FROM 'admin.auth.role';
UPDATE scom.system_menus SET route_key = 'admin.auth.user-role'
 WHERE path IN ('/admin/auth/user-role', '/auth/user-role') AND route_key IS DISTINCT FROM 'admin.auth.user-role';
UPDATE scom.system_menus SET route_key = 'admin.common'
 WHERE path IN ('/admin/common', '/common') AND route_key IS DISTINCT FROM 'admin.common';
UPDATE scom.system_menus SET route_key = 'admin.company'
 WHERE path IN ('/admin/company', '/company') AND route_key IS DISTINCT FROM 'admin.company';
UPDATE scom.system_menus SET route_key = 'admin.company.dept'
 WHERE path IN ('/admin/company/dept', '/system/dept') AND route_key IS DISTINCT FROM 'admin.company.dept';
UPDATE scom.system_menus SET route_key = 'admin.company.list'
 WHERE path IN ('/admin/company/list', '/system/company') AND route_key IS DISTINCT FROM 'admin.company.list';
UPDATE scom.system_menus SET route_key = 'admin.company.org-chart'
 WHERE path IN ('/admin/company/org-chart', '/company/org-chart') AND route_key IS DISTINCT FROM 'admin.company.org-chart';
UPDATE scom.system_menus SET route_key = 'admin.company.user'
 WHERE path IN ('/admin/company/user', '/company/user') AND route_key IS DISTINCT FROM 'admin.company.user';
UPDATE scom.system_menus SET route_key = 'admin.notice'
 WHERE path IN ('/admin/notice', '/portal/notice') AND route_key IS DISTINCT FROM 'admin.notice';
UPDATE scom.system_menus SET route_key = 'admin.profile'
 WHERE path IN ('/admin/profile', '/profile') AND route_key IS DISTINCT FROM 'admin.profile';
UPDATE scom.system_menus SET route_key = 'admin.push'
 WHERE path IN ('/admin/push', '/system/push') AND route_key IS DISTINCT FROM 'admin.push';
UPDATE scom.system_menus SET route_key = 'admin.push.dashboard'
 WHERE path IN ('/admin/push/dashboard', '/system/push/dashboard') AND route_key IS DISTINCT FROM 'admin.push.dashboard';
UPDATE scom.system_menus SET route_key = 'admin.push.history'
 WHERE path IN ('/admin/push/history', '/system/push/history') AND route_key IS DISTINCT FROM 'admin.push.history';
UPDATE scom.system_menus SET route_key = 'admin.push.logs'
 WHERE path IN ('/admin/push/logs', '/system/push/logs') AND route_key IS DISTINCT FROM 'admin.push.logs';
UPDATE scom.system_menus SET route_key = 'admin.push.setting'
 WHERE path IN ('/admin/push/setting', '/system/push/setting') AND route_key IS DISTINCT FROM 'admin.push.setting';
UPDATE scom.system_menus SET route_key = 'admin.release'
 WHERE path IN ('/admin/release', '/portal/release') AND route_key IS DISTINCT FROM 'admin.release';
UPDATE scom.system_menus SET route_key = 'admin.status'
 WHERE path IN ('/admin/status', '/system/status') AND route_key IS DISTINCT FROM 'admin.status';
UPDATE scom.system_menus SET route_key = 'admin.status.deploy'
 WHERE path IN ('/admin/status/deploy', '/system/deploy-status') AND route_key IS DISTINCT FROM 'admin.status.deploy';
UPDATE scom.system_menus SET route_key = 'admin.status.jin114'
 WHERE path IN ('/admin/status/jin114', '/system/server-status/jin114') AND route_key IS DISTINCT FROM 'admin.status.jin114';
UPDATE scom.system_menus SET route_key = 'admin.status.player-release'
 WHERE path IN ('/admin/status/player-release', '/system/player-release') AND route_key IS DISTINCT FROM 'admin.status.player-release';
UPDATE scom.system_menus SET route_key = 'admin.status.server'
 WHERE path IN ('/admin/status/server', '/system/server-status') AND route_key IS DISTINCT FROM 'admin.status.server';
UPDATE scom.system_menus SET route_key = 'admin.system.account'
 WHERE path IN ('/admin/system/account', '/system/account') AND route_key IS DISTINCT FROM 'admin.system.account';
UPDATE scom.system_menus SET route_key = 'admin.system.common-code'
 WHERE path IN ('/admin/system/common-code', '/system/common-code') AND route_key IS DISTINCT FROM 'admin.system.common-code';
UPDATE scom.system_menus SET route_key = 'admin.system.i18n'
 WHERE path IN ('/admin/system/i18n', '/system/i18n') AND route_key IS DISTINCT FROM 'admin.system.i18n';
UPDATE scom.system_menus SET route_key = 'admin.system.menu'
 WHERE path IN ('/admin/system/menu', '/system/menu') AND route_key IS DISTINCT FROM 'admin.system.menu';
UPDATE scom.system_menus SET route_key = 'admin.system.metadata'
 WHERE path IN ('/admin/system/metadata', '/system/metadata_manager') AND route_key IS DISTINCT FROM 'admin.system.metadata';
UPDATE scom.system_menus SET route_key = 'admin.system.signup'
 WHERE path IN ('/admin/system/signup') AND route_key IS DISTINCT FROM 'admin.system.signup';
UPDATE scom.system_menus SET route_key = 'funeral'
 WHERE path IN ('/funeral', '/funerals') AND route_key IS DISTINCT FROM 'funeral';
UPDATE scom.system_menus SET route_key = 'funeral.building.audio'
 WHERE path IN ('/building/audio', '/funeral/building/audio') AND route_key IS DISTINCT FROM 'funeral.building.audio';
UPDATE scom.system_menus SET route_key = 'funeral.building.background'
 WHERE path IN ('/device/background', '/funeral/building/background') AND route_key IS DISTINCT FROM 'funeral.building.background';
UPDATE scom.system_menus SET route_key = 'funeral.building.decoration'
 WHERE path IN ('/decoration', '/funeral/building/decoration') AND route_key IS DISTINCT FROM 'funeral.building.decoration';
UPDATE scom.system_menus SET route_key = 'funeral.building.device'
 WHERE path IN ('/building/device', '/funeral/building/device') AND route_key IS DISTINCT FROM 'funeral.building.device';
UPDATE scom.system_menus SET route_key = 'funeral.building.floor'
 WHERE path IN ('/building/floor', '/funeral/building/floor') AND route_key IS DISTINCT FROM 'funeral.building.floor';
UPDATE scom.system_menus SET route_key = 'funeral.building.info'
 WHERE path IN ('/building/info', '/funeral/building/info') AND route_key IS DISTINCT FROM 'funeral.building.info';
UPDATE scom.system_menus SET route_key = 'funeral.building.music-build'
 WHERE path IN ('/building/music-build', '/funeral/building/music-build') AND route_key IS DISTINCT FROM 'funeral.building.music-build';
UPDATE scom.system_menus SET route_key = 'funeral.building.room'
 WHERE path IN ('/building/room', '/funeral/building/room') AND route_key IS DISTINCT FROM 'funeral.building.room';
UPDATE scom.system_menus SET route_key = 'funeral.building.video'
 WHERE path IN ('/building/video', '/funeral/building/video') AND route_key IS DISTINCT FROM 'funeral.building.video';
UPDATE scom.system_menus SET route_key = 'funeral.deceased'
 WHERE path IN ('/building/deceased', '/funeral/deceased') AND route_key IS DISTINCT FROM 'funeral.deceased';
UPDATE scom.system_menus SET route_key = 'funeral.help'
 WHERE path IN ('/funeral/help', '/help') AND route_key IS DISTINCT FROM 'funeral.help';
UPDATE scom.system_menus SET route_key = 'funeral.help.archive'
 WHERE path IN ('/funeral/help/archive', '/help/archive') AND route_key IS DISTINCT FROM 'funeral.help.archive';
UPDATE scom.system_menus SET route_key = 'funeral.help.faq'
 WHERE path IN ('/funeral/help/faq', '/help/faq') AND route_key IS DISTINCT FROM 'funeral.help.faq';
UPDATE scom.system_menus SET route_key = 'funeral.help.qna'
 WHERE path IN ('/funeral/help/qna', '/help/qna') AND route_key IS DISTINCT FROM 'funeral.help.qna';
UPDATE scom.system_menus SET route_key = 'funeral.info'
 WHERE path IN ('/funeral/info', '/info') AND route_key IS DISTINCT FROM 'funeral.info';
UPDATE scom.system_menus SET route_key = 'funeral.info.deceased-search'
 WHERE path IN ('/funeral/info/deceased-search', '/info/deceased-search') AND route_key IS DISTINCT FROM 'funeral.info.deceased-search';
UPDATE scom.system_menus SET route_key = 'funeral.info.my-info'
 WHERE path IN ('/funeral/info/my-info', '/info/my-info') AND route_key IS DISTINCT FROM 'funeral.info.my-info';
UPDATE scom.system_menus SET route_key = 'funeral.info.preview'
 WHERE path IN ('/funeral/info/preview', '/info/preview') AND route_key IS DISTINCT FROM 'funeral.info.preview';
UPDATE scom.system_menus SET route_key = 'funeral.info.room-history'
 WHERE path IN ('/funeral/info/room-history', '/info/room-history') AND route_key IS DISTINCT FROM 'funeral.info.room-history';
UPDATE scom.system_menus SET route_key = 'funeral.player-download'
 WHERE path IN ('/funeral/player-download', '/system/player-download') AND route_key IS DISTINCT FROM 'funeral.player-download';
UPDATE scom.system_menus SET route_key = 'funeral.room-status'
 WHERE path IN ('/funeral/room-status', '/room_status') AND route_key IS DISTINCT FROM 'funeral.room-status';
UPDATE scom.system_menus SET route_key = 'funeral.setting'
 WHERE path IN ('/funeral/setting', '/setting') AND route_key IS DISTINCT FROM 'funeral.setting';
UPDATE scom.system_menus SET route_key = 'funeral.setting.environment'
 WHERE path IN ('/funeral/setting/environment', '/setting/environment') AND route_key IS DISTINCT FROM 'funeral.setting.environment';
UPDATE scom.system_menus SET route_key = 'funeral.setting.work-options'
 WHERE path IN ('/funeral/setting/work-options', '/setting/work-options') AND route_key IS DISTINCT FROM 'funeral.setting.work-options';
UPDATE scom.system_menus SET route_key = 'funeral.stat'
 WHERE path IN ('/funeral/stat', '/stat') AND route_key IS DISTINCT FROM 'funeral.stat';
UPDATE scom.system_menus SET route_key = 'funeral.stat.billing'
 WHERE path IN ('/funeral/stat/billing', '/stat/billing') AND route_key IS DISTINCT FROM 'funeral.stat.billing';
UPDATE scom.system_menus SET route_key = 'funeral.stat.room-usage'
 WHERE path IN ('/funeral/stat/room-usage', '/stat/room-usage') AND route_key IS DISTINCT FROM 'funeral.stat.room-usage';
UPDATE scom.system_menus SET route_key = 'funeral.status'
 WHERE path IN ('/funeral/status', '/status') AND route_key IS DISTINCT FROM 'funeral.status';
UPDATE scom.system_menus SET route_key = 'funeral.status.deceased-status'
 WHERE path IN ('/funeral/status/deceased-status', '/status/deceased-status') AND route_key IS DISTINCT FROM 'funeral.status.deceased-status';
UPDATE scom.system_menus SET route_key = 'funeral.status.funeral-info'
 WHERE path IN ('/funeral/status/funeral-info', '/status/funeral-info') AND route_key IS DISTINCT FROM 'funeral.status.funeral-info';
UPDATE scom.system_menus SET route_key = 'funeral.status.funeral-status'
 WHERE path IN ('/funeral/status/funeral-status', '/status/funeral-status') AND route_key IS DISTINCT FROM 'funeral.status.funeral-status';
UPDATE scom.system_menus SET route_key = 'funeral.status.mobile'
 WHERE path IN ('/funeral/status/mobile', '/status/mobile') AND route_key IS DISTINCT FROM 'funeral.status.mobile';
UPDATE scom.system_menus SET route_key = 'funeral.status.simple'
 WHERE path IN ('/funeral/status/simple', '/status/simple') AND route_key IS DISTINCT FROM 'funeral.status.simple';
UPDATE scom.system_menus SET route_key = 'helpdesk'
 WHERE path IN ('/helpdesk') AND route_key IS DISTINCT FROM 'helpdesk';
UPDATE scom.system_menus SET route_key = 'helpdesk.contact-us'
 WHERE path IN ('/helpdesk/contact-us') AND route_key IS DISTINCT FROM 'helpdesk.contact-us';
UPDATE scom.system_menus SET route_key = 'helpdesk.dashboard'
 WHERE path IN ('/helpdesk/dashboard') AND route_key IS DISTINCT FROM 'helpdesk.dashboard';
UPDATE scom.system_menus SET route_key = 'helpdesk.dashboard.customer'
 WHERE path IN ('/helpdesk/dashboard/customer') AND route_key IS DISTINCT FROM 'helpdesk.dashboard.customer';
UPDATE scom.system_menus SET route_key = 'helpdesk.hanju.collection-status'
 WHERE path IN ('/helpdesk/hanju/collection-status') AND route_key IS DISTINCT FROM 'helpdesk.hanju.collection-status';
UPDATE scom.system_menus SET route_key = 'helpdesk.hanju.equipment-log'
 WHERE path IN ('/helpdesk/hanju/equipment-log') AND route_key IS DISTINCT FROM 'helpdesk.hanju.equipment-log';
UPDATE scom.system_menus SET route_key = 'helpdesk.hanju.fms-log'
 WHERE path IN ('/helpdesk/hanju/fms-log') AND route_key IS DISTINCT FROM 'helpdesk.hanju.fms-log';
UPDATE scom.system_menus SET route_key = 'helpdesk.hanju.health-check'
 WHERE path IN ('/helpdesk/hanju/health-check') AND route_key IS DISTINCT FROM 'helpdesk.hanju.health-check';
UPDATE scom.system_menus SET route_key = 'helpdesk.hanju.procedure-result'
 WHERE path IN ('/helpdesk/hanju/procedure-result') AND route_key IS DISTINCT FROM 'helpdesk.hanju.procedure-result';
UPDATE scom.system_menus SET route_key = 'helpdesk.monitor.maintenance'
 WHERE path IN ('/helpdesk/monitor/maintenance') AND route_key IS DISTINCT FROM 'helpdesk.monitor.maintenance';
UPDATE scom.system_menus SET route_key = 'helpdesk.monitor.sm'
 WHERE path IN ('/helpdesk/monitor/sm') AND route_key IS DISTINCT FROM 'helpdesk.monitor.sm';
UPDATE scom.system_menus SET route_key = 'helpdesk.org.admin'
 WHERE path IN ('/helpdesk/org/admin') AND route_key IS DISTINCT FROM 'helpdesk.org.admin';
UPDATE scom.system_menus SET route_key = 'helpdesk.org.profile'
 WHERE path IN ('/helpdesk/org/profile') AND route_key IS DISTINCT FROM 'helpdesk.org.profile';
UPDATE scom.system_menus SET route_key = 'helpdesk.org.team'
 WHERE path IN ('/helpdesk/org/team') AND route_key IS DISTINCT FROM 'helpdesk.org.team';
UPDATE scom.system_menus SET route_key = 'helpdesk.org.team-company'
 WHERE path IN ('/helpdesk/org/team-company') AND route_key IS DISTINCT FROM 'helpdesk.org.team-company';
UPDATE scom.system_menus SET route_key = 'helpdesk.project.gantt'
 WHERE path IN ('/helpdesk/project/gantt') AND route_key IS DISTINCT FROM 'helpdesk.project.gantt';
UPDATE scom.system_menus SET route_key = 'helpdesk.project.info'
 WHERE path IN ('/helpdesk/project/info') AND route_key IS DISTINCT FROM 'helpdesk.project.info';
UPDATE scom.system_menus SET route_key = 'helpdesk.project.manage'
 WHERE path IN ('/helpdesk/project/manage') AND route_key IS DISTINCT FROM 'helpdesk.project.manage';
UPDATE scom.system_menus SET route_key = 'helpdesk.project.wbs'
 WHERE path IN ('/helpdesk/project/wbs') AND route_key IS DISTINCT FROM 'helpdesk.project.wbs';
UPDATE scom.system_menus SET route_key = 'helpdesk.project.wbs-gantt'
 WHERE path IN ('/helpdesk/project/wbs-gantt') AND route_key IS DISTINCT FROM 'helpdesk.project.wbs-gantt';
UPDATE scom.system_menus SET route_key = 'helpdesk.project.wbs-readonly'
 WHERE path IN ('/helpdesk/project/wbs-readonly') AND route_key IS DISTINCT FROM 'helpdesk.project.wbs-readonly';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.availability'
 WHERE path IN ('/helpdesk/report/availability') AND route_key IS DISTINCT FROM 'helpdesk.report.availability';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.capacity-planning'
 WHERE path IN ('/helpdesk/report/capacity-planning') AND route_key IS DISTINCT FROM 'helpdesk.report.capacity-planning';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.io-deep-dive'
 WHERE path IN ('/helpdesk/report/io-deep-dive') AND route_key IS DISTINCT FROM 'helpdesk.report.io-deep-dive';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.monitoring'
 WHERE path IN ('/helpdesk/report/monitoring') AND route_key IS DISTINCT FROM 'helpdesk.report.monitoring';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.monthly'
 WHERE path IN ('/helpdesk/report/monthly') AND route_key IS DISTINCT FROM 'helpdesk.report.monthly';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.prediction'
 WHERE path IN ('/helpdesk/report/prediction') AND route_key IS DISTINCT FROM 'helpdesk.report.prediction';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.root-cause'
 WHERE path IN ('/helpdesk/report/root-cause') AND route_key IS DISTINCT FROM 'helpdesk.report.root-cause';
UPDATE scom.system_menus SET route_key = 'helpdesk.report.weekly'
 WHERE path IN ('/helpdesk/report/weekly') AND route_key IS DISTINCT FROM 'helpdesk.report.weekly';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.detail'
 WHERE path IN ('/helpdesk/request/detail/:id', '/helpdesk/request/detail/{Id}') AND route_key IS DISTINCT FROM 'helpdesk.request.detail';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.edit'
 WHERE path IN ('/helpdesk/request/edit/:id', '/helpdesk/request/edit/{Id}') AND route_key IS DISTINCT FROM 'helpdesk.request.edit';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.list'
 WHERE path IN ('/helpdesk/request/list') AND route_key IS DISTINCT FROM 'helpdesk.request.list';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.manage'
 WHERE path IN ('/helpdesk/request/manage') AND route_key IS DISTINCT FROM 'helpdesk.request.manage';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.monitor'
 WHERE path IN ('/helpdesk/request/monitor') AND route_key IS DISTINCT FROM 'helpdesk.request.monitor';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.my-comments'
 WHERE path IN ('/helpdesk/request/my-comments') AND route_key IS DISTINCT FROM 'helpdesk.request.my-comments';
UPDATE scom.system_menus SET route_key = 'helpdesk.request.new'
 WHERE path IN ('/helpdesk/request/new') AND route_key IS DISTINCT FROM 'helpdesk.request.new';
UPDATE scom.system_menus SET route_key = 'helpdesk.schedule.all'
 WHERE path IN ('/helpdesk/schedule/all') AND route_key IS DISTINCT FROM 'helpdesk.schedule.all';
UPDATE scom.system_menus SET route_key = 'helpdesk.schedule.my'
 WHERE path IN ('/helpdesk/schedule/my') AND route_key IS DISTINCT FROM 'helpdesk.schedule.my';
UPDATE scom.system_menus SET route_key = 'helpdesk.system.account-link'
 WHERE path IN ('/helpdesk/system/account-link') AND route_key IS DISTINCT FROM 'helpdesk.system.account-link';
UPDATE scom.system_menus SET route_key = 'helpdesk.system.checklist'
 WHERE path IN ('/helpdesk/system/checklist') AND route_key IS DISTINCT FROM 'helpdesk.system.checklist';
UPDATE scom.system_menus SET route_key = 'helpdesk.system.user-properties'
 WHERE path IN ('/helpdesk/system/user-properties') AND route_key IS DISTINCT FROM 'helpdesk.system.user-properties';
UPDATE scom.system_menus SET route_key = 'helpdesk.util.ascii-parser'
 WHERE path IN ('/helpdesk/util/ascii-parser') AND route_key IS DISTINCT FROM 'helpdesk.util.ascii-parser';
UPDATE scom.system_menus SET route_key = 'helpdesk.util.binary-parser'
 WHERE path IN ('/helpdesk/util/binary-parser') AND route_key IS DISTINCT FROM 'helpdesk.util.binary-parser';
UPDATE scom.system_menus SET route_key = 'helpdesk.util.diagram'
 WHERE path IN ('/helpdesk/util/diagram') AND route_key IS DISTINCT FROM 'helpdesk.util.diagram';
UPDATE scom.system_menus SET route_key = 'helpdesk.util.mc-model'
 WHERE path IN ('/helpdesk/util/mc-model') AND route_key IS DISTINCT FROM 'helpdesk.util.mc-model';
UPDATE scom.system_menus SET route_key = 'life'
 WHERE path IN ('/life') AND route_key IS DISTINCT FROM 'life';
UPDATE scom.system_menus SET route_key = 'life.birthday.calendar'
 WHERE path IN ('/life/birthday/calendar') AND route_key IS DISTINCT FROM 'life.birthday.calendar';
UPDATE scom.system_menus SET route_key = 'life.birthday.list'
 WHERE path IN ('/life/birthday/list') AND route_key IS DISTINCT FROM 'life.birthday.list';
UPDATE scom.system_menus SET route_key = 'life.birthday.messages'
 WHERE path IN ('/life/birthday/messages') AND route_key IS DISTINCT FROM 'life.birthday.messages';
UPDATE scom.system_menus SET route_key = 'life.weather.dashboard'
 WHERE path IN ('/life/weather/dashboard') AND route_key IS DISTINCT FROM 'life.weather.dashboard';
UPDATE scom.system_menus SET route_key = 'life.weather.events'
 WHERE path IN ('/life/weather/events') AND route_key IS DISTINCT FROM 'life.weather.events';
UPDATE scom.system_menus SET route_key = 'life.weather.forecast'
 WHERE path IN ('/life/weather/forecast') AND route_key IS DISTINCT FROM 'life.weather.forecast';
UPDATE scom.system_menus SET route_key = 'life.weather.history'
 WHERE path IN ('/life/weather/history') AND route_key IS DISTINCT FROM 'life.weather.history';
UPDATE scom.system_menus SET route_key = 'life.weather.manage.locations'
 WHERE path IN ('/life/weather/manage/locations') AND route_key IS DISTINCT FROM 'life.weather.manage.locations';
UPDATE scom.system_menus SET route_key = 'life.weather.manage.standards'
 WHERE path IN ('/life/weather/manage/standards') AND route_key IS DISTINCT FROM 'life.weather.manage.standards';
UPDATE scom.system_menus SET route_key = 'life.weather.responses'
 WHERE path IN ('/life/weather/responses') AND route_key IS DISTINCT FROM 'life.weather.responses';
UPDATE scom.system_menus SET route_key = 'life.weather.warning'
 WHERE path IN ('/life/weather/warning') AND route_key IS DISTINCT FROM 'life.weather.warning';
UPDATE scom.system_menus SET route_key = 'projmng'
 WHERE path IN ('/projmng') AND route_key IS DISTINCT FROM 'projmng';
UPDATE scom.system_menus SET route_key = 'projmng._smoke'
 WHERE path IN ('/projmng/_smoke') AND route_key IS DISTINCT FROM 'projmng._smoke';
UPDATE scom.system_menus SET route_key = 'projmng.comm.common-code'
 WHERE path IN ('/projmng/comm/common-code') AND route_key IS DISTINCT FROM 'projmng.comm.common-code';
UPDATE scom.system_menus SET route_key = 'projmng.db.code'
 WHERE path IN ('/projmng/db/code') AND route_key IS DISTINCT FROM 'projmng.db.code';
UPDATE scom.system_menus SET route_key = 'projmng.db.list'
 WHERE path IN ('/projmng/db/list') AND route_key IS DISTINCT FROM 'projmng.db.list';
UPDATE scom.system_menus SET route_key = 'projmng.db.table'
 WHERE path IN ('/projmng/db/table') AND route_key IS DISTINCT FROM 'projmng.db.table';
UPDATE scom.system_menus SET route_key = 'projmng.db.tester'
 WHERE path IN ('/projmng/db/tester') AND route_key IS DISTINCT FROM 'projmng.db.tester';
UPDATE scom.system_menus SET route_key = 'projmng.db.tools'
 WHERE path IN ('/projmng/db/tools') AND route_key IS DISTINCT FROM 'projmng.db.tools';
UPDATE scom.system_menus SET route_key = 'projmng.design.erd'
 WHERE path IN ('/projmng/design/erd') AND route_key IS DISTINCT FROM 'projmng.design.erd';
UPDATE scom.system_menus SET route_key = 'projmng.design.flow'
 WHERE path IN ('/projmng/design/flow') AND route_key IS DISTINCT FROM 'projmng.design.flow';
UPDATE scom.system_menus SET route_key = 'projmng.design.use-case'
 WHERE path IN ('/projmng/design/use-case') AND route_key IS DISTINCT FROM 'projmng.design.use-case';
UPDATE scom.system_menus SET route_key = 'projmng.external.funeral-monitor'
 WHERE path IN ('/projmng/external/funeral-monitor') AND route_key IS DISTINCT FROM 'projmng.external.funeral-monitor';
UPDATE scom.system_menus SET route_key = 'projmng.external.jsini'
 WHERE path IN ('/projmng/external/jsini') AND route_key IS DISTINCT FROM 'projmng.external.jsini';
UPDATE scom.system_menus SET route_key = 'projmng.proj.appointment'
 WHERE path IN ('/projmng/proj/appointment') AND route_key IS DISTINCT FROM 'projmng.proj.appointment';
UPDATE scom.system_menus SET route_key = 'projmng.proj.manage'
 WHERE path IN ('/projmng/proj/manage') AND route_key IS DISTINCT FROM 'projmng.proj.manage';
UPDATE scom.system_menus SET route_key = 'projmng.proj.monitoring'
 WHERE path IN ('/projmng/proj/monitoring') AND route_key IS DISTINCT FROM 'projmng.proj.monitoring';
UPDATE scom.system_menus SET route_key = 'projmng.proj.scheduler'
 WHERE path IN ('/projmng/proj/scheduler') AND route_key IS DISTINCT FROM 'projmng.proj.scheduler';
UPDATE scom.system_menus SET route_key = 'projmng.proj.source'
 WHERE path IN ('/projmng/proj/source') AND route_key IS DISTINCT FROM 'projmng.proj.source';
UPDATE scom.system_menus SET route_key = 'projmng.proj.user'
 WHERE path IN ('/projmng/proj/user') AND route_key IS DISTINCT FROM 'projmng.proj.user';
UPDATE scom.system_menus SET route_key = 'projmng.proj.wbs'
 WHERE path IN ('/projmng/proj/wbs') AND route_key IS DISTINCT FROM 'projmng.proj.wbs';
UPDATE scom.system_menus SET route_key = 'projmng.source.glue'
 WHERE path IN ('/projmng/source/glue') AND route_key IS DISTINCT FROM 'projmng.source.glue';
UPDATE scom.system_menus SET route_key = 'projmng.source.scaner'
 WHERE path IN ('/projmng/source/scaner') AND route_key IS DISTINCT FROM 'projmng.source.scaner';
UPDATE scom.system_menus SET route_key = 'projmng.source.trace'
 WHERE path IN ('/projmng/source/trace') AND route_key IS DISTINCT FROM 'projmng.source.trace';
UPDATE scom.system_menus SET route_key = 'projmng.sys.db-logic'
 WHERE path IN ('/projmng/sys/db-logic') AND route_key IS DISTINCT FROM 'projmng.sys.db-logic';
UPDATE scom.system_menus SET route_key = 'projmng.sys.db-logic-item'
 WHERE path IN ('/projmng/sys/db-logic-item') AND route_key IS DISTINCT FROM 'projmng.sys.db-logic-item';
UPDATE scom.system_menus SET route_key = 'projmng.todo.list'
 WHERE path IN ('/projmng/todo/list') AND route_key IS DISTINCT FROM 'projmng.todo.list';
UPDATE scom.system_menus SET route_key = 'projmng.todo.monitor'
 WHERE path IN ('/projmng/todo/monitor') AND route_key IS DISTINCT FROM 'projmng.todo.monitor';
UPDATE scom.system_menus SET route_key = 'projmng.tool.com-test'
 WHERE path IN ('/projmng/tool/com-test') AND route_key IS DISTINCT FROM 'projmng.tool.com-test';
UPDATE scom.system_menus SET route_key = 'projmng.tool.component'
 WHERE path IN ('/projmng/tool/component') AND route_key IS DISTINCT FROM 'projmng.tool.component';
UPDATE scom.system_menus SET route_key = 'projmng.tool.fast-test'
 WHERE path IN ('/projmng/tool/fast-test') AND route_key IS DISTINCT FROM 'projmng.tool.fast-test';
UPDATE scom.system_menus SET route_key = 'projmng.tool.sheet'
 WHERE path IN ('/projmng/tool/sheet') AND route_key IS DISTINCT FROM 'projmng.tool.sheet';
UPDATE scom.system_menus SET route_key = 'site'
 WHERE path IN ('/devs', '/site') AND route_key IS DISTINCT FROM 'site';
UPDATE scom.system_menus SET route_key = 'site.ai.chat'
 WHERE path IN ('/ai/chat', '/site/ai/chat') AND route_key IS DISTINCT FROM 'site.ai.chat';
UPDATE scom.system_menus SET route_key = 'site.inquiries'
 WHERE path IN ('/company/site-inquiries', '/site/inquiries') AND route_key IS DISTINCT FROM 'site.inquiries';

COMMIT;

-- ── 확인 ────────────────────────────────────────────────────────
--
-- 1) 열쇠가 붙은 메뉴 수. 153 이 나와야 한다.
-- SELECT count(*) FROM scom.system_menus WHERE route_key IS NOT NULL;
--
-- 2) 아직 안 붙은 메뉴 26건. 묶음과 대시보드 잔재만 남아야 한다.
-- SELECT path, type, coalesce(title, name) FROM scom.system_menus
--  WHERE route_key IS NULL ORDER BY type, path;
--
-- 3) 같은 열쇠를 두 메뉴가 쓰고 있지 않은지. 한 행도 안 나와야 한다.
-- SELECT route_key, count(*) FROM scom.system_menus
--  WHERE route_key IS NOT NULL GROUP BY route_key HAVING count(*) > 1;
