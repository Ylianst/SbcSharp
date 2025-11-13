using Xunit;

namespace SbcSharp.Tests;

public class SbcEncoderTests
{
    [Fact]
    public void Encoder_ShouldInitializeWithDefaultConfiguration()
    {
        var encoder = new SbcEncoder();
        Assert.NotNull(encoder);
    }
    
    [Fact]
    public void Encoder_ShouldConfigureParameters()
    {
        var encoder = new SbcEncoder();
        
        // Configure with typical A2DP settings
        encoder.Configure(
            samplingFrequency: SbcConstants.Freq44100,
            blocks: 16,
            channelMode: SbcChannelMode.Stereo,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 53
        );
        
        Assert.NotNull(encoder);
    }
    
    [Fact]
    public void Encoder_ShouldEncodeMonoAudio()
    {
        var encoder = new SbcEncoder();
        encoder.Configure(
            samplingFrequency: SbcConstants.Freq44100,
            blocks: 16,
            channelMode: SbcChannelMode.Mono,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 26
        );
        
        // Create test audio: 128 samples (16 blocks * 8 subbands * 1 channel)
        short[] pcmData = new short[128];
        for (int i = 0; i < pcmData.Length; i++)
        {
            // Generate a simple sine wave
            pcmData[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100) * 16000);
        }
        
        byte[] sbcData = new byte[256];
        int encoded = encoder.Encode(pcmData, 0, sbcData, 0);
        
        Assert.True(encoded > 0);
        Assert.Equal(SbcConstants.Syncword, sbcData[0]);
    }
    
    [Fact]
    public void Encoder_ShouldEncodeStereoAudio()
    {
        var encoder = new SbcEncoder();
        encoder.Configure(
            samplingFrequency: SbcConstants.Freq44100,
            blocks: 16,
            channelMode: SbcChannelMode.Stereo,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 53
        );
        
        // Create test audio: 256 samples (16 blocks * 8 subbands * 2 channels)
        short[] pcmData = new short[256];
        for (int i = 0; i < pcmData.Length; i++)
        {
            // Generate a simple sine wave
            pcmData[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100) * 16000);
        }
        
        byte[] sbcData = new byte[512];
        int encoded = encoder.Encode(pcmData, 0, sbcData, 0);
        
        Assert.True(encoded > 0);
        Assert.Equal(SbcConstants.Syncword, sbcData[0]);
    }
    
    [Fact]
    public void Encoder_ShouldHandleSilence()
    {
        var encoder = new SbcEncoder();
        encoder.Configure(
            samplingFrequency: SbcConstants.Freq44100,
            blocks: 16,
            channelMode: SbcChannelMode.Mono,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 26
        );
        
        // Create silent audio
        short[] pcmData = new short[128];
        
        byte[] sbcData = new byte[256];
        int encoded = encoder.Encode(pcmData, 0, sbcData, 0);
        
        Assert.True(encoded > 0);
        Assert.Equal(SbcConstants.Syncword, sbcData[0]);
    }
    
    [Theory]
    [InlineData(SbcConstants.Freq16000)]
    [InlineData(SbcConstants.Freq32000)]
    [InlineData(SbcConstants.Freq44100)]
    [InlineData(SbcConstants.Freq48000)]
    public void Encoder_ShouldSupportAllSamplingFrequencies(int frequency)
    {
        var encoder = new SbcEncoder();
        encoder.Configure(
            samplingFrequency: frequency,
            blocks: 16,
            channelMode: SbcChannelMode.Mono,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 26
        );
        
        short[] pcmData = new short[128];
        byte[] sbcData = new byte[256];
        
        int encoded = encoder.Encode(pcmData, 0, sbcData, 0);
        Assert.True(encoded > 0);
    }
    
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Encoder_ShouldSupportAllBlockSizes(int blocks)
    {
        var encoder = new SbcEncoder();
        encoder.Configure(
            samplingFrequency: SbcConstants.Freq44100,
            blocks: blocks,
            channelMode: SbcChannelMode.Mono,
            allocationMethod: SbcAllocationMethod.Loudness,
            subbands: 8,
            bitpool: 26
        );
        
        // Create appropriate sized input
        short[] pcmData = new short[blocks * 8]; // blocks * subbands
        byte[] sbcData = new byte[256];
        
        int encoded = encoder.Encode(pcmData, 0, sbcData, 0);
        Assert.True(encoded > 0);
    }
}
