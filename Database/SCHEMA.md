# Live Database Schema (Authoritative)

This file records the **actual schema installed on site** for the `Systems_One`
database. The service connects to a database created and owned by a separate
scanning application — we do **not** control its schema, we must match it.

**Before changing `Models/UploadRecord.cs`, `Data/ApplicationDbContext.cs`, or
`Database/Setup.sql`, consult this file and keep all three in sync with it.**

CLR type mismatches (e.g. mapping a `DECIMAL` column to `long`/`int`) throw
`System.InvalidCastException` at query time — they compile fine but fail at
runtime. Match the SQL type exactly.

Last verified against the live `INFORMATION_SCHEMA` dump: **2026-05-27**.

## ItemLog (the only table this service reads/writes)

| # | Column | SQL Type | Nullable | Identity | Default | CLR type in `UploadRecord` |
|---|--------|----------|----------|----------|---------|-----------------------------|
| 1 | Id | INT | NO | YES | — | `int` |
| 2 | ItemDateTime | DATETIME | NO | — | — | `DateTime` |
| 3 | Barcode | NVARCHAR(200) | YES | — | — | `string?` |
| 4 | Length | DECIMAL(10,1) | YES | — | — | `decimal?` |
| 5 | Width | DECIMAL(10,1) | YES | — | — | `decimal?` |
| 6 | Height | DECIMAL(10,1) | YES | — | — | `decimal?` |
| 7 | Weight | DECIMAL(18,3) | YES | — | — | `decimal?` |
| 8 | BoxVolume | DECIMAL(18,2) | YES | — | — | `decimal?` |
| 9 | LiquidVolume | DECIMAL(18,2) | YES | — | — | `decimal?` |
| 10 | NoDimension | BIT | YES | — | — | `bool?` |
| 11 | NoWeight | BIT | YES | — | — | `bool?` |
| 12 | Sent | BIT | YES | — | — | `bool?` |
| 13 | ImageSent | BIT | YES | — | — | `bool?` |
| 14 | Valid | BIT | YES | — | — | `bool?` |
| 15 | Complete | BIT | YES | — | — | `bool?` |
| 16 | ItemSpec | SMALLINT | YES | — | — | `short?` |
| 17 | ItemCount | SMALLINT | YES | — | — | `short?` |
| 18 | LegacyId | INT | YES | — | `NEXT VALUE FOR [dbo].[LegacyIdSeq]` | not mapped |
| 19 | StoreId | NVARCHAR(64) | YES | — | — | `string?` |
| 20 | StoreName | NVARCHAR(400) | YES | — | — | `string?` |
| 21 | NoData | BIT | NO | — | `((0))` | `bool` |
| 22 | ErrorDescription | NVARCHAR(1000) | YES | — | — | `string?` |
| 23 | Direction | VARCHAR(10) | NO | — | `('Forward')` | `string` |
| 24 | TransactionType | NVARCHAR(40) | YES | — | `(N'Normal')` | `string?` |

`CK_ItemLog_Direction`: `Direction` must be `'Both'`, `'Backward'`, or `'Forward'`.

### Gotchas that have already bitten us
- `BoxVolume` / `LiquidVolume` are **DECIMAL(18,2)**, not BIGINT. Mapping them to
  `long` caused `InvalidCastException: Unable to cast Decimal to Int64`.
- `ItemSpec` / `ItemCount` are **SMALLINT** → `short`, not `int`.
- `Valid` is nullable BIT; the unsent query must use `r.Valid == true`, not `r.Valid`.
- String column widths above are the live widths — `ApplicationDbContext` `HasMaxLength`
  values must match so future inserts don't silently truncate.

## Other tables in the database (NOT used by this service)

The live `Systems_One` database also contains the tables below. They belong to
other applications (scanning app, PLC bridge, trip/driver tracking, legacy stats).
**Do not read or write these from this service.** Listed only so the schema picture
is complete and we don't accidentally confuse `tbl_Measurement` (a legacy table) with
`ItemLog` (the current one this service uses).

- `DailyStats`, `tbl_Daily_Stats` — daily statistics rollups
- `driver_log` — driver name/code
- `Plc2OutboundQueue` — PLC outbound message queue
- `tbl_Error_Codes`, `tbl_Error_Log` — error logging
- `tbl_Measurement`, `tbl_Weight` — **legacy** measurement tables (predecessors to `ItemLog`)
- `Trip`, `TripMessageLog` — trip / event tracking
