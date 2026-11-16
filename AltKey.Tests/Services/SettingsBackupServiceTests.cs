using System.IO.Compression;
using AltKey.Services;

namespace AltKey.Tests.Services;

// 사용자 설정 백업·복원·초기화 서비스(SettingsBackupService)의 파일 단위 동작을 검증하는 테스트입니다.
public class SettingsBackupServiceTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "AltKeyBackupTests", Guid.NewGuid().ToString("N"));
    private readonly string _outDir = Path.Combine(Path.GetTempPath(), "AltKeyBackupTests", Guid.NewGuid().ToString("N"));

    public SettingsBackupServiceTests()
    {
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_outDir);
    }

    public void Dispose()
    {
        foreach (var dir in new[] { _dataDir, _outDir })
        {
            if (Directory.Exists(dir))
                try { Directory.Delete(dir, recursive: true); } catch { /* 테스트 정리 실패는 무시 */ }
        }
    }

    // ── 공통 셋업 헬퍼 ────────────────────────────────────────────────────

    private void SeedUserData(string? extraConfig = null)
    {
        File.WriteAllText(Path.Combine(_dataDir, "config.json"), extraConfig ?? """{"Theme":"dark"}""");
        Directory.CreateDirectory(Path.Combine(_dataDir, "layouts"));
        // 내장 레이아웃(Basic/Basic Plus) + 커스텀 — 초기화·복원 시 내장은 보존되고 커스텀만 사라져야 합니다.
        File.WriteAllText(Path.Combine(_dataDir, "layouts", "Basic.json"), """{"Name":"Basic"}""");
        File.WriteAllText(Path.Combine(_dataDir, "layouts", "Basic Plus.json"), """{"Name":"Basic Plus"}""");
        File.WriteAllText(Path.Combine(_dataDir, "layouts", "custom.json"), """{"Name":"custom"}""");
        File.WriteAllText(Path.Combine(_dataDir, "user-words.ko.json"), """{"a":1}""");
        File.WriteAllText(Path.Combine(_dataDir, "user-bigrams.ko.json"), """{"b":2}""");
        File.WriteAllText(Path.Combine(_dataDir, "scratchpad.json"), """{"text":"메모"}""");
        File.WriteAllText(Path.Combine(_dataDir, "clipboard_history.json"), """{"history":["x"]}""");

        // 로컬 AI 엔진·모델 — 백업·초기화 대상이 아니어야 하는 파일들.
        Directory.CreateDirectory(Path.Combine(_dataDir, "engine"));
        File.WriteAllText(Path.Combine(_dataDir, "engine", "llama-server.exe"), "dummy-engine");
        Directory.CreateDirectory(Path.Combine(_dataDir, "models"));
        File.WriteAllText(Path.Combine(_dataDir, "models", "model.gguf"), "dummy-model");
    }

    private string BackupPath(string name = "backup.zip") => Path.Combine(_outDir, name);

    // ── CreateBackup ─────────────────────────────────────────────────────

    [Fact]
    public void CreateBackup_includes_settings_layouts_and_user_data()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);

        service.CreateBackup(BackupPath());

        using var zip = ZipFile.OpenRead(BackupPath());
        var names = new HashSet<string>();
        foreach (var entry in zip.Entries)
            names.Add(entry.FullName.Replace('\\', '/'));
        Assert.Contains("config.json", names);
        Assert.Contains("layouts/Basic.json", names);
        Assert.Contains("user-words.ko.json", names);
        Assert.Contains("user-bigrams.ko.json", names);
        Assert.Contains("scratchpad.json", names);
        Assert.Contains("clipboard_history.json", names);
    }

    [Fact]
    public void CreateBackup_excludes_local_ai_engine_and_models()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);

        service.CreateBackup(BackupPath());

        using var zip = ZipFile.OpenRead(BackupPath());
        var entries = zip.Entries.ToList();
        Assert.DoesNotContain(entries, e => e.FullName.StartsWith("engine/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e => e.FullName.StartsWith("models/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateBackup_overwrites_existing_backup_with_same_name()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);
        var path = BackupPath();

        service.CreateBackup(path);
        File.WriteAllText(Path.Combine(_dataDir, "config.json"), """{"Theme":"light"}""");
        service.CreateBackup(path); // 같은 이름으로 다시 백업

        using var zip = ZipFile.OpenRead(path);
        var configEntry = zip.GetEntry("config.json")!;
        using var reader = new StreamReader(configEntry.Open());
        Assert.Contains("\"light\"", reader.ReadToEnd());
    }

    // ── RestoreFromZip ───────────────────────────────────────────────────

    [Fact]
    public void RestoreFromZip_round_trips_file_contents()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);
        service.CreateBackup(BackupPath());

        // 백업 후 파일이 바뀌었다가 복원으로 돌아오는지를 확인합니다.
        File.WriteAllText(Path.Combine(_dataDir, "config.json"), """{"Theme":"light"}""");
        File.Delete(Path.Combine(_dataDir, "user-words.ko.json"));

        service.RestoreFromZip(BackupPath());

        Assert.Equal("""{"Theme":"dark"}""", File.ReadAllText(Path.Combine(_dataDir, "config.json")));
        Assert.Equal("""{"a":1}""", File.ReadAllText(Path.Combine(_dataDir, "user-words.ko.json")));
    }

    [Fact]
    public void Restore_removes_custom_layouts_not_in_backup_but_keeps_builtins()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);
        service.CreateBackup(BackupPath());

        // 백업 시점에 없던 커스텀 레이아웃 — 복원 후에는 남아선 안 됩니다.
        File.WriteAllText(Path.Combine(_dataDir, "layouts", "extra.json"), """{"Name":"extra"}""");

        service.RestoreFromZip(BackupPath());

        Assert.True(File.Exists(Path.Combine(_dataDir, "layouts", "Basic.json")));
        Assert.True(File.Exists(Path.Combine(_dataDir, "layouts", "Basic Plus.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "layouts", "extra.json")));
    }

    [Fact]
    public void RestoreFromZip_throws_for_zip_without_altkey_content()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);
        var path = BackupPath("other.zip");

        using var stream = new FileStream(path, FileMode.Create);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            zip.CreateEntryFromFile(Path.Combine(_dataDir, "config.json"), "readme.txt");

        Assert.Throws<InvalidDataException>(() => service.RestoreFromZip(path));
    }

    // ── ValidateBackup ───────────────────────────────────────────────────

    [Fact]
    public void ValidateBackup_accepts_created_backup()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);
        service.CreateBackup(BackupPath());

        // 예외가 발생하면 테스트가 실패합니다.
        SettingsBackupService.ValidateBackup(BackupPath());
    }

    [Fact]
    public void ValidateBackup_rejects_missing_file()
    {
        Assert.Throws<InvalidDataException>(
            () => SettingsBackupService.ValidateBackup(BackupPath("nope.zip")));
    }

    [Fact]
    public void ValidateBackup_rejects_plain_text_file()
    {
        var path = BackupPath("text.txt");
        File.WriteAllText(path, "hello");

        Assert.Throws<InvalidDataException>(() => SettingsBackupService.ValidateBackup(path));
    }

    [Fact]
    public void ValidateBackup_rejects_corrupt_zip()
    {
        var path = BackupPath("corrupt.zip");
        File.WriteAllBytes(path, [0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03]); // PK 헤더만 있는 깨진 zip

        Assert.Throws<InvalidDataException>(() => SettingsBackupService.ValidateBackup(path));
    }

    [Fact]
    public void RestoreFromZip_rejects_zip_with_unsafe_entry_path()
    {
        // 검증은 통과하지만(정상 항목 포함), 압축된 경로가 데이터 폴더를 벗어날 때 복원을 거부해야 합니다.
        var path = BackupPath("unsafe.zip");
        File.WriteAllText(Path.Combine(_dataDir, "config.json"), """{"Theme":"dark"}""");
        using var stream = new FileStream(path, FileMode.Create);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(Path.Combine(_dataDir, "config.json"), "config.json");
            zip.CreateEntry("../evil.txt");
        }

        Assert.Throws<InvalidDataException>(() => new SettingsBackupService(_dataDir).RestoreFromZip(path));
    }

    // ── ResetUserSettings ────────────────────────────────────────────────

    [Fact]
    public void ResetUserSettings_deletes_user_data_but_keeps_engine_and_models()
    {
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);

        service.ResetUserSettings();

        // config.json은 파일을 지우지 않습니다 — 포터블 모드 마커라 내용만 기본값으로 덮어씀(설정 화면에서).
        Assert.True(File.Exists(Path.Combine(_dataDir, "config.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "user-words.ko.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "user-bigrams.ko.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "scratchpad.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "clipboard_history.json")));

        // 로컬 AI 엔진·모델은 유지되어야 합니다.
        Assert.True(File.Exists(Path.Combine(_dataDir, "engine", "llama-server.exe")));
        Assert.True(File.Exists(Path.Combine(_dataDir, "models", "model.gguf")));
    }

    [Fact]
    public void ResetUserSettings_keeps_builtin_layouts_and_removes_custom()
    {
        // 포터블 모드에서는 layouts 폴더가 exe 옆 내장 레이아웃 시드 소스 그 자체 — 초기화가 내장을 지우면 안 됩니다.
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);

        service.ResetUserSettings();

        Assert.True(File.Exists(Path.Combine(_dataDir, "layouts", "Basic.json")));
        Assert.True(File.Exists(Path.Combine(_dataDir, "layouts", "Basic Plus.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "layouts", "custom.json")));
    }

    [Fact]
    public void RestoreFromZip_keeps_builtin_layouts_when_backup_lacks_them()
    {
        // 백업에 내장 레이아웃이 없어도(예: 오래된/손상된 백업) 디스크의 내장 파일을 지우지 않아야 합니다.
        SeedUserData();
        var service = new SettingsBackupService(_dataDir);

        using (var stream = new FileStream(BackupPath("no-layouts.zip"), FileMode.Create))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            zip.CreateEntryFromFile(Path.Combine(_dataDir, "config.json"), "config.json");

        service.RestoreFromZip(BackupPath("no-layouts.zip"));

        Assert.True(File.Exists(Path.Combine(_dataDir, "layouts", "Basic.json")));
        Assert.True(File.Exists(Path.Combine(_dataDir, "layouts", "Basic Plus.json")));
        Assert.False(File.Exists(Path.Combine(_dataDir, "layouts", "custom.json")));
    }

    [Fact]
    public void ResetUserSettings_is_safe_when_data_dir_is_empty()
    {
        var service = new SettingsBackupService(_dataDir);

        // 예외가 발생하면 테스트가 실패합니다.
        service.ResetUserSettings();
    }
}
