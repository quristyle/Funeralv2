using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthServer.DTOs;

/// <summary>
/// 계정별 화면 환경설정.
/// </summary>
/// <remarks>
/// <b>서버는 내용을 해석하지 않는다.</b> 프론트가 만든 JSON 을 그대로 보관하고 돌려준다.
/// 설정 항목이 40개가 넘고 상위 동기화마다 늘어나므로, 칸으로 쪼개면 항목이 생길 때마다
/// 서버를 고쳐야 한다.
///
/// <para>
/// 담기는 것은 <b>기본값과 다른 항목만</b>이다(프론트의 <c>diffPreference</c>).
/// 전체를 담으면 나중에 프레임워크 기본값이 바뀌어도 옛 값이 박혀 따라오지 않는다.
/// </para>
///
/// <para>
/// <see cref="JsonElement"/> 로 주고받는다. 문자열로 두면 프론트가 한 번 더
/// <c>JSON.parse</c> 해야 하고, 응답에 문자열이 실려 오는 것도 자연스럽지 않다.
/// </para>
/// </remarks>
public class AccountPreferenceDto
{
    /// <summary>
    /// 기본값과 다른 설정 항목만 담은 JSON 객체. 저장된 것이 없으면 빈 객체다.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
