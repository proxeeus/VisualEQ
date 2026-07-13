using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageLib
{
    public static class Bmp
    {
        public static Image Load(string name, byte[] data)
        {
            // Classic EQ foliage/mask textures are 8-bit palette-indexed BMPs where palette
            // index 0 is the "transparent" background color. SixLabors.BmpDecoder<Rgb24>
            // resolves the palette to RGB and throws away the index info, so we lose the
            // "which pixels should be transparent" signal. Manually parse the header for
            // 8-bit indexed and emit RGBA with alpha=0 at index-0 pixels. Fall through to
            // the standard 24-bit decoder for everything else (16bpp, 24bpp, RLE, etc.).
            if (TryLoadIndexed8(name, data, out var indexed))
                return indexed;

            using (var ms = new MemoryStream(data))
            using (var simage = new BmpDecoder().Decode<Rgb24>(Configuration.Default, ms))
                return new Image(ColorMode.Rgb, (simage.Width, simage.Height), simage.Frames[0].SavePixelData(), name);
        }

        // Parses BITMAPFILEHEADER (14) + BITMAPINFOHEADER (40) + palette + pixel data.
        // Returns true and populates `image` iff the BMP is uncompressed 8-bit indexed. Any
        // structural weirdness (bad magic, truncated buffer, unexpected compression) returns
        // false so we fall through to SixLabors.
        static bool TryLoadIndexed8(string name, byte[] data, out Image image)
        {
            image = null;
            if (data.Length < 54) return false;
            if (data[0] != 'B' || data[1] != 'M') return false;

            int bfOffBits    = BitConverter.ToInt32(data, 10);
            int biWidth      = BitConverter.ToInt32(data, 18);
            int biHeight     = BitConverter.ToInt32(data, 22);
            short biBitCount = BitConverter.ToInt16(data, 28);
            int biCompression = BitConverter.ToInt32(data, 30);
            int biClrUsed    = BitConverter.ToInt32(data, 46);

            if (biBitCount != 8) return false;
            if (biCompression != 0) return false;   // BI_RGB only — no RLE8 support here
            if (biWidth <= 0 || biHeight == 0) return false;

            var topDown = biHeight < 0;
            var height = Math.Abs(biHeight);
            var paletteCount = biClrUsed == 0 ? 256 : biClrUsed;
            if (paletteCount <= 0 || paletteCount > 256) return false;

            const int paletteStart = 54;
            if (data.Length < paletteStart + paletteCount * 4) return false;

            // Row size padded to 4 bytes per BMP spec.
            var rowSize = ((biWidth + 3) / 4) * 4;
            if (data.Length < bfOffBits + rowSize * height) return false;

            var rgba = new byte[biWidth * height * 4];
            for (var y = 0; y < height; y++)
            {
                // BMP file rows are bottom-up when biHeight > 0. Normalize to top-down in
                // our buffer so downstream FlipY() (called by ConvertTexture) works the same
                // way it does for the SixLabors path.
                var fileRow = topDown ? y : (height - 1 - y);
                var rowOffset = bfOffBits + fileRow * rowSize;
                for (var x = 0; x < biWidth; x++)
                {
                    var idx = data[rowOffset + x];
                    var palEntry = paletteStart + idx * 4;
                    var b = data[palEntry + 0];
                    var g = data[palEntry + 1];
                    var r = data[palEntry + 2];
                    var a = idx == 0 ? (byte)0 : (byte)255;

                    var outOffset = (y * biWidth + x) * 4;
                    rgba[outOffset + 0] = r;
                    rgba[outOffset + 1] = g;
                    rgba[outOffset + 2] = b;
                    rgba[outOffset + 3] = a;
                }
            }

            image = new Image(ColorMode.Rgba, (biWidth, height), rgba, name);
            return true;
        }
    }
}
