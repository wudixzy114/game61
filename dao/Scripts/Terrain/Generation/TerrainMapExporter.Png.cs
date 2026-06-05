using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using Godot;

namespace Dao.Terrain.Generation;

public static partial class TerrainMapExporter
{
    private static string FileSystemPath(string path)
    {
        return path.Contains("://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }

    private static void WritePng(Stream stream, TerrainMapRaster raster)
    {
        if (raster.Width <= 0 || raster.Height <= 0 || raster.PixelCount < raster.Width * raster.Height)
        {
            throw new ArgumentException("Terrain PNG raster dimensions are invalid.", nameof(raster));
        }

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[0..4], raster.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], raster.Height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WritePngChunk(stream, "IHDR", ihdr);

        using var idatStream = new MemoryStream(Math.Max(1024, raster.Height * ((raster.Width * 4) + 1)));
        using (var deflate = new ZLibStream(idatStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] row = new byte[(raster.Width * 4) + 1];
            for (int y = 0; y < raster.Height; y++)
            {
                row[0] = 0;
                int cursor = 1;
                for (int x = 0; x < raster.Width; x++)
                {
                    Color color = raster.GetPixel(x, y);
                    row[cursor++] = ColorByte(color.R);
                    row[cursor++] = ColorByte(color.G);
                    row[cursor++] = ColorByte(color.B);
                    row[cursor++] = ColorByte(color.A);
                }

                deflate.Write(row, 0, row.Length);
            }
        }

        WritePngChunk(stream, "IDAT", idatStream.ToArray());
        WritePngChunk(stream, "IEND", ReadOnlySpan<byte>.Empty);
    }

    private static byte ColorByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(value, 0.0f, 1.0f) * 255.0f), 0, 255);
    }

    private static void WritePngChunk(Stream stream, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        Span<byte> typeBytes = stackalloc byte[4];
        typeBytes[0] = (byte)type[0];
        typeBytes[1] = (byte)type[1];
        typeBytes[2] = (byte)type[2];
        typeBytes[3] = (byte)type[3];
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = UpdatePngCrc(0xFFFFFFFFu, typeBytes);
        crc = UpdatePngCrc(crc, data) ^ 0xFFFFFFFFu;

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint UpdatePngCrc(uint crc, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            crc = PngCrcTable[(crc ^ data[i]) & 0xFFu] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildPngCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1u) != 0u
                    ? 0xEDB88320u ^ (c >> 1)
                    : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
