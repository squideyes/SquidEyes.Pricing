using System.IO.Compression;

namespace SquidEyes.Pricing.Stba;

public static class StbaEncoder
{
    private static readonly byte[] Magic = "STBA"u8.ToArray();
    private const byte Version = 4;

    // 2-bit wire codes for PriceKind. Stored alongside time delta as (timeDelta << 2) | wire.
    // TradeBid and TradeAsk share the trade delta stream — futures trades alternate aggressor
    // rapidly at near-identical prices, so a single shared stream yields tighter price deltas.
    private const int WireBid      = 0;
    private const int WireAsk      = 1;
    private const int WireTradeBid = 2;
    private const int WireTradeAsk = 3;

    public static void Encode(TickSet tickSet, Stream output)
    {
        using var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        var rawTicks = tickSet.RawTicks;

        int basePriceT = 0, basePriceB = 0, basePriceA = 0;
        int baseTime = int.MaxValue;

        foreach (var tick in rawTicks)
        {
            if (tick.TimeMs < baseTime)
                baseTime = tick.TimeMs;

            switch (tick.Kind)
            {
                case PriceKind.TradeBid when basePriceT == 0: basePriceT = tick.PriceTicks; break;
                case PriceKind.TradeAsk when basePriceT == 0: basePriceT = tick.PriceTicks; break;
                case PriceKind.Bid      when basePriceB == 0: basePriceB = tick.PriceTicks; break;
                case PriceKind.Ask      when basePriceA == 0: basePriceA = tick.PriceTicks; break;
            }
        }

        if (baseTime == int.MaxValue)
            baseTime = 0;

        writer.Write(Magic);
        writer.Write(Version);

        var symbolBytes = System.Text.Encoding.ASCII.GetBytes(tickSet.Instrument.Symbol.ToString().PadRight(2)[..2]);
        writer.Write(symbolBytes);

        writer.Write(tickSet.Date.DayNumber);

        var contractBytes = System.Text.Encoding.ASCII.GetBytes(tickSet.Contract.Code.PadRight(4)[..4]);
        writer.Write(contractBytes);

        writer.Write(basePriceT);
        writer.Write(basePriceB);
        writer.Write(basePriceA);
        writer.Write(baseTime);
        writer.Write(tickSet.Count);

        using var recordBuffer = new MemoryStream();
        EncodeRecords(rawTicks, recordBuffer, baseTime, basePriceT, basePriceB, basePriceA);

        using var compressedBuffer = new MemoryStream();
        using (var brotli = new BrotliStream(compressedBuffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(recordBuffer.GetBuffer(), 0, (int)recordBuffer.Length);
        }

        writer.Write((int)compressedBuffer.Length);
        writer.Write(compressedBuffer.GetBuffer(), 0, (int)compressedBuffer.Length);
    }

    private static void EncodeRecords(IReadOnlyList<TickData> ticks, Stream output,
        int baseTime, int basePriceT, int basePriceB, int basePriceA)
    {
        int lastTime = baseTime;
        int lastPriceT = basePriceT, lastPriceB = basePriceB, lastPriceA = basePriceA;

        foreach (var tick in ticks)
        {
            var wire = ToWireKind(tick.Kind);   // single point of PriceKind validation
            var timeDelta = tick.TimeMs - lastTime;
            WriteVarInt(output, (timeDelta << 2) | wire);
            lastTime = tick.TimeMs;

            int priceDelta;
            switch (wire)
            {
                case WireBid:
                    priceDelta = tick.PriceTicks - lastPriceB;
                    lastPriceB = tick.PriceTicks;
                    break;
                case WireAsk:
                    priceDelta = tick.PriceTicks - lastPriceA;
                    lastPriceA = tick.PriceTicks;
                    break;
                default:   // WireTradeBid or WireTradeAsk — shared trade delta stream
                    priceDelta = tick.PriceTicks - lastPriceT;
                    lastPriceT = tick.PriceTicks;
                    break;
            }
            WriteSignedVarInt(output, priceDelta);
            WriteVarInt(output, tick.Size);
        }
    }

    private static int ToWireKind(PriceKind kind) => kind switch
    {
        PriceKind.Bid      => WireBid,
        PriceKind.Ask      => WireAsk,
        PriceKind.TradeBid => WireTradeBid,
        PriceKind.TradeAsk => WireTradeAsk,
        _ => throw new InvalidOperationException($"Unsupported PriceKind: {kind}")
    };

    internal static void WriteVarInt(Stream s, int value)
    {
        var v = (uint)value;
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        s.WriteByte((byte)v);
    }

    internal static void WriteSignedVarInt(Stream s, int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        WriteVarInt(s, (int)zigzag);
    }
}
