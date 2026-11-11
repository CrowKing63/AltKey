using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AltKey.Services;

/// <summary>
/// [역할] 로컬 llama-server(OpenAI 호환 API)를 호출해 텍스트를 AI로 가공하는 백엔드입니다.
/// 서버가 안 띄어 있으면 사이드카(ISidecarProcess)로 시작합니다(CPU 전용).
/// [기능] 추론 완료 후 일정 시간(idle) 동안 새 요청이 없으면 모델을 메모리에서 내려 RAM을 회수합니다.
/// </summary>
public class LocalGgufBackend : IAsyncDisposable
{
    private readonly ISidecarProcess _sidecar;
    private readonly ModelStore _store;
    private readonly ConfigService _configService;
    // 테스트용 idle 타임아웃 오버라이드 — null이면 설정값(AiLocalIdleUnloadMinutes)을 사용합니다.
    private readonly TimeSpan? _idleTimeoutOverride;

    private readonly object _lock = new();
    private CancellationTokenSource? _idleCts;
    // in-flight 추론 요청 수 (Interlocked). 이 값이 > 0이면 idle 타이머는 언로드하지 않습니다.
    private int _activeRequests;

    // 로컬 모델 파일명 (manifest의 model.fileName과 일치).
    private const string ModelFileName = "EXAONE-4.0-1.2B-Q8_0.gguf";

    /// <summary>모델이 메모리에 로드돼 있는지(사이드카 실행 중).</summary>
    public bool IsLoaded => _sidecar.IsRunning;

    /// <summary>
    /// 모델이 언로드(사이드카 종료)된 뒤 한 번 발생합니다. 설정 창 상태 표시 갱신용입니다.
    /// (배경 스레드에서 발생하므로 UI 갱신은 바인딩이 자동 마샬링합니다.)
    /// </summary>
    public event Action? Unloaded;

    /// <summary>모델이 로드(사이드카 시작)된 뒤 한 번 발생합니다. 설정 창 상태 표시 갱신용입니다.</summary>
    public event Action? Loaded;

    public LocalGgufBackend(ISidecarProcess sidecar, ModelStore store, ConfigService configService)
        : this(sidecar, store, configService, null) { }

    // internal: 테스트에서 짧은 idle 타임아웃으로 언로드 동작을 검증합니다.
    internal LocalGgufBackend(ISidecarProcess sidecar, ModelStore store, ConfigService configService, TimeSpan? idleTimeoutOverride)
    {
        _sidecar = sidecar;
        _store = store;
        _configService = configService;
        _idleTimeoutOverride = idleTimeoutOverride;
    }

    /// <summary>
    /// 입력 텍스트를 프롬프트에 따라 로컬 모델로 가공합니다.
    /// 서버가 안 띄어 있으면 시작(모델 로드 포함)하고, 성공하면 idle 타이머를 재시작합니다.
    /// </summary>
    public async Task<string> ProcessTextAsync(string inputText, string prompt, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _activeRequests);
        try
        {
            // 한 번의 재시도: 서버가 살아 있는데 추론만 일시적으로 실패하면 사이드카를
            // 유지한 채 한 번 더 시도합니다(보조 장치). 시작 자체가 실패하면 재시도하지 않습니다.
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var wasRunning = _sidecar.IsRunning;
                    if (!wasRunning)
                        await _sidecar.StartAsync(_store.EnginePath, _store.ModelPath(ModelFileName), ct);

                    var result = await SendChatCompletionAsync(_sidecar.BaseUrl, inputText, prompt, ct);

                    // 방금 로드된 경우 상태 표시 갱신 (설정 창 "로드됨").
                    if (!wasRunning)
                        Loaded?.Invoke();
                    return result;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // 사용자 취소 — 재시도하지 않습니다.
                }
                catch when (attempt == 0 && _sidecar.IsRunning)
                {
                    // 서버는 살아 있는데 추론만 실패 → 사이드카 유지한 채 한 번 더 시도.
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeRequests);
            // 요청이 완전히 끝난 뒤에 idle 타이머를 시작합니다.
            // decrement 이후에 켜야 idle=0일 때 OnIdleTimerElapsed가 _activeRequests==0을 보고
            // 항상 언로드합니다 (이전에 타이머가 decrement 전에 발화해 언로드가 간헐적으로 안 됨).
            RestartIdleTimer();
        }
    }

    /// <summary>
    /// 모델을 메모리에서 내려 사이드카 프로세스를 종료합니다 (RAM 회수).
    /// 수동 호출(설정 [모델 언로드]) 또는 idle 타이머에 의해 호출됩니다.
    /// 멱등 — 미로드 상태에선 아무것도 안 합니다.
    /// </summary>
    public async Task UnloadAsync()
    {
        CancelIdleTimer();
        if (!_sidecar.IsRunning)
            return; // 미로드 — no-op
        await _sidecar.DisposeAsync();
        Unloaded?.Invoke();
    }

    // idle 타이머 재시작: 추론 완료 후 새 요청이 일정 시간 없으면 언로드합니다.
    internal void RestartIdleTimer()
    {
        lock (_lock)
        {
            CancelIdleTimerLocked();
            var timeout = _idleTimeoutOverride
                ?? TimeSpan.FromMinutes(Math.Max(0, _configService.Current.AiLocalIdleUnloadMinutes));
            // 자동 타임아웃 없이 생성 — 타이머 취소는 "새 요청"에 의해서만 발생합니다.
            _idleCts = new CancellationTokenSource();
            var token = _idleCts.Token;
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(timeout, token); }
                catch (OperationCanceledException) { return; } // 새 요청으로 타이머 취소됨 → 언로드 안 함
                if (Volatile.Read(ref _activeRequests) > 0) return; // in-flight 요청 있으면 언로드 안 함
                await UnloadAsync();
            });
        }
    }

    internal void CancelIdleTimer()
    {
        lock (_lock)
        {
            CancelIdleTimerLocked();
        }
    }

    // _lock을 이미 보유한 상태에서 호출합니다.
    private void CancelIdleTimerLocked()
    {
        _idleCts?.Cancel();
        _idleCts?.Dispose();
        _idleCts = null;
    }

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

    public async ValueTask DisposeAsync()
    {
        CancelIdleTimer();
        await _sidecar.DisposeAsync();
    }
}
