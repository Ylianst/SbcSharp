using System;
using System.IO;
using System.Text;

namespace SBC;

/// <summary>
/// Simple WAV file reader/writer for 16-bit PCM audio
/// </summary>
public class WavFile
{
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public short[] Samples { get; set; } = Array.Empty<short>();

    /// <summary>
    /// Read a WAV file
    /// </summary>
    public static WavFile? Read(string filename)
    {
        try
        {
            using var fs = File.OpenRead(filename);
            using var br = new BinaryReader(fs);

            // Read RIFF header
            string riff = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (riff != "RIFF")
            {
                Console.WriteLine("Error: Not a valid WAV file (missing RIFF header)");
                return null;
            }

            int fileSize = br.ReadInt32();
            string wave = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (wave != "WAVE")
            {
                Console.WriteLine("Error: Not a valid WAV file (missing WAVE header)");
                return null;
            }

            // Read fmt chunk
            string fmt = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (fmt != "fmt ")
            {
                Console.WriteLine("Error: Cannot find fmt chunk");
                return null;
            }

            int fmtSize = br.ReadInt32();
            int audioFormat = br.ReadInt16();
            int channels = br.ReadInt16();
            int sampleRate = br.ReadInt32();
            int byteRate = br.ReadInt32();
            int blockAlign = br.ReadInt16();
            int bitsPerSample = br.ReadInt16();

            if (audioFormat != 1)
            {
                Console.WriteLine($"Error: Only PCM format supported (found format {audioFormat})");
                return null;
            }

            if (bitsPerSample != 16)
            {
                Console.WriteLine($"Error: Only 16-bit samples supported (found {bitsPerSample}-bit)");
                return null;
            }

            // Skip any extra fmt bytes
            if (fmtSize > 16)
                br.ReadBytes(fmtSize - 16);

            // Find data chunk
            while (fs.Position < fs.Length)
            {
                string chunkId = Encoding.ASCII.GetString(br.ReadBytes(4));
                int chunkSize = br.ReadInt32();

                if (chunkId == "data")
                {
                    // Read sample data
                    int sampleCount = chunkSize / 2;
                    short[] samples = new short[sampleCount];

                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = br.ReadInt16();

                    return new WavFile
                    {
                        SampleRate = sampleRate,
                        Channels = channels,
                        Samples = samples
                    };
                }
                else
                {
                    // Skip unknown chunk
                    br.ReadBytes(chunkSize);
                }
            }

            Console.WriteLine("Error: Cannot find data chunk");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading WAV file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Write a WAV file
    /// </summary>
    public bool Write(string filename)
    {
        try
        {
            using var fs = File.Create(filename);
            using var bw = new BinaryWriter(fs);

            int dataSize = Samples.Length * 2;
            int fileSize = 36 + dataSize;

            // Write RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(fileSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // Write fmt chunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16); // fmt chunk size
            bw.Write((short)1); // PCM format
            bw.Write((short)Channels);
            bw.Write(SampleRate);
            bw.Write(SampleRate * Channels * 2); // byte rate
            bw.Write((short)(Channels * 2)); // block align
            bw.Write((short)16); // bits per sample

            // Write data chunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            foreach (short sample in Samples)
                bw.Write(sample);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing WAV file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get samples for a specific channel (interleaved data)
    /// </summary>
    public short[] GetChannelSamples(int channel)
    {
        if (channel >= Channels)
            throw new ArgumentException($"Channel {channel} does not exist (file has {Channels} channels)");

        int samplesPerChannel = Samples.Length / Channels;
        short[] channelSamples = new short[samplesPerChannel];

        for (int i = 0; i < samplesPerChannel; i++)
            channelSamples[i] = Samples[i * Channels + channel];

        return channelSamples;
    }

    /// <summary>
    /// Create WAV from separate channel data
    /// </summary>
    public static WavFile FromChannels(int sampleRate, params short[][] channels)
    {
        int numChannels = channels.Length;
        int samplesPerChannel = channels[0].Length;

        // Verify all channels have same length
        for (int i = 1; i < numChannels; i++)
        {
            if (channels[i].Length != samplesPerChannel)
                throw new ArgumentException("All channels must have the same number of samples");
        }

        // Interleave samples
        short[] interleaved = new short[samplesPerChannel * numChannels];
        for (int i = 0; i < samplesPerChannel; i++)
        {
            for (int ch = 0; ch < numChannels; ch++)
                interleaved[i * numChannels + ch] = channels[ch][i];
        }

        return new WavFile
        {
            SampleRate = sampleRate,
            Channels = numChannels,
            Samples = interleaved
        };
    }
}
