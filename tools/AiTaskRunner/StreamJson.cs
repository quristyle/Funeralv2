using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// <c>--output-format stream-json</c> 한 줄에서 쓸 것만 가려낸다.
/// </summary>
/// <remarks>
/// <para>
/// 두 CLI 의 모양이 다르다. <c>claude</c> 는 <c>{"type":"result","result":…}</c>
/// 계열이고, <c>agy</c> 는 <c>{"event":"result","result":{…}}</c> 계열이다
/// (그 값은 실행해서 눈으로 확인했다).
/// </para>
/// <para>
/// <b>모르는 모양이면 원문을 그대로 로그로 남긴다.</b> 파싱에 실패했다고
/// 줄을 버리면 무슨 일이 있었는지 볼 방법이 사라진다 — 그것이 이 자리에서
/// 가장 나쁜 실패다.
/// </para>
/// </remarks>
public static class StreamJson
{
    public static (string? Human, string? Result, string? SessionId) Pick(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return (line, null, null);
            }

            var session = Text(root, "session_id") ?? Text(root, "conversation_id");

            // agy: {"event":"init","conversation_id":…} · {"event":"result","result":{…}}
            if (root.TryGetProperty("event", out var ev))
            {
                var name = ev.GetString();

                if (root.TryGetProperty("result", out var agyResult))
                {
                    session ??= Text(agyResult, "conversation_id");

                    var response = Text(agyResult, "response");
                    var error = Text(agyResult, "error");

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        return ($"[오류] {error}", response, session);
                    }

                    return ($"[{name}] 끝", response, session);
                }

                if (root.TryGetProperty("init", out var init))
                {
                    session ??= Text(init, "conversation_id");
                    return ($"[{name}] 시작", null, session);
                }

                // agy 의 진행 보고. **여기서 가려내지 않으면 로그가
                // `[step_update]` 스무 줄로만 남는다** — 무엇을 하고 있었는지
                // 하나도 안 보인다(실제로 한 번 그렇게 찍혔다).
                if (root.TryGetProperty("step_update", out var step))
                {
                    session ??= Text(step, "conversation_id");

                    var state = Text(step, "state");
                    var stepType = Text(step, "step_type");
                    var tool = Text(step, "tool_name");

                    // 도구 인자는 싣지 않는다. 파일 내용이 통째로 들어온다.
                    var what = tool is { Length: > 0 } ? $"{stepType}:{tool}" : stepType;

                    return ($"[{state}] {what}", null, session);
                }

                return ($"[{name}]", null, session);
            }

            // claude: {"type":"result","result":"…"} · {"type":"assistant","message":{…}}
            if (root.TryGetProperty("type", out var type))
            {
                var name = type.GetString();

                if (name == "result")
                {
                    var text = Text(root, "result");
                    return ("[result] 끝", text, session);
                }

                if (root.TryGetProperty("message", out var msg)
                    && msg.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();

                    foreach (var part in content.EnumerateArray())
                    {
                        if (Text(part, "text") is { Length: > 0 } t)
                        {
                            parts.Add(t);
                        }
                        else if (Text(part, "name") is { Length: > 0 } toolName)
                        {
                            // 도구 호출은 이름만 남긴다. 인자를 통째로 남기면
                            // 파일 내용이 로그에 통째로 들어온다.
                            parts.Add($"[도구] {toolName}");
                        }
                    }

                    return (parts.Count == 0 ? Quiet(name) : string.Join('\n', parts), null, session);
                }

                return (Quiet(name), null, session);
            }

            return (line, null, session);
        }
        catch (JsonException)
        {
            // JSON 이 아니었다. 그대로 남긴다.
            return (line, null, null);
        }
    }

    /// <summary>
    /// 글자가 없는 표식을 어떻게 적을까.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>claude</c> 의 <c>system</c> · <c>user</c> 줄에는 사람이 읽을 것이
    /// 없다 — <c>user</c> 는 도구 결과를 되돌려 주는 줄이고(바로 앞에
    /// <c>[도구]</c> 가 이미 찍혀 있다), <c>system</c> 은 <c>init</c> ·
    /// <c>post_turn_summary</c> 같은 내부 표식이다.
    /// </para>
    /// <para>
    /// 그대로 두면 스무 줄 중 다섯이 <c>[system]</c> · <c>[user]</c> 로 채워져
    /// <b>무엇을 하고 있었는지가 오히려 안 보인다.</b> 빈 글자를 돌려주면
    /// 부르는 쪽이 그 줄을 버린다.
    /// </para>
    /// <para>
    /// <b>모르는 이름은 남긴다.</b> 목록에 없는 것을 버리면 새 모양이 생겼을 때
    /// 조용히 사라진다.
    /// </para>
    /// </remarks>
    private static string Quiet(string? name) =>
        name is "system" or "user" ? string.Empty : $"[{name}]";

    private static string? Text(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
