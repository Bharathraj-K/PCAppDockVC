using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] bytesData = ConvertTo16Bit(samples);
        byte[] header = GetWavHeader(bytesData.Length, clip.channels, clip.frequency);

        byte[] wavFile = new byte[header.Length + bytesData.Length];
        Buffer.BlockCopy(header, 0, wavFile, 0, header.Length);
        Buffer.BlockCopy(bytesData, 0, wavFile, header.Length, bytesData.Length);

        return wavFile;
    }

    private static byte[] ConvertTo16Bit(float[] samples)
    {
        var byteArr = new byte[samples.Length * 2];
        int i = 0;
        foreach (var f in samples)
        {
            short s = (short)(Mathf.Clamp(f, -1f, 1f) * short.MaxValue);
            byteArr[i++] = (byte)(s & 0x00ff);
            byteArr[i++] = (byte)((s & 0xff00) >> 8);
        }
        return byteArr;
    }

    private static byte[] GetWavHeader(int dataLength, int channels, int sampleRate)
    {
        int totalLength = 44 + dataLength;

        MemoryStream stream = new MemoryStream(44);
        BinaryWriter writer = new BinaryWriter(stream);

        writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
        writer.Write(totalLength - 8);
        writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16); // Bits per sample
        writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
        writer.Write(dataLength);

        return stream.ToArray();
    }
}
