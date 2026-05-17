using SquidEyes.Pricing.Stba;

namespace SquidEyes.Pricing.UnitTests.Stba;

public class VarIntTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]            // single-byte boundary
    [InlineData(128)]            // two-byte boundary
    [InlineData(16_383)]         // two-byte max
    [InlineData(16_384)]         // three-byte boundary
    [InlineData(int.MaxValue)]
    public void Unsigned_Roundtrip(int value)
    {
        using var ms = new MemoryStream();
        TickSetEncoder.WriteVarInt(ms, value);
        ms.Position = 0;
        var decoded = TickSetDecoder.ReadVarInt(ms);
        Assert.Equal(value, decoded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(63)]
    [InlineData(-64)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Signed_Roundtrip(int value)
    {
        using var ms = new MemoryStream();
        TickSetEncoder.WriteSignedVarInt(ms, value);
        ms.Position = 0;
        var decoded = TickSetDecoder.ReadSignedVarInt(ms);
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void ReadVarInt_EmptyStream_Throws()
    {
        using var ms = new MemoryStream();
        Assert.Throws<EndOfStreamException>(() => TickSetDecoder.ReadVarInt(ms));
    }

    [Fact]
    public void ReadVarInt_TruncatedMidStream_Throws()
    {
        // 0x80 = continuation bit set with low nibble 0 — needs at least one more byte
        using var ms = new MemoryStream([0x80]);
        Assert.Throws<EndOfStreamException>(() => TickSetDecoder.ReadVarInt(ms));
    }

    [Fact]
    public void ReadVarInt_TooLong_Throws()
    {
        // Six 0x80 bytes = 5 continuations + still asking for more → exceeds MaxVarIntShift (35)
        using var ms = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80, 0x80]);
        Assert.Throws<InvalidDataException>(() => TickSetDecoder.ReadVarInt(ms));
    }
}
