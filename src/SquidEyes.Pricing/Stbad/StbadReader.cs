using System.Text;

namespace SquidEyes.Pricing.Stbad;

/// <summary>
/// Forward-streaming, allocation-free reader over a <c>.stbad</c> file. Pull one event at a time with
/// <see cref="Read"/>; <see cref="Book"/> reflects the materialized depth book <em>after</em> the most
/// recently returned event. <see cref="Seek(DateTime)"/> jumps to a mid-session time in O(block) via
/// the footer index. Forward iteration mutates one reused <see cref="DepthBook"/> and allocates
/// nothing per event (only per block, when the next block is decompressed).
/// </summary>
public sealed class StbadReader : IDisposable
{
    private readonly Stream _stream;
    private readonly BinaryReader _reader;
    private readonly StbadHeader _header;
    private readonly long _headerLen;
    private readonly DepthBook _book;
    private StbadHeader.BlockIndexEntry[]? _index;

    private byte[] _payload = [];
    private int _pos;
    private int _payloadLen;
    private long _nextBlockFilePos;
    private long _lastTimeNs;
    private long _lastTradePx;

    private bool _hasPending;
    private StbadBlockCodec.DecodedEvent _pending;
    private StbadBlockCodec.DecodedEvent _decoded;

    public StbadReader(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _stream = input;
        _reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
        _header = StbadHeader.Read(_reader);
        _headerLen = input.Position;
        _nextBlockFilePos = _headerLen;
        _book = new DepthBook((double)_header.Instrument.TickSize);
    }

    public Instrument Instrument => _header.Instrument;
    public Contract Contract => _header.Contract;
    public DateOnly Date => _header.Date;
    public SessionKind Session => _header.Session;
    public Source Source => _header.Source;
    public DateTime SessionStart => _header.SessionStart;

    /// <summary>The materialized book after the most recently returned event.</summary>
    public DepthBook Book => _book;

    /// <summary>
    /// Advances to the next event. Returns false at end of stream. On success, <paramref name="evt"/>
    /// is set and <see cref="Book"/> reflects the state after it.
    /// </summary>
    public bool Read(out DepthEvent evt)
    {
        if (_hasPending)
        {
            _hasPending = false;
            evt = ToDepthEvent(in _pending);
            return true;
        }

        if (DecodeNext(ref _decoded))
        {
            evt = ToDepthEvent(in _decoded);
            return true;
        }

        evt = default;
        return false;
    }

    /// <summary>Convenience enumeration over <see cref="Read"/> (allocates one enumerator, not per event).</summary>
    public IEnumerable<DepthEvent> ReadAll()
    {
        while (Read(out var e))
            yield return e;
    }

    /// <summary>
    /// Positions the reader so the next <see cref="Read"/> returns the first event at or after
    /// <paramref name="et"/>, with <see cref="Book"/> correct as of that point. O(block size).
    /// </summary>
    public void Seek(DateTime et)
    {
        var index = _index ??= _header.ReadBlockIndex(_reader);
        var targetNs = Math.Max(0, (et - _header.SessionStart).Ticks * 100L);

        // Rightmost block whose first event ts <= target (else the first block).
        var blockPos = _headerLen;
        if (index.Length > 0)
        {
            var chosen = index[0];
            foreach (var entry in index)
            {
                if (entry.FirstTsNs <= targetNs) chosen = entry;
                else break;
            }
            blockPos = chosen.ByteOffset;
        }

        _nextBlockFilePos = blockPos;
        _payload = [];
        _pos = 0;
        _payloadLen = 0;
        _hasPending = false;

        while (DecodeNext(ref _decoded))
        {
            if (_decoded.TimeNs >= targetNs)
            {
                _pending = _decoded;
                _hasPending = true;
                return;
            }
        }
    }

    private bool DecodeNext(ref StbadBlockCodec.DecodedEvent ev)
    {
        while (_pos >= _payloadLen)
        {
            if (!LoadNextBlock())
                return false;
        }

        StbadBlockCodec.ReadEvent(
            _payload, ref _pos, _book, _header.HasOrderCount,
            ref _lastTimeNs, ref _lastTradePx, ref ev, changes: null);
        return true;
    }

    private bool LoadNextBlock()
    {
        if (_nextBlockFilePos >= _header.FooterOffset)
            return false;

        _stream.Position = _nextBlockFilePos;
        var compLen = (int)_reader.ReadUInt32();
        var compressed = _reader.ReadBytes(compLen);
        if (compressed.Length != compLen)
            throw new InvalidDataException("Truncated .stbad block.");
        _nextBlockFilePos = _stream.Position;

        _payload = StbadBlockCodec.Decompress(compressed, 0, compLen, _header.Codec);
        _payloadLen = _payload.Length;
        _pos = 0;

        _book.ResetAll();
        var keyTs = StbadBlockCodec.ReadKeyframe(_payload, ref _pos, _book, _header.HasOrderCount);
        _lastTimeNs = keyTs;
        _lastTradePx = _book.BidSize(0) > 0 ? _book.BidPxTicks(0) : 0;
        return true;
    }

    private DepthEvent ToDepthEvent(in StbadBlockCodec.DecodedEvent ev)
    {
        var onET = _header.SessionStart.AddTicks(ev.TimeNs / 100);
        return ev.Type == DepthEventType.Trade
            ? DepthEvent.Trade(onET, ev.Aggressor, ev.TradePriceTicks * (double)_header.TickSize, ev.TradeSize)
            : DepthEvent.Quote(onET, ev.BidMask, ev.AskMask);
    }

    public void Dispose() => _reader.Dispose();
}
