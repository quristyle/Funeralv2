using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「AI 작업 요청」 화면이 <b>올린 사람의 눈으로</b> 읽는 진행 단계.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AiTaskDto.StatusText"/> 를 그대로 쓰지 않는다. 그쪽은
/// <b>기계의 현재 위치</b>를 적는 말이라 「작성중」·「대기」 같은 값이 나오는데,
/// 지시를 올려 두고 기다리는 사람에게 그 두 글자는 <b>거짓말에 가깝다</b> —
/// 본인은 다 적어 저장했는데 화면이 「작성중」이라고 말하면
/// <i>내가 뭘 덜 했나</i>를 찾게 된다.
/// </para>
/// <para>
/// 여기서 답하는 물음은 셋뿐이다 — <b>내가 더 고칠 수 있나 · 관리자가 봤나 ·
/// 끝났나.</b> 그래서 상태값 아홉을 다섯 단계로 접는다.
/// </para>
/// <para>
/// [<see cref="Editable"/> 는 서버와 <b>같은 판정</b>이어야 한다]
/// </para>
/// <para>
/// 서버의 <c>AiTaskService.IsStillEditable</c> 이 같은 넷을 본다. 화면이 더
/// 너그러우면 <b>단추는 보이는데 눌러도 409 가 돌아오고</b>, 화면이 더 빡빡하면
/// 고칠 수 있는 건이 잠긴 것처럼 보인다. 둘 다 사람이 이유를 알 수 없는 모양이라
/// 한쪽을 고치면 다른 쪽도 함께 본다.
/// </para>
/// </remarks>
internal static class AiRequestStage
{
    /// <summary>
    /// 올린 사람이 아직 고칠 수 있나. <b>관리자가 손대기 전까지</b>다.
    /// </summary>
    /// <remarks>
    /// 상태만 보지 않는다. 관리자가 작업 대상을 채워 두었으면 시키기 직전이라는
    /// 뜻이고, 그때 본문이 바뀌면 <b>관리자가 읽고 판단한 글과 실제로 도는 글이
    /// 달라진다.</b>
    /// </remarks>
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

            // 여기부터는 전부 `idle` 이다. **관리자가 손댔는지로 갈린다** —
            // 대상이 채워졌거나 한 번이라도 돈 적이 있으면 접수는 끝난 것이다.
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
