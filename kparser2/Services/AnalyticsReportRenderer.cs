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
