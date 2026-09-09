using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using kparser2.Abstractions;

namespace kparser2.Services;

public static class AnalyticsReportRenderer
{
    private static readonly FontFamily MonospaceFont = new("Consolas");

    public static FlowDocument ToFlowDocument(AnalyticsReportDto? report)
    {
        var document = new FlowDocument
        {
            FontFamily = MonospaceFont,
            FontSize = 12,
            PagePadding = new Thickness(8),
            TextAlignment = TextAlignment.Left
        };

        if (report?.Spans is null || report.Spans.Count == 0)
        {
            document.Blocks.Add(new Paragraph(new Run("(no data)")));
            return document;
        }

        var paragraph = new Paragraph { Margin = new Thickness(0) };

        foreach (var span in report.Spans)
        {
            var run = new Run(span.Text)
            {
                FontWeight = span.Bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = ParseColor(span.Color)
            };

            if (span.Underline)
            {
                run.TextDecorations = TextDecorations.Underline;
            }

            paragraph.Inlines.Add(run);
        }

        // Reports use space-padded columns. A narrow viewport must scroll the
        // document horizontally instead of wrapping one data row into several.
        var text = string.Concat(report.Spans.Select(s => s.Text));
        var typeface = new Typeface(MonospaceFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var widestLine = text.Split('\n').Select(line => new FormattedText(
            line.TrimEnd('\r'), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, document.FontSize, Brushes.Black, 1.0).WidthIncludingTrailingWhitespace).DefaultIfEmpty(0).Max();
        document.PageWidth = Math.Max(40, Math.Ceiling(widestLine) + document.PagePadding.Left + document.PagePadding.Right + 2);
        document.MinPageWidth = document.PageWidth;

        document.Blocks.Add(paragraph);
        return document;
    }

    private static Brush ParseColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return Brushes.Black;
        }

        try
        {
            var converted = ColorConverter.ConvertFromString(color);
            if (converted is Color c)
            {
                return new SolidColorBrush(c);
            }
        }
        catch (FormatException)
        {
        }

        return Brushes.Black;
    }
}
