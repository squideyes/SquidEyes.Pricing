# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0](https://github.com/squideyes/SquidEyes.Pricing/compare/v1.0.1...v2.0.0) (2026-05-25)


### ⚠ BREAKING CHANGES

* Instrument.TickSize is now double (was decimal)
* replace NewsImpact minute properties with PrePost record

### Features

* add e-micro futures (MES, MNQ, RTY, M2K, MGC, MCL) ([be8e31e](https://github.com/squideyes/SquidEyes.Pricing/commit/be8e31eace07809c5f338c25fc2cb7be6fcceded))
* add e-micro futures (MES, MNQ, RTY, M2K, MGC, MCL) ([bdc1ee4](https://github.com/squideyes/SquidEyes.Pricing/commit/bdc1ee4cc875fd4f0fa7b8ba94e3ce64e1f03341))
* CandleSet family — rolling, tick-driven, capacity-bounded candle collections ([e3cb80b](https://github.com/squideyes/SquidEyes.Pricing/commit/e3cb80bb423ef8e9254e13b3e92afe266cd9621d))
* EasternTime, PricingFile, StbaCsvEncoder, trade-calendar helpers ([d7b7f74](https://github.com/squideyes/SquidEyes.Pricing/commit/d7b7f7417716137c8e6d17ccb7d0d72f3376b8ea))
* Instrument.TickSize is now double (was decimal) ([2e109f1](https://github.com/squideyes/SquidEyes.Pricing/commit/2e109f1bc2bffe4b186044e834418519b69b2b63))
* replace NewsImpact minute properties with PrePost record ([30cdf02](https://github.com/squideyes/SquidEyes.Pricing/commit/30cdf02e9d73c871282e159a38706143b717e5f6))

## [Unreleased]

### Added

- `SessionKind.RTH` — Regular Trading Hours, 09:30–16:00 ET (CME cash session).
- `InstrumentKind` enum (currently `Future = 1`) and corresponding `Instrument.Kind` property.
- `Embargo` — no-trade sub-window of a `Session`, with three kinds: `AdHoc` (explicit times), `News` (anchored to a wall-clock DateTime, widened by `NewsImpactDefaults`), `Anchored` (relative to session Start/End for a `TimeSpan`). Construct via `Session.AddAdHocEmbargo` / `AddNewsEmbargo` / `AddAnchoredEmbargo`.
- `NewsImpact` enum (`Medium`, `High`) plus globally-settable `NewsImpactDefaults` (defaults: High 5 before / 15 after, Medium 3 before / 10 after).
- `SessionAnchor` enum (`Start`, `End`) for anchored embargoes.
- `Session.IsTradable(when)` — convenience: in session window AND not embargoed.
- `Session.IsEmbargoed(when)` — true if any attached embargo covers `when`.
- `Candle` — single OHLCV bar over an arbitrary `[FromET, UntilET)` window. Two constructors: load pre-computed OHLCV, or start empty and build tick-by-tick via `AddTrade(price, size)` / `TryAdd(Tick)`. Trade-only (`TryAdd` silently skips Bid/Ask quotes and out-of-window ticks). Mutable by design — accumulating one trade at a time would otherwise allocate a new instance per tick.
- `CandleSet` — rolling collection of `Candle`s built tick-by-tick, with capacity-based FIFO eviction (newest at index `0`, oldest evicted when full) and a `CandleClosed` event. Two concrete kinds: `IntervalCandleSet(intervalSeconds, capacity)` for time-bucketed bars (wall-clock aligned via floor-to-interval), and `RenkoCandleSet(instrument, brickTicks, capacity, withWicks)` for price-driven Renko bricks. Renko brick size is a tick count multiplied by the instrument's `TickSize` (so brick math is exact); price reversals or single-tick chains emit multiple bricks in one call. With wicks: only the chain's first/last bricks carry extension wicks (interior bricks are body-only).
- `EasternTime` — shared DST-correct ET helpers: `Zone`, `FromUtc(DateTimeOffset)`, `ToUtc(DateTime)`, `TodayEt()`, and `WindowToUtc(date, fromEt, untilEt)`. The library already deals exclusively in ET internally; this exposes the resolver publicly so downstream consumers (downloaders, viewers, replay tools) don't have to duplicate it.
- `PricingFile.BuildStem` / `PricingFile.TryParseStem` — canonical SquidEyes filename convention `{Symbol}_{yyyyMMdd}_{Contract}_{Source}_{Session}_ET`. The forward direction lets producers write files in a uniform shape; the inverse direction lets loaders recover the typed `(Symbol, Date, Contract, Source, SessionKind)` tuple from a filename without peeking at file contents.
- `StbaCsvEncoder` — symmetric CSV companion to `StbaEncoder`. `Encode(TickSet, TextWriter)` and `Encode(TickSet, Stream)` write `OnET,Kind,Price,Size` rows (kinds `B/A/H/L`), header always included, UTF-8 without BOM. Same logical content as the binary STBA, just diffable and spreadsheet-friendly.
- `DateOnlyExtenders.EarliestTradeDate()`, `LatestTradeDateBefore(this DateOnly date)`, and `EnumerateTradeDates(from, until)` — trade-calendar query helpers that pair with the existing `IsTradeDate()` predicate.

### Changed

- **BREAKING:** Renamed `Asset` → `Instrument` (class, file, property on `TickSet`). The conceptual model now reads "Instrument = Symbol + Kind + tick mechanics".
- **BREAKING:** Nested `TickSet.TickSetBuilder` renamed to `TickSet.Builder`.
- **BREAKING:** Renamed `TimeRange` → `SessionKind`, `TimeRangeExtenders` → `SessionKindExtenders`. The enum is "what kind of session" — the concrete realized window is now called `Session`.
- **BREAKING:** Renamed `TickSpan` → `Session`. `Session` is now a class (was a readonly struct), carries its `SessionKind`, and owns a list of `Embargo`s.
- **BREAKING:** `TickSpan.MinDate` / `MaxDate` → `Session.MinDate` / `MaxDate`.
- **BREAKING:** Renamed `TickSetEncoder` → `StbaEncoder` and `TickSetDecoder` → `StbaDecoder`. Classes are STBA-format-specific; naming them after the format reads more clearly and leaves room for future format-specific encoder/decoder pairs.
- **BREAKING:** Collapsed `SquidEyes.Pricing.Calendars` namespace into the root `SquidEyes.Pricing`. `DateOnlyExtenders` is now reachable with just `using SquidEyes.Pricing;`.
- `HolidayExtenders` and `GenericValueExtenders` are now `internal` — they were implementation details previously leaking into the public surface.
- **BREAKING:** `PriceKind` is now a `[Flags]` enum with four members starting at 1: `Bid=1`, `Ask=2`, `TradeBid=4`, `TradeAsk=8`. The previous `Trade=2` becomes a derived mask `Trade = TradeBid | TradeAsk` (test "is this any trade" via `(kind & PriceKind.Trade) != 0`).
- **BREAKING:** STBA wire format bumped to **v4**. Wire kind is still 2 bits but values are remapped: `0=Bid, 1=Ask, 2=TradeBid, 3=TradeAsk`. v3 files are no longer readable.
- TradeBid and TradeAsk share one trade-price delta stream (`lastPriceTrade`) in the STBA encoder for tighter price deltas.
- Renamed `SourceExtensions` → `SourceExtenders` and `TimeRangeExtensions` → `TimeRangeExtenders` for consistency with the project's `*Extenders` naming.

## [0.1.0] - 2026-05-17

### Added

- Initial public release.
- `Symbol`, `Instrument`, `Contract`, `Source`, `PriceKind`, `TimeRange`, `TickSpan`, `Tick`, `TickSet` primitives.
- `SymbolContractParser` for Databento-style symbol+contract strings.
- STBA (v4) binary format encoder/decoder (`StbaEncoder` / `StbaDecoder`) under `SquidEyes.Pricing.Stba`.
- US futures holiday calendar under `SquidEyes.Pricing.Calendars`.
- 150 unit tests covering roundtrip encoding, calendar logic, instrument/contract validation.

[Unreleased]: https://github.com/squideyes/SquidEyes.Pricing/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/squideyes/SquidEyes.Pricing/releases/tag/v0.1.0
