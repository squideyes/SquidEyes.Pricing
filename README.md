# SquidEyes.Pricing

[![NuGet](https://img.shields.io/nuget/v/SquidEyes.Pricing.svg)](https://www.nuget.org/packages/SquidEyes.Pricing)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Pricing-data primitives for futures backtesting:

- `Symbol` / `Instrument` / `Contract` — typed futures identities with tick sizes and point values. `Instrument.Kind` is an `InstrumentKind` enum (currently only `Future`).
- `PriceKind` — `[Flags]` enum: `Bid`, `Ask`, `TradeBid` (seller hit bid), `TradeAsk` (buyer lifted ask); the `Trade` member is `TradeBid | TradeAsk` — use it as a mask, not a stored value
- `SessionKind` — named ET trading windows (`DTH`, `MTH`, `RTH`) with simple expansion to `(TimeOnly From, TimeOnly Until)`
- `Session` — `SessionKind` + date, validated against a US-futures holiday calendar. Supports `Embargo`s (no-trade sub-windows) of three kinds: **AdHoc** (explicit times), **News** (anchored to a wall-clock DateTime, widened by `NewsImpactDefaults`), and **Anchored** (relative to session Start/End for a `TimeSpan`). `session.IsTradable(when)` answers "in window AND not embargoed?"
- `TickSet` — immutable, ordered collection of `(OnET, Kind, Price, Size)` ticks with same-key aggregation
- `Candle` — single OHLCV bar over an arbitrary `[FromET, UntilET)` window. Interval-agnostic — build tick-by-tick via `AddTrade`/`TryAdd`, or load from pre-computed OHLCV. Trade-only (Bid/Ask quotes are silently skipped by `TryAdd`).
- `CandleSet` — rolling, capacity-bounded collection of candles built tick-by-tick. Two flavours: `IntervalCandleSet` (time-bucketed, wall-clock aligned) and `RenkoCandleSet` (price-driven; brick size = `brickTicks * Instrument.TickSize`, with or without wicks). Both raise a `CandleClosed` event and expose newest-first indexing (`set[0]` = most recent).
- `EasternTime` — shared DST-correct ET ↔ UTC helpers (`FromUtc`, `ToUtc`, `TodayEt`, `WindowToUtc`). Single source of truth for "what zone does this library think it's in?"
- `PricingFile` — canonical SquidEyes filename convention `{Symbol}_{yyyyMMdd}_{Contract}_{Source}_{Session}_ET`. `BuildStem` produces it; `TryParseStem` recovers the typed tuple back out.
- `DateOnlyExtenders` trade-calendar query helpers — `IsTradeDate`, `EarliestTradeDate`, `LatestTradeDateBefore`, `EnumerateTradeDates`.
- `StbaEncoder` / `StbaDecoder` — **STBA** (Squideyes Trade/Bid/Ask) compact binary format; typically ~10× smaller than Parquet for MBP-1 tick streams
- `StbaCsvEncoder` — symmetric CSV companion to `StbaEncoder`. Same logical content (`OnET,Kind,Price,Size`, kinds `B/A/H/L`), human-readable, diffable.

## Install

```powershell
dotnet add package SquidEyes.Pricing
```

Target: `net8.0+`.

## Quickstart

```csharp
using SquidEyes.Pricing;
using SquidEyes.Pricing.Stba;

// Build a tick set
var date = new DateOnly(2026, 1, 5);
var contract = Contract.Create(Symbol.ES, "H26");

var builder = TickSet.CreateBuilder(Symbol.ES, date, contract);
builder.Add(timeMs: 28_800_000, PriceKind.Bid,      6900.00m, 10);  // 08:00:00.000 ET
builder.Add(timeMs: 28_800_000, PriceKind.Ask,      6900.25m, 15);
builder.Add(timeMs: 28_800_000, PriceKind.TradeAsk, 6900.25m,  3);  // buyer lifted ask
var ticks = builder.Build();

// Query: count any trades (matches both TradeBid and TradeAsk)
var tradeCount = ticks.Count(t => (t.Kind & PriceKind.Trade) != 0);

// Encode to STBA
using (var fs = File.Create("ES_20260105_H26.stba"))
    StbaEncoder.Encode(ticks, fs);

// Decode
using (var fs = File.OpenRead("ES_20260105_H26.stba"))
{
    var decoded = StbaDecoder.Decode(fs);
    foreach (var tick in decoded)
        Console.WriteLine($"{tick.OnET:o}  {tick.Kind}  {tick.Price}  {tick.Volume}");
}

// Sessions + embargoes — filter ticks to a tradable RTH window
var session = Session.Create(date, SessionKind.RTH);                    // 09:30–16:00 ET
session.AddAnchoredEmbargo(SessionAnchor.Start, TimeSpan.FromMinutes(1), "open-1m");
session.AddAnchoredEmbargo(SessionAnchor.End,   TimeSpan.FromMinutes(1), "close-1m");
session.AddNewsEmbargo(new DateTime(2026, 1, 5, 14, 0, 0), NewsImpact.High, "FOMC");

var tradable = ticks.Where(t => session.IsTradable(t.OnET)).ToList();

// Build 1-minute interval candles and 4-tick Renko bricks tick-by-tick
var oneMin = new IntervalCandleSet(intervalSeconds: 60, capacity: 390);
var renko  = new RenkoCandleSet(Instrument.Create(Symbol.ES),
                                brickTicks: 4, capacity: 200, withWicks: true);

oneMin.CandleClosed += (_, e) =>
    Console.WriteLine($"1m closed: {e.ClosedCandle.FromET:HH:mm} O={e.ClosedCandle.Open} C={e.ClosedCandle.Close}");

foreach (var tick in ticks)
{
    oneMin.ProcessTick(tick);
    renko.ProcessTick(tick);
}

var lastMinute = oneMin.Current;   // in-progress bar
var lastBrick  = renko.Current;    // most recently closed brick (Renko bricks are born closed)
```

## STBA binary format (v4)

```text
"STBA" magic           (4 bytes)
version                u8 = 4
symbol                 2 ASCII chars, space-padded
date day_number        i32
contract_code          4 ASCII chars, space-padded
base_price_trade       i32   (first TradeBid/TradeAsk price in ticks)
base_price_bid         i32
base_price_ask         i32
base_time_ms           i32   (first event ms-since-midnight ET)
record_count           i32
compressed_length      i32
brotli-compressed records:
    foreach tick:
        (time_delta << 2) | wire_kind   — varint
        price_delta                      — zig-zag varint, relative to last same-stream price
        size                             — varint

wire_kind (2 bits):
    0 = Bid           uses lastPriceBid    delta stream
    1 = Ask           uses lastPriceAsk    delta stream
    2 = TradeBid      uses lastPriceTrade  delta stream  (shared with TradeAsk)
    3 = TradeAsk      uses lastPriceTrade  delta stream  (shared with TradeBid)
```

TradeBid and TradeAsk share one trade-price delta stream because futures trades alternate aggressor rapidly at near-identical prices; sharing yields tighter price deltas than two separate streams.

The encoder is deterministic — byte-identical output for byte-identical input. See [StbaEncoder.cs](src/SquidEyes.Pricing/Stba/StbaEncoder.cs).

## Namespace map

| Namespace | What's in it |
| --- | --- |
| `SquidEyes.Pricing` | `Symbol`, `Instrument`, `InstrumentKind`, `Contract`, `Source`, `PriceKind`, `SessionKind`, `Session`, `Embargo`, `EmbargoKind`, `NewsImpact`, `SessionAnchor`, `NewsImpactDefaults`, `PrePost`, `Tick`, `TickSet`, `Candle`, `CandleSet`, `IntervalCandleSet`, `RenkoCandleSet`, `CandleClosedEventArgs`, `SymbolContractParser`, `EasternTime`, `PricingFile`, `DateOnlyExtenders` (`IsTradeDate`, `IsWeekday`, `Format`, `EarliestTradeDate`, `LatestTradeDateBefore`, `EnumerateTradeDates`) |
| `SquidEyes.Pricing.Stba` | `StbaEncoder`, `StbaDecoder`, `StbaCsvEncoder` |

## Opinionated choices

- **US futures only.** Supported symbols (`Symbol.cs`): ES, NQ, CL, GC, TY, FV, US, JY, EU, BP. Adding a symbol is one enum value + one row in `Instrument.BuildCache()`. Adding a non-futures asset class is one `InstrumentKind` enum value plus a corresponding row.
- **Holiday-aware trade dates.** `Session.Create(...)` rejects weekends, US market holidays (NYD, MLK, Presidents, Good Friday, Memorial, Juneteenth, Independence, Labor, Thanksgiving, Christmas), early-close days, Easter Monday, and Boxing Day. Calendar lives in `HolidayExtenders.cs`.
- **Named session windows** — `DTH` 08:00–16:00 ET, `MTH` 08:00–12:00 ET, `RTH` 09:30–16:00 ET (CME cash session). Add more in `SessionKind.cs` — one enum value plus a switch arm in `ToTimes()` / `ToCode()` / `ParseCode()`.
- **Embargo defaults.** A News-kind embargo widens by `NewsImpactDefaults` minutes before/after its anchor (High: 5/15, Medium: 3/10 by default; settable globally at app startup).
- **Date window.** `Session.MinDate` = `2024-01-02`, `Session.MaxDate` = `2028-12-22`. Edit if you need a wider range.
- **Enum values start at 1.** `default(SomeEnum)` is always `0` and therefore invalid — easy to spot uninitialized values in debug.

## Versioning

This package follows [SemVer 2.0.0](https://semver.org/). The version is derived from the latest git tag via [MinVer](https://github.com/adamralph/minver):

- Tag `v1.2.3` → package version `1.2.3`
- Commits past a tag → pre-release `1.2.4-alpha.0.N+sha`

While `0.x.y`: API and STBA format may change between minor versions. At `1.0.0` the STBA format and core API freeze; breaking changes will bump major.

## Releasing

The package is published to [nuget.org](https://www.nuget.org/packages/SquidEyes.Pricing) by a GitHub Actions workflow that fires on `v*` tags. Setup (one-time):

1. **Get a NuGet API key** at <https://www.nuget.org/account/apikeys>. Scope it to *Push new packages and package versions* matching the glob `SquidEyes.Pricing*`. Don't make it broader than that.
2. **Add the secret to GitHub.** In the repo settings → Secrets and variables → Actions → New repository secret. Name: `NUGET_API_KEY`, value: the key from step 1.
3. **Confirm CI is green** for the commit you're about to tag (`.github/workflows/ci.yml` runs on every push/PR).

To cut a release:

```powershell
git tag v0.1.0
git push --tags
```

The `release.yml` workflow will pick it up, restore/build/test, run `dotnet pack`, and push both the `.nupkg` and the `.snupkg` (symbols) to nuget.org with `--skip-duplicate`. MinVer reads the tag and stamps the package version automatically — no version string in any csproj.

For a pre-release:

```powershell
git tag v0.2.0-beta.1
git push --tags
```

MinVer emits `0.2.0-beta.1` exactly as tagged; nuget.org marks it as a pre-release.

### Local pack (no publish)

To inspect what would ship without pushing:

```powershell
dotnet pack src\SquidEyes.Pricing -c Release -o artifacts
```

Output lands in `artifacts\SquidEyes.Pricing.<version>.nupkg`. Open it with [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer) or unzip it to verify the contents — README, LICENSE, the dll, the XML doc file, and the `.snupkg` companion.

### Consuming locally before publishing

If you want to test the package against a real consumer before tagging, add a local feed:

```powershell
mkdir C:\LocalNuGet
dotnet pack src\SquidEyes.Pricing -c Release -o C:\LocalNuGet
dotnet nuget add source C:\LocalNuGet --name LocalNuGet
```

Then in the consumer project: `dotnet add package SquidEyes.Pricing --version <semver>`. The downloader in this monorepo uses a `<ProjectReference>` instead, which is even simpler for live development.

### Yanking a bad release

If a published version is broken, **unlist** it on nuget.org rather than republishing the same version — package contents are immutable. Bump the version, fix, retag.

## License

MIT — see [LICENSE](LICENSE).
