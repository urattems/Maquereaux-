using System.Globalization;
using System.Text;
using Maquereaux.Core.Input;
using Maquereaux.Core.Lab;
using Maquereaux.Core.Tests.Profiles;

namespace Maquereaux.Core.Tests.Lab;

public class LabRecorderTests
{
    private static RawKeyEvent Ev(double t, ushort vk, bool down, bool injected = false, bool ours = false) =>
        new(t, vk, 0x1E, down, injected, ours);

    [Fact]
    public void Nothing_is_recorded_outside_a_session()
    {
        var r = new LabRecorder();
        r.OnKeyEvent(Ev(1, VirtualKeys.A, true));
        Assert.Equal(LabRecorderState.Idle, r.State);
        Assert.Equal(0, r.EventCount);
        Assert.Null(r.Stop(10));
        Assert.Equal(0, r.Elapsed(5));
    }

    [Fact]
    public void Records_relative_times_and_sources()
    {
        var r = new LabRecorder();
        r.Start(1000, "S");
        r.OnKeyEvent(Ev(1010, VirtualKeys.A, true));
        r.OnKeyEvent(Ev(1015, VirtualKeys.A, false));
        r.OnKeyEvent(Ev(1020, VirtualKeys.Z, true, injected: true, ours: true));
        r.OnKeyEvent(Ev(1030, VirtualKeys.E, true, injected: true, ours: false));
        Assert.Equal(4, r.EventCount);
        Assert.Equal(30, r.Elapsed(1030));
        var s = r.Stop(1100)!;
        Assert.Equal(100, s.DurationMs);
        Assert.Equal("S", s.Name);
        Assert.Equal(new[] { 10.0, 15, 20, 30 }, s.Events.Select(e => e.TimeMs));
        Assert.Equal(new[] { InputSource.Physical, InputSource.Physical, InputSource.Maquereaux, InputSource.OtherInjected }, s.Events.Select(e => e.Source));
        Assert.Equal(LabRecorderState.Idle, r.State);
    }

    [Fact]
    public void Autorepeat_is_flagged()
    {
        var r = new LabRecorder();
        r.Start(0, "S");
        r.OnKeyEvent(Ev(1, VirtualKeys.A, true));
        r.OnKeyEvent(Ev(500, VirtualKeys.A, true)); // répétition auto du clavier
        r.OnKeyEvent(Ev(530, VirtualKeys.A, true));
        r.OnKeyEvent(Ev(540, VirtualKeys.A, false));
        r.OnKeyEvent(Ev(600, VirtualKeys.A, true)); // nouvel appui
        var s = r.Stop(700)!;
        Assert.Equal(new[] { false, true, true, false, false }, s.Events.Select(e => e.IsRepeat));
    }

    [Fact]
    public void Capture_stops_at_end_of_fixed_duration()
    {
        var r = new LabRecorder();
        LabSession? finished = null;
        r.Finished += s => finished = s;
        r.Start(0, "20s", durationMs: 20_000);
        r.OnKeyEvent(Ev(19_999, VirtualKeys.A, true));
        r.Tick(19_999.5);
        Assert.Null(finished);
        r.Tick(20_000);
        Assert.NotNull(finished);
        Assert.Equal(20_000, finished!.DurationMs);
        Assert.Equal(LabRecorderState.Idle, r.State);
        r.OnKeyEvent(Ev(20_001, VirtualKeys.A, true)); // après la fin : ignoré
        Assert.Single(finished.Events);
    }

    [Fact]
    public void Event_after_duration_ends_session_even_without_tick()
    {
        var r = new LabRecorder();
        LabSession? finished = null;
        r.Finished += s => finished = s;
        r.Start(0, "S", 100);
        r.OnKeyEvent(Ev(50, VirtualKeys.A, true));
        r.OnKeyEvent(Ev(150, VirtualKeys.A, false)); // au-delà → fin, non enregistré
        Assert.NotNull(finished);
        Assert.Single(finished!.Events);
        Assert.Equal(100, finished.DurationMs);
    }

    [Fact]
    public void Manual_stop_before_duration_and_elapsed_capped()
    {
        var r = new LabRecorder();
        r.Start(0, "S", 1000);
        Assert.Equal(1000, r.Elapsed(5000));
        var s = r.Stop(400)!;
        Assert.Equal(400, s.DurationMs);
        r.Start(0, "S2", 1000);
        Assert.Equal(1000, r.Stop(9999)!.DurationMs);
    }

    [Fact]
    public void Events_before_start_are_ignored()
    {
        var r = new LabRecorder();
        r.Start(1000, "S");
        r.OnKeyEvent(Ev(999, VirtualKeys.A, true));
        Assert.Equal(0, r.EventCount);
    }

    [Fact]
    public void Double_start_and_invalid_duration_throw()
    {
        var r = new LabRecorder();
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Start(0, "S", 0));
        r.Start(0, "S");
        Assert.Throws<InvalidOperationException>(() => r.Start(1, "S2"));
    }
}

public class SessionAnalyzerTests
{
    /// <summary>Session synthétique : appuis physiques toutes les 100 ms (appui 30 ms) + Maquereaux toutes les 10 ms.</summary>
    private static LabSession Build()
    {
        var s = new LabSession { Name = "Test", DurationMs = 1000 };
        for (int i = 0; i < 10; i++)
        {
            s.Events.Add(new CapturedKey(i * 100.0, VirtualKeys.A, 0x10, true, InputSource.Physical, false));
            s.Events.Add(new CapturedKey(i * 100.0 + 30, VirtualKeys.A, 0x10, false, InputSource.Physical, false));
        }
        for (int i = 0; i < 100; i++)
        {
            s.Events.Add(new CapturedKey(i * 10.0 + 5, VirtualKeys.F24, 0x76, true, InputSource.Maquereaux, false));
            s.Events.Add(new CapturedKey(i * 10.0 + 5, VirtualKeys.F24, 0x76, false, InputSource.Maquereaux, false));
        }
        s.Events.Add(new CapturedKey(50, VirtualKeys.A, 0x10, true, InputSource.Physical, true)); // répétition : à ignorer
        return s;
    }

    [Fact]
    public void Stats_per_source()
    {
        var r = SessionAnalyzer.Analyze(Build());
        var phys = r.For(InputSource.Physical)!;
        Assert.Equal(10, phys.Inputs);
        Assert.Equal(10, phys.InputsPerSecond, 6);
        Assert.Equal(9, phys.Intervals.Count);
        Assert.Equal(100, phys.Intervals.Mean, 6);
        Assert.Equal(0, phys.Intervals.StdDev, 6);
        Assert.Equal(30, phys.PressDurations.Mean, 6);

        var ours = r.For(InputSource.Maquereaux)!;
        Assert.Equal(100, ours.Inputs);
        Assert.Equal(100, ours.InputsPerSecond, 6);
        Assert.Equal(10, ours.Intervals.Mean, 6);
        Assert.Equal(10, ours.Intervals.P95, 6);
        Assert.Equal(0, ours.PressDurations.Mean, 6);

        Assert.Null(r.For(InputSource.OtherInjected));
        Assert.Equal(InputSource.Maquereaux, r.DominantSource);
    }

    [Fact]
    public void Totals_keys_and_series()
    {
        var r = SessionAnalyzer.Analyze(Build());
        Assert.Equal(110, r.TotalInputs);
        Assert.Equal(110, r.InputsPerSecond, 6);
        Assert.Equal(221, r.TotalEvents);
        Assert.Equal(1, r.Repeats);
        Assert.Equal(1, r.DurationSeconds);
        Assert.Equal(("F24", 100), r.ByKey[0]);
        Assert.Equal(("A", 10), r.ByKey[1]);
        Assert.Equal(9 + 99, r.Series.Count);
        Assert.Equal(r.Series.OrderBy(p => p.TimeMs), r.Series);
        Assert.Null(r.All.Source);
    }

    [Fact]
    public void Empty_session_is_safe()
    {
        var r = SessionAnalyzer.Analyze(new LabSession());
        Assert.Equal(0, r.TotalInputs);
        Assert.Equal(0, r.InputsPerSecond);
        Assert.Empty(r.BySource);
        Assert.Null(r.DominantSource);
        Assert.True(r.All.Intervals.IsEmpty);
    }

    [Fact]
    public void Unsorted_events_are_sorted_first()
    {
        var s = new LabSession { DurationMs = 100 };
        s.Events.Add(new CapturedKey(50, VirtualKeys.A, 0, true, InputSource.Physical, false));
        s.Events.Add(new CapturedKey(10, VirtualKeys.A, 0, true, InputSource.Physical, false));
        Assert.Equal(40, SessionAnalyzer.Analyze(s).All.Intervals.Mean, 6);
    }

    [Theory]
    [InlineData(InputSource.Physical, "Physique (toi)")]
    [InlineData(InputSource.Maquereaux, "Maquereaux")]
    [InlineData(InputSource.OtherInjected, "Autre logiciel")]
    [InlineData(null, "Toutes sources")]
    public void Describe_sources(InputSource? s, string expected) => Assert.Equal(expected, SessionAnalyzer.Describe(s));

    [Fact]
    public void Comparison_rows()
    {
        var manual = new LabSession { DurationMs = 1000 };
        for (int i = 0; i < 8; i++) manual.Events.Add(new CapturedKey(i * 125.0, VirtualKeys.A, 0, true, InputSource.Physical, false));
        var auto = new LabSession { DurationMs = 1000 };
        for (int i = 0; i < 100; i++) auto.Events.Add(new CapturedKey(i * 10.0, VirtualKeys.A, 0, true, InputSource.Maquereaux, false));

        var rows = SessionComparison.Compare(SessionAnalyzer.Analyze(manual), SessionAnalyzer.Analyze(auto));
        var rate = rows.Single(r => r.Metric == "Inputs / seconde");
        Assert.Equal(8, rate.A, 6);
        Assert.Equal(100, rate.B, 6);
        Assert.Equal(92, rate.Delta, 6);
        Assert.Equal(1150, rate.DeltaPercent, 6);
        Assert.True(rate.BIsBetter);
        var mean = rows.Single(r => r.Metric == "Intervalle moyen");
        Assert.True(mean.BIsBetter); // plus bas = mieux
        var dur = rows.Single(r => r.Metric == "Durée");
        Assert.Null(dur.BIsBetter);
        Assert.Contains("+", rate.DeltaText);
        Assert.False(string.IsNullOrEmpty(rate.AText + rate.BText));
        Assert.Throws<ArgumentNullException>(() => SessionComparison.Compare(null!, SessionAnalyzer.Analyze(auto)));
    }

    [Fact]
    public void Comparison_row_with_zero_baseline()
    {
        var row = new ComparisonRow("x", 0, 5, "ms", true);
        Assert.True(double.IsNaN(row.DeltaPercent));
        Assert.False(row.BIsBetter);
        Assert.Equal("—", row.Format(double.NaN));
        Assert.DoesNotContain("%", row.DeltaText);
    }
}

public class CsvExporterTests : IDisposable
{
    private readonly TempDir _dir = new();
    public void Dispose() => _dir.Dispose();

    private static LabSession Sample()
    {
        var s = new LabSession { Name = "csv", DurationMs = 100 };
        s.Events.Add(new CapturedKey(0.5, VirtualKeys.A, 0x10, true, InputSource.Physical, false));
        s.Events.Add(new CapturedKey(20.25, VirtualKeys.A, 0x10, false, InputSource.Physical, false));
        s.Events.Add(new CapturedKey(10, VirtualKeys.Escape, 0x01, true, InputSource.Maquereaux, false));
        s.Events.Add(new CapturedKey(30.125, VirtualKeys.A, 0x10, true, InputSource.Physical, false));
        return s;
    }

    [Fact]
    public void French_culture_uses_semicolon_and_decimal_comma()
    {
        var csv = CsvExporter.ToCsv(Sample(), new CultureInfo("fr-FR"));
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("index;time_ms;event;key;vk;scan;source;repeat;interval_ms", lines[0]);
        Assert.Equal(5, lines.Length);
        Assert.Equal("1;0,5;down;A;65;16;Physical;0;", lines[1]);
        Assert.Equal("2;10;down;Échap;27;1;Maquereaux;0;", lines[2]); // trié par temps
        Assert.Equal("3;20,25;up;A;65;16;Physical;0;", lines[3]);
        Assert.Equal("4;30,125;down;A;65;16;Physical;0;29,625", lines[4]); // intervalle depuis le précédent appui physique
    }

    [Fact]
    public void Invariant_culture_uses_comma_and_dot()
    {
        var csv = CsvExporter.ToCsv(Sample(), CultureInfo.InvariantCulture);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(string.Join(",", CsvExporter.Headers), lines[0]);
        Assert.Equal("4,30.125,down,A,65,16,Physical,0,29.625", lines[4]);
    }

    [Fact]
    public void Separator_equal_to_decimal_falls_back_to_semicolon()
    {
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.NumberFormat.NumberDecimalSeparator = ",";
        var csv = CsvExporter.ToCsv(Sample(), c);
        Assert.StartsWith("index;time_ms", csv);
    }

    [Fact]
    public void Values_containing_separator_are_quoted()
    {
        var s = new LabSession();
        s.Events.Add(new CapturedKey(1, 0xBC, 0, true, InputSource.Physical, false)); // VK_OEM_COMMA → nom "VK 0xBC"
        var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        c.TextInfo.ListSeparator = " ";
        var csv = CsvExporter.ToCsv(s, c);
        Assert.Contains("\"VK 0xBC\"", csv);
    }

    [Fact]
    public void File_has_utf8_bom_and_repeat_flag()
    {
        var s = Sample();
        s.Events.Add(new CapturedKey(40, VirtualKeys.A, 0x10, true, InputSource.Physical, true));
        var path = _dir.File("s.csv");
        CsvExporter.Export(s, path, new CultureInfo("fr-FR"));
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("Échap", text);
        Assert.EndsWith("5;40;down;A;65;16;Physical;1;\r\n", text);
    }

    [Fact]
    public void Session_save_and_load_roundtrip()
    {
        var s = Sample();
        s.Note = "mannequin, 20 s → 12 340 dégâts";
        var path = _dir.File("a" + LabSessionStore.Extension);
        LabSessionStore.Save(path, s);
        var loaded = LabSessionStore.Load(path);
        Assert.Equal(s.Id, loaded.Id);
        Assert.Equal(s.Note, loaded.Note);
        Assert.Equal(s.Events, loaded.Events);
        Assert.Equal(100, loaded.DurationMs);
    }

    [Fact]
    public void Session_load_repairs_missing_fields_and_rejects_garbage()
    {
        var path = _dir.File("b.json");
        File.WriteAllText(path, "{\"session\":{\"name\":\"\",\"durationMs\":0,\"events\":[{\"timeMs\":42,\"virtualKey\":65,\"isDown\":true,\"source\":\"Physical\"}]}}");
        var s = LabSessionStore.Load(path);
        Assert.Equal("b", s.Name);
        Assert.Equal(42, s.DurationMs);
        Assert.Equal("", s.Note);

        File.WriteAllText(path, "garbage");
        Assert.Throws<InvalidDataException>(() => LabSessionStore.Load(path));
        File.WriteAllText(path, "{}");
        Assert.Throws<InvalidDataException>(() => LabSessionStore.Load(path));
        Assert.Throws<InvalidDataException>(() => LabSessionStore.Load(_dir.File("absent.json")));
    }

    [Fact]
    public void Captured_key_name()
    {
        Assert.Equal("F24", new CapturedKey(0, VirtualKeys.F24, 0, true, InputSource.Physical, false).KeyName);
    }
}
