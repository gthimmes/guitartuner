using System.Buffers.Binary;

namespace Tuner.Diagnostics;

/// <summary>
/// Minimal RIFF/WAVE reader and writer. Reads 16/24/32-bit PCM and 32-bit float,
/// returns mono float samples (channels averaged). Writes 32-bit float mono.
/// </summary>
public static class WavIo
{
    public sealed record WavData(float[] Samples, int SampleRate);

    public static WavData Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var span = bytes.AsSpan();

        if (span.Length < 12 || !Ascii(span, 0, "RIFF") || !Ascii(span, 8, "WAVE"))
            throw new InvalidDataException($"Not a RIFF/WAVE file: {path}");

        int pos = 12;
        int channels = 0, sampleRate = 0, bitsPerSample = 0;
        ushort audioFormat = 0; // 1 = PCM, 3 = IEEE float
        ReadOnlySpan<byte> dataChunk = default;

        while (pos + 8 <= span.Length)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(span.Slice(pos, 4));
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(pos + 4, 4));
            int body = pos + 8;
            if (body + chunkSize > span.Length) chunkSize = span.Length - body; // tolerate truncation

            if (chunkId == "fmt ")
            {
                audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(body + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 14, 2));
                // WAVE_FORMAT_EXTENSIBLE: real format sits in the subformat GUID's first 2 bytes
                if (audioFormat == 0xFFFE && chunkSize >= 26)
                    audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 24, 2));
            }
            else if (chunkId == "data")
            {
                dataChunk = span.Slice(body, chunkSize);
            }

            pos = body + chunkSize + (chunkSize & 1); // chunks are word-aligned
        }

        if (channels == 0 || sampleRate == 0 || dataChunk.IsEmpty)
            throw new InvalidDataException($"Missing fmt/data chunk: {path}");

        int bytesPerSample = bitsPerSample / 8;
        int frameCount = dataChunk.Length / (bytesPerSample * channels);
        float[] mono = new float[frameCount];

        for (int f = 0; f < frameCount; f++)
        {
            double acc = 0;
            for (int c = 0; c < channels; c++)
            {
                int off = (f * channels + c) * bytesPerSample;
                acc += ReadSample(dataChunk, off, bitsPerSample, audioFormat);
            }
            mono[f] = (float)(acc / channels);
        }

        return new WavData(mono, sampleRate);
    }

    private static double ReadSample(ReadOnlySpan<byte> data, int off, int bits, ushort format)
    {
        if (format == 3) // IEEE float
            return BinaryPrimitives.ReadSingleLittleEndian(data.Slice(off, 4));

        return bits switch
        {
            16 => BinaryPrimitives.ReadInt16LittleEndian(data.Slice(off, 2)) / 32768.0,
            24 => Sign24((data[off]) | (data[off + 1] << 8) | (data[off + 2] << 16)) / 8388608.0,
            32 => BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off, 4)) / 2147483648.0,
            8 => (data[off] - 128) / 128.0,
            _ => 0
        };
    }

    private static int Sign24(int v) => (v & 0x800000) != 0 ? v | unchecked((int)0xFF000000) : v;

    public static void WriteFloatMono(string path, ReadOnlySpan<float> samples, int sampleRate)
    {
        int dataBytes = samples.Length * 4;
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        w.Write("RIFF"u8); w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((ushort)3); // IEEE float
        w.Write((ushort)1);                                 // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 4);                            // byte rate
        w.Write((ushort)4); w.Write((ushort)32);
        w.Write("data"u8); w.Write(dataBytes);
        foreach (float s in samples) w.Write(s);
    }

    private static bool Ascii(ReadOnlySpan<byte> span, int off, string tag)
    {
        for (int i = 0; i < tag.Length; i++)
            if (span[off + i] != tag[i]) return false;
        return true;
    }
}
