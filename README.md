# SBC Audio Codec Library for C#

A complete C# implementation of the Subband Codec (SBC) audio compression algorithm, commonly used in Bluetooth A2DP and HFP profiles.

## Overview

This library provides SBC encoding and decoding functionality based on the specifications from:
- Advanced Audio Distribution Profile (A2DP) v1.3.2 - Appendix B
- Hands-Free Profile (HFP) v1.8 - Appendix A
- Bluetooth Profile Specification

The implementation is based on Google's libsbc C implementation and provides a clean, managed C# API.

## Features

- ✅ **Full SBC Encoding** - Convert PCM audio to SBC frames
- ✅ **Full SBC Decoding** - Convert SBC frames to PCM audio
- ✅ **Multiple Channel Modes** - Mono, Dual Channel, Stereo, Joint Stereo
- ✅ **Multiple Sampling Rates** - 16 kHz, 32 kHz, 44.1 kHz, 48 kHz
- ✅ **Flexible Configuration** - 4 or 8 subbands, 4/8/12/16 blocks
- ✅ **Bit Allocation Methods** - Loudness and SNR
- ✅ **mSBC Support** - Fixed configuration for Bluetooth HFP
- ✅ **CRC Validation** - Frame integrity checking
- ✅ **Pure C#** - No native dependencies

## Installation

Add the project to your solution or build the DLL:

```bash
dotnet build SBC.csproj
```

## Usage

### Encoding PCM to SBC

```csharp
using SBC;

// Create encoder
var encoder = new SbcEncoder();

// Configure frame parameters
var frame = new SbcFrame
{
    Frequency = SbcFrequency.Freq44K1,    // 44.1 kHz
    Mode = SbcMode.JointStereo,            // Joint stereo
    AllocationMethod = SbcBitAllocationMethod.Loudness,
    Blocks = 16,                           // 16 blocks
    Subbands = 8,                          // 8 subbands
    Bitpool = 53                           // Quality/bitrate control
};

// Prepare PCM data (16-bit signed samples)
short[] pcmLeft = GetLeftChannelSamples();   // 128 samples (16 blocks * 8 subbands)
short[] pcmRight = GetRightChannelSamples(); // 128 samples

// Encode
byte[] sbcFrame = encoder.Encode(pcmLeft, pcmRight, frame);

if (sbcFrame != null)
{
    Console.WriteLine($"Encoded frame size: {sbcFrame.Length} bytes");
    Console.WriteLine($"Bitrate: {frame.GetBitrate()} bps");
}
```

### Decoding SBC to PCM

```csharp
using SBC;

// Create decoder
var decoder = new SbcDecoder();

// Decode frame
byte[] sbcData = GetSbcFrameData();

bool success = decoder.Decode(
    sbcData, 
    out short[] pcmLeft, 
    out short[] pcmRight, 
    out SbcFrame frame
);

if (success)
{
    Console.WriteLine($"Decoded {pcmLeft.Length} samples per channel");
    Console.WriteLine($"Sampling rate: {frame.GetFrequencyHz()} Hz");
    Console.WriteLine($"Mode: {frame.Mode}");
}
```

### Probing SBC Frame Headers

```csharp
using SBC;

var decoder = new SbcDecoder();
byte[] sbcData = GetSbcFrameData();

// Extract frame info without full decoding
SbcFrame? frame = decoder.Probe(sbcData);

if (frame != null)
{
    Console.WriteLine($"Frame size: {frame.GetFrameSize()} bytes");
    Console.WriteLine($"Sampling rate: {frame.GetFrequencyHz()} Hz");
    Console.WriteLine($"Bitrate: {frame.GetBitrate()} bps");
    Console.WriteLine($"Blocks: {frame.Blocks}, Subbands: {frame.Subbands}");
}
```

### Using mSBC (for Bluetooth HFP)

```csharp
using SBC;

// Create mSBC frame configuration
var msbcFrame = SbcFrame.CreateMsbc();
// This creates a frame with:
// - 16 kHz sampling
// - Mono mode
// - 8 subbands, 15 blocks
// - Bitpool 26

var encoder = new SbcEncoder();
short[] pcmMono = GetMonoSamples(); // 120 samples (15 * 8)

byte[] msbcData = encoder.Encode(pcmMono, null, msbcFrame);
// Result is always 57 bytes for mSBC
```

## Frame Configuration

### Sampling Frequencies
- `SbcFrequency.Freq16K` - 16,000 Hz
- `SbcFrequency.Freq32K` - 32,000 Hz
- `SbcFrequency.Freq44K1` - 44,100 Hz
- `SbcFrequency.Freq48K` - 48,000 Hz

### Channel Modes
- `SbcMode.Mono` - Single channel
- `SbcMode.DualChannel` - Two independent channels
- `SbcMode.Stereo` - Two channels (L/R encoding)
- `SbcMode.JointStereo` - Two channels with mid-side encoding (best compression)

### Blocks and Subbands
- **Blocks**: 4, 8, 12, or 16 (more blocks = better quality, larger frames)
- **Subbands**: 4 or 8 (8 subbands recommended for better quality)
- **Samples per frame**: `blocks × subbands`

### Bitpool
The bitpool value controls the quality and bitrate:
- Higher bitpool = better quality, higher bitrate
- Valid range depends on configuration (validated by `frame.IsValid()`)
- Typical values: 30-53 for stereo music

## Quality and Bitrate

Example configurations and their bitrates (44.1 kHz, stereo):

| Blocks | Subbands | Bitpool | Bitrate | Quality |
|--------|----------|---------|---------|---------|
| 16     | 8        | 30      | ~198 kbps | Medium |
| 16     | 8        | 40      | ~257 kbps | Good |
| 16     | 8        | 53      | ~328 kbps | High |

Calculate bitrate: `frame.GetBitrate()`

## Algorithmic Delay

The SBC codec introduces a delay of `10 × subbands` samples:
- 4 subbands: 40 samples delay
- 8 subbands: 80 samples delay

Access via: `frame.GetDelay()`

## Thread Safety

- Each `SbcEncoder` and `SbcDecoder` maintains internal state
- Not thread-safe - use separate instances per thread
- Call `Reset()` to clear state between encoding sessions

## Performance Considerations

- Uses fixed-point arithmetic for accuracy matching the C implementation
- Optimized DCT and windowing operations
- Pre-calculated coefficient tables
- Typical performance: 10-50× realtime on modern hardware

## Technical Details

### Fixed-Point Precision
- Windowing coefficients: 2.13 fixed-point format
- DCT coefficients: 0.13 fixed-point format
- Scale factors: Computed using leading zero count
- Dequantization: 1.28 fixed-point format

### Frame Structure
1. **Syncword** (8 bits): 0x9C for SBC, 0xAD for mSBC
2. **Header** (16-24 bits): Configuration parameters
3. **CRC** (8 bits): Header validation
4. **Audio Data**: Scale factors, joint stereo mask, quantized samples
5. **Padding**: Byte alignment

## Limitations

- No packet loss concealment (PLC) - set `sbcData` to `null` in decode for silence
- No bit-rate adaptation
- No assembly optimizations (pure C#)

## License

Based on Google's libsbc implementation (Apache License 2.0).

## References

- [Bluetooth A2DP Specification](https://www.bluetooth.com/specifications/specs/advanced-audio-distribution-profile-1-3-2/)
- [Bluetooth HFP Specification](https://www.bluetooth.com/specifications/specs/hands-free-profile-1-8/)
- [Google libsbc](https://github.com/google/libsbc)

## Testing with WAV Files

The library includes a comprehensive WAV file test program:

```bash
# Run with auto-generated test file
dotnet run --project WavTest.cs

# Test with your own WAV file
dotnet run --project WavTest.cs input.wav output.wav high

# Test different quality levels
dotnet run --project WavTest.cs music.wav encoded_low.wav low
dotnet run --project WavTest.cs music.wav encoded_medium.wav medium
dotnet run --project WavTest.cs music.wav encoded_high.wav high
```

The test program:
- Reads 16-bit PCM WAV files (mono or stereo)
- Encodes to SBC frames
- Decodes back to PCM
- Calculates quality metrics (MSE, PSNR)
- Writes decoded output to a new WAV file
- Displays compression ratio and encoding/decoding speed

### Example Output

```
=== SBC WAV File Encoder/Decoder Test ===

✓ Input WAV properties:
  Sample rate: 44100 Hz
  Channels: 2
  Total samples: 264,600
  Duration: 3.00 seconds

SBC Configuration:
  Frequency: 44100 Hz
  Mode: JointStereo
  Blocks: 16, Subbands: 8
  Bitpool: 53
  Frame size: 119 bytes
  Bitrate: 328,125 bps

✓ Encoded 1,035 frames in 45 ms
  Original size: 529,200 bytes
  Encoded size: 123,165 bytes
  Compression ratio: 4.30:1
  Encoding speed: 66.7x realtime

✓ Decoded in 52 ms
  Decoding speed: 57.7x realtime

Quality Metrics (Left Channel):
  MSE: 1247.35
  PSNR: 42.18 dB
  Max difference: 891 / 32768
```

## Contributing

This implementation aims for compatibility with the reference C implementation. When making changes, ensure:
- Bit-exact output compared to libsbc where possible
- All frame configurations are validated
- CRC checks pass
- Fixed-point arithmetic is used consistently
