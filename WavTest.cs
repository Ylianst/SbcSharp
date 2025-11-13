using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SBC;

/// <summary>
/// Test program for encoding and decoding WAV files using SBC codec
/// </summary>
public class WavTest
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== SBC WAV File Encoder/Decoder Test ===\n");

        if (args.Length < 1)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run <input.wav> [output.wav] [quality]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  input.wav  - Input WAV file to encode/decode");
            Console.WriteLine("  output.wav - Output WAV file (default: output.wav)");
            Console.WriteLine("  quality    - low, medium, high (default: high)");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run test.wav decoded.wav high");
            Console.WriteLine();
            Console.WriteLine("Generating test WAV file instead...");
            Console.WriteLine();
            GenerateTestWavFile("test_input.wav");
            TestWavFile("test_input.wav", "test_output.wav", "high");
        }
        else
        {
            string inputFile = args[0];
            string outputFile = args.Length > 1 ? args[1] : "output.wav";
            string quality = args.Length > 2 ? args[2] : "high";

            TestWavFile(inputFile, outputFile, quality);
        }
    }

    static void GenerateTestWavFile(string filename)
    {
        Console.WriteLine($"Generating test WAV file: {filename}");
        
        int sampleRate = 44100;
        double duration = 3.0; // 3 seconds
        int samples = (int)(sampleRate * duration);

        short[] leftChannel = new short[samples];
        short[] rightChannel = new short[samples];

        // Generate test tones
        double amplitude = 16384;
        for (int i = 0; i < samples; i++)
        {
            double t = i / (double)sampleRate;
            
            // Left: 440 Hz (A4 note)
            leftChannel[i] = (short)(amplitude * Math.Sin(2 * Math.PI * 440 * t));
            
            // Right: 554.37 Hz (C#5 note)
            rightChannel[i] = (short)(amplitude * Math.Sin(2 * Math.PI * 554.37 * t));
        }

        var wav = WavFile.FromChannels(sampleRate, leftChannel, rightChannel);
        if (wav.Write(filename))
        {
            Console.WriteLine($"✓ Generated {filename}");
            Console.WriteLine($"  Sample rate: {sampleRate} Hz");
            Console.WriteLine($"  Duration: {duration:F1} seconds");
            Console.WriteLine($"  Channels: 2 (stereo)");
            Console.WriteLine();
        }
    }

    static void TestWavFile(string inputFile, string outputFile, string quality)
    {
        Console.WriteLine($"Input file: {inputFile}");
        Console.WriteLine($"Output file: {outputFile}");
        Console.WriteLine($"Quality: {quality}");
        Console.WriteLine();

        // Read input WAV file
        Console.WriteLine("Reading input WAV file...");
        var inputWav = WavFile.Read(inputFile);
        if (inputWav == null)
        {
            Console.WriteLine("Failed to read input file!");
            return;
        }

        Console.WriteLine($"✓ Input WAV properties:");
        Console.WriteLine($"  Sample rate: {inputWav.SampleRate} Hz");
        Console.WriteLine($"  Channels: {inputWav.Channels}");
        Console.WriteLine($"  Total samples: {inputWav.Samples.Length:N0}");
        Console.WriteLine($"  Duration: {inputWav.Samples.Length / inputWav.Channels / (double)inputWav.SampleRate:F2} seconds");
        Console.WriteLine();

        // Determine SBC frame configuration
        var frame = GetFrameConfig(inputWav.SampleRate, inputWav.Channels, quality);
        if (frame == null || !frame.IsValid())
        {
            Console.WriteLine("Failed to create valid frame configuration!");
            return;
        }

        Console.WriteLine($"SBC Configuration:");
        Console.WriteLine($"  Frequency: {frame.GetFrequencyHz()} Hz");
        Console.WriteLine($"  Mode: {frame.Mode}");
        Console.WriteLine($"  Blocks: {frame.Blocks}, Subbands: {frame.Subbands}");
        Console.WriteLine($"  Bitpool: {frame.Bitpool}");
        Console.WriteLine($"  Frame size: {frame.GetFrameSize()} bytes");
        Console.WriteLine($"  Bitrate: {frame.GetBitrate()} bps");
        Console.WriteLine($"  Samples per frame: {frame.Blocks * frame.Subbands}");
        Console.WriteLine();

        // Encode
        Console.WriteLine("Encoding...");
        var sw = Stopwatch.StartNew();
        var encoded = EncodeWav(inputWav, frame);
        sw.Stop();

        if (encoded == null || encoded.Count == 0)
        {
            Console.WriteLine("Encoding failed!");
            return;
        }

        int totalEncodedBytes = 0;
        foreach (var frameData in encoded)
            totalEncodedBytes += frameData.Length;

        double compressionRatio = (double)(inputWav.Samples.Length * 2) / totalEncodedBytes;
        Console.WriteLine($"✓ Encoded {encoded.Count} frames in {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  Original size: {inputWav.Samples.Length * 2:N0} bytes");
        Console.WriteLine($"  Encoded size: {totalEncodedBytes:N0} bytes");
        Console.WriteLine($"  Compression ratio: {compressionRatio:F2}:1");
        Console.WriteLine($"  Encoding speed: {(inputWav.Samples.Length / inputWav.Channels / (double)inputWav.SampleRate) / sw.Elapsed.TotalSeconds:F1}x realtime");
        Console.WriteLine();

        // Decode
        Console.WriteLine("Decoding...");
        sw.Restart();
        var decoded = DecodeFrames(encoded, frame);
        sw.Stop();

        if (decoded == null)
        {
            Console.WriteLine("Decoding failed!");
            return;
        }

        Console.WriteLine($"✓ Decoded in {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  Decoded samples: {decoded.Samples.Length:N0}");
        Console.WriteLine($"  Decoding speed: {(decoded.Samples.Length / decoded.Channels / (double)decoded.SampleRate) / sw.Elapsed.TotalSeconds:F1}x realtime");
        Console.WriteLine();

        // Calculate quality metrics
        Console.WriteLine("Calculating quality metrics...");
        CalculateQualityMetrics(inputWav, decoded);
        Console.WriteLine();

        // Write output file
        Console.WriteLine($"Writing output file: {outputFile}");
        if (decoded.Write(outputFile))
        {
            Console.WriteLine($"✓ Successfully written to {outputFile}");
            Console.WriteLine();
            Console.WriteLine("=== Test completed successfully! ===");
        }
        else
        {
            Console.WriteLine("Failed to write output file!");
        }
    }

    static SbcFrame? GetFrameConfig(int sampleRate, int channels, string quality)
    {
        // Map sample rate to SBC frequency
        SbcFrequency freq = sampleRate switch
        {
            16000 => SbcFrequency.Freq16K,
            32000 => SbcFrequency.Freq32K,
            44100 => SbcFrequency.Freq44K1,
            48000 => SbcFrequency.Freq48K,
            _ => SbcFrequency.Freq44K1 // Default
        };

        // Map channels to mode
        SbcMode mode = channels == 1 ? SbcMode.Mono : SbcMode.JointStereo;

        // Get bitpool based on quality
        int bitpool = quality.ToLower() switch
        {
            "low" => 30,
            "medium" => 40,
            "high" => 53,
            _ => 53
        };

        return new SbcFrame
        {
            Frequency = freq,
            Mode = mode,
            AllocationMethod = SbcBitAllocationMethod.Loudness,
            Blocks = 16,
            Subbands = 8,
            Bitpool = bitpool
        };
    }

    static List<byte[]>? EncodeWav(WavFile wav, SbcFrame frameConfig)
    {
        var encoder = new SbcEncoder();
        var frames = new List<byte[]>();
        
        int samplesPerFrame = frameConfig.Blocks * frameConfig.Subbands;
        int samplesPerChannel = wav.Samples.Length / wav.Channels;
        
        // Extract channel data
        short[] leftChannel = wav.GetChannelSamples(0);
        short[]? rightChannel = wav.Channels > 1 ? wav.GetChannelSamples(1) : null;

        // Encode frame by frame
        for (int offset = 0; offset < samplesPerChannel; offset += samplesPerFrame)
        {
            int remaining = samplesPerChannel - offset;
            if (remaining < samplesPerFrame)
            {
                // Pad last frame with zeros
                short[] paddedLeft = new short[samplesPerFrame];
                short[]? paddedRight = rightChannel != null ? new short[samplesPerFrame] : null;
                
                Array.Copy(leftChannel, offset, paddedLeft, 0, remaining);
                if (rightChannel != null && paddedRight != null)
                    Array.Copy(rightChannel, offset, paddedRight, 0, remaining);

                byte[]? frame = encoder.Encode(paddedLeft, paddedRight, frameConfig);
                if (frame != null)
                    frames.Add(frame);
            }
            else
            {
                // Normal frame
                short[] frameLeft = new short[samplesPerFrame];
                short[]? frameRight = rightChannel != null ? new short[samplesPerFrame] : null;
                
                Array.Copy(leftChannel, offset, frameLeft, 0, samplesPerFrame);
                if (rightChannel != null && frameRight != null)
                    Array.Copy(rightChannel, offset, frameRight, 0, samplesPerFrame);

                byte[]? frame = encoder.Encode(frameLeft, frameRight, frameConfig);
                if (frame != null)
                    frames.Add(frame);
                else
                    return null;
            }
        }

        return frames;
    }

    static WavFile? DecodeFrames(List<byte[]> frames, SbcFrame frameConfig)
    {
        var decoder = new SbcDecoder();
        var leftSamples = new List<short>();
        var rightSamples = new List<short>();

        foreach (var frameData in frames)
        {
            bool success = decoder.Decode(frameData, out short[] left, out short[]? right, out _);
            if (!success)
                return null;

            leftSamples.AddRange(left);
            if (right != null)
                rightSamples.AddRange(right);
        }

        if (frameConfig.Mode == SbcMode.Mono)
        {
            return WavFile.FromChannels(frameConfig.GetFrequencyHz(), leftSamples.ToArray());
        }
        else
        {
            return WavFile.FromChannels(frameConfig.GetFrequencyHz(), 
                leftSamples.ToArray(), rightSamples.ToArray());
        }
    }

    static void CalculateQualityMetrics(WavFile original, WavFile decoded)
    {
        // Get channel data
        short[] origLeft = original.GetChannelSamples(0);
        short[] decLeft = decoded.GetChannelSamples(0);

        // Trim to same length
        int minLen = Math.Min(origLeft.Length, decLeft.Length);
        
        // Calculate MSE and PSNR for left channel
        double mse = 0;
        long maxDiff = 0;
        
        for (int i = 0; i < minLen; i++)
        {
            long diff = origLeft[i] - decLeft[i];
            mse += diff * diff;
            if (Math.Abs(diff) > maxDiff)
                maxDiff = Math.Abs(diff);
        }
        
        mse /= minLen;
        double psnr = mse > 0 ? 20 * Math.Log10(32768.0 / Math.Sqrt(mse)) : double.PositiveInfinity;

        Console.WriteLine($"Quality Metrics (Left Channel):");
        Console.WriteLine($"  MSE: {mse:F2}");
        Console.WriteLine($"  PSNR: {psnr:F2} dB");
        Console.WriteLine($"  Max difference: {maxDiff} / 32768");

        // Right channel if stereo
        if (original.Channels > 1 && decoded.Channels > 1)
        {
            short[] origRight = original.GetChannelSamples(1);
            short[] decRight = decoded.GetChannelSamples(1);
            
            mse = 0;
            maxDiff = 0;
            
            for (int i = 0; i < minLen; i++)
            {
                long diff = origRight[i] - decRight[i];
                mse += diff * diff;
                if (Math.Abs(diff) > maxDiff)
                    maxDiff = Math.Abs(diff);
            }
            
            mse /= minLen;
            psnr = mse > 0 ? 20 * Math.Log10(32768.0 / Math.Sqrt(mse)) : double.PositiveInfinity;

            Console.WriteLine($"Quality Metrics (Right Channel):");
            Console.WriteLine($"  MSE: {mse:F2}");
            Console.WriteLine($"  PSNR: {psnr:F2} dB");
            Console.WriteLine($"  Max difference: {maxDiff} / 32768");
        }
    }
}
