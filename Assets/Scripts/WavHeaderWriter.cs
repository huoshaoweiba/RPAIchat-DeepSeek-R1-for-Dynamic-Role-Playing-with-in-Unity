using System;
using System.IO;
using System.Text;

public static class WavHeaderWriter
{
    const int HEADER_SIZE = 44; // 标准WAV头大小

    public static void WriteWavHeader(FileStream stream, int sampleRate, int channels, int bitsPerSample, int dataSize)
    {
        stream.Seek(0, SeekOrigin.Begin);

        // RIFF块
        WriteString(stream, "RIFF");
        WriteInt(stream, dataSize + 36); // 文件总大小-8
        WriteString(stream, "WAVE");

        // fmt块
        WriteString(stream, "fmt ");
        WriteInt(stream, 16); // fmt块大小
        WriteShort(stream, 1); // PCM格式=1
        WriteShort(stream, (short)channels); // 声道数
        WriteInt(stream, sampleRate); // 采样率
        WriteInt(stream, sampleRate * channels * bitsPerSample / 8); // 字节率
        WriteShort(stream, (short)(channels * bitsPerSample / 8)); // 块对齐
        WriteShort(stream, (short)bitsPerSample); // 位深度

        // data块
        WriteString(stream, "data");
        WriteInt(stream, dataSize); // 音频数据大小
    }

    private static void WriteString(FileStream stream, string s)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(s);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteInt(FileStream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteShort(FileStream stream, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}