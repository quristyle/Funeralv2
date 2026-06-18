namespace AIAgentServer.DTOs;

public class ChatRequestDto
{
    public List<Message> Messages { get; set; } = new();
}

public class OpenAIRequest
{
    public string model { get; set; } = string.Empty;
    public List<Message> messages { get; set; } = new();
    public double temperature { get; set; } = 0.1;
    public int max_tokens { get; set; } = 100;
    public bool stream { get; set; } = false;
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
