using System.Text;
using SquidEyes.Pricing.Stbad;

namespace SquidEyes.Pricing.UnitTests.Stbad;

public class Crc32CTests
{
    [Fact]
    public void Compute_KnownVector_Matches()
    {
        // CRC-32C of the canonical "123456789" check string is 0xE3069283.
        var data = Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0xE3069283u, Crc32C.Compute(data));
    }

    [Fact]
    public void Compute_Empty_IsZero()
    {
        Assert.Equal(0u, Crc32C.Compute([]));
    }

    [Fact]
    public void Compute_DetectsSingleBitFlip()
    {
        var a = new byte[] { 1, 2, 3, 4, 5 };
        var b = new byte[] { 1, 2, 3, 4, 7 };
        Assert.NotEqual(Crc32C.Compute(a), Crc32C.Compute(b));
    }
}
