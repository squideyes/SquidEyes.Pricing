using SquidEyes.Pricing.Stbad;

namespace SquidEyes.Pricing.UnitTests.Stbad;

public class StbadVarIntTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(127UL)]
    [InlineData(128UL)]
    [InlineData(16_383UL)]
    [InlineData(16_384UL)]
    [InlineData(ulong.MaxValue)]
    public void Unsigned_Roundtrip_Stream_And_Span(ulong value)
    {
        using var ms = new MemoryStream();
        StbadVarInt.WriteU(ms, value);
        var bytes = ms.ToArray();

        ms.Position = 0;
        Assert.Equal(value, StbadVarInt.ReadU(ms));

        var pos = 0;
        Assert.Equal(value, StbadVarInt.ReadU(bytes, ref pos));
        Assert.Equal(bytes.Length, pos);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(63L)]
    [InlineData(-64L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Signed_Roundtrip_Stream_And_Span(long value)
    {
        using var ms = new MemoryStream();
        StbadVarInt.WriteS(ms, value);
        var bytes = ms.ToArray();

        ms.Position = 0;
        Assert.Equal(value, StbadVarInt.ReadS(ms));

        var pos = 0;
        Assert.Equal(value, StbadVarInt.ReadS(bytes, ref pos));
    }

    [Fact]
    public void ReadU_EmptyStream_Throws()
    {
        using var ms = new MemoryStream();
        Assert.Throws<EndOfStreamException>(() => StbadVarInt.ReadU(ms));
    }

    [Fact]
    public void ReadU_Span_PastEnd_Throws()
    {
        var buf = new byte[] { 0x80 };
        var pos = 0;
        Assert.Throws<EndOfStreamException>(() =>
        {
            var p = pos;
            StbadVarInt.ReadU(buf, ref p);
        });
    }
}
