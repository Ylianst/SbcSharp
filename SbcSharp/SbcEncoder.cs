using System;

namespace SbcSharp;

/// <summary>
/// SBC encoder implementation based on A2DP v1.3.2 and HFP v1.8 specifications.
/// Converts PCM audio data to SBC-encoded format.
/// </summary>
public class SbcEncoder
{
    private readonly SbcFrame frame;
    private readonly double[,] X = new double[SbcConstants.MaxChannels, 160];
    private readonly int[] position = new int[SbcConstants.MaxChannels];
    
    public SbcEncoder()
    {
        frame = new SbcFrame
        {
            SamplingFrequency = SbcConstants.Freq44100,
            Blocks = 16,
            ChannelMode = SbcChannelMode.Stereo,
            AllocationMethod = SbcAllocationMethod.Loudness,
            Subbands = 8,
            Bitpool = 53
        };
    }
    
    /// <summary>
    /// Configure encoder parameters.
    /// </summary>
    public void Configure(int samplingFrequency, int blocks, SbcChannelMode channelMode, 
        SbcAllocationMethod allocationMethod, int subbands, int bitpool)
    {
        frame.SamplingFrequency = samplingFrequency;
        frame.Blocks = blocks;
        frame.ChannelMode = channelMode;
        frame.AllocationMethod = allocationMethod;
        frame.Subbands = subbands;
        frame.Bitpool = bitpool;
    }
    
    /// <summary>
    /// Encode PCM audio samples to SBC format.
    /// </summary>
    /// <param name="input">PCM input buffer (16-bit signed samples)</param>
    /// <param name="inputOffset">Offset in input buffer</param>
    /// <param name="output">SBC output buffer</param>
    /// <param name="outputOffset">Offset in output buffer</param>
    /// <returns>Number of bytes written to output</returns>
    public int Encode(short[] input, int inputOffset, byte[] output, int outputOffset)
    {
        if (input == null || output == null)
            throw new ArgumentNullException();
            
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        
        // Analyze input audio
        AnalyzeAudio(input, inputOffset);
        
        // Write frame header
        int written = WriteHeader(output, outputOffset);
        
        // Calculate bit allocation
        int[,] bits = CalculateBitAllocation();
        
        // Quantize samples
        QuantizeSamples(bits);
        
        // Pack frame data
        written += PackFrame(output, outputOffset + written, bits);
        
        return written;
    }
    
    private void AnalyzeAudio(short[] input, int inputOffset)
    {
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        
        double[] protoFilter = subbands == 4 ? SbcConstants.ProtoFilter4 : SbcConstants.ProtoFilter8;
        
        int inPos = inputOffset;
        
        for (int blk = 0; blk < blocks; blk++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                // Shift X buffer and add new samples
                int pos = position[ch];
                
                // Shift existing samples down
                for (int i = subbands * 10 - 1; i >= subbands; i--)
                {
                    X[ch, i] = X[ch, i - subbands];
                }
                
                // Add new samples at the beginning
                for (int i = subbands - 1; i >= 0; i--)
                {
                    X[ch, i] = input[inPos++] / 32768.0;
                }
                
                // Apply analysis filter
                double[] sb_sample = new double[subbands];
                
                for (int sb = 0; sb < subbands; sb++)
                {
                    double sum = 0;
                    
                    for (int k = 0; k < subbands * 10; k++)
                    {
                        double angle = (k + 0.5) * (sb + 0.5) * Math.PI / subbands;
                        sum += X[ch, k] * protoFilter[k] * Math.Cos(angle);
                    }
                    
                    sb_sample[sb] = sum;
                }
                
                // Calculate scale factors
                for (int sb = 0; sb < subbands; sb++)
                {
                    double max = Math.Abs(sb_sample[sb]);
                    
                    if (max < 1e-10)
                    {
                        frame.ScaleFactors[ch, sb] = 0;
                    }
                    else
                    {
                        int scale = 0;
                        while (max < 32768.0 && scale < 15)
                        {
                            max *= 2.0;
                            scale++;
                        }
                        frame.ScaleFactors[ch, sb] = scale;
                    }
                    
                    // Store normalized sample
                    double scalefactor = Math.Pow(2.0, frame.ScaleFactors[ch, sb]);
                    frame.AudioSamples[blk, ch, sb] = (int)(sb_sample[sb] * scalefactor);
                }
            }
        }
    }
    
    private int[,] CalculateBitAllocation()
    {
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        int bitpool = frame.Bitpool;
        
        int[,] bits = new int[channels, subbands];
        
        if (frame.ChannelMode == SbcChannelMode.Mono || frame.ChannelMode == SbcChannelMode.DualChannel)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int[] loudness = new int[subbands];
                int max_bitneed = 0;
                
                for (int sb = 0; sb < subbands; sb++)
                {
                    int bitneed;
                    if (frame.AllocationMethod == SbcAllocationMethod.Snr)
                    {
                        bitneed = frame.ScaleFactors[ch, sb];
                    }
                    else
                    {
                        if (frame.ScaleFactors[ch, sb] == 0)
                            bitneed = -5;
                        else
                            bitneed = (subbands > 4)
                                ? frame.ScaleFactors[ch, sb] - ((sb < 2) ? 2 : 0)
                                : frame.ScaleFactors[ch, sb] - ((sb < 1) ? 2 : 0);
                    }
                    
                    if (bitneed > max_bitneed)
                        max_bitneed = bitneed;
                    
                    loudness[sb] = bitneed;
                }
                
                int bitcount = 0;
                int slicecount = 0;
                int bitslice = max_bitneed + 1;
                
                do
                {
                    bitslice--;
                    bitcount += slicecount;
                    slicecount = 0;
                    
                    for (int sb = 0; sb < subbands; sb++)
                    {
                        if (loudness[sb] > bitslice + 1 && loudness[sb] < bitslice + 16)
                            slicecount++;
                        else if (loudness[sb] == bitslice + 1 && bitpool > bitcount + slicecount)
                            slicecount++;
                    }
                } while (bitcount + slicecount < bitpool && bitslice > 0);
                
                for (int sb = 0; sb < subbands; sb++)
                {
                    if (loudness[sb] < bitslice + 2)
                        bits[ch, sb] = 0;
                    else
                        bits[ch, sb] = Math.Min(loudness[sb] - bitslice, 16);
                }
                
                int remaining = bitpool;
                for (int sb = 0; sb < subbands && remaining > 0; sb++)
                {
                    if (bits[ch, sb] < 16)
                    {
                        int add = Math.Min(remaining, 16 - bits[ch, sb]);
                        bits[ch, sb] += add;
                        remaining -= add;
                    }
                }
            }
        }
        else
        {
            // Stereo or joint stereo
            int[] loudness = new int[subbands];
            int max_bitneed = 0;
            
            for (int sb = 0; sb < subbands; sb++)
            {
                int bitneed;
                if (frame.AllocationMethod == SbcAllocationMethod.Snr)
                {
                    bitneed = Math.Max(frame.ScaleFactors[0, sb], frame.ScaleFactors[1, sb]);
                }
                else
                {
                    int sf0 = frame.ScaleFactors[0, sb];
                    int sf1 = frame.ScaleFactors[1, sb];
                    
                    if (sf0 == 0 && sf1 == 0)
                        bitneed = -5;
                    else
                    {
                        int max_sf = Math.Max(sf0, sf1);
                        bitneed = (subbands > 4)
                            ? max_sf - ((sb < 2) ? 2 : 0)
                            : max_sf - ((sb < 1) ? 2 : 0);
                    }
                }
                
                if (bitneed > max_bitneed)
                    max_bitneed = bitneed;
                
                loudness[sb] = bitneed;
            }
            
            int bitcount = 0;
            int slicecount = 0;
            int bitslice = max_bitneed + 1;
            
            do
            {
                bitslice--;
                bitcount += slicecount;
                slicecount = 0;
                
                for (int sb = 0; sb < subbands; sb++)
                {
                    if (loudness[sb] > bitslice + 1 && loudness[sb] < bitslice + 16)
                        slicecount += 2;
                    else if (loudness[sb] == bitslice + 1 && bitpool > bitcount + slicecount)
                        slicecount += 2;
                }
            } while (bitcount + slicecount < bitpool && bitslice > 0);
            
            for (int sb = 0; sb < subbands; sb++)
            {
                if (loudness[sb] < bitslice + 2)
                {
                    bits[0, sb] = 0;
                    bits[1, sb] = 0;
                }
                else
                {
                    int b = Math.Min(loudness[sb] - bitslice, 16);
                    bits[0, sb] = b;
                    bits[1, sb] = b;
                }
            }
            
            int remaining = bitpool;
            for (int ch = 0; ch < 2; ch++)
            {
                for (int sb = 0; sb < subbands && remaining > 0; sb++)
                {
                    if (bits[ch, sb] < 16)
                    {
                        int add = Math.Min(remaining, 16 - bits[ch, sb]);
                        bits[ch, sb] += add;
                        remaining -= add;
                    }
                }
            }
        }
        
        return bits;
    }
    
    private void QuantizeSamples(int[,] bits)
    {
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        
        for (int blk = 0; blk < blocks; blk++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                for (int sb = 0; sb < subbands; sb++)
                {
                    int numBits = bits[ch, sb];
                    if (numBits > 0)
                    {
                        int levels = (1 << numBits) - 1;
                        double scalefactor = Math.Pow(2.0, frame.ScaleFactors[ch, sb]);
                        double sample = frame.AudioSamples[blk, ch, sb] / scalefactor;
                        
                        int quantized = (int)((sample + 1.0) * levels / 2.0 + 0.5);
                        if (quantized < 0) quantized = 0;
                        if (quantized > levels) quantized = levels;
                        
                        frame.AudioSamples[blk, ch, sb] = quantized;
                    }
                    else
                    {
                        frame.AudioSamples[blk, ch, sb] = 0;
                    }
                }
            }
        }
    }
    
    private int WriteHeader(byte[] output, int offset)
    {
        int pos = offset;
        
        // Syncword
        output[pos++] = SbcConstants.Syncword;
        
        // Header byte
        byte header = 0;
        
        // Sampling frequency
        int freqIndex = frame.SamplingFrequency switch
        {
            SbcConstants.Freq16000 => 0,
            SbcConstants.Freq32000 => 1,
            SbcConstants.Freq44100 => 2,
            SbcConstants.Freq48000 => 3,
            _ => 2
        };
        header |= (byte)(freqIndex << 6);
        
        // Blocks
        int blocksIndex = (frame.Blocks / 4) - 1;
        header |= (byte)(blocksIndex << 4);
        
        // Channel mode
        header |= (byte)((int)frame.ChannelMode << 2);
        
        // Allocation method
        header |= (byte)((int)frame.AllocationMethod << 1);
        
        // Subbands
        header |= (byte)(frame.Subbands == 8 ? 1 : 0);
        
        output[pos++] = header;
        
        // Bitpool
        output[pos++] = (byte)frame.Bitpool;
        
        // CRC (calculate later)
        frame.Crc = CalculateCrc(output, offset, 3);
        output[pos++] = frame.Crc;
        
        return 4;
    }
    
    private byte CalculateCrc(byte[] data, int offset, int length)
    {
        byte crc = 0x0F;
        
        for (int i = 0; i < length; i++)
        {
            crc = SbcConstants.Crc8Table[crc ^ data[offset + i]];
        }
        
        return crc;
    }
    
    private int PackFrame(byte[] output, int offset, int[,] bits)
    {
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        
        BitWriter writer = new BitWriter(output, offset);
        
        // Write joint stereo flags for joint stereo mode
        if (frame.ChannelMode == SbcChannelMode.JointStereo)
        {
            for (int sb = 0; sb < subbands; sb++)
            {
                writer.WriteBits(frame.Join[sb], 1);
            }
        }
        
        // Write scale factors
        for (int ch = 0; ch < channels; ch++)
        {
            for (int sb = 0; sb < subbands; sb++)
            {
                writer.WriteBits(frame.ScaleFactors[ch, sb], 4);
            }
        }
        
        // Write audio samples
        for (int blk = 0; blk < blocks; blk++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                for (int sb = 0; sb < subbands; sb++)
                {
                    int numBits = bits[ch, sb];
                    if (numBits > 0)
                    {
                        writer.WriteBits(frame.AudioSamples[blk, ch, sb], numBits);
                    }
                }
            }
        }
        
        writer.Flush();
        return writer.BytesWritten;
    }
    
    private class BitWriter
    {
        private readonly byte[] data;
        private int byteOffset;
        private int bitOffset;
        private byte currentByte;
        
        public int BytesWritten => byteOffset + (bitOffset > 0 ? 1 : 0);
        
        public BitWriter(byte[] data, int offset)
        {
            this.data = data;
            this.byteOffset = offset;
            this.bitOffset = 0;
            this.currentByte = 0;
        }
        
        public void WriteBits(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                int bit = (value >> i) & 1;
                currentByte = (byte)((currentByte << 1) | bit);
                bitOffset++;
                
                if (bitOffset == 8)
                {
                    data[byteOffset++] = currentByte;
                    bitOffset = 0;
                    currentByte = 0;
                }
            }
        }
        
        public void Flush()
        {
            if (bitOffset > 0)
            {
                currentByte <<= (8 - bitOffset);
                data[byteOffset++] = currentByte;
                bitOffset = 0;
                currentByte = 0;
            }
        }
    }
}
