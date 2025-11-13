using System;
using SBC;

/// <summary>
/// Example program demonstrating SBC encoder and decoder usage
/// </summary>
public class SbcExample
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== SBC Codec Example ===\n");

        // Example 1: Encode and decode stereo audio
        EncodeDecodeStereo();

        Console.WriteLine();

        // Example 2: Test mSBC (Bluetooth HFP)
        TestMsbc();

        Console.WriteLine();

        // Example 3: Probe frame information
        ProbeFrameInfo();

        Console.WriteLine("\n=== All examples completed ===");
    }

    static void EncodeDecodeStereo()
    {
        Console.WriteLine("Example 1: Stereo Encoding/Decoding");
        Console.WriteLine("------------------------------------");

        // Create encoder and decoder
        var encoder = new SbcEncoder();
        var decoder = new SbcDecoder();

        // Configure frame for high-quality stereo
        var frame = new SbcFrame
        {
            Frequency = SbcFrequency.Freq44K1,
            Mode = SbcMode.JointStereo,
            AllocationMethod = SbcBitAllocationMethod.Loudness,
            Blocks = 16,
            Subbands = 8,
            Bitpool = 53
        };

        // Generate test PCM data (sine wave)
        int samplesPerChannel = frame.Blocks * frame.Subbands; // 128 samples
        short[] pcmLeft = GenerateSineWave(samplesPerChannel, 440.0, frame.GetFrequencyHz());
        short[] pcmRight = GenerateSineWave(samplesPerChannel, 880.0, frame.GetFrequencyHz());

        Console.WriteLine($"Input: {samplesPerChannel} samples per channel");
        Console.WriteLine($"Sampling rate: {frame.GetFrequencyHz()} Hz");
        Console.WriteLine($"Configuration: {frame.Blocks} blocks, {frame.Subbands} subbands, bitpool {frame.Bitpool}");

        // Encode
        byte[]? encoded = encoder.Encode(pcmLeft, pcmRight, frame);
        if (encoded == null)
        {
            Console.WriteLine("ERROR: Encoding failed!");
            return;
        }

        Console.WriteLine($"Encoded frame size: {encoded.Length} bytes");
        Console.WriteLine($"Bitrate: {frame.GetBitrate()} bps");
        Console.WriteLine($"Compression ratio: {(samplesPerChannel * 4.0) / encoded.Length:F2}:1");

        // Decode
        bool success = decoder.Decode(encoded, out short[] decodedLeft, out short[]? decodedRight, out SbcFrame decodedFrame);
        if (!success)
        {
            Console.WriteLine("ERROR: Decoding failed!");
            return;
        }

        Console.WriteLine($"Decoded: {decodedLeft.Length} samples per channel");
        Console.WriteLine($"Mode: {decodedFrame.Mode}");

        // Calculate error (MSE)
        double mseLeft = CalculateMSE(pcmLeft, decodedLeft);
        double mseRight = decodedRight != null ? CalculateMSE(pcmRight, decodedRight) : 0;
        Console.WriteLine($"MSE Left: {mseLeft:F2}, MSE Right: {mseRight:F2}");
    }

    static void TestMsbc()
    {
        Console.WriteLine("Example 2: mSBC (Bluetooth HFP)");
        Console.WriteLine("--------------------------------");

        var encoder = new SbcEncoder();
        var decoder = new SbcDecoder();

        // Create mSBC configuration
        var msbcFrame = SbcFrame.CreateMsbc();

        Console.WriteLine($"mSBC Configuration:");
        Console.WriteLine($"  Frequency: {msbcFrame.GetFrequencyHz()} Hz");
        Console.WriteLine($"  Mode: {msbcFrame.Mode}");
        Console.WriteLine($"  Blocks: {msbcFrame.Blocks}, Subbands: {msbcFrame.Subbands}");
        Console.WriteLine($"  Bitpool: {msbcFrame.Bitpool}");

        // Generate mono test data
        int samples = msbcFrame.Blocks * msbcFrame.Subbands; // 120 samples
        short[] pcmMono = GenerateSineWave(samples, 1000.0, msbcFrame.GetFrequencyHz());

        // Encode
        byte[]? encoded = encoder.Encode(pcmMono, null, msbcFrame);
        if (encoded == null)
        {
            Console.WriteLine("ERROR: mSBC encoding failed!");
            return;
        }

        Console.WriteLine($"mSBC frame size: {encoded.Length} bytes (expected: 57)");
        Console.WriteLine($"First bytes: 0x{encoded[0]:X2} (syncword, expected: 0xAD)");

        // Decode
        bool success = decoder.Decode(encoded, out short[] decoded, out _, out _);
        if (!success)
        {
            Console.WriteLine("ERROR: mSBC decoding failed!");
            return;
        }

        Console.WriteLine($"Successfully decoded {decoded.Length} samples");
        double mse = CalculateMSE(pcmMono, decoded);
        Console.WriteLine($"MSE: {mse:F2}");
    }

    static void ProbeFrameInfo()
    {
        Console.WriteLine("Example 3: Frame Probing");
        Console.WriteLine("------------------------");

        var decoder = new SbcDecoder();
        var encoder = new SbcEncoder();

        // Create a test frame
        var frame = new SbcFrame
        {
            Frequency = SbcFrequency.Freq48K,
            Mode = SbcMode.Stereo,
            AllocationMethod = SbcBitAllocationMethod.SNR,
            Blocks = 12,
            Subbands = 4,
            Bitpool = 30
        };

        // Encode some data
        int samples = frame.Blocks * frame.Subbands;
        short[] pcm = GenerateSineWave(samples, 440.0, frame.GetFrequencyHz());
        byte[]? encoded = encoder.Encode(pcm, pcm, frame);

        if (encoded == null)
        {
            Console.WriteLine("ERROR: Failed to create test frame!");
            return;
        }

        // Probe the frame
        SbcFrame? probedFrame = decoder.Probe(encoded);
        if (probedFrame == null)
        {
            Console.WriteLine("ERROR: Failed to probe frame!");
            return;
        }

        Console.WriteLine("Probed frame information:");
        Console.WriteLine($"  Frequency: {probedFrame.GetFrequencyHz()} Hz");
        Console.WriteLine($"  Mode: {probedFrame.Mode}");
        Console.WriteLine($"  Allocation: {probedFrame.AllocationMethod}");
        Console.WriteLine($"  Blocks: {probedFrame.Blocks}, Subbands: {probedFrame.Subbands}");
        Console.WriteLine($"  Bitpool: {probedFrame.Bitpool}");
        Console.WriteLine($"  Frame size: {probedFrame.GetFrameSize()} bytes");
        Console.WriteLine($"  Bitrate: {probedFrame.GetBitrate()} bps");
        Console.WriteLine($"  Delay: {probedFrame.GetDelay()} samples");
    }

    /// <summary>
    /// Generate a sine wave test signal
    /// </summary>
    static short[] GenerateSineWave(int samples, double frequency, int sampleRate)
    {
        short[] wave = new short[samples];
        double amplitude = 16384.0; // Half of short max to avoid clipping
        
        for (int i = 0; i < samples; i++)
        {
            double t = i / (double)sampleRate;
            double value = amplitude * Math.Sin(2.0 * Math.PI * frequency * t);
            wave[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
        
        return wave;
    }

    /// <summary>
    /// Calculate Mean Squared Error between two signals
    /// </summary>
    static double CalculateMSE(short[] original, short[] decoded)
    {
        if (original.Length != decoded.Length)
            return double.MaxValue;

        double sum = 0;
        for (int i = 0; i < original.Length; i++)
        {
            double diff = original[i] - decoded[i];
            sum += diff * diff;
        }

        return sum / original.Length;
    }
}
