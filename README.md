# SbcSharp

A C# implementation of the SBC (Sub-Band Codec) commonly used with Bluetooth audio. This library provides SBC encoding and decoding functionality based on the specifications from:

- **Advanced Audio Distribution Profile (A2DP) v1.3.2** - Appendix B
- **Hands-Free Profile (HFP) v1.8** - Appendix A
- **Bluetooth Profile Specification**

The implementation is based on Google's libsbc C implementation and provides a clean, managed C# API.

## Features

- ✅ Full SBC encoding support
- ✅ Full SBC decoding support
- ✅ Support for all sampling frequencies (16kHz, 32kHz, 44.1kHz, 48kHz)
- ✅ Multiple channel modes (Mono, Dual Channel, Stereo, Joint Stereo)
- ✅ Configurable block sizes (4, 8, 12, 16 blocks)
- ✅ Configurable subbands (4 or 8 subbands)
- ✅ Loudness and SNR bit allocation methods
- ✅ Compatible with .NET 8.0+
- ✅ Zero external dependencies
- ✅ Comprehensive unit tests

## Installation

```bash
dotnet add package SbcSharp
```

Or clone and build from source:

```bash
git clone https://github.com/Ylianst/SbcSharp.git
cd SbcSharp
dotnet build
```

## Usage

### Encoding PCM to SBC

```csharp
using SbcSharp;

// Create encoder with default configuration (44.1kHz, Stereo, 16 blocks, 8 subbands)
var encoder = new SbcEncoder();

// Or configure custom settings
encoder.Configure(
    samplingFrequency: SbcConstants.Freq44100,
    blocks: 16,
    channelMode: SbcChannelMode.Stereo,
    allocationMethod: SbcAllocationMethod.Loudness,
    subbands: 8,
    bitpool: 53
);

// Prepare input PCM data (16-bit signed samples)
// For stereo with 16 blocks and 8 subbands: 256 samples (16 * 8 * 2)
short[] pcmData = new short[256];
// ... fill with audio samples ...

// Encode
byte[] sbcData = new byte[512];
int bytesWritten = encoder.Encode(pcmData, 0, sbcData, 0);

Console.WriteLine($"Encoded {pcmData.Length} PCM samples into {bytesWritten} SBC bytes");
```

### Decoding SBC to PCM

```csharp
using SbcSharp;

// Create decoder
var decoder = new SbcDecoder();

// Prepare SBC input data
byte[] sbcData = /* ... your SBC encoded data ... */;

// Prepare output buffer for decoded PCM samples
short[] pcmData = new short[2048];

// Decode
int bytesConsumed = decoder.Decode(sbcData, 0, pcmData, 0);

if (bytesConsumed > 0)
{
    Console.WriteLine($"Decoded {bytesConsumed} SBC bytes into PCM samples");
}
else
{
    Console.WriteLine("Decoding failed");
}
```

### Common Configurations

#### A2DP High Quality Stereo (44.1kHz)
```csharp
encoder.Configure(
    samplingFrequency: SbcConstants.Freq44100,
    blocks: 16,
    channelMode: SbcChannelMode.Stereo,
    allocationMethod: SbcAllocationMethod.Loudness,
    subbands: 8,
    bitpool: 53  // ~328 kbps
);
```

#### HFP/HSP Mono (16kHz)
```csharp
encoder.Configure(
    samplingFrequency: SbcConstants.Freq16000,
    blocks: 16,
    channelMode: SbcChannelMode.Mono,
    allocationMethod: SbcAllocationMethod.Loudness,
    subbands: 8,
    bitpool: 26
);
```

## API Reference

### SbcEncoder

**Methods:**
- `Configure(int samplingFrequency, int blocks, SbcChannelMode channelMode, SbcAllocationMethod allocationMethod, int subbands, int bitpool)` - Configure encoder parameters
- `int Encode(short[] input, int inputOffset, byte[] output, int outputOffset)` - Encode PCM samples to SBC format. Returns bytes written to output.

### SbcDecoder

**Methods:**
- `int Decode(byte[] input, int inputOffset, short[] output, int outputOffset)` - Decode SBC data to PCM samples. Returns bytes consumed from input, or negative on error.

### Enumerations

**SbcChannelMode:**
- `Mono` - Single channel
- `DualChannel` - Two independent channels
- `Stereo` - Two correlated channels
- `JointStereo` - Sum and difference encoding

**SbcAllocationMethod:**
- `Loudness` - Loudness-based bit allocation (typical for music)
- `Snr` - SNR-based bit allocation

### Constants

**SbcConstants:**
- `Freq16000`, `Freq32000`, `Freq44100`, `Freq48000` - Supported sampling frequencies
- `Syncword` - SBC frame synchronization word (0x9C)

## Technical Details

### SBC Frame Structure

An SBC frame consists of:
1. Syncword (1 byte): 0x9C
2. Frame header (3 bytes): Contains sampling frequency, blocks, channel mode, allocation method, subbands, bitpool, and CRC
3. Scale factors (4 bits per subband per channel)
4. Audio samples (variable length based on bitpool)

### Bitrate Calculation

Approximate bitrate can be calculated as:
```
bitrate = (8 * frame_length * sampling_frequency) / (blocks * subbands)
```

Where `frame_length` depends on the configuration and bitpool value.

### Input Requirements

For encoding, the number of input PCM samples per frame must be:
```
samples_per_frame = blocks * subbands * channels
```

Examples:
- Mono, 16 blocks, 8 subbands: 128 samples
- Stereo, 16 blocks, 8 subbands: 256 samples
- Mono, 4 blocks, 4 subbands: 16 samples

## Building and Testing

```bash
# Build
dotnet build

# Run tests
dotnet test

# Create NuGet package
dotnet pack -c Release
```

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## References

- [A2DP Specification v1.3.2](https://www.bluetooth.org/docman/handlers/downloaddoc.ashx?doc_id=457082)
- [HFP Specification v1.8](https://www.bluetooth.org/docman/handlers/downloaddoc.ashx?doc_id=489628)
- [Google libsbc](https://android.googlesource.com/platform/external/bluetooth/bluedroid/+/master/embdrv/sbc/)

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Acknowledgments

This implementation is based on the SBC codec specification and inspired by Google's libsbc implementation.

