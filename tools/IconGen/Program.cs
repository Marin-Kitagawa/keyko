using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

// Icon pipeline for Keyko.
//
//   dotnet run --project tools/IconGen -- <outDir> [--from <png>]
//
// Default: draws the vector heart-keyhole squircle. With --from: crops the
// given raster logo to its opaque bounding box, square-pads it, and builds
// app.ico (16–256), tray.png (32), logo256.png and logo.png (512) from it.
internal static class Program
{
    private const string HeartPath =
        "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z";

    private static readonly SKColor Grad1 = new(0xF4, 0x72, 0xB6); // rose
    private static readonly SKColor Grad2 = new(0xA7, 0x8B, 0xFA); // lavender

    private static int Main(string[] args)
    {
        var outDir = ".";
        string? from = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--from" && i + 1 < args.Length) from = args[++i];
            else outDir = args[i];
        }
        Directory.CreateDirectory(outDir);

        var sizes = new[] { 256, 128, 64, 48, 32, 16 };
        var pngs = new Dictionary<int, byte[]>();

        SKBitmap? source = null;
        if (from is not null)
        {
            source = SKBitmap.Decode(from);
            if (source is null)
            {
                Console.Error.WriteLine("cannot decode " + from);
                return 1;
            }
        }

        foreach (var s in sizes)
        {
            using var bmp = source is null ? Draw(s) : RenderFrom(source, s);
            pngs[s] = Encode(bmp);
            if (s == 256)
                File.WriteAllBytes(Path.Combine(outDir, "logo256.png"), pngs[s]);
        }

        // 512 master for in-app <Image> use (no upscaling: only if source is large enough)
        if (source is null)
        {
            using var bmp512 = Draw(512);
            File.WriteAllBytes(Path.Combine(outDir, "logo.png"), Encode(bmp512));
        }
        else if (Math.Max(source.Width, source.Height) >= 512)
        {
            using var bmp512 = RenderFrom(source, 512);
            File.WriteAllBytes(Path.Combine(outDir, "logo.png"), Encode(bmp512));
        }
        else
        {
            File.WriteAllBytes(Path.Combine(outDir, "logo.png"), Encode(source));
        }

        File.WriteAllBytes(Path.Combine(outDir, "app.ico"), PackIco(pngs));
        File.WriteAllBytes(Path.Combine(outDir, "tray.png"), pngs[32]);
        Console.WriteLine("icons written to " + Path.GetFullPath(outDir) + (from is null ? "" : $" (from {from})"));
        return 0;
    }

    // ---- raster pipeline: bbox crop → square pad → resize ----

    private static SKBitmap RenderFrom(SKBitmap src, int size)
    {
        int l = src.Width, t = src.Height, r = 0, b = 0;
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                if (src.GetPixel(x, y).Alpha > 16)
                {
                    if (x < l) l = x;
                    if (x > r) r = x;
                    if (y < t) t = y;
                    if (y > b) b = y;
                }

        if (r < l || b < t) // fully transparent: fall back to full canvas
        {
            l = 0; t = 0; r = src.Width - 1; b = src.Height - 1;
        }

        Console.WriteLine($"source {src.Width}x{src.Height}, opaque bbox: [{l},{t}]..[{r},{b}] ({r - l + 1}x{b - t + 1})");

        // square with a small margin around the glyph
        float glyphW = r - l + 1;
        float glyphH = b - t + 1;
        float side = Math.Max(glyphW, glyphH) * 1.10f;
        var dst = new SKBitmap(size, size);
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Transparent);
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        var scaleF = size / side;
        var dest = new SKRect(
            (size - glyphW * scaleF) / 2f,
            (size - glyphH * scaleF) / 2f,
            (size - glyphW * scaleF) / 2f + glyphW * scaleF,
            (size - glyphH * scaleF) / 2f + glyphH * scaleF);
        using (var img = SKImage.FromBitmap(src))
        {
            canvas.DrawImage(img, new SKRect(l, t, r + 1, b + 1), dest, sampling);
        }
        return dst;
    }

    // ---- vector pipeline (original) ----

    private static SKBitmap Draw(int size)
    {
        var bmp = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bmp);

        float inset = size * 0.02f;
        float radius = size * 0.26f; // extra round = soft
        var rect = new SKRect(inset, inset, size - inset, size - inset);
        using var rrect = new SKRoundRect(rect, radius);

        using var gradient = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Right, rect.Bottom),
            new[] { Grad1, Grad2 },
            null, SKShaderTileMode.Clamp);

        // gradient body
        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            paint.Shader = gradient;
            canvas.DrawRoundRect(rrect, paint);
        }

        // top glass highlight
        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, rect.Top),
                new SKPoint(0, rect.MidY),
                new[] { new SKColor(255, 255, 255, 90), new SKColor(255, 255, 255, 0) },
                null, SKShaderTileMode.Clamp);
            paint.Shader = shader;
            var hi = new SKRoundRect(rect, radius);
            canvas.Save();
            canvas.ClipRoundRect(hi, antialias: true);
            canvas.DrawRect(rect.Left, rect.Top, rect.Width, rect.Height * 0.5f, paint);
            canvas.Restore();
        }

        // heart with keyhole punched in the gradient color
        using (var paint = new SKPaint())
        {
            paint.IsAntialias = true;

            float grid = 24f;
            float scale = size * 0.048f;
            float offsetX = (size - grid * scale) / 2f;
            float offsetY = (size - grid * scale) / 2f + size * 0.008f;

            using var heart = SKPath.ParseSvgPathData(HeartPath);
            using var transformed = new SKPath();
            var matrix = SKMatrix.CreateScaleTranslation(scale, scale, offsetX, offsetY);
            heart.Transform(matrix, transformed);

            paint.Color = SKColors.White;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawPath(transformed, paint);

            // keyhole = circle + widening wedge, filled with the same gradient → looks punched
            paint.Shader = gradient;
            using var keyhole = new SKPath();
            float u = scale; // one grid unit
            keyhole.AddCircle(offsetX + 12 * u, offsetY + 9.4f * u, 2.05f * u);
            keyhole.MoveTo(offsetX + 10.75f * u, offsetY + 11.2f * u);
            keyhole.LineTo(offsetX + 13.25f * u, offsetY + 11.2f * u);
            keyhole.LineTo(offsetX + 14.15f * u, offsetY + 16.6f * u);
            keyhole.LineTo(offsetX + 9.85f * u, offsetY + 16.6f * u);
            keyhole.Close();
            canvas.DrawPath(keyhole, paint);
        }

        return bmp;
    }

    private static byte[] Encode(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Packs PNG bitmaps into a Vista+ ICO container.</summary>
    private static byte[] PackIco(Dictionary<int, byte[]> pngs)
    {
        var sizes = new[] { 256, 128, 64, 48, 32, 16 };
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        w.Write((ushort)0);              // reserved
        w.Write((ushort)1);              // type: icon
        w.Write((ushort)sizes.Length);   // count

        int offset = 6 + 16 * sizes.Length;
        foreach (var s in sizes)
        {
            var data = pngs[s];
            w.Write((byte)(s >= 256 ? 0 : s)); // width (0 = 256)
            w.Write((byte)(s >= 256 ? 0 : s)); // height
            w.Write((byte)0);                  // palette
            w.Write((byte)0);                  // reserved
            w.Write((ushort)1);                // planes
            w.Write((ushort)32);               // bpp
            w.Write((uint)data.Length);
            w.Write((uint)offset);
            offset += data.Length;
        }

        foreach (var s in sizes)
            w.Write(pngs[s]);

        w.Flush();
        return ms.ToArray();
    }
}
