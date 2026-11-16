using System;
using System.IO;
using System.IO.Compression;

namespace AltKey.Services;

/// <summary>
/// [역할] 사용자 설정 데이터(설정·레이아웃·사용자 사전 등)를 백업/복원/초기화하는 서비스입니다.
/// [위치] 모든 사용자 데이터는 PathResolver.DataDir 한 폴더에 있습니다.
///       로컬 AI 엔진·모델(engine/, models/)은 수백 MB라 백업·초기화 대상에서 제외합니다 — 설정 화면에서 다시 다운로드 가능.
/// </summary>
public sealed class SettingsBackupService
{
    // zip 안의 항목 이름 = 데이터 폴더 기준 상대 경로 (예: "config.json", "layouts/basic.json")
    private readonly string _dataDir;

    /// <param name="dataDir">사용자 설정 데이터 폴더. 기본값은 PathResolver.DataDir입니다 (테스트에서 임시 경로를 줌).</param>
    public SettingsBackupService(string? dataDir = null) =>
        _dataDir = dataDir ?? PathResolver.DataDir;

    // 백업 대상 루트 파일 패턴 (로컬 AI 엔진·모델 제외).
    private static readonly string[] RootFilePatterns =
    [
        "config.json",
        "scratchpad.json",
        "clipboard_history.json",
        "user-words.*.json",
        "user-bigrams.*.json",
    ];

    // 초기화 시 삭제할 루트 파일 패턴 — config.json은 제외합니다.
    // [중요] 포터블 모드에서 config.json이 exe 옆에 존재하는 것 자체가 포터블 판별 마커라,
    // 파일을 지우면 재시작 시 설치판(%AppData%)으로 오인됩니다. 설정 내용 자체는
    // ConfigService.ResetToDefaults()가 기본값으로 덮어써 초기화합니다.
    private static readonly string[] ResetDeletionPatterns =
    [
        "scratchpad.json",
        "clipboard_history.json",
        "user-words.*.json",
        "user-bigrams.*.json",
    ];

    /// <summary>
    /// 현재 사용자 설정 데이터를 단일 zip 백업 파일로 만듭니다.
    /// 이미 같은 이름의 파일이 있으면 덮어씁니다.
    /// </summary>
    public string CreateBackup(string destZipPath)
    {
        Directory.CreateDirectory(_dataDir);
        if (File.Exists(destZipPath)) File.Delete(destZipPath);

        // .NET(코어)에서는 새 zip 생성용 ZipFile.Open(path, CompressionMode) 오버로드가 없어
        // FileStream + ZipArchive로 직접 만듭니다 (기존 파일은 덮어씀).
        using var stream = new FileStream(destZipPath, FileMode.Create);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var pattern in RootFilePatterns)
        {
            foreach (var file in Directory.EnumerateFiles(_dataDir, pattern))
                zip.CreateEntryFromFile(file, ToEntryName(file));
        }

        // 레이아웃 폴더(내장 수정본 + 커스텀) 전체를 포함합니다.
        var layoutsDir = Path.Combine(_dataDir, "layouts");
        if (Directory.Exists(layoutsDir))
        {
            foreach (var file in Directory.EnumerateFiles(layoutsDir, "*", SearchOption.AllDirectories))
                zip.CreateEntryFromFile(file, ToEntryName(file));
        }

        return destZipPath;
    }

    /// <summary>
    /// 백업 zip으로 사용자 설정을 복원합니다(기존 파일은 덮어씀).
    /// 레이아웃 폴더는 백업 내용으로 교체 — "백업 시점의 상태"를 되돌립니다.
    /// 단, 내장 레이아웃(Basic/Basic Plus)은 앱 리소스라 삭제하지 않습니다 —
    /// 포터블 모드에서는 이 파일들이 exe 옆 시드 소스 그 자체이기 때문입니다.
    /// </summary>
    public void RestoreFromZip(string zipPath)
    {
        ValidateBackup(zipPath);

        Directory.CreateDirectory(_dataDir);

        // 백업에 없는 커스텀 레이아웃이 남지 않도록 layouts 폴더의 커스텀 파일만 지웁니다 (내장은 보존).
        DeleteCustomLayouts();

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // 폴더 엔트리
            var target = SafeTargetPath(entry.FullName);
            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>
    /// 해당 경로가 복원 가능한 AltKey 백업 zip인지 검증합니다.
    /// 파일이 없거나 zip이 아니거나 AltKey 내용이 아니면 InvalidDataException(사용자에게 보여줄 메시지)을 던집니다.
    /// </summary>
    public static void ValidateBackup(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new InvalidDataException("파일을 찾을 수 없습니다.");

        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(zipPath);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("압축 파일을 열 수 없습니다(파일이 손상된 것 같습니다).");
        }
        catch (InvalidOperationException)
        {
            // 비-zip 파일 등 — ZipFile이 던지는 예외는 영어 메시지만이라 한국어로 대체합니다.
            throw new InvalidDataException("ZIP 압축 파일이 아닙니다.");
        }

        using (zip)
        {
            var entries = zip.Entries;
            if (!entries.Any(e => IsEntry(e, "config.json")) &&
                !entries.Any(e => EntryDir(e) == "layouts"))
            {
                throw new InvalidDataException("AltKey 백업 파일이 아닙니다. '사용자 설정 백업'으로 만든 ZIP 파일을 선택해주세요.");
            }
        }
    }

    /// <summary>
    /// 사용자 설정을 처음 설치한 상태로 초기화합니다(사용자 데이터 파일·커스텀 레이아웃 삭제).
    /// [중요] config.json은 내용만 기본값으로 되돌립니다(ConfigService.ResetToDefaults) — 파일을 지우면 안 됩니다.
    /// 내장 레이아웃(Basic/Basic Plus)도 보존합니다. 포터블 모드에서는 exe 옆 시드 소스 그 자체라,
    /// 지우면 다음 시작 시 내장 레이아웃이 사라져 "레이아웃 파일 없음" 오류가 납니다.
    /// 로컬 AI 엔진·모델(engine/, models/)은 건드리지 않습니다.
    /// </summary>
    public void ResetUserSettings()
    {
        foreach (var pattern in ResetDeletionPatterns)
        {
            foreach (var file in Directory.EnumerateFiles(_dataDir, pattern))
                File.Delete(file);
        }

        DeleteCustomLayouts();
    }

    // 레이아웃 폴더의 커스텀 파일만 삭제합니다 — 내장(Basic/Basic Plus)은 앱 리소스라 보존.
    private void DeleteCustomLayouts()
    {
        var layoutsDir = Path.Combine(_dataDir, "layouts");
        if (!Directory.Exists(layoutsDir)) return;

        foreach (var file in Directory.EnumerateFiles(layoutsDir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!LayoutService.IsBuiltin(name)) File.Delete(file);
        }
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────

    // 데이터 폴더 기준 상대 경로를 zip 표준(슬래시 구분) 이름으로 바꿉니다.
    private string ToEntryName(string filePath) =>
        Path.GetRelativePath(_dataDir, filePath).Replace('\\', '/');

    // zip 항목의 상위 폴더 이름을 정규화해 반환 (예: "layouts/basic.json" → "layouts").
    private static string EntryDir(ZipArchiveEntry entry)
    {
        var name = entry.FullName.Replace('\\', '/').TrimStart('/');
        var slash = name.LastIndexOf('/');
        return slash < 0 ? "" : name[..slash];
    }

    private static bool IsEntry(ZipArchiveEntry entry, string relativeName) =>
        EntryDir(entry) == "" &&
        string.Equals(Path.GetFileName(entry.FullName), relativeName, StringComparison.OrdinalIgnoreCase);

    // 경로 조작(defensive): zip 항목이 데이터 폴더 밖을 가리키면 복원하지 않습니다.
    // GetFullPath가 ".." 등을 실제로 해석한 뒤, root(분리자 포함) 접두어로 검증합니다.
    private string SafeTargetPath(string entryName)
    {
        var normalized = entryName.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrEmpty(normalized))
            throw new InvalidDataException($"백업 파일에 허용되지 않는 경로가 포함되어 있습니다: {entryName}");

        var root = Path.GetFullPath(_dataDir) + Path.DirectorySeparatorChar; // 접두어 오인 방지용 분리자
        var target = Path.GetFullPath(Path.Combine(root, normalized));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"백업 파일에 허용되지 않는 경로가 포함되어 있습니다: {entryName}");

        return target;
    }
}
