namespace AltKey.Models;

/// <summary>
/// [역할] 로컬 AI 엔진과 모델 파일의 설치 명세(manifest)를 담는 클래스입니다.
/// "로컬 AI 다운로드" 버튼이 엔진·모델 둘 다 설치됐는지 판단하는 데 쓰입니다.
/// </summary>
public class ModelManifest
{
    // 설치 대상 구성 요소 목록(엔진 + 모델).
    public List<ManifestEntry> Entries { get; set; } = [];

    // 특정 구성 종류("engine"/"model")의 명세를 찾습니다. 없으면 null을 반환합니다.
    public ManifestEntry? GetEntry(string component) =>
        Entries.FirstOrDefault(e => e.Component == component);

    /// <summary>
    /// [역할] 기본(내장) manifest입니다. 현재 PoC로 검증된 Gemma 4 E2B (q4_0) +
    /// llama.cpp b12262 Windows CPU 빌드를 담습니다. "로컬 AI 다운로드" 버튼이
    /// 이 명세에 따라 엔진·모델을 설치합니다.
    /// </summary>
    public static ModelManifest Default => new()
    {
        Entries =
        {
            new ManifestEntry
            {
                Component = "engine",
                FileName = "llama-b12262-bin-win-cpu-x64.zip",
                SourceUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b12262/llama-b12262-bin-win-cpu-x64.zip",
                Sha256 = "7d28975f1acc6c142954c4a0d1dfb9c3a3a13736c41f898890748cc067487d7a",
                SizeBytes = 25_044_988,
                Version = "b12262"
            },
            new ManifestEntry
            {
                Component = "model",
                FileName = "gemma-4-E2B_q4_0-it.gguf",
                SourceUrl = "https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/resolve/main/gemma-4-E2B_q4_0-it.gguf",
                Sha256 = "fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634",
                SizeBytes = 3_349_516_256,
                Version = "q4_0"
            }
        }
    };
}

/// <summary>
/// [역할] manifest의 개별 구성 요소(엔진 exe 또는 모델 gguf)를 설명하는 클래스입니다.
/// </summary>
public class ManifestEntry
{
    // 구성 종류 ("engine" / "model").
    public string Component { get; set; } = "";

    // 저장될 파일 이름 (예: llama-server.exe, gemma-4-E2B_q4_0-it.gguf).
    public string FileName { get; set; } = "";

    // 다운로드 원본 URL (HuggingFace resolve).
    public string SourceUrl { get; set; } = "";

    // 완료 후 검증할 SHA-256 (hex).
    public string Sha256 { get; set; } = "";

    // 파일 크기(바이트) — 다운로드 진행률 계산용.
    public long SizeBytes { get; set; }

    // 버전/빌드 라벨 (예: "b12262", "Q8_0").
    public string Version { get; set; } = "";
}
