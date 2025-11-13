using System;
using SbcSharp;

namespace SbcSharp.Example;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("SbcSharp Example - SBC Encoder/Decoder Demo");
        Console.WriteLine("============================================\n");

        // Example 1: Encode and decode a sine wave
        EncodeSineWave();

        // Example 2: Show configuration options
        ShowConfigurations();
    }

    static void EncodeSineWave()
    {
        Console.WriteLine("Example 1: Encoding and Decoding a 440Hz Sine Wave");
        Console.WriteLine("--------------------------------------------------");

        // Create encoder with A2DP high quality settings
        var encoder = new SbcEncoder();
        encoder.Configure(
            samplingFrequency: SbcConstants.Freq44100,
            blocks: 16,
            channelMode: SbcChannelMode.Stereo,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 53
        );

        // Generate a 440Hz sine wave (one frame worth of samples)
        int samplesPerFrame = 16 * 8 * 2; // blocks * subbands * channels
        short[] pcmInput = new short[samplesPerFrame];
        
        for (int i = 0; i < samplesPerFrame; i++)
        {
            double t = i / 44100.0;
            double sample = Math.Sin(2 * Math.PI * 440 * t) * 16000;
            pcmInput[i] = (short)sample;
        }

        // Encode
        byte[] sbcData = new byte[512];
        int bytesEncoded = encoder.Encode(pcmInput, 0, sbcData, 0);
        
        Console.WriteLine($"Input: {pcmInput.Length} PCM samples (16-bit)");
        Console.WriteLine($"Encoded: {bytesEncoded} bytes of SBC data");
        Console.WriteLine($"Compression ratio: {(pcmInput.Length * 2.0 / bytesEncoded):F2}:1");

        // Decode
        var decoder = new SbcDecoder();
        short[] pcmOutput = new short[samplesPerFrame];
        int bytesDecoded = decoder.Decode(sbcData, 0, pcmOutput, 0);

        Console.WriteLine($"Decoded: {bytesDecoded} bytes consumed, {pcmOutput.Length} PCM samples recovered");

        // Calculate error
        double maxError = 0;
        double sumSquaredError = 0;
        for (int i = 0; i < Math.Min(pcmInput.Length, pcmOutput.Length); i++)
        {
            double error = Math.Abs(pcmInput[i] - pcmOutput[i]);
            maxError = Math.Max(maxError, error);
            sumSquaredError += error * error;
        }
        double rmse = Math.Sqrt(sumSquaredError / pcmInput.Length);

        Console.WriteLine($"Max error: {maxError:F1} samples");
        Console.WriteLine($"RMS error: {rmse:F1} samples");
        Console.WriteLine($"SNR: {(20 * Math.Log10(16000 / rmse)):F1} dB\n");
    }

    static void ShowConfigurations()
    {
        Console.WriteLine("Example 2: Common SBC Configurations");
        Console.WriteLine("------------------------------------");

        var configs = new[]
        {
            new
            {
                Name = "A2DP High Quality",
                Freq = SbcConstants.Freq44100,
                Blocks = 16,
                Mode = SbcChannelMode.Stereo,
                Subbands = 8,
                Bitpool = 53,
                Bitrate = 328
            },
            new
            {
                Name = "A2DP Medium Quality",
                Freq = SbcConstants.Freq44100,
                Blocks = 16,
                Mode = SbcChannelMode.Stereo,
                Subbands = 8,
                Bitpool = 35,
                Bitrate = 229
            },
            new
            {
                Name = "HFP/HSP Mono",
                Freq = SbcConstants.Freq16000,
                Blocks = 16,
                Mode = SbcChannelMode.Mono,
                Subbands = 8,
                Bitpool = 26,
                Bitrate = 56
            },
            new
            {
                Name = "Low Latency",
                Freq = SbcConstants.Freq44100,
                Blocks = 4,
                Mode = SbcChannelMode.Stereo,
                Subbands = 4,
                Bitpool = 32,
                Bitrate = 353
            }
        };

        foreach (var config in configs)
        {
            Console.WriteLine($"\n{config.Name}:");
            Console.WriteLine($"  Sampling: {config.Freq / 1000.0:F1} kHz");
            Console.WriteLine($"  Blocks: {config.Blocks}, Subbands: {config.Subbands}");
            Console.WriteLine($"  Mode: {config.Mode}");
            Console.WriteLine($"  Bitpool: {config.Bitpool}");
            Console.WriteLine($"  Approximate bitrate: ~{config.Bitrate} kbps");
        }

        Console.WriteLine("\nNote: Actual bitrates may vary slightly based on content.");
    }
}
