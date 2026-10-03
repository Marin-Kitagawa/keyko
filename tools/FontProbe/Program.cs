using System;
using System.IO;
using SkiaSharp;

// renders "Shortcuts" with a given font for visual reference
var fontPath = args[0];
var outPath = args[1];
using var bmp = new SKBitmap(700, 140);
using var canvas = new SKCanvas(bmp);
canvas.Clear(new SKColor(0x1F, 0x15, 0x28));
using var tf = SKTypeface.FromFile(fontPath) ?? throw new Exception("cannot load");
using var font = new SKFont(tf, 56);
using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
canvas.DrawText("Shortcuts", 20, 90, font, paint);
using var img = SKImage.FromBitmap(bmp);
using var data = img.Encode(SKEncodedImageFormat.Png, 100);
File.WriteAllBytes(outPath, data.ToArray());
Console.WriteLine($"[{tf.FamilyName}] -> {outPath}");
return 0;
