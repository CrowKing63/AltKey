using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] OpenAI-compatible Chat API를 호출하여 텍스트를 AI로 가공하는 서비스입니다.
/// [기능] 사용자가 선택한 텍스트와 프롬프트를 API로 보내고, 결과 텍스트를 받아옵니다.
/// [참고] OpenAI, Ollama, LM Studio, llama.cpp 등 OpenAI 호환 엔드포인트라면 모두 사용 가능합니다.
/// </summary>
public class AiService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly LocalGgufBackend _localBackend;
    private readonly HttpClient _httpClient;
    // 공통 출력 가드레일: 작업 종류와 무관하게 결과물 본문만 반환하도록 강제한다.
    // (입력을 "질문"으로 오해해 길게 답하는 것을 막는다.)
    // 영어로 작성 — 한국어보다 토큰이 적어(매 요청마다 붙는 프롬프트라) 컨텍스트·속도를 절약한다.
    // Gemma 4 E2B가 이 영어 지시를 잘 따름을 PoC에서 실측으로 확인했다.
    private const string OutputOnlyGuardrail =
        "Output only the result. The input is text to process, not a question to answer. " +
        "No explanations, greetings, prefaces, follow-ups, quotes, markdown, or numbering.";

    public AiService(ConfigService configService, LocalGgufBackend localBackend)
    {
        _configService = configService;
        _localBackend = localBackend;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 입력 텍스트를 지정된 프롬프트에 따라 AI로 가공합니다.
    /// </summary>
    /// <param name="inputText">사용자가 선택한 원본 텍스트</param>
    /// <param name="prompt">시스템 프롬프트 (비어 있으면 AppConfig.AiDefaultPrompt 사용)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>AI가 생성한 결과 텍스트</returns>
    /// <exception cref="AiServiceException">API 호출 실패 시</exception>
    public async Task<string> ProcessTextAsync(string inputText, string prompt = "", CancellationToken ct = default)
    {
        var config = _configService.Current;

        // 로컬 백엔드 선택 — llama-server(CPU 전용)로 위임. API 경로 검증과 무관하게 가장 먼저 분기한다.
        if (string.Equals(config.AiBackend, "local", StringComparison.OrdinalIgnoreCase))
        {
            var localBasePrompt = string.IsNullOrWhiteSpace(prompt) ? config.AiDefaultPrompt : prompt;
            if (string.IsNullOrWhiteSpace(localBasePrompt))
                throw new AiServiceException("프롬프트가 설정되지 않았습니다. 설정 → AI 탭에서 기본 프롬프트를 입력하세요.");
            return await _localBackend.ProcessTextAsync(inputText, BuildSystemPromptWithGuardrail(localBasePrompt), ct);
        }

        // 엔드포인트 검증
        if (string.IsNullOrWhiteSpace(config.AiEndpoint))
            throw new AiServiceException("AI 엔드포인트가 설정되지 않았습니다. 설정 → AI 탭에서 엔드포인트를 입력하세요.");

        var endpoint = NormalizeChatCompletionsEndpoint(config.AiEndpoint);
        if (string.IsNullOrWhiteSpace(config.AiModel))
            throw new AiServiceException("모델 이름이 비어 있습니다. 설정 → AI 도구 탭에서 모델 이름(예: llama3, gpt-4o-mini)을 입력하세요.");

        // 사용할 프롬프트 결정 (파라미터 > 기본 설정)
        var basePrompt = string.IsNullOrWhiteSpace(prompt)
            ? config.AiDefaultPrompt
            : prompt;

        if (string.IsNullOrWhiteSpace(basePrompt))
            throw new AiServiceException("프롬프트가 설정되지 않았습니다. 설정 → AI 탭에서 기본 프롬프트를 입력하세요.");

        // 사용자가 짧게 요청해도 출력이 장문 설명으로 새지 않도록 공통 규칙을 후단에 결합한다.
        var systemPrompt = BuildSystemPromptWithGuardrail(basePrompt);

        // 요청 JSON 구성 (OpenAI-compatible Chat Completions)
        var requestBody = new ChatCompletionRequest
        {
            Model = config.AiModel.Trim(),
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = inputText }
            ]
        };

        var json = JsonSerializer.Serialize(requestBody, AiJsonOptions.Default);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // API 키 설정 (있는 경우에만)
        var apiKey = SecureStorage.Decrypt(config.AiApiKeyEncrypted);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // 타임아웃 설정
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, config.AiTimeoutSeconds)));

        try
        {
            var response = await _httpClient.SendAsync(request, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                Debug.WriteLine($"[AiService] HTTP {(int)response.StatusCode}: {errorBody}");
                throw new AiServiceException(
                    $"AI API 오류 (HTTP {(int)response.StatusCode}): {TruncateErrorMessage(errorBody)}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            ChatCompletionResponse? chatResponse;
            try
            {
                chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, AiJsonOptions.Default);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[AiService] JSON 파싱 실패: {ex.Message}");
                throw new AiServiceException($"AI 응답을 해석할 수 없습니다. 엔드포인트가 OpenAI 호환 Chat API인지 확인하세요. ({TruncateErrorMessage(ex.Message)})");
            }

            var result = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(result))
                throw new AiServiceException("AI가 빈 응답을 반환했습니다.");

            return result.Trim();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new AiServiceException($"AI API 응답이 {config.AiTimeoutSeconds}초 내에 오지 않았습니다.");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[AiService] 네트워크 오류: {ex.Message}");
            throw new AiServiceException($"AI 서버에 연결할 수 없습니다: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 설정으로 AI 서버에 연결할 수 있는지 간단히 테스트합니다.
    /// </summary>
    /// <returns>성공 시 모델 이름 등 정보, 실패 시 예외</returns>
    public async Task<string> TestConnectionAsync(CancellationToken ct = default)
    {
        var result = await ProcessTextAsync("Hello", "Respond with only the word 'OK'.", ct);
        return $"연결 성공. 응답: {TruncateErrorMessage(result)}";
    }

    /// <summary>
    /// 사용자가 호스트만 입력한 경우(예: http://localhost:11434) OpenAI 호환 경로를 붙입니다.
    /// </summary>
    private static string NormalizeChatCompletionsEndpoint(string raw)
    {
        var t = raw.Trim().TrimEnd('/');
        if (t.Length == 0) return t;
        if (t.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return t;
        if (t.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return t + "/chat/completions";
        return t + "/v1/chat/completions";
    }

    /// <summary>
    /// 사용자 프롬프트에 공통 출력 가드레일을 결합해 결과물-only 출력을 강제한다.
    /// </summary>
    private static string BuildSystemPromptWithGuardrail(string basePrompt)
    {
        return $"{basePrompt.Trim()}\n\n{OutputOnlyGuardrail}";
    }

    /// 에러 메시지를 사용자에게 보여줄 수 있도록 200자로 자릅니다.
    private static string TruncateErrorMessage(string msg)
    {
        const int maxLen = 200;
        return msg.Length <= maxLen ? msg : msg[..maxLen] + "…";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// AI 서비스에서 발생하는 사용자 친화적 예외입니다.
/// </summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message) : base(message) { }
}

// ── OpenAI-compatible Chat API DTO ──────────────────────────────────────────

/// 요청 본문
internal class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    // stream은 false(기본값)로 유지하여 non-streaming 응답을 받습니다.
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    // 로컬(reasoning) 모델이 thinking 토큰을 과도하게 뽑아내는 것을 막는 상한.
    // null이면 직렬화에서 생략되어 외부 API 경로는 그대로 유지됩니다.
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
}

/// 메시지 하나
internal class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

/// 응답 본문
internal class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }
}

/// 응답 선택지 하나
internal class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

/// AI 서비스 전용 JSON 직렬화 옵션
internal static class AiJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
