using System;

using AIAgentServer.Services.Portal;

using Xunit;

namespace AIAgentServer.Tests;

/// <summary>PortalToolRunner 의 순수 줄 꾸미기 함수들. 입출력만 보는 시험이라 HTTP 는 전혀 안 걸린다.</summary>
public class PortalToolRunnerFormatTests
{
    [Fact]
    public void 헬프데스크_일정은_담당을_그대로_적는다()
    {
        var row = new HelpDeskGateway.ScheduleRow(
            "helpdesk", "정기 점검", new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc), false, "홍길동");

        var line = PortalToolRunner.RenderHelpDeskSchedule(row);

        Assert.Contains("[헬프데스크]", line);
        Assert.Contains("담당 홍길동", line);
        Assert.DoesNotContain("원문", line);
    }

    [Fact]
    public void 프로젝트관리_일정은_담당을_원문이라고_밝힌다()
    {
        // dev_user 칸은 로그인 아이디일 수도 있다 — 여기서는 "quristyle" 이 그 예다.
        var row = new ProjMngGateway.ScheduleRow(
            "projmng", "포털 로그인 개선", new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25),
            false, "quristyle", "RUNNING", "개발", null);

        var line = PortalToolRunner.RenderProjMngSchedule(row);

        Assert.Contains("[프로젝트관리]", line);
        Assert.Contains("담당(원문) quristyle", line);
    }

    [Fact]
    public void 담당자가_없으면_미정이라고_적는다()
    {
        var helpDesk = new HelpDeskGateway.ScheduleRow(
            "helpdesk", "제목", DateTime.UtcNow, DateTime.UtcNow, false, null);
        var projMng = new ProjMngGateway.ScheduleRow(
            "projmng", "제목", null, null, false, null, "READY", null, null);

        Assert.Contains("담당 미정", PortalToolRunner.RenderHelpDeskSchedule(helpDesk));
        Assert.Contains("담당(원문) 미정", PortalToolRunner.RenderProjMngSchedule(projMng));
    }

    [Fact]
    public void 요청_글_줄은_걸린_길을_함께_적는다()
    {
        var row = new HelpDeskGateway.RequestRow(
            1, "로그인이 안 돼요", "InProgress", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            null, null, "김철수", "박영희", "㈜지시니", "쓴 글");

        var line = PortalToolRunner.RenderRequest(row);

        // "요청 글 #{id}" 모양 — 화면(web/.../Assistant.razor.cs 의 RequestRefPattern)이
        // 근거 링크를 걸 때 찾는 바로 그 낱말+번호다. GetRequestAsync 의 머리글·못 찾음
        // 메시지와 한 모양이어야 한다(fix round 3).
        Assert.Contains("요청 글 #1", line);
        Assert.Contains("글쓴이 김철수", line);
        Assert.Contains("담당 박영희", line);
        Assert.Contains("관련 쓴 글", line);
    }

    [Fact]
    public void 할일_줄은_완료_여부를_적는다()
    {
        var done = new ProjMngGateway.TodoRow("문서 정리", new DateOnly(2026, 10, 25), true, "quristyle", null);
        var pending = new ProjMngGateway.TodoRow("문서 정리", null, false, null, "급하지 않음");

        Assert.Contains("완료", PortalToolRunner.RenderTodo(done));
        Assert.Contains("미완료", PortalToolRunner.RenderTodo(pending));
        Assert.Contains("급하지 않음", PortalToolRunner.RenderTodo(pending));
    }

    [Fact]
    public void 메일_줄은_종류와_받는_주소를_적는다()
    {
        var row = new MailGateway.MailRow("m1", "공지", "a@b.com", DateTime.UtcNow, "보낸 메일");

        var line = PortalToolRunner.RenderMail(row);

        Assert.Contains("[보낸 메일]", line);
        Assert.Contains("공지", line);
        Assert.Contains("a@b.com", line);
    }

    // ── search_schedules 의 (아이디, 이름) 결정 — 아이디만 왔을 때 이름을 되찾아 채우되,
    // 이름만 왔을 때는 절대 아이디를 지어내지 않는다(동명이인 때문에). ──

    [Fact]
    public void 아이디만_왔고_되찾은_이름이_있으면_이름을_채운다()
    {
        var (userId, name) = PortalToolRunner.ResolveScheduleIdentity("quristyle", null, "박경수");

        Assert.Equal("quristyle", userId);
        Assert.Equal("박경수", name);
    }

    [Fact]
    public void 이름만_왔으면_아이디를_지어내지_않는다()
    {
        // userId 가 null 인 채로 들어왔으므로 lookedUpName 은 애초에 계산되지 않는다(호출부에서
        // 아이디가 없을 때는 되찾기를 안 하므로 여기서도 null 로 둔다) — 그래도 name 은 그대로 살아야 한다.
        var (userId, name) = PortalToolRunner.ResolveScheduleIdentity(null, "김지수", null);

        Assert.Null(userId);
        Assert.Equal("김지수", name);
    }

    [Fact]
    public void 되찾기에_실패해도_아이디만으로_진행한다()
    {
        var (userId, name) = PortalToolRunner.ResolveScheduleIdentity("ghost", null, null);

        Assert.Equal("ghost", userId);
        Assert.Null(name);
    }

    [Fact]
    public void 아이디와_이름이_둘다_왔으면_그대로_둔다()
    {
        var (userId, name) = PortalToolRunner.ResolveScheduleIdentity("quristyle", "박경수", "다른이름");

        Assert.Equal("quristyle", userId);
        Assert.Equal("박경수", name); // lookedUpName 이 아니라 모델이 준 name 이 이긴다.
    }
}
