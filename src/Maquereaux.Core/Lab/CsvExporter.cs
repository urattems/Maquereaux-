using System.Globalization;
using System.Text;

namespace Maquereaux.Core.Lab;

/// <summary>
/// Export CSV d'une session, lisible directement dans Excel.
/// <para>
/// Pourquoi la culture compte : Excel FR attend « ; » comme séparateur et « , » comme décimale ;
/// Excel EN attend « , » et « . ». On utilise donc le séparateur de liste et la décimale de la culture
/// courante, + un BOM UTF-8 pour que les accents s'affichent correctement.
/// </para>
/// </summary>
public static class CsvExporter
{
    public static readonly string[] Headers =
        { "index", "time_ms", "event", "key", "vk", "scan", "source", "repeat", "interval_ms" };

    public static string ToCsv(LabSession session, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(session);
        var sep = culture.TextInfo.ListSeparator;
        if (string.IsNullOrEmpty(sep)) sep = ",";
        // Si la décimale de la culture est identique au séparateur (ex. "," / ","), on bascule sur ";"
        if (sep == culture.NumberFormat.NumberDecimalSeparator) sep = ";";

        var sb = new StringBuilder();
        sb.Append(string.Join(sep, Headers)).Append("\r\n");

        // Intervalle depuis l'appui précédent de la même source (vide pour les relâchements et répétitions)
        var lastDown = new Dictionary<InputSource, double>();
        int i = 0;
        foreach (var e in session.Events.OrderBy(e => e.TimeMs))
        {
            string interval = "";
            if (e.IsDown && !e.IsRepeat)
            {
                if (lastDown.TryGetValue(e.Source, out var prev)) interval = (e.TimeMs - prev).ToString("0.###", culture);
                lastDown[e.Source] = e.TimeMs;
            }
            var fields = new[]
            {
                (++i).ToString(culture),
                e.TimeMs.ToString("0.###", culture),
                e.IsDown ? "down" : "up",
                Escape(e.KeyName, sep),
                e.VirtualKey.ToString(culture),
                e.ScanCode.ToString(culture),
                e.Source.ToString(),
                e.IsRepeat ? "1" : "0",
                interval,
            };
            sb.Append(string.Join(sep, fields)).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Écrit le fichier en UTF-8 avec BOM (sinon Excel affiche « Ã© » à la place de « é »).</summary>
    public static void Export(LabSession session, string path, CultureInfo? culture = null) =>
        File.WriteAllText(path, ToCsv(session, culture ?? CultureInfo.CurrentCulture), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private static string Escape(string value, string sep) =>
        value.Contains(sep) || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
