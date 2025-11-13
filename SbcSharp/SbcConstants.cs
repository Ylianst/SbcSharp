namespace SbcSharp;

/// <summary>
/// Constants and tables for SBC codec based on A2DP v1.3.2 and HFP v1.8 specifications.
/// </summary>
public static class SbcConstants
{
    // SBC frame constants
    public const int MaxChannels = 2;
    public const int MaxSubbands = 8;
    public const int MaxBlocks = 16;
    
    // Sampling frequencies (Hz)
    public const int Freq16000 = 16000;
    public const int Freq32000 = 32000;
    public const int Freq44100 = 44100;
    public const int Freq48000 = 48000;
    
    // Syncword
    public const byte Syncword = 0x9C;
    
    // CRC-8 polynomial: x^8 + x^4 + x^3 + x^2 + 1
    public static readonly byte[] Crc8Table = new byte[256];
    
    static SbcConstants()
    {
        // Initialize CRC-8 table
        for (int i = 0; i < 256; i++)
        {
            int crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x80) != 0)
                    crc = (crc << 1) ^ 0x1D;
                else
                    crc = crc << 1;
            }
            Crc8Table[i] = (byte)(crc & 0xFF);
        }
    }
    
    // Prototype filter coefficients for analysis (4-band and 8-band)
    public static readonly double[] ProtoFilter4 = new double[]
    {
        0.00000000E+00, 5.36548976E-04,
        1.49188357E-03, 2.73370904E-03,
        3.83720193E-03, 3.89205149E-03,
        1.86581691E-03, -3.06012286E-03,
        
        1.09137620E-02, 2.04385087E-02,
        2.88757392E-02, 3.21939290E-02,
        2.58767811E-02, 6.13245186E-03,
        -2.88217274E-02, -7.76463494E-02,
        
        1.35593274E-01, 1.94987841E-01,
        2.46636662E-01, 2.81828203E-01,
        2.94315332E-01, 2.81828203E-01,
        2.46636662E-01, 1.94987841E-01,
        
        1.35593274E-01, -7.76463494E-02,
        -2.88217274E-02, 6.13245186E-03,
        2.58767811E-02, 3.21939290E-02,
        2.88757392E-02, 2.04385087E-02,
        
        1.09137620E-02, -3.06012286E-03,
        1.86581691E-03, 3.89205149E-03,
        3.83720193E-03, 2.73370904E-03,
        1.49188357E-03, 5.36548976E-04,
    };
    
    public static readonly double[] ProtoFilter8 = new double[]
    {
        0.00000000E+00, 1.56575398E-04,
        3.43256425E-04, 5.54620202E-04,
        8.23919506E-04, 1.13992507E-03,
        1.47640169E-03, 1.78371725E-03,
        2.01182542E-03, 2.10371989E-03,
        1.99454554E-03, 1.61656283E-03,
        9.02154502E-04, -1.78805361E-04,
        -1.64973098E-03, -3.49717454E-03,
        
        5.65949473E-03, 8.02941163E-03,
        1.04584443E-02, 1.27472335E-02,
        1.46525263E-02, 1.59045603E-02,
        1.62208471E-02, 1.53184106E-02,
        1.29371806E-02, 8.85757540E-03,
        2.92408442E-03, -4.91578024E-03,
        -1.46404076E-02, -2.61098752E-02,
        -3.90751381E-02, -5.31873032E-02,
        
        6.79989431E-02, 8.29847578E-02,
        9.75753918E-02, 1.11196689E-01,
        1.23264548E-01, 1.33264415E-01,
        1.40753505E-01, 1.45389847E-01,
        1.46955068E-01, 1.45389847E-01,
        1.40753505E-01, 1.33264415E-01,
        1.23264548E-01, 1.11196689E-01,
        9.75753918E-02, 8.29847578E-02,
        
        6.79989431E-02, -5.31873032E-02,
        -3.90751381E-02, -2.61098752E-02,
        -1.46404076E-02, -4.91578024E-03,
        2.92408442E-03, 8.85757540E-03,
        1.29371806E-02, 1.53184106E-02,
        1.62208471E-02, 1.59045603E-02,
        1.46525263E-02, 1.27472335E-02,
        1.04584443E-02, 8.02941163E-03,
        
        5.65949473E-03, -3.49717454E-03,
        -1.64973098E-03, -1.78805361E-04,
        9.02154502E-04, 1.61656283E-03,
        1.99454554E-03, 2.10371989E-03,
        2.01182542E-03, 1.78371725E-03,
        1.47640169E-03, 1.13992507E-03,
        8.23919506E-04, 5.54620202E-04,
        3.43256425E-04, 1.56575398E-04,
    };
    
    // Quantization levels for scale factors
    public static readonly int[] Levels = new int[] { 4, 8, 12, 16 };
    
    // Offset values for dequantization
    public static readonly double[] Offset4 = new double[]
    {
        -1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0
    };
    
    public static readonly double[] Offset8 = new double[]
    {
        -2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0
    };
}
