using CommunityToolkit.Mvvm.ComponentModel;
using Maquereaux.Core.Lab;
using Maquereaux.Core.Stats;

namespace Maquereaux.App.ViewModels;

/// <summary>Une ligne du tableau de stats par source.</summary>
public sealed record SourceRow(string Source, int Inputs, double Rate, TimingStats Intervals, TimingStats Press);

/// <summary>Une session du Labo (résultats calculés une fois, nom et note éditables).</summary>
public sealed partial class LabSessionViewModel : ObservableObject
{
    public LabSessionViewModel(LabSession session)
    {
        Session = session;
        Results = SessionAnalyzer.Analyze(session);
        Rows = Results.BySource.Select(ToRow).Append(ToRow(Results.All)).ToList();
        TopKeys = string.Join("  ·  ", Results.ByKey.Take(8).Select(k => $"{k.Key} ×{k.Count}"));
    }

    public LabSession Session { get; }
    public SessionResults Results { get; }
    public IReadOnlyList<SourceRow> Rows { get; }
    public string TopKeys { get; }
    public IReadOnlyList<IntervalPoint> Series => Results.Series;
    public double DurationMs => Session.DurationMs;

    public string Name
    {
        get => Session.Name;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "Session" : value.Trim();
            if (SetProperty(Session.Name, v, Session, (s, x) => s.Name = x)) OnPropertyChanged(nameof(Title));
        }
    }

    public string Note
    {
        get => Session.Note;
        set => SetProperty(Session.Note, value ?? "", Session, (s, x) => s.Note = x);
    }

    public string Title => Session.Name;

    public string Summary =>
        $"{Session.StartedAt:HH:mm:ss} · {Results.DurationSeconds:0.0} s · {Results.TotalInputs} inputs · {Results.InputsPerSecond:0.0}/s";

    /// <summary>Libellé de la source dominante (« Manuel », « Maquereaux », « Mixte »).</summary>
    public string DominantLabel
    {
        get
        {
            var withInputs = Results.BySource.Where(s => s.Inputs > 0).ToList();
            if (withInputs.Count == 0) return "Vide";
            if (withInputs.Count > 1 && withInputs.Min(s => s.Inputs) * 5 > withInputs.Max(s => s.Inputs)) return "Mixte";
            return Results.DominantSource switch
            {
                InputSource.Physical => "Manuel",
                InputSource.Maquereaux => "Maquereaux",
                _ => "Autre logiciel",
            };
        }
    }

    private static SourceRow ToRow(SourceStats s) =>
        new(SessionAnalyzer.Describe(s.Source), s.Inputs, s.InputsPerSecond, s.Intervals, s.PressDurations);

    public override string ToString() => Name;
}
