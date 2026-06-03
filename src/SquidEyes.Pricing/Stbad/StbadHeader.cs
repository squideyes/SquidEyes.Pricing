using System.Text;

namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// The parsed fixed header of a <c>.stbad</c> file, plus the lazily-read footer block index. Both
/// are cheap to read (fixed header, footer at a known offset), keeping the format mmap-friendly.
/// </summary>
internal sealed class StbadHeader
{
    public required Instrument Instrument { get; init; }
    public required Contract Contract { get; init; }
    public required DateOnly Date { get; init; }
    public required Source Source { get; init; }
    public required SessionKind Session { get; init; }
    public required bool HasOrderCount { get; init; }
    public required double TickSize { get; init; }
    public required double PointValue { get; init; }
    public required DateTime SessionStart { get; init; }
    public required byte Codec { get; init; }
    public required int KeyframeMaxEvents { get; init; }
    public required int KeyframeMaxMillis { get; init; }
    public required int BlockCount { get; init; }
    public required long FooterOffset { get; init; }

    public readonly record struct BlockIndexEntry(long FirstTsNs, long ByteOffset, int EventCount);

    public static StbadHeader Read(BinaryReader r)
    {
        var magic = r.ReadBytes(StbadFormat.Magic.Length);
        if (!magic.AsSpan().SequenceEqual(StbadFormat.Magic))
            throw new InvalidDataException("Invalid .stbad file: bad magic.");

        var version = r.ReadByte();
        if (version != StbadFormat.Version)
            throw new InvalidDataException(
                $"Unsupported .stbad version: {version} (expected {StbadFormat.Version}).");

        var symbolCode = Encoding.ASCII.GetString(r.ReadBytes(4)).TrimEnd('\0', ' ');
        if (!Instrument.IsSupported(symbolCode))
            throw new InvalidDataException($"Unsupported symbol: {symbolCode}");
        var instrument = Instrument.Parse(symbolCode);

        var contractCode = Encoding.ASCII.GetString(r.ReadBytes(4)).TrimEnd('\0', ' ');
        var contract = Contract.Create(instrument.Symbol, contractCode);

        var date = DateOnly.FromDayNumber(r.ReadInt32());
        var source = (Source)r.ReadByte();
        var session = (SessionKind)r.ReadByte();

        var levels = r.ReadByte();
        if (levels != StbadFormat.Levels)
            throw new InvalidDataException($"Unsupported level count: {levels} (expected {StbadFormat.Levels}).");

        var fields = r.ReadByte();
        var hasCount = (fields & StbadFormat.FieldHasOrderCount) != 0;
        var tickSize = r.ReadDouble();
        var pointValue = r.ReadDouble();

        var tsResolution = r.ReadByte();
        if (tsResolution != StbadFormat.TsResolutionNanos)
            throw new InvalidDataException($"Unsupported timestamp resolution id: {tsResolution}.");

        var sessionStart = new DateTime(r.ReadInt64(), DateTimeKind.Unspecified);
        var codec = r.ReadByte();
        var kfMaxEvents = r.ReadInt32();
        var kfMaxMillis = r.ReadInt32();
        var blockCount = r.ReadInt32();
        var footerOffset = r.ReadInt64();

        return new StbadHeader
        {
            Instrument = instrument,
            Contract = contract,
            Date = date,
            Source = source,
            Session = session,
            HasOrderCount = hasCount,
            TickSize = tickSize,
            PointValue = pointValue,
            SessionStart = sessionStart,
            Codec = codec,
            KeyframeMaxEvents = kfMaxEvents,
            KeyframeMaxMillis = kfMaxMillis,
            BlockCount = blockCount,
            FooterOffset = footerOffset,
        };
    }

    /// <summary>Reads the footer block index (seek table). Leaves the stream position undefined.</summary>
    public BlockIndexEntry[] ReadBlockIndex(BinaryReader r)
    {
        r.BaseStream.Position = FooterOffset;
        var count = r.ReadInt32();
        var entries = new BlockIndexEntry[count];
        for (var i = 0; i < count; i++)
            entries[i] = new BlockIndexEntry(r.ReadInt64(), r.ReadInt64(), r.ReadInt32());
        return entries;
    }
}
