namespace AIAgentServer.DTOs;

public class OpenAIRequest
{
    public string model { get; set; } = string.Empty;
    public List<Message> messages { get; set; } = new();
    public double temperature { get; set; } = 0.1;
    public int max_tokens { get; set; } = 100;
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
}
