using Ajazz.Keyboard.Hid;

namespace Ajazz.Keyboard;

/// <summary>
/// The 2.4 GHz dongle's command channel (VID 0C45 PID FEFE, MI_03, usage page 0xFF60).
/// Requests are 33-byte output reports: report ID 0, command bytes, and a byte-sum checksum in the last byte.
/// The reply echoes the first three command bytes (stock DeviceDriver.exe 0x44fa90).
/// It only answers while the keyboard is switched to 2.4G mode; in wired mode the dongle has no link.
/// </summary>
public sealed class Ak980Dongle : IDisposable
{
    private const byte OpBattery = 0x20;

    private readonly HidDevice _dev;
    private readonly object _io = new();

    private Ak980Dongle(HidDevice dev) => _dev = dev;

    public static Ak980Dongle? Open()
    {
        var info = HidDevice.Enumerate(Ak980Keyboard.VendorId)
            .FirstOrDefault(d => d.ProductId == Ak980Keyboard.DongleProductId && d.UsagePage == 0xFF60 && d.OutputLength >= 33);
        return info is null ? null : new Ak980Dongle(HidDevice.Open(info));
    }

    /// <summary>Sends a command and returns the reply data (report ID stripped), or null if the keyboard didn't answer.</summary>
    public byte[]? Request(params byte[] command)
    {
        var report = new byte[_dev.Info.OutputLength];
        command.AsSpan(0, Math.Min(command.Length, report.Length - 2)).CopyTo(report.AsSpan(1));
        byte sum = 0;
        for (int i = 0; i < report.Length - 1; i++) sum += report[i];
        report[^1] = sum;

        lock (_io)
        {
            _dev.Write(report, timeoutMs: 500);
            // The stock app polls up to 20 times; replies to other requests may still be queued, so skip non-matching ones.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var r = _dev.Read(50);
                if (r is null) continue;
                var data = r.AsSpan(1);
                if (data.Length >= 3 && data[0] == report[1] && data[1] == report[2] && data[2] == report[3])
                    return data.ToArray();
            }
        }
        return null;
    }

    /// <summary>Battery charge in percent (stock 0x435b00: command 20 01, level in reply byte 3), or null when unavailable.</summary>
    public int? ReadBattery()
    {
        // After a while idle the first request only wakes the radio link, so ask twice before giving up.
        var reply = Request(OpBattery, 0x01) ?? Request(OpBattery, 0x01);
        if (reply is null || reply.Length < 4 || reply[3] == 0) return null;
        return Math.Min((int)reply[3], 100);
    }

    public void Dispose() => _dev.Dispose();
}
