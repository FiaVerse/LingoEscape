using System;
using System.IO;
using UnityEngine;

namespace QuestCameraKit.OpenAI
{
    public static class SaveWav
    {
        public static byte[] Save(string filename, AudioClip clip)
        {
            if (!clip)
            {
                Debug.LogError("SaveWav: AudioClip is null! Cannot save.");
                return null;
            }

            if (!filename.ToLower().EndsWith(".wav"))
            {
                filename += ".wav";
            }

            using var memoryStream = CreateEmptyWavFile();
            ConvertAndWrite(memoryStream, clip);
            WriteWavHeader(memoryStream, clip);
            return memoryStream.ToArray();
        }

        private static MemoryStream CreateEmptyWavFile()
        {
            var memoryStream = new MemoryStream();
            for (var i = 0; i < 44; i++)
            {
                memoryStream.WriteByte(0);
            }

            return memoryStream;
        }

        private static void ConvertAndWrite(MemoryStream memoryStream, AudioClip clip)
        {
            if (!clip)
            {
                Debug.LogError("SaveWav: AudioClip is null! Cannot convert.");
                return;
            }

            var samples = new float[clip.samples];
            clip.GetData(samples, 0);

            var intData = new short[samples.Length];
            var bytesData = new byte[samples.Length * 2];

            var rescaleFactor = 32767;
            for (var i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * rescaleFactor);
                BitConverter.GetBytes(intData[i]).CopyTo(bytesData, i * 2);
            }

            memoryStream.Write(bytesData, 0, bytesData.Length);
        }

        private static void WriteWavHeader(MemoryStream memoryStream, AudioClip clip)
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
            memoryStream.Write(BitConverter.GetBytes(memoryStream.Length - 8), 0, 4);
            memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
            memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
            memoryStream.Write(BitConverter.GetBytes(16), 0, 4);
            memoryStream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
            memoryStream.Write(BitConverter.GetBytes(clip.channels), 0, 2);
            memoryStream.Write(BitConverter.GetBytes(clip.frequency), 0, 4);
            memoryStream.Write(BitConverter.GetBytes(clip.frequency * clip.channels * 2), 0, 4);
            memoryStream.Write(BitConverter.GetBytes((ushort)(clip.channels * 2)), 0, 2);
            memoryStream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
            memoryStream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
            memoryStream.Write(BitConverter.GetBytes(clip.samples * clip.channels * 2), 0, 4);
        }
    }
}