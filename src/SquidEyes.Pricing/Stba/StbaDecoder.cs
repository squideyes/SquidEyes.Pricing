using System.IO.Compression;

namespace SquidEyes.Pricing.Stba;

public static class StbaDecoder
{
    private static readonly byte[] ExpectedMagic = "STBA"u8.ToArray();
    private const byte ExpectedVersion = 4;
    private const int MaxDecompressedSize = 100 * 1024 * 1024;
    private const int MaxVarIntShift = 35;

    public static TickSet Decode(Stream input)
    {
        using var reader = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);

        var magic = reader.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(ExpectedMagic))
            throw new InvalidDataException("Invalid STBA file: bad magic");

        var version = reader.ReadByte();
        if (version != ExpectedVersion)
            throw new InvalidDataException($"Unsupported STBA version: {version} (expected {ExpectedVersion})");

        var symbolBytes = reader.ReadBytes(2);
        var symbolCode = System.Text.Encoding.ASCII.GetString(symbolBytes).TrimEnd('\0', ' ');
        if (!Instrument.IsSupported(symbolCode))
            throw new InvalidDataException($"Unsupported symbol: {symbolCode}");
        var instrument = Instrument.Parse(symbolCode);

        var dayNumber = reader.ReadInt32();
        var date = DateOnly.FromDayNumber(dayNumber);

        var contractBytes = reader.ReadBytes(4);
        var contractCode = System.Text.Encoding.ASCII.GetString(contractBytes).TrimEnd('\0', ' ');
        var contract = Contract.Create(instrument.Symbol, contractCode);

        var basePriceT = reader.ReadInt32();
        var basePriceB = reader.ReadInt32();
        var basePriceA = reader.ReadInt32();
        var baseTime = reader.ReadInt32();
        var recordCount = reader.ReadInt32();

        var compressedLength = reader.ReadInt32();
        var compressedData = reader.ReadBytes(compressedLength);

        using var compressedStream = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();

        var buffer = new byte[8192];
        int totalRead = 0, bytesRead;
        while ((bytesRead = brotli.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxDecompressedSize)
                throw new InvalidDataException($"Decompressed data exceeds {MaxDecompressedSize} bytes");
            decompressed.Write(buffer, 0, bytesRead);
        }
        decompressed.Position = 0;

        var builder = TickSet.CreateBuilder(instrument, date, contract);
        DecodeRecords(decompressed, builder, recordCount, baseTime, basePriceT, basePriceB, basePriceA);

        return builder.Build();
    }

    private static void DecodeRecords(Stream input, TickSet.Builder builder,
        int recordCount, int baseTime, int basePriceT, int basePriceB, int basePriceA)
    {
        int lastTime = baseTime;
        int lastPriceT = basePriceT, lastPriceB = basePriceB, lastPriceA = basePriceA;

        for (int i = 0; i < recordCount; i++)
        {
            var packed = ReadVarInt(input);
            var timeDelta = packed >> 2;
            var wire = packed & 0x03;             // always 0..3
            lastTime += timeDelta;

            var priceDelta = ReadSignedVarInt(input);

            PriceKind kind;
            int priceTicks;
            switch (wire)
            {
                case 0:
                    kind = PriceKind.Bid;
                    priceTicks = lastPriceB + priceDelta;
                    lastPriceB = priceTicks;
                    break;
                case 1:
                    kind = PriceKind.Ask;
                    priceTicks = lastPriceA + priceDelta;
                    lastPriceA = priceTicks;
                    break;
                case 2:
                    kind = PriceKind.TradeBid;
                    priceTicks = lastPriceT + priceDelta;
                    lastPriceT = priceTicks;
                    break;
                default:   // wire == 3, TradeAsk; shared lastPriceT
                    kind = PriceKind.TradeAsk;
                    priceTicks = lastPriceT + priceDelta;
                    lastPriceT = priceTicks;
                    break;
            }

            var size = ReadVarInt(input);
            builder.Add(lastTime, kind, priceTicks, size);
        }
    }

    internal static int ReadVarInt(Stream s)
    {
        int result = 0, shift = 0;
        int b;

        do
        {
            b = s.ReadByte();

            if (b < 0)
                throw new EndOfStreamException();

            result |= (b & 0x7F) << shift;
            shift += 7;

            if (shift > MaxVarIntShift)
                throw new InvalidDataException("VarInt too long (possible data corruption)");
        }
        while ((b & 0x80) != 0);

        return result;
    }

    internal static int ReadSignedVarInt(Stream s)
    {
        var zigzag = (uint)ReadVarInt(s);
        return (int)((zigzag >> 1) ^ (~(zigzag & 1) + 1));
    }
}
