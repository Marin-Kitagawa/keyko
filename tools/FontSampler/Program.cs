using System;
using System.IO;
using SkiaSharp;

// Renders a Keyko-styled sampler sheet for candidate UI fonts.
//   dotnet run --project tools/FontSampler -- <fontsDir> <outPng>
internal static class Program
{
    private static readonly (string File, string Name, bool HasBoldFile)[] Fonts =
    {
        ("Baloo2.ttf",         "Baloo 2",          false),
        ("Fredoka.ttf",        "Fredoka",          false),
        ("Comfortaa.ttf",      "Comfortaa",        false),
        ("Nunito.ttf",         "Nunito",           false),
        ("VarelaRound.ttf",    "Varela Round",     false),
        ("MplusRounded.ttf",   "M PLUS Rounded 1c", true),
        ("Poppins.ttf",        "Poppins",          false),
        ("Manrope.ttf",        "Manrope",          false),
        ("Lexend.ttf",         "Lexend",           false),
        ("Figtree.ttf",        "Figtree",          false),
    };

    private static int Main(string[] args)
    {
        var dir = args.Length > 0 ? args[0] : ".";
        var outPath = args.Length > 1 ? args[1] : "font-sampler.png";

        const int rowH = 150;
        const int width = 1240;
        const int padX = 36;
        using var bmp = new SKBitmap(width, rowH * Fonts.Length + 30);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(0x1F, 0x15, 0x28)); // plum dark like the app

        var labelColor = new SKColor(0xF4, 0x72, 0xB6);
        var textColor = new SKColor(0xFB, 0xF3, 0xF8);
        var dimColor = new SKColor(0xC2, 0xB0, 0xC8);
        
        

        for (int i = 0; i < Fonts.Length; i++)
        {
            var f = Fonts[i];
            var path = Path.Combine(dir, f.File);
            if (!File.Exists(path)) continue;

            using var regular = SKTypeface.FromFile(path);
            SKTypeface? boldTf = null;
            if (f.HasBoldFile)
            {
                var boldPath = Path.Combine(dir, "MplusRoundedBold.ttf");
                if (File.Exists(boldPath)) boldTf = SKTypeface.FromFile(boldPath);
            }

            float y = 24 + i * rowH;

            // separator
            using (var sep = new SKPaint { Color = new SKColor(255, 255, 255, 30) })
                canvas.DrawLine(padX, y - 18, width - padX, y - 18, sep);

            // pink number + name label
            canvas.DrawText($"{i + 1}.  {f.Name}", padX, y + 6, new SKFont(regular, 15), new SKPaint { Color = labelColor, IsAntialias = true });

            // big title (bold)
            canvas.DrawText("Keyko ♡", padX, y + 56, new SKFont(boldTf ?? regular, 40), new SKPaint { Color = textColor, IsAntialias = true, FakeBoldText = boldTf is null });

            // heading
            canvas.DrawText("Shortcuts — your cozy little launcher", padX + 240, y + 20, new SKFont(boldTf ?? regular, 23), new SKPaint { Color = textColor, IsAntialias = true, FakeBoldText = boldTf is null });

            // body line
            canvas.DrawText("Launch at Windows startup · Ctrl+Alt+K · search shortcuts, hotkeys, targets…", padX + 240, y + 50, new SKFont(regular, 17), new SKPaint { Color = dimColor, IsAntialias = true });

            // numbers / keycaps
            canvas.DrawText("1 2 3  45% 0.95  F5 Esc  {Ctrl+C}  1234567890", padX + 240, y + 80, new SKFont(regular, 17), new SKPaint { Color = textColor, IsAntialias = true });

            boldTf?.Dispose();
        }

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(outPath, data.ToArray());
        Console.WriteLine("sampler written to " + Path.GetFullPath(outPath));
        return 0;
    }
}
