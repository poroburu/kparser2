using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using kparser2.Abstractions;

namespace kparser2.ViewModels;

public sealed partial class DebugViewModel : ObservableObject, IDisposable
{
    private readonly IPacketSession _session;
    private readonly IDisposable _subscription;

    public ObservableCollection<PacketRowDto> Packets { get; } = [];

    [ObservableProperty]
    private PacketRowDto? _selectedPacket;

    [ObservableProperty]
    private string _hexDump = string.Empty;

    [ObservableProperty]
    private string _metaSummary = string.Empty;

    public DebugViewModel(IPacketSession session)
    {
        _session = session;
        foreach (var packet in session.GetRecentPackets(500))
        {
            Packets.Add(packet);
        }

        _subscription = session.Packets.Subscribe(packet =>
            UiThread.Run(() =>
            {
                Packets.Insert(0, packet);
                while (Packets.Count > 5000)
                {
                    Packets.RemoveAt(Packets.Count - 1);
                }
            }));
    }

    partial void OnSelectedPacketChanged(PacketRowDto? value)
    {
        _session.SelectPacket(value);
        if (value?.Data is null || value.Data.Length == 0)
        {
            HexDump = string.Empty;
            MetaSummary = value is null
                ? string.Empty
                : $"{value.PacketName} 0x{value.PacketId:X4} {value.Direction} ({value.Size} bytes)";
            return;
        }

        HexDump = FormatHexDump(value.Data);
        MetaSummary =
            $"{value.PacketName} 0x{value.PacketId:X4} {value.Direction} ({value.Size} bytes)\n" +
            $"Topic: {value.Topic}\nInjected: {value.Injected} Blocked: {value.Blocked}";
    }

    private static string FormatHexDump(byte[] data)
    {
        var lines = new List<string>
        {
            "   |  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F | 0123456789ABCDEF",
            new('-', 16 * 3 + 20)
        };

        for (var row = 0; row < (data.Length + 15) / 16; row++)
        {
            var from = row * 16;
            var line = $"{row,2} |";
            var ascii = string.Empty;

            for (var col = 0; col < 16; col++)
            {
                var index = from + col;
                if (index < data.Length)
                {
                    line += $" {data[index]:X2}";
                    ascii += data[index] is >= 32 and < 127 ? (char)data[index] : '.';
                }
                else
                {
                    line += " --";
                    ascii += '-';
                }
            }

            lines.Add($"{line} | {ascii}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public void Dispose() => _subscription.Dispose();
}
