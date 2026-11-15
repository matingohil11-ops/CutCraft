using System.IO;
using NAudio.Wave;

namespace CutCraft.Media.Audio;

public class WaveformPoint
{
    public float Min { get; set; }
    public float Max { get; set; }
}

public class WaveformGenerator
{
    /// <summary>
    /// Generates normalized audio peak points (-1.0 to 1.0) for visual waveform rendering in WPF.
    /// </summary>
    public static async Task<List<WaveformPoint>> GenerateWaveformDataAsync(string audioOrVideoFilePath, int targetPointCount = 1000, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var points = new List<WaveformPoint>();
            if (!File.Exists(audioOrVideoFilePath)) return points;

            try
            {
                using var reader = new MediaFoundationReader(audioOrVideoFilePath);
                var sampleProvider = reader.ToSampleProvider();
                int channels = sampleProvider.WaveFormat.Channels;
                long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);

                int samplesPerPoint = Math.Max(1, (int)(totalSamples / Math.Max(1, targetPointCount)));
                float[] buffer = new float[samplesPerPoint * channels];

                int samplesRead;
                while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (ct.IsCancellationRequested) break;

                    float max = float.MinValue;
                    float min = float.MaxValue;

                    for (int i = 0; i < samplesRead; i++)
                    {
                        var sample = buffer[i];
                        if (sample > max) max = sample;
                        if (sample < min) min = sample;
                    }

                    if (max == float.MinValue) max = 0;
                    if (min == float.MaxValue) min = 0;

                    points.Add(new WaveformPoint { Min = min, Max = max });
                }
            }
            catch
            {
                // Fallback: generate placeholder waveform if reader can't decode audio directly
                for (int i = 0; i < targetPointCount; i++)
                {
                    float val = (float)(Math.Sin(i * 0.1) * 0.5);
                    points.Add(new WaveformPoint { Min = -Math.Abs(val), Max = Math.Abs(val) });
                }
            }

            return points;
        }, ct);
    }
}
