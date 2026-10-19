using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「AI 작업 요청」 화면이 올린 사람의 눈으로 읽는 진행 단계. 기계 상태 그대로는 「작성중」처럼 기다리는 사람에게 거짓말에 가깝다.
/// Editable 은 서버 AiTaskService.IsStillEditable 과 같은 판정이어야 한다 — 어긋나면 단추가 409 를 낸다.
/// </summary>
internal static class AiRequestStage
{
    /// <summary>올린 사람이 아직 고칠 수 있나. 관리자가 대상을 채웠으면 시키기 직전이라, 본문을 바꾸면 판단한 글과 도는 글이 달라진다.</summary>
    public static bool Editable(AiTaskDto t) =>
        t.RequestFlag == "none"
        && t.TaskStatus == "idle"
        && t.TargetKey is null
        && t.LastRunKey is null;

    /// <summary>사람이 읽는 단계 이름.</summary>
    public static string Text(AiTaskDto t)
    {
        if (t.IsBusy)
        {
            return "작업 진행중";
        }

        return t.TaskStatus switch
        {
            "succeeded" => "작업 완료",
            "failed" or "timeout" or "interrupted" => "작업 실패",
            "canceled" => "취소됨",

            // 여기부터는 전부 idle 이다. 관리자가 손댔는지(대상 채움 · 한 번이라도 돈 적)로 접수 여부가 갈린다.
            _ => Editable(t) ? "접수 대기" : "관리자 확인",
        };
    }

    /// <summary>배지 수식어. 공통 배지(<c>jsini-badge--*</c>)를 그대로 쓴다.</summary>
    public static string Tone(AiTaskDto t)
    {
        if (t.IsBusy)
        {
            return "warn";
        }

        return t.TaskStatus switch
        {
            "succeeded" => "on",
            "failed" or "timeout" or "interrupted" => "err",
            "canceled" => "off",
            _ => Editable(t) ? "off" : "warn",
        };
    }
}
