using System;

namespace SbcSharp;

/// <summary>
/// SBC decoder implementation based on A2DP v1.3.2 and HFP v1.8 specifications.
/// Converts SBC-encoded audio data back to PCM format.
/// </summary>
public class SbcDecoder
{
    private readonly SbcFrame frame;
    private readonly double[,] V = new double[SbcConstants.MaxChannels, 160];
    private readonly int[] offset = new int[SbcConstants.MaxChannels];
    
    public SbcDecoder()
    {
        frame = new SbcFrame();
    }
    
    /// <summary>
    /// Decode SBC frame to PCM audio samples.
    /// </summary>
    /// <param name="input">SBC encoded data</param>
    /// <param name="inputOffset">Offset in input buffer</param>
    /// <param name="output">PCM output buffer (16-bit signed samples)</param>
    /// <param name="outputOffset">Offset in output buffer</param>
    /// <returns>Number of bytes consumed from input, or negative on error</returns>
    public int Decode(byte[] input, int inputOffset, short[] output, int outputOffset)
    {
        if (input == null || output == null)
            throw new ArgumentNullException();
            
        if (inputOffset >= input.Length)
            return -1;
            
        // Parse frame header
        int consumed = ParseHeader(input, inputOffset);
        if (consumed < 0)
            return consumed;
            
        // Unpack frame data
        int unpacked = UnpackFrame(input, inputOffset + consumed);
        if (unpacked < 0)
            return unpacked;
            
        consumed += unpacked;
        
        // Decode audio samples
        DecodeAudioSamples(output, outputOffset);
        
        return consumed;
    }
    
    private int ParseHeader(byte[] data, int offset)
    {
        if (offset + 4 > data.Length)
            return -1;
            
        // Check syncword
        if (data[offset] != SbcConstants.Syncword)
            return -1;
            
        int pos = offset + 1;
        byte header = data[pos++];
        
        // Parse sampling frequency
        int freqIndex = (header >> 6) & 0x03;
        frame.SamplingFrequency = freqIndex switch
        {
            0 => SbcConstants.Freq16000,
            1 => SbcConstants.Freq32000,
            2 => SbcConstants.Freq44100,
            3 => SbcConstants.Freq48000,
            _ => SbcConstants.Freq44100
        };
        
        // Parse blocks
        int blocksIndex = (header >> 4) & 0x03;
        frame.Blocks = (blocksIndex + 1) * 4;
        
        // Parse channel mode
        frame.ChannelMode = (SbcChannelMode)((header >> 2) & 0x03);
        
        // Parse allocation method
        frame.AllocationMethod = (SbcAllocationMethod)((header >> 1) & 0x01);
        
        // Parse subbands
        frame.Subbands = ((header & 0x01) != 0) ? 8 : 4;
        
        // Parse bitpool
        frame.Bitpool = data[pos++];
        
        // Verify bitpool range
        if (frame.Bitpool < 2 || frame.Bitpool > 250)
            return -1;
            
        // Parse CRC
        frame.Crc = data[pos++];
        
        return 4;
    }
    
    private int UnpackFrame(byte[] data, int offset)
    {
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        
        BitReader reader = new BitReader(data, offset);
        
        // Read joint stereo flags for joint stereo mode
        if (frame.ChannelMode == SbcChannelMode.JointStereo)
        {
            for (int sb = 0; sb < subbands; sb++)
            {
                frame.Join[sb] = (byte)reader.ReadBits(1);
            }
        }
        
        // Read scale factors
        for (int ch = 0; ch < channels; ch++)
        {
            for (int sb = 0; sb < subbands; sb++)
            {
                frame.ScaleFactors[ch, sb] = reader.ReadBits(4);
            }
        }
        
        // Calculate bit allocation
        int[,] bits = CalculateBitAllocation();
        
        // Read audio samples
        for (int blk = 0; blk < blocks; blk++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                for (int sb = 0; sb < subbands; sb++)
                {
                    int numBits = bits[ch, sb];
                    if (numBits > 0)
                    {
                        frame.AudioSamples[blk, ch, sb] = reader.ReadBits(numBits);
                    }
                    else
                    {
                        frame.AudioSamples[blk, ch, sb] = 0;
                    }
                }
            }
        }
        
        return reader.BytesRead;
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
            int bitneed, max_bitneed = 0;
            int[] loudness = new int[subbands];
            
            for (int ch = 0; ch < channels; ch++)
            {
                max_bitneed = 0;
                
                for (int sb = 0; sb < subbands; sb++)
                {
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
    
    private void DecodeAudioSamples(short[] output, int outputOffset)
    {
        int channels = frame.Channels;
        int subbands = frame.Subbands;
        int blocks = frame.Blocks;
        
        double[] protoFilter = subbands == 4 ? SbcConstants.ProtoFilter4 : SbcConstants.ProtoFilter8;
        
        int outPos = outputOffset;
        
        for (int blk = 0; blk < blocks; blk++)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                // Dequantize samples
                double[] sb_sample = new double[subbands];
                
                for (int sb = 0; sb < subbands; sb++)
                {
                    int levels = 1 << frame.ScaleFactors[ch, sb];
                    double scalefactor = levels * Math.Pow(2.0, -frame.ScaleFactors[ch, sb]);
                    
                    int raw = frame.AudioSamples[blk, ch, sb];
                    if (raw == 0)
                    {
                        sb_sample[sb] = 0;
                    }
                    else
                    {
                        double sample = (raw * 2.0 + 1.0) / levels - 1.0;
                        sb_sample[sb] = scalefactor * sample;
                    }
                }
                
                // Shift V buffer
                for (int i = subbands * 10 - 1; i >= subbands; i--)
                {
                    V[ch, i] = V[ch, i - subbands];
                }
                
                // Matrixing - compute new V values
                for (int k = 0; k < subbands; k++)
                {
                    double sum = 0;
                    for (int i = 0; i < subbands; i++)
                    {
                        double angle = (i + 0.5) * (k + 0.5) * Math.PI / subbands;
                        sum += sb_sample[i] * Math.Cos(angle);
                    }
                    V[ch, k] = sum;
                }
                
                // Generate output samples using prototype filter
                for (int i = 0; i < subbands; i++)
                {
                    double sum = 0;
                    for (int k = 0; k < subbands * 10; k++)
                    {
                        double angle = (k + 0.5) * (i + 0.5) * Math.PI / subbands;
                        sum += V[ch, k] * protoFilter[k] * Math.Cos(angle);
                    }
                    
                    // Clamp to 16-bit range
                    int sample = (int)(sum * 32768.0);
                    if (sample > 32767) sample = 32767;
                    if (sample < -32768) sample = -32768;
                    
                    output[outPos++] = (short)sample;
                }
            }
        }
    }
    
    private class BitReader
    {
        private readonly byte[] data;
        private int byteOffset;
        private int bitOffset;
        
        public int BytesRead => byteOffset + (bitOffset > 0 ? 1 : 0);
        
        public BitReader(byte[] data, int offset)
        {
            this.data = data;
            this.byteOffset = offset;
            this.bitOffset = 0;
        }
        
        public int ReadBits(int count)
        {
            int result = 0;
            
            for (int i = 0; i < count; i++)
            {
                if (byteOffset >= data.Length)
                    return 0;
                    
                int bit = (data[byteOffset] >> (7 - bitOffset)) & 1;
                result = (result << 1) | bit;
                
                bitOffset++;
                if (bitOffset == 8)
                {
                    bitOffset = 0;
                    byteOffset++;
                }
            }
            
            return result;
        }
    }
}
