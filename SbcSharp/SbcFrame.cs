namespace SbcSharp;

/// <summary>
/// Channel mode for SBC encoding/decoding.
/// </summary>
public enum SbcChannelMode
{
    /// <summary>Mono - single channel</summary>
    Mono = 0,
    
    /// <summary>Dual channel - two independent channels</summary>
    DualChannel = 1,
    
    /// <summary>Stereo - two correlated channels</summary>
    Stereo = 2,
    
    /// <summary>Joint stereo - sum and difference encoding</summary>
    JointStereo = 3
}

/// <summary>
/// Allocation method for bit allocation.
/// </summary>
public enum SbcAllocationMethod
{
    /// <summary>Loudness allocation</summary>
    Loudness = 0,
    
    /// <summary>SNR (Signal-to-Noise Ratio) allocation</summary>
    Snr = 1
}

/// <summary>
/// SBC frame parameters and configuration.
/// </summary>
public class SbcFrame
{
    /// <summary>Sampling frequency in Hz (16000, 32000, 44100, 48000)</summary>
    public int SamplingFrequency { get; set; }
    
    /// <summary>Number of blocks (4, 8, 12, 16)</summary>
    public int Blocks { get; set; }
    
    /// <summary>Channel mode</summary>
    public SbcChannelMode ChannelMode { get; set; }
    
    /// <summary>Allocation method</summary>
    public SbcAllocationMethod AllocationMethod { get; set; }
    
    /// <summary>Number of subbands (4 or 8)</summary>
    public int Subbands { get; set; }
    
    /// <summary>Bitpool value (determines bitrate)</summary>
    public int Bitpool { get; set; }
    
    /// <summary>Number of channels</summary>
    public int Channels => ChannelMode == SbcChannelMode.Mono ? 1 : 2;
    
    /// <summary>Joint stereo flags (one per subband)</summary>
    public byte[] Join { get; set; }
    
    /// <summary>Scale factors for each channel and subband</summary>
    public int[,] ScaleFactors { get; set; }
    
    /// <summary>Audio samples for each block, channel, and subband</summary>
    public int[,,] AudioSamples { get; set; }
    
    /// <summary>CRC checksum</summary>
    public byte Crc { get; set; }
    
    public SbcFrame()
    {
        Join = new byte[SbcConstants.MaxSubbands];
        ScaleFactors = new int[SbcConstants.MaxChannels, SbcConstants.MaxSubbands];
        AudioSamples = new int[SbcConstants.MaxBlocks, SbcConstants.MaxChannels, SbcConstants.MaxSubbands];
    }
    
    /// <summary>
    /// Calculate frame length in bytes.
    /// </summary>
    public int FrameLength
    {
        get
        {
            int length = 4 + (4 * Subbands * Channels) / 8;
            
            if (ChannelMode == SbcChannelMode.Mono || ChannelMode == SbcChannelMode.DualChannel)
            {
                length += ((Blocks * Channels * Bitpool) + 7) / 8;
            }
            else
            {
                int join = ChannelMode == SbcChannelMode.JointStereo ? 1 : 0;
                length += ((join * Subbands + Blocks * Bitpool) + 7) / 8;
            }
            
            return length;
        }
    }
}
