using System.Drawing.Imaging;
using Ajazz.Keyboard;
using Ajazz.Keyboard.Lcd;

static int Usage()
{
    Console.WriteLine("""
        ak980 - AJAZZ AK980 command line tool

          ak980 list                                   list the keyboard's HID interfaces
          ak980 time [--slot N]                        set the LCD clock to the PC's local time
          ak980 lcd <image|gif|frame-dir> [--slot N] [--fit fit|fill|stretch] [--delay MS]
                                                       upload a picture/animation to the LCD
          ak980 preview <image|gif> <out-dir> [--fit ...]
                                                       render the LCD frames to PNGs without touching the keyboard
          ak980 light <mode 0-19> [--color RRGGBB] [--bright 0-5] [--speed 0-5] [--dir N] [--rainbow]
          ak980 side <off|flowing|red|yellow|green|iceblue|blue|pink|white|neon>
          ak980 raw <hex bytes>                        send one 64-byte command packet, print the reply
        """);
    return 1;
}

string? Opt(string name) =>
    Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

LcdFitMode Fit() => Enum.TryParse<LcdFitMode>(Opt("--fit"), true, out var f) ? f : LcdFitMode.Fit;

byte Slot() => byte.TryParse(Opt("--slot"), out var s) ? s : (byte)1;

Ak980Keyboard OpenKeyboard() =>
    Ak980Keyboard.Open() ?? throw new InvalidOperationException("AK980 not found on USB (VID 0C45 / PID 8009)");

if (args.Length == 0) return Usage();

try
{
    switch (args[0])
    {
        case "list":
            foreach (var d in Ak980Keyboard.ListInterfaces())
                Console.WriteLine($"{d.ProductId:X4} MI_{d.Interface:X2} page {d.UsagePage:X4}:{d.Usage:X2} in {d.InputLength,4} out {d.OutputLength,4} feat {d.FeatureLength,3}  {d.Product}");
            return 0;

        case "time":
        {
            using var kb = OpenKeyboard();
            kb.SyncClock(DateTime.Now, Slot());
            Console.WriteLine($"Clock set to {DateTime.Now:yyyy-MM-dd HH:mm:ss} (slot {Slot()})");
            return 0;
        }

        case "lcd" when args.Length >= 2:
        {
            using var anim = LcdAnimation.FromPath(args[1], LcdGeometry.AK980, Fit());
            if (int.TryParse(Opt("--delay"), out var delay))
                anim.Frames.ForEach(f => f.DelayMs = delay);
            using var kb = OpenKeyboard();
            Console.WriteLine($"Uploading {anim.Frames.Count} frame(s) to slot {Slot()}...");
            var progress = new Progress<double>(p => Console.Write($"\r  {p:P0}   "));
            kb.UploadLcd(anim, Slot(), progress);
            Console.WriteLine("\nDone.");
            return 0;
        }

        case "preview" when args.Length >= 3:
        {
            using var anim = LcdAnimation.FromPath(args[1], LcdGeometry.AK980, Fit());
            Directory.CreateDirectory(args[2]);
            for (int i = 0; i < anim.Frames.Count; i++)
                anim.Frames[i].Image.Save(Path.Combine(args[2], $"{i:D3}.png"), ImageFormat.Png);
            var blob = anim.Encode();
            Console.WriteLine($"{anim.Frames.Count} frame(s), delays {string.Join(",", anim.Frames.Select(f => f.DelayMs).Distinct())} ms, blob {blob.Length} bytes ({blob.Length / LcdGeometry.PageSize} pages)");
            return 0;
        }

        case "light" when args.Length >= 2:
        {
            var color = Convert.FromHexString(Opt("--color") ?? "B48CFF");
            var s = new LightSettings
            {
                Mode = byte.Parse(args[1]),
                R = color[0], G = color[1], B = color[2],
                Rainbow = args.Contains("--rainbow"),
                Brightness = int.TryParse(Opt("--bright"), out var br) ? br : LightSettings.MaxLevel,
                Speed = int.TryParse(Opt("--speed"), out var sp) ? sp : 3,
                Direction = int.TryParse(Opt("--dir"), out var dir) ? dir : 0,
            };
            using var kb = OpenKeyboard();
            kb.SetLighting(s);
            Console.WriteLine($"Lighting: {LightMode.All.FirstOrDefault(m => m.Id == s.Mode)?.Name ?? "?"} {s}");
            return 0;
        }

        case "custom" when args.Length >= 2:
        {
            // custom <RRGGBB for every LED> [index=RRGGBB ...] [--bright N]
            static (byte, byte, byte) Rgb(string hex) { var c = Convert.FromHexString(hex); return (c[0], c[1], c[2]); }
            var colors = Enumerable.Range(1, 127).ToDictionary(i => i, _ => Rgb(args[1]));
            foreach (var a in args.Skip(2).Where(a => a.Contains('=')))
                colors[int.Parse(a.Split('=')[0])] = Rgb(a.Split('=')[1]);
            using var kb = OpenKeyboard();
            kb.SetCustomColors(colors, int.TryParse(Opt("--bright"), out var b) ? b : LightSettings.MaxLevel);
            Console.WriteLine($"Custom colors set ({colors.Count} LEDs)");
            return 0;
        }

        case "options":
        {
            // options [--sleep 0-3] [--response N] [--nowin] [--noalttab] [--noaltf4] [--fnswitch]
            var o = new KeyboardOptions
            {
                DisableWinKey = args.Contains("--nowin"),
                DisableAltTab = args.Contains("--noalttab"),
                DisableAltF4 = args.Contains("--noaltf4"),
                FnSwitch = args.Contains("--fnswitch"),
                SleepTime = int.TryParse(Opt("--sleep"), out var sl) ? sl : 2,
                KeyResponse = int.TryParse(Opt("--response"), out var rs) ? rs : 2,
            };
            using var kb = OpenKeyboard();
            kb.SetOptions(o);
            Console.WriteLine($"Options: {o}");
            return 0;
        }

        case "side" when args.Length >= 2:
        {
            var mode = Enum.Parse<SideLightMode>(args[1], ignoreCase: true);
            using var kb = OpenKeyboard();
            kb.SetSideLight(mode);
            Console.WriteLine($"Side/logo light: {mode}");
            return 0;
        }

        case "raw" when args.Length >= 2:
        {
            var bytes = Convert.FromHexString(string.Concat(args.Skip(1)).Replace(" ", ""));
            using var kb = OpenKeyboard();
            var reply = kb.SendPacket(bytes);
            Console.WriteLine(Convert.ToHexString(reply));
            return 0;
        }

        default:
            return Usage();
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 2;
}
