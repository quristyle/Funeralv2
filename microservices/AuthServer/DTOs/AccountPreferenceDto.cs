using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthServer.DTOs;

/// <summary>계정별 화면 환경설정. 서버는 내용을 해석하지 않고 프론트가 만든 JSON 을 그대로 보관한다 — 항목이 40개를 넘고 늘어나므로 칸으로 쪼개면 항목마다 서버를 고쳐야 한다.</summary>
public class AccountPreferenceDto
{
    /// <summary>기본값과 다른 설정 항목만 담은 JSON 객체. 저장된 것이 없으면 빈 객체다.</summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}
