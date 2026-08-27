namespace AIAgentServer.DTOs;

public class ChatRequestDto
{
    public List<Message> Messages { get; set; } = new();

    /// <summary>
    /// 쓸 AI 공급자(<c>jsini</c> · <c>groq</c>). 사용자가 환경설정에서 고른 값이다.
    /// </summary>
    /// <remarks>
    /// 비어 있거나 모르는 값이면 설정의 기본 공급자로 돈다. 옛 화면이 이 값을 안 보내도
    /// 그대로 동작해야 하고, 공급자 이름이 바뀌었을 때 옛 값을 든 브라우저가
    /// AI 기능을 통째로 잃는 것도 막아야 한다.
    /// </remarks>
    public string? Provider { get; set; }

    /// <summary>
    /// 쓸 모델. 사용자가 환경설정에서 고른 값이며 <b>공급자가 모델 선택을 허용할
    /// 때만</b> 쓰인다(지금은 OpenRouter).
    /// </summary>
    /// <remarks>
    /// <b>믿을 수 없는 입력이다.</b> 브라우저에서 온 문자열이고, OpenRouter 는 같은
    /// API 로 유료 모델도 부를 수 있다. 서버가 무료 여부를 반드시 확인한다
    /// (<see cref="AIAgentServer.Services.FreeModelGuard"/>).
    /// </remarks>
    public string? Model { get; set; }
}

public class OpenAIRequest
{
    public string model { get; set; } = string.Empty;
    public List<Message> messages { get; set; } = new();
    public double temperature { get; set; } = 0.1;
    public int max_tokens { get; set; } = 100;
    public bool stream { get; set; } = false;

    /// <summary>
    /// OpenRouter 전용 라우팅 조건. <b>다른 공급자에게는 보내지 않는다</b>(null 이면 빠진다).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OpenRouterProviderPrefs? provider { get; set; }
}

/// <summary>
/// OpenRouter 에게 <b>돈이 드는 경로를 쓰지 말라</b>고 요청 본문으로 못 박는 값.
/// </summary>
/// <remarks>
/// <para>
/// 모델 이름을 우리가 검사하는 것(<see cref="AIAgentServer.Services.FreeModelGuard"/>)과
/// <b>별개의 두 번째 방어선</b>이다. 우리 판단이 틀렸거나 목록이 최신이 아니어도
/// 공급자 쪽에서 막히게 둔다.
/// </para>
/// <para>
/// <b>보장하는 것은 <see cref="max_price"/> 다.</b> 전부 0 으로 보내면 OpenRouter 가
/// 고를 수 있는 경로가 <b>무료뿐</b>이다. 어떤 경로를 고르더라도 이 상한을 넘을 수 없다.
/// </para>
/// </remarks>
public class OpenRouterProviderPrefs
{
    /// <summary>
    /// 같은 모델을 서비스하는 <b>다른 제공자로 넘기는 것을 허용</b>한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>처음에는 이것을 껐다가 되돌렸다.</b> "알아서 넘긴다" 가 유료로 새는 통로처럼
    /// 보였기 때문인데, 두 가지를 잘못 본 것이었다.
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     이 값이 바꾸는 것은 <b>같은 모델을 서비스하는 상류 제공자</b>다. 모델 이름은
    ///     그대로이고, OpenRouter 의 가격은 <b>모델 이름</b>에 붙는다 —
    ///     <c>:free</c> 모델은 누가 서비스하든 0 이다.
    ///   </item>
    ///   <item>
    ///     설령 값이 드는 경로가 있어도 <see cref="max_price"/> 가 0 이라 고를 수 없다.
    ///     즉 과금 방어는 이 값이 아니라 단가 상한이 한다.
    ///   </item>
    /// </list>
    /// <para>
    /// 끄면 <b>가용성만 잃는다.</b> 무료 모델의 상류 제공자는 자주 일시적으로
    /// 한도에 걸리는데(429 "temporarily rate-limited upstream"), 껐을 때는 그 한 곳에
    /// 묶여 그대로 실패한다. 실제로 이 때문에 정밀 확인이 계속 429 로 떨어졌다.
    /// </para>
    /// </remarks>
    public bool allow_fallbacks { get; set; } = true;

    /// <summary>
    /// 백만 토큰당 허용 최고 단가. <b>전부 0 이라 무료 경로만 남는다 — 이것이 보장이다.</b>
    /// </summary>
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

public class OpenAIResponse
{
    public List<Choice> choices { get; set; } = new();
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
}
