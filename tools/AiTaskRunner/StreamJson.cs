using System.Text.Json;

namespace AiTaskRunner;

/// <summary>
/// stream-json 한 줄에서 쓸 것만 가려낸다. claude 와 agy 는 모양이 다르다(실행해 확인한 값).
/// 모르는 모양은 원문을 그대로 로그로 남긴다 — 파싱 실패로 줄을 버리면 무슨 일이 있었는지 볼 방법이 사라진다.
/// </summary>
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

                // agy 의 진행 보고. 여기서 가려내지 않으면 로그가 [step_update] 줄로만 남아 무엇을 하는지 안 보인다.
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
                            // 도구 호출은 이름만 남긴다 — 인자를 통째로 남기면 파일 내용이 로그에 들어온다.
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
    /// 글자가 없는 표식(claude 의 system · user 줄 등)은 빈 글자를 돌려 부르는 쪽이 그 줄을 버리게 한다.
    /// 목록에 없는 이름은 남긴다 — 버리면 새 모양이 생겼을 때 조용히 사라진다.
    /// </summary>
    private static string Quiet(string? name) =>
        name is "system" or "user" ? string.Empty : $"[{name}]";

    private static string? Text(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
