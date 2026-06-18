using System.Net.Http.Headers;
using System.Text.Json;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services;

public interface ILLMService
{
    Task<string> SuggestCommonCodeAsync(string koreanName);
    Task<string> ChatAsync(List<Message> messages);
    IAsyncEnumerable<string> StreamChatAsync(List<Message> messages);
}

public class LLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<LLMService> _logger;

    public LLMService(HttpClient httpClient, IConfiguration config, ILogger<LLMService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(List<Message> messages)
    {
        var apiBase = _config["LLM:ApiBase"];
        var apiKey = _config["LLM:ApiKey"];
        var model = _config["LLM:Model"];

        // 시스템 프롬프트 주입 (기존과 동일)
        if (!messages.Any(m => m.role == "system"))
        {
            messages.Insert(0, new Message 
            { 
                role = "system", 
                content = "당신은 시스템 관리를 돕는 친절하고 전문적인 AI 어시스턴트입니다. 한국어로 자연스럽게 답변해주세요." 
            });
        }

        var requestDto = new OpenAIRequest
        {
            model = model,
            temperature = 0.7,
            max_tokens = 2000,
            messages = messages,
            stream = true // 스트리밍 활성화
        };

        var request = new HttpRequestMessage(HttpMethod.Post, apiBase);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(requestDto);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("LLM Streaming API failed. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
            yield return "⚠️ 오류가 발생했습니다.";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6).Trim();
                if (json == "[DONE]") break;

                OpenAIStreamResponse? chunk = null;
                try 
                {
                    chunk = JsonSerializer.Deserialize<OpenAIStreamResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to deserialize chunk: {Error}, JSON: {Json}", ex.Message, json);
                    continue;
                }

                var content = chunk?.choices?.FirstOrDefault()?.delta?.content;
                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }
    }

    public async Task<string> ChatAsync(List<Message> messages)
    {
        var apiBase = _config["LLM:ApiBase"];
        var apiKey = _config["LLM:ApiKey"];
        var model = _config["LLM:Model"];

        if (string.IsNullOrEmpty(apiBase) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
        {
            throw new Exception("LLM configuration is missing.");
        }

        // 시스템 프롬프트가 없다면 기본값 주입
        if (!messages.Any(m => m.role == "system"))
        {
            messages.Insert(0, new Message 
            { 
                role = "system", 
                content = "당신은 시스템 관리를 돕는 친절하고 전문적인 AI 어시스턴트입니다. 한국어로 자연스럽게 답변해주세요." 
            });
        }

        var requestDto = new OpenAIRequest
        {
            model = model,
            temperature = 0.7, // 일반 채팅은 약간의 창의성 허용
            max_tokens = 2000, // 긴 답변을 위해 넉넉히 설정
            messages = messages
        };

        var request = new HttpRequestMessage(HttpMethod.Post, apiBase);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(requestDto);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("LLM API failed. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
            throw new Exception("LLM API request failed.");
        }

        var resultString = await response.Content.ReadAsStringAsync();
        var resultDto = JsonSerializer.Deserialize<OpenAIResponse>(resultString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var reply = resultDto?.choices?.FirstOrDefault()?.message?.content?.Trim();

        return string.IsNullOrEmpty(reply) ? "죄송합니다. 응답을 생성하지 못했습니다." : reply;
    }

    public async Task<string> SuggestCommonCodeAsync(string koreanName)
    {
        var apiBase = _config["LLM:ApiBase"];
        var apiKey = _config["LLM:ApiKey"];
        var model = _config["LLM:Model"];

        if (string.IsNullOrEmpty(apiBase) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
        {
            throw new Exception("LLM configuration is missing.");
        }

        var requestDto = new OpenAIRequest
        {
            model = model,
            temperature = 0.1, // 창의성보다는 정확성을 위해 낮게 설정
            max_tokens = 1000, // Reasoning 모델을 위해 토큰을 충분히 넉넉하게 설정
            messages = new List<Message>
            {
                new Message
                {
                    role = "system",
                    content = "당신은 소프트웨어 엔지니어입니다. 입력된 한글 명칭을 보고, 프로그래밍 변수명으로 적합한 '영어 대문자 스네이크 케이스(SNAKE_CASE)' 코드로 변환하세요. 부연 설명 없이 오직 결과 코드만 한 줄로 출력하세요."
                },
                new Message
                {
                    role = "user",
                    content = koreanName
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, apiBase);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(requestDto);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("LLM API failed. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
            throw new Exception("LLM API request failed.");
        }

        var resultString = await response.Content.ReadAsStringAsync();
        var resultDto = JsonSerializer.Deserialize<OpenAIResponse>(resultString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var suggestedCode = resultDto?.choices?.FirstOrDefault()?.message?.content?.Trim();

        if (string.IsNullOrEmpty(suggestedCode))
        {
            throw new Exception("LLM returned empty result.");
        }

        // 혹시 LLM이 마크다운(예: `CODE`)을 넣었을 경우를 대비한 후처리
        suggestedCode = suggestedCode.Replace("`", "").Trim();
        
        return suggestedCode;
    }
}
