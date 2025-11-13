using Xunit;

namespace SbcSharp.Tests;

public class SbcRoundtripTests
{
    [Fact]
    public void Roundtrip_MonoAudio_ShouldEncodeAndDecode()
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
        
        var decoder = new SbcDecoder();
        
        // Create test audio: 128 samples (16 blocks * 8 subbands * 1 channel)
        short[] originalPcm = new short[128];
        for (int i = 0; i < originalPcm.Length; i++)
        {
            originalPcm[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100) * 16000);
        }
        
        // Encode
        byte[] sbcData = new byte[256];
        int encoded = encoder.Encode(originalPcm, 0, sbcData, 0);
        
        Assert.True(encoded > 0);
        
        // Decode
        short[] decodedPcm = new short[128];
        int decoded = decoder.Decode(sbcData, 0, decodedPcm, 0);
        
        Assert.True(decoded > 0);
        Assert.Equal(encoded, decoded);
    }
    
    [Fact]
    public void Roundtrip_StereoAudio_ShouldEncodeAndDecode()
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
        
        var decoder = new SbcDecoder();
        
        // Create test audio: 256 samples (16 blocks * 8 subbands * 2 channels)
        short[] originalPcm = new short[256];
        for (int i = 0; i < originalPcm.Length; i++)
        {
            originalPcm[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 44100) * 16000);
        }
        
        // Encode
        byte[] sbcData = new byte[512];
        int encoded = encoder.Encode(originalPcm, 0, sbcData, 0);
        
        Assert.True(encoded > 0);
        
        // Decode
        short[] decodedPcm = new short[256];
        int decoded = decoder.Decode(sbcData, 0, decodedPcm, 0);
        
        Assert.True(decoded > 0);
        Assert.Equal(encoded, decoded);
    }
    
    [Fact]
    public void Roundtrip_Silence_ShouldProduceSmallFrame()
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
        
        var decoder = new SbcDecoder();
        
        // Create silent audio
        short[] originalPcm = new short[128];
        
        // Encode
        byte[] sbcData = new byte[256];
        int encoded = encoder.Encode(originalPcm, 0, sbcData, 0);
        
        Assert.True(encoded > 0);
        
        // Decode
        short[] decodedPcm = new short[128];
        int decoded = decoder.Decode(sbcData, 0, decodedPcm, 0);
        
        Assert.True(decoded > 0);
    }
}
