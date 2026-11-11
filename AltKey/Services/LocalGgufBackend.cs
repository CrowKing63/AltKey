using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AltKey.Services;

/// <summary>
/// [역할] 로컬 llama-server(OpenAI 호환 API)를 호출해 텍스트를 AI로 가공하는 백엔드입니다.
/// 서버가 안 띄어 있으면 SidecarProcessManager로 시작합니다(CPU 전용).
/// </summary>
public class LocalGgufBackend : IAsyncDisposable
{
    private readonly SidecarProcessManager _sidecar;
    private readonly ModelStore _store;

    public LocalGgufBackend(SidecarProcessManager sidecar, ModelStore store)
    {
        _sidecar = sidecar;
        _store = store;
    }

    /// <summary>
    /// 입력 텍스트를 프롬프트에 따라 로컬 모델로 가공합니다.
    /// </summary>
    public async Task<string> ProcessTextAsync(string inputText, string prompt, CancellationToken ct)
    {
        // 서버가 안 띄어 있으면 시작합니다(모델 로드 포함, health OK까지 대기).
        if (!_sidecar.IsRunning)
            await _sidecar.StartAsync(_store.EnginePath, _store.ModelPath(ModelFileName), ct);

        return await SendChatCompletionAsync(_sidecar.BaseUrl, inputText, prompt, ct);
    }

    // 로컬 모델 파일명 (manifest의 model.fileName과 일치).
    private const string ModelFileName = "EXAONE-4.0-1.2B-Q8_0.gguf";

    /// <summary>
    /// [역할] 지정된 baseUrl의 /v1/chat/completions에 chat completion 요청을 보내고
    /// 응답의 content를 반환합니다. (테스트 가능 핵심 — 스텁 서버로 검증)
    /// </summary>
    internal static async Task<string> SendChatCompletionAsync(
        string baseUrl, string inputText, string systemPrompt, CancellationToken ct)
    {
        var requestBody = new ChatCompletionRequest
        {
            Model = "local",
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = inputText }
            ]
        };

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/v1/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, AiJsonOptions.Default), Encoding.UTF8, "application/json")
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
                throw new AiServiceException($"로컬 AI 오류 (HTTP {(int)response.StatusCode})");

            var json = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(json, AiJsonOptions.Default);
            var result = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(result))
                throw new AiServiceException("로컬 AI가 빈 응답을 반환했습니다.");
            return result.Trim();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new AiServiceException("로컬 AI 응답이 60초 내에 오지 않았습니다.");
        }
        catch (HttpRequestException ex)
        {
            throw new AiServiceException($"로컬 AI 서버에 연결할 수 없습니다: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync() => await _sidecar.DisposeAsync();
}
