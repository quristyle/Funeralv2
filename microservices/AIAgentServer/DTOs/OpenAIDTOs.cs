namespace AIAgentServer.DTOs;

public class ChatRequestDto
{
    public List<Message> Messages { get; set; } = new();

    /// <summary>쓸 AI 공급자 키(jsini · groq). 비었거나 모르는 값이면 기본 공급자로 돈다 — 이름이 바뀐 옛 값을 든 브라우저도 AI 기능을 잃지 않게 하려고다.</summary>
    public string? Provider { get; set; }

    /// <summary>사내 문서(RAG)를 먼저 뒤질지. 안 보내면 켠 것으로 본다. 맞는 것이 있으면 웹은 안 뒤진다(Mcp:SkipWhenRagScore). 끄면 사내 문서를 아예 안 본다.</summary>
    public bool? PreferRag { get; set; }

    /// <summary>사내 문서(RAG)로만 답한다. 켜면 <see cref="PreferRag"/> 보다 앞선다.</summary>
    public bool? RagOnly { get; set; }

    /// <summary>사내 문서를 찾을 카테고리. 비우면 설정대로(기본은 전부). 볼 수 있는 것인지는 서버가 다시 확인한다.</summary>
    public string? RagCategory { get; set; }

    /// <summary>그 카테고리 안의 분류. 하위 분류의 문서까지 찾는다. 카테고리 없이 오면 무시한다.</summary>
    public long? RagFolderId { get; set; }

    public AIAgentServer.Services.Rag.RagMode RagMode => RagOnly == true
        ? AIAgentServer.Services.Rag.RagMode.Only
        : PreferRag == false ? AIAgentServer.Services.Rag.RagMode.Off : AIAgentServer.Services.Rag.RagMode.Auto;

    /// <summary>쓸 모델(OpenRouter 만 쓴다). 브라우저에서 온 믿을 수 없는 입력이라 서버가 무료 여부를 반드시 확인한다(FreeModelGuard).</summary>
    public string? Model { get; set; }

    /// <summary>담을 대화 열쇠(scom.ai_chat_sessions.id). 비우면 오간 말이 어디에도 안 남는다. 서버가 담는다 — 화면이 따로 올리면 창을 먼저 닫은 대화가 사라진다.
    /// 남의 열쇠는 AiChatStore.RecordTurn 이 주인을 조건에 넣어 거른다.</summary>
    public string? SessionId { get; set; }
}

public class OpenAIRequest
{
    public string model { get; set; } = string.Empty;
    public List<Message> messages { get; set; } = new();
    public double temperature { get; set; } = 0.1;
    public int max_tokens { get; set; } = 100;
    public bool stream { get; set; } = false;

    /// <summary>스트리밍 마지막 조각에 토큰 수를 실어 달라는 부탁. 스트리밍일 때만 보내고 비스트리밍에서는 null 로 빠진다. 모르는 공급자는 무시하므로 토큰 칸이 비고 호출 건수만 남을 수 있다.</summary>
    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OpenAIStreamOptions? stream_options { get; set; }

    /// <summary>OpenRouter 전용 라우팅 조건. 다른 공급자에게는 보내지 않는다(null 이면 빠진다).</summary>
    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OpenRouterProviderPrefs? provider { get; set; }
}

/// <summary>OpenRouter 에 돈 드는 경로를 쓰지 말라고 본문으로 못 박는 값. FreeModelGuard 와 별개의 두 번째 방어선이다. 보장하는 것은 max_price 다(전부 0 이면 무료 경로만 남는다).</summary>
public class OpenRouterProviderPrefs
{
    /// <summary>같은 모델을 서비스하는 다른 상류 제공자로 넘기는 것을 허용한다. 가격은 모델 이름에 붙고 max_price 가 0 이라, 과금은 이 값이 아니라 단가 상한이 막는다. 끄면 무료 모델의 상류가 일시적으로 막힐 때 그대로 실패한다.</summary>
    public bool allow_fallbacks { get; set; } = true;

    /// <summary>백만 토큰당 허용 최고 단가. 전부 0 이라 무료 경로만 남는다 — 이것이 보장이다.</summary>
    public OpenRouterMaxPrice max_price { get; set; } = new();
}

/// <summary>백만 토큰당 허용 최고 단가. 전부 0 으로 보낸다.</summary>
public class OpenRouterMaxPrice
{
    public int prompt { get; set; }
    public int completion { get; set; }
    public int request { get; set; }
    public int image { get; set; }
    public int audio { get; set; }
}

public class Message
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

/// <summary>스트리밍에서 토큰 수를 함께 달라고 적는 자리.</summary>
public class OpenAIStreamOptions
{
    public bool include_usage { get; set; } = true;
}

public class OpenAIResponse
{
    public List<Choice> choices { get; set; } = new();

    /// <summary>쓴 토큰 수. 공급자가 안 주면 null 이다.</summary>
    public OpenAIUsage? usage { get; set; }
}

/// <summary>공급자가 알려 준 토큰 수. 이 호출 한 번의 값이다. 대화는 기록을 문맥으로 다시 올리므로 prompt_tokens 는 대화가 길어질수록 커진다.</summary>
public class OpenAIUsage
{
    public int prompt_tokens { get; set; }
    public int completion_tokens { get; set; }
    public int total_tokens { get; set; }
}

public class Choice
{
    public Message message { get; set; } = new();
    public Delta delta { get; set; } = new();
    public string finish_reason { get; set; } = string.Empty;
}

public class Delta
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

public class OpenAIStreamResponse
{
    public string id { get; set; } = string.Empty;
    public string @object { get; set; } = string.Empty;
    public long created { get; set; }
    public string model { get; set; } = string.Empty;
    public List<Choice> choices { get; set; } = new();

    /// <summary>토큰 수. 맨 마지막 조각에만 들어 있고, 그 조각은 choices 가 비어 있다(OpenAIStreamOptions).</summary>
    public OpenAIUsage? usage { get; set; }
}
