using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using kparser2.Abstractions;

namespace kparser2.Ui.Qa;

internal sealed partial class ReplayQa
{
    private async Task CheckDiagnosticsAsync(UserControl control, IPacketSession session)
    {
        var grid = Descendants(control).OfType<DataGrid>().Single();
        var expectedRows = session.GetRecentPackets(500).ToArray();
        await PumpAsync();
        var actualRows = grid.Items.Cast<PacketRowDto>().ToArray();
        var expected = JsonSerializer.Serialize(expectedRows, Json);
        var actual = JsonSerializer.Serialize(actualRows, Json);
        SaveCase("raw-data", "default", control, expected, actual, new { }, expectedRows.Length == 0 ? "empty-or-partial" : "populated", 0, expected == actual);
        _window.Width = 760; _window.Height = 480;
        await PumpAsync();
        SaveCase("raw-data", "compact", control, expected, actual, new { }, expectedRows.Length == 0 ? "empty-or-partial" : "populated", 0, expected == actual);
        _window.Width = 1200; _window.Height = 800;
        foreach (var index in new[] { 0, Math.Max(0, expectedRows.Length - 1) }.Distinct())
        {
            if (expectedRows.Length == 0) break;
            grid.SelectedIndex = index;
            grid.ScrollIntoView(grid.SelectedItem);
            await PumpAsync();
            var row = expectedRows[index];
            var dump = Descendants(control).OfType<TextBox>().Single().Text;
            var bytes = new List<byte>();
            foreach (var line in dump.Split('\n').Skip(2))
            {
                var columns = line.Split('|');
                if (columns.Length < 3) continue;
                foreach (Match token in Regex.Matches(columns[1], @"\b[0-9A-F]{2}\b")) bytes.Add(Convert.ToByte(token.Value, 16));
            }
            var expectedHex = Convert.ToHexString(row.Data ?? []);
            var actualHex = Convert.ToHexString(bytes.ToArray());
            var meta = Descendants(control).OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => t.Contains("Topic:")) ?? "";
            var metadataMatches = meta.Contains(row.Topic) && meta.Contains(row.PacketName) && meta.Contains($"0x{row.PacketId:X4}");
            expected = JsonSerializer.Serialize(new { hex = expectedHex, metadata_matches = true, selected_packet_matches = true }, Json);
            actual = JsonSerializer.Serialize(new { hex = actualHex, metadata_matches = metadataMatches, selected_packet_matches = ReferenceEquals(session.GetSelectedPacket(), row) }, Json);
            SaveCase("raw-data", "select-" + index, control, expected, actual, new { index }, "populated", 0, expected == actual);
        }
        grid.SelectedIndex = -1;
        await PumpAsync();
        var cleared = Descendants(control).OfType<TextBox>().Single().Text;
        SaveCase("raw-data", "clear-selection", control, "", cleared, new { }, "empty-or-partial", 0, cleared.Length == 0 && session.GetSelectedPacket() is null);
    }
}
