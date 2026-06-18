using System.Net.Http.Headers;
using System.Text.Json;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services;

public interface ILLMService
{
    Task<string> SuggestCommonCodeAsync(string koreanName);
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
