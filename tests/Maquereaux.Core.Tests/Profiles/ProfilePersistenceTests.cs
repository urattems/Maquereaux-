using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Profiles;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Profiles;

/// <summary>Dossier temporaire supprimé à la fin du test.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir() => Directory.CreateDirectory(Path);
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "maquereaux-tests-" + Guid.NewGuid().ToString("N"));
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
}

public class JsonFileStoreTests : IDisposable
{
    private readonly TempDir _dir = new();
    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Missing_file_returns_default()
    {
        var r = JsonFileStore.Load(_dir.File("nope.json"), () => new AppSettings());
        Assert.Equal(LoadStatus.Missing, r.Status);
        Assert.NotNull(r.Value);
    }

    [Fact]
    public void Save_then_load_roundtrip()
    {
        var path = _dir.File("s.json");
        var s = new AppSettings { AlwaysOnTop = true, TimerStrategy = TimerStrategy.Hybrid, EmergencyStopHotkey = new Hotkey(HotkeyModifiers.Shift, VirtualKeys.F8) };
        JsonFileStore.SaveAtomic(path, s);
        var r = JsonFileStore.Load(path, () => new AppSettings());
        Assert.Equal(LoadStatus.Loaded, r.Status);
        Assert.True(r.Value.AlwaysOnTop);
        Assert.Equal(TimerStrategy.Hybrid, r.Value.TimerStrategy);
        Assert.Equal(new Hotkey(HotkeyModifiers.Shift, VirtualKeys.F8), r.Value.EmergencyStopHotkey);
    }

    [Fact]
    public void Enums_are_written_as_strings_and_computed_props_are_skipped()
    {
        var json = JsonFileStore.Serialize(TestProfiles.Aze(SequenceMode.Burst));
        Assert.Contains("\"Burst\"", json);
        Assert.DoesNotContain("displayName", json);
        Assert.DoesNotContain("isValid", json);
    }

    [Theory]
    [InlineData("{ ceci n'est pas du json")]
    [InlineData("null")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"timerStrategy\": 42.5}")]
    public void Corrupted_file_gives_default_and_backup_without_crash(string content)
    {
        var path = _dir.File("settings.json");
        File.WriteAllText(path, content);
        var r = JsonFileStore.Load(path, () => new AppSettings());
        Assert.Equal(LoadStatus.Corrupted, r.Status);
        Assert.NotNull(r.Value);
        Assert.NotNull(r.BackupPath);
        Assert.True(File.Exists(r.BackupPath));
        Assert.Equal(content, File.ReadAllText(r.BackupPath!));
        Assert.False(string.IsNullOrEmpty(r.Error));
    }

    [Fact]
    public void Atomic_save_leaves_no_temp_file_and_overwrites()
    {
        var path = _dir.File("p.json");
        JsonFileStore.SaveAtomic(path, new AppSettings { AlwaysOnTop = false });
        JsonFileStore.SaveAtomic(path, new AppSettings { AlwaysOnTop = true });
        Assert.Single(Directory.GetFiles(_dir.Path));
        Assert.True(JsonFileStore.Load(path, () => new AppSettings()).Value.AlwaysOnTop);
    }

    [Fact]
    public void Save_creates_missing_directory()
    {
        var path = Path.Combine(_dir.Path, "sub", "dir", "x.json");
        JsonFileStore.SaveAtomic(path, new AppSettings());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated()
    {
        var path = _dir.File("c.json");
        File.WriteAllText(path, "{ // réglage édité à la main\n \"alwaysOnTop\": true, }");
        Assert.True(JsonFileStore.Load(path, () => new AppSettings()).Value.AlwaysOnTop);
    }
}

public class ProfileRepositoryTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly ProfileRepository _repo;

    public ProfileRepositoryTests() => _repo = new ProfileRepository(new AppPaths(_dir.Path));
    public void Dispose() => _dir.Dispose();

    private static Profile Rich()
    {
        var p = TestProfiles.Aze(SequenceMode.Burst, repeat: 5, interval: 33, hold: 2);
        p.Name = "Rotation DPS";
        p.ActivationMode = ActivationMode.Hold;
        p.Hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, VirtualKeys.F7);
        p.TargetProcess = "notepad.exe";
        p.Steps[1].DelayAfterMs = 50;
        p.Steps[2] = new MacroStep(new KeyStroke(VirtualKeys.Left, 0x4B, true, "←"), 3);
        return p;
    }

    [Fact]
    public void Profiles_roundtrip_preserves_everything()
    {
        var original = Rich();
        _repo.SaveProfiles(new[] { original, Profile.CreateDefault() });
        var loaded = _repo.LoadProfiles();
        Assert.Equal(2, loaded.Count);
        var p = loaded[0];
        Assert.Equal(original.Id, p.Id);
        Assert.Equal("Rotation DPS", p.Name);
        Assert.Equal(SequenceMode.Burst, p.SequenceMode);
        Assert.Equal(ActivationMode.Hold, p.ActivationMode);
        Assert.Equal(5, p.RepeatCount);
        Assert.Equal(33, p.IntervalMs);
        Assert.Equal(original.Hotkey, p.Hotkey);
        Assert.Equal("notepad.exe", p.TargetProcess);
        Assert.Equal(50, p.Steps[1].DelayAfterMs);
        Assert.Null(p.Steps[0].DelayAfterMs);
        Assert.Equal(new KeyStroke(VirtualKeys.Left, 0x4B, true, "←"), p.Steps[2].Key);
        Assert.Equal(3, p.Steps[2].HoldMs);
        Assert.Empty(_repo.Warnings);
    }

    [Fact]
    public void Missing_profiles_file_gives_default_profile()
    {
        var loaded = _repo.LoadProfiles();
        Assert.Single(loaded);
        Assert.Equal("Profil 1", loaded[0].Name);
    }

    [Fact]
    public void Corrupted_profiles_file_backup_and_default_with_warning()
    {
        File.WriteAllText(_dir.File("profiles.json"), "{\"profiles\": [ {\"name\": ");
        var loaded = _repo.LoadProfiles();
        Assert.Single(loaded);
        Assert.Single(_repo.Warnings);
        Assert.Contains("illisible", _repo.Warnings[0]);
        Assert.Single(Directory.GetFiles(_dir.Path, "profiles.json.corrupt-*.bak"));
    }

    [Fact]
    public void Settings_roundtrip_and_corruption()
    {
        var s = new AppSettings { ActiveProfileId = Guid.NewGuid(), TimerStrategy = TimerStrategy.Standard };
        _repo.SaveSettings(s);
        var loaded = _repo.LoadSettings();
        Assert.Equal(s.ActiveProfileId, loaded.ActiveProfileId);
        Assert.Equal(TimerStrategy.Standard, loaded.TimerStrategy);

        File.WriteAllText(_dir.File("settings.json"), "garbage");
        var fallback = _repo.LoadSettings();
        Assert.Equal(Hotkey.DefaultEmergencyStop, fallback.EmergencyStopHotkey);
        Assert.Contains(_repo.Warnings, w => w.Contains("settings.json"));
    }

    [Fact]
    public void Ui_settings_roundtrip_and_clamping()
    {
        var fresh = new AppSettings();
        Assert.False(fresh.SidebarCollapsed);
        Assert.False(fresh.OnboardingCompleted);
        Assert.Equal(0.95, fresh.CompactOpacity);

        _repo.SaveSettings(new AppSettings { SidebarCollapsed = true, OnboardingCompleted = true, CompactOpacity = 0.5 });
        var s = _repo.LoadSettings();
        Assert.True(s.SidebarCollapsed);
        Assert.True(s.OnboardingCompleted);
        Assert.Equal(0.5, s.CompactOpacity);

        File.WriteAllText(_dir.File("settings.json"), "{\"compactOpacity\": 0.01}");
        Assert.Equal(AppSettings.MinCompactOpacity, _repo.LoadSettings().CompactOpacity);
        File.WriteAllText(_dir.File("settings.json"), "{\"compactOpacity\": 7}");
        Assert.Equal(1, _repo.LoadSettings().CompactOpacity);
    }

    [Fact]
    public void Legacy_emergency_hotkey_is_migrated_to_F9_once()
    {
        // Réglages v1.x : Ctrl+Alt+Pause (ancien défaut) → F9
        File.WriteAllText(_dir.File("settings.json"), "{\"emergencyStopHotkey\": {\"modifiers\": \"Alt, Control\", \"virtualKey\": 19}}");
        var s = _repo.LoadSettings();
        Assert.Equal(Hotkey.DefaultEmergencyStop, s.EmergencyStopHotkey);
        Assert.Equal(AppSettings.CurrentVersion, s.SettingsVersion);

        // Choisi à la main après la migration : conservé
        s.EmergencyStopHotkey = Hotkey.LegacyEmergencyStop;
        _repo.SaveSettings(s);
        Assert.Equal(Hotkey.LegacyEmergencyStop, _repo.LoadSettings().EmergencyStopHotkey);

        // Un autre raccourci personnalisé d'avant la v1.6 n'est pas touché
        File.WriteAllText(_dir.File("settings.json"), "{\"emergencyStopHotkey\": {\"modifiers\": \"Control\", \"virtualKey\": 123}}");
        Assert.Equal(new Hotkey(HotkeyModifiers.Control, 0x7B), _repo.LoadSettings().EmergencyStopHotkey);
    }

    [Fact]
    public void Mouse_button_is_refused_as_emergency_hotkey()
    {
        File.WriteAllText(_dir.File("settings.json"), "{\"emergencyStopHotkey\": {\"modifiers\": \"None\", \"virtualKey\": 2}, \"settingsVersion\": 2}");
        Assert.Equal(Hotkey.DefaultEmergencyStop, _repo.LoadSettings().EmergencyStopHotkey);
    }

    [Fact]
    public void Settings_with_invalid_emergency_hotkey_is_repaired()
    {
        File.WriteAllText(_dir.File("settings.json"), "{\"emergencyStopHotkey\": {\"modifiers\": \"None\", \"virtualKey\": 0}, \"timerStrategy\": \"Hybrid\"}");
        var s = _repo.LoadSettings();
        Assert.Equal(Hotkey.DefaultEmergencyStop, s.EmergencyStopHotkey);
        Assert.Equal(TimerStrategy.Hybrid, s.TimerStrategy);
    }

    [Fact]
    public void Export_then_import_regenerates_conflicting_ids()
    {
        var p = Rich();
        var path = _dir.File("export.json");
        ProfileRepository.Export(path, new[] { p });
        var imported = ProfileRepository.Import(path, new[] { p.Id });
        var single = Assert.Single(imported);
        Assert.NotEqual(p.Id, single.Id); // l'import n'écrase jamais un profil existant
        Assert.Equal(p.Name, single.Name);

        var fresh = ProfileRepository.Import(path, Array.Empty<Guid>());
        Assert.Equal(p.Id, fresh[0].Id);
    }

    [Fact]
    public void Import_accepts_a_single_profile_object()
    {
        var path = _dir.File("one.json");
        File.WriteAllText(path, JsonFileStore.Serialize(Rich()));
        var imported = ProfileRepository.Import(path, Array.Empty<Guid>());
        Assert.Equal("Rotation DPS", Assert.Single(imported).Name);
    }

    [Fact]
    public void Import_sanitizes_values()
    {
        var path = _dir.File("weird.json");
        File.WriteAllText(path, "{\"profiles\":[{\"name\":\"  \",\"intervalMs\":-5,\"repeatCount\":-1,\"steps\":[{\"key\":{\"virtualKey\":65},\"holdMs\":999999},{\"key\":null}]}]}");
        var p = Assert.Single(ProfileRepository.Import(path, Array.Empty<Guid>()));
        Assert.Equal("Sans nom", p.Name);
        Assert.Equal(1, p.IntervalMs);
        Assert.Equal(0, p.RepeatCount);
        Assert.Single(p.Steps);
        Assert.Equal(ProfileLimits.MaxHoldMs, p.Steps[0].HoldMs);
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("{\"profiles\": []}")]
    [InlineData("null")]
    public void Import_invalid_file_throws_InvalidDataException(string content)
    {
        var path = _dir.File("bad.json");
        File.WriteAllText(path, content);
        Assert.Throws<InvalidDataException>(() => ProfileRepository.Import(path, Array.Empty<Guid>()));
    }

    [Fact]
    public void Import_missing_file_throws_InvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => ProfileRepository.Import(_dir.File("absent.json"), Array.Empty<Guid>()));
    }

    [Fact]
    public void AppPaths_default_honours_env_var()
    {
        var old = Environment.GetEnvironmentVariable(AppPaths.DataDirEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirEnvVar, _dir.Path);
            var paths = AppPaths.Default();
            Assert.Equal(_dir.Path, paths.DataDir);
            paths.EnsureCreated();
            Assert.True(Directory.Exists(paths.LogsDir));
            Assert.EndsWith("profiles.json", paths.ProfilesFile);
            Assert.EndsWith("settings.json", paths.SettingsFile);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirEnvVar, old);
        }
        Assert.Contains(AppInfo.Name, AppPaths.Default().DataDir);
    }
}

public class ProfileValidatorTests
{
    private readonly AppSettings _settings = new();

    [Fact]
    public void Mouse_hotkey_requires_hold_mode_and_suggests_a_target()
    {
        var p = Profile.CreateDefault();
        p.Hotkey = new Hotkey(HotkeyModifiers.None, VirtualKeys.RButton);
        p.ActivationMode = ActivationMode.Toggle;
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("Maintenir"));

        p.ActivationMode = ActivationMode.Hold;
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("fenêtre cible"));

        p.TargetProcess = "jeu.exe";
        Assert.Empty(ProfileValidator.Validate(p, new[] { p }, _settings));
    }

    [Fact]
    public void SanitizeAll_regenerates_duplicate_ids_and_skips_nulls()
    {
        var a = TestProfiles.Aze();
        var b = TestProfiles.Aze();
        b.Id = a.Id;
        var list = ProfileValidator.SanitizeAll(new Profile?[] { a, null, b });
        Assert.Equal(2, list.Count);
        Assert.NotEqual(list[0].Id, list[1].Id);
    }

    [Fact]
    public void SanitizeAll_empty_gives_default()
    {
        Assert.Single(ProfileValidator.SanitizeAll(null));
        Assert.Single(ProfileValidator.SanitizeAll(Array.Empty<Profile>()));
    }

    [Fact]
    public void Sanitize_fixes_everything()
    {
        var p = new Profile
        {
            Id = Guid.Empty,
            Name = new string('x', 200),
            IntervalMs = 0,
            RepeatCount = -3,
            SequenceMode = (SequenceMode)99,
            ActivationMode = (ActivationMode)99,
            Steps = null!,
            Hotkey = new Hotkey(HotkeyModifiers.None, VirtualKeys.LShift),
            TargetProcess = "  ",
        };
        ProfileValidator.Sanitize(p);
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal(ProfileLimits.MaxNameLength, p.Name.Length);
        Assert.Equal(1, p.IntervalMs);
        Assert.Equal(0, p.RepeatCount);
        Assert.Equal(SequenceMode.Cycle, p.SequenceMode);
        Assert.Equal(ActivationMode.Toggle, p.ActivationMode);
        Assert.Empty(p.Steps);
        Assert.Null(p.Hotkey);
        Assert.Null(p.TargetProcess);
    }

    [Fact]
    public void Sanitize_truncates_steps_and_clamps_delays()
    {
        var p = TestProfiles.Aze();
        for (int i = 0; i < 100; i++) p.Steps.Add(new MacroStep(TestProfiles.A, -1, -10));
        ProfileValidator.Sanitize(p);
        Assert.Equal(ProfileLimits.MaxSteps, p.Steps.Count);
        Assert.Equal(0, p.Steps[^1].HoldMs);
        Assert.Equal(1, p.Steps[^1].DelayAfterMs);
    }

    [Theory]
    [InlineData("notepad", "notepad.exe")]
    [InlineData("Notepad.EXE", "notepad.exe")]
    [InlineData(@"C:\Windows\System32\notepad.exe", "notepad.exe")]
    [InlineData("\"game.exe\"", "game.exe")]
    [InlineData("/usr/bin/x", "x.exe")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void NormalizeProcessName(string? input, string? expected)
    {
        Assert.Equal(expected, ProfileValidator.NormalizeProcessName(input));
    }

    [Fact]
    public void Valid_profile_has_no_issues()
    {
        var p = TestProfiles.Aze();
        Assert.Empty(ProfileValidator.Validate(p, new[] { p }, _settings));
    }

    [Fact]
    public void Detects_empty_sequence()
    {
        var p = new Profile();
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("vide"));
    }

    [Fact]
    public void Detects_hotkey_equal_to_emergency()
    {
        var p = TestProfiles.Aze();
        p.Hotkey = Hotkey.DefaultEmergencyStop;
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("urgence"));
    }

    [Fact]
    public void Detects_duplicate_hotkeys_between_profiles()
    {
        var a = TestProfiles.Aze(); a.Name = "A";
        var b = TestProfiles.Aze(); b.Name = "B";
        Assert.Contains(ProfileValidator.Validate(a, new[] { a, b }, _settings), i => i.Contains("« B »"));
    }

    [Fact]
    public void Hold_mode_hotkey_cannot_be_in_sequence()
    {
        var p = TestProfiles.Aze();
        p.ActivationMode = ActivationMode.Hold;
        p.Hotkey = new Hotkey(HotkeyModifiers.None, VirtualKeys.Z);
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("ne peut pas faire partie"));
    }

    [Fact]
    public void Hold_mode_requires_hotkey()
    {
        var p = TestProfiles.Aze();
        p.ActivationMode = ActivationMode.Hold;
        p.Hotkey = null;
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("besoin d'un raccourci"));
    }

    [Fact]
    public void Burst_with_too_long_holds_warns()
    {
        var p = TestProfiles.Aze(SequenceMode.Burst, interval: 10, hold: 4);
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("Burst"));
    }

    [Fact]
    public void Cycle_hold_longer_than_delay_warns()
    {
        var p = TestProfiles.Aze(interval: 10, hold: 10);
        Assert.Contains(ProfileValidator.Validate(p, new[] { p }, _settings), i => i.Contains("dure plus longtemps"));
    }
}

public class ProfileRegistryTests
{
    [Fact]
    public void Publish_find_remove_are_copies()
    {
        var reg = new ProfileRegistry();
        var p = TestProfiles.Aze();
        reg.Publish(p);
        p.Name = "modifié après publication";
        var found = reg.Find(p.Id)!;
        Assert.Equal("AZE", found.Name);
        found.Name = "modifié par le lecteur";
        Assert.Equal("AZE", reg.Find(p.Id)!.Name);
        Assert.Equal(1, reg.Count);
        Assert.Single(reg.All);
        reg.Remove(p.Id);
        Assert.Null(reg.Find(p.Id));
        reg.Publish(p);
        reg.Clear();
        Assert.Equal(0, reg.Count);
    }
}
