using System.Buffers.Binary;
using System.IO.Compression;

namespace Earmatch.Core.Tests;

/// Decodes the 8-bit, non-interlaced RGB and RGBA PNG fixtures into RGBA bytes, with the base library only. Colour
/// profiles are ignored, as the macOS tests draw each image in its own colour space: the bytes are the stored values.
static class Png
{
    static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static (int Width, int Height, byte[] Rgba) Decode(byte[] file)
    {
        if (!file.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("not a PNG file");
        int width = 0, height = 0, channels = 0;
        using var compressed = new MemoryStream();
        var offset = 8;
        while (offset + 8 <= file.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(offset));
            var type = System.Text.Encoding.ASCII.GetString(file, offset + 4, 4);
            var data = file.AsSpan(offset + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    var (bitDepth, colourType, interlace) = (data[8], data[9], data[12]);
                    if (bitDepth != 8 || interlace != 0 || colourType is not (2 or 6))
                        throw new InvalidDataException($"unsupported PNG: depth {bitDepth}, colour type {colourType}, interlace {interlace}");
                    channels = colourType == 6 ? 4 : 3;
                    break;
                case "IDAT":
                    compressed.Write(data);
                    break;
            }
            offset += 12 + length;
            if (type == "IEND") break;
        }
        if (channels == 0) throw new InvalidDataException("PNG without a header");

        compressed.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress)) zlib.CopyTo(inflated);
        var raw = inflated.GetBuffer();
        var stride = width * channels;
        if (inflated.Length < (long)height * (stride + 1)) throw new InvalidDataException("PNG data too short");

        var rgba = new byte[width * height * 4];
        var previous = new byte[stride];
        var line = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var start = y * (stride + 1);
            var filter = raw[start];
            Array.Copy(raw, start + 1, line, 0, stride);
            Unfilter(filter, line, previous, channels);
            for (var x = 0; x < width; x++)
            {
                var target = (y * width + x) * 4;
                rgba[target] = line[x * channels];
                rgba[target + 1] = line[x * channels + 1];
                rgba[target + 2] = line[x * channels + 2];
                rgba[target + 3] = channels == 4 ? line[x * channels + 3] : (byte)255;
            }
            (previous, line) = (line, previous);
        }
        return (width, height, rgba);
    }

    static void Unfilter(byte filter, byte[] line, byte[] previous, int bytesPerPixel)
    {
        for (var index = 0; index < line.Length; index++)
        {
            int left = index >= bytesPerPixel ? line[index - bytesPerPixel] : 0;
            int up = previous[index];
            int upLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : 0;
            var predictor = filter switch
            {
                0 => 0,
                1 => left,
                2 => up,
                3 => (left + up) / 2,
                4 => Paeth(left, up, upLeft),
                _ => throw new InvalidDataException($"unknown PNG filter {filter}"),
            };
            line[index] = (byte)(line[index] + predictor);
        }
    }

    static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var (toLeft, toUp, toUpLeft) = (Math.Abs(estimate - left), Math.Abs(estimate - up), Math.Abs(estimate - upLeft));
        if (toLeft <= toUp && toLeft <= toUpLeft) return left;
        return toUp <= toUpLeft ? up : upLeft;
    }
}
