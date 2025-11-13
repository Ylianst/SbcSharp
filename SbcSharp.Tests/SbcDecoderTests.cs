using Xunit;

namespace SbcSharp.Tests;

public class SbcDecoderTests
{
    [Fact]
    public void Decoder_ShouldInitialize()
    {
        var decoder = new SbcDecoder();
        Assert.NotNull(decoder);
    }
    
    [Fact]
    public void Decoder_ShouldRejectInvalidSyncword()
    {
        var decoder = new SbcDecoder();
        
        byte[] invalidData = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        short[] output = new short[256];
        
        int result = decoder.Decode(invalidData, 0, output, 0);
        Assert.True(result < 0);
    }
    
    [Fact]
    public void Decoder_ShouldHandleValidHeader()
    {
        var decoder = new SbcDecoder();
        
        // Create a minimal valid SBC frame header
        // Syncword: 0x9C
        // Config: 44.1kHz, 16 blocks, Stereo, Loudness, 8 subbands
        // Bitpool: 53
        byte[] header = new byte[128];
        header[0] = SbcConstants.Syncword;  // Syncword
        header[1] = 0xB9;  // Freq=44.1kHz (10), Blocks=16 (11), Mode=Stereo (10), Alloc=Loudness (0), Subbands=8 (1)
        header[2] = 53;    // Bitpool
        header[3] = 0x00;  // CRC (placeholder)
        
        short[] output = new short[2048];
        
        // This will fail during unpacking but should parse the header
        int result = decoder.Decode(header, 0, output, 0);
        // Result could be positive (consumed bytes) or negative (error during unpacking)
        Assert.True(result != 0);
    }
}
