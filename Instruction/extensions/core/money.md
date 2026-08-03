# Price, Money, Currency

Three `readonly struct` value objects in namespace `Zonit.Extensions`. Nothing to register.

`Price` and `Money` hold an *amount*. `Currency` holds a *code*. They are not joined into one type —
if an entity needs both, it carries both properties.

## Read this first

- **`Price` rejects a negative value in the constructor but not in arithmetic.** `new Price(-1m)` throws;
  `new Price(10m) - new Price(30m)` quietly returns `-20`. `Price` expresses non-negative *intent*, it
  does not enforce a non-negative *invariant*. If a subtraction must not go negative, check the result.
- **`Price.ToString()` and `Money.ToString()` use the current culture.** On a `pl-PL` host
  `new Price(19.99m).ToString()` is `"19,99"`, not `"19.99"`. Pass a provider when the output is machine-
  readable.
- **Parsing accepts both `,` and `.` on purpose**, ignoring the culture *and* the `IFormatProvider` you
  pass. `"19,99"` and `"19.99"` are the same price everywhere.
- **`Price`/`Money` have no `TypeConverter`.** They cannot be bound from a raw string form field. Bind
  them as `decimal` — see `.zonit/extensions/core/binding.md`.

## Price vs Money

| | `Price` | `Money` |
|---|---|---|
| Intent | product prices, unit costs | balances, transactions, adjustments, refunds |
| Constructor with a negative | throws `ArgumentOutOfRangeException` | fine |
| Negative escape hatch | `new Price(v, allowNegative: true)` / `Price.CreateAllowNegative(v)` | not needed |
| `Zero` | yes | yes |
| Emptiness flag | none — `Zero` is the only "nothing" | none |
| Sign helpers | none | `IsNegative`, `IsPositive`, `IsZero` |
| `Min`/`Max`/`Clamp` | no | yes |

Both store `decimal` rounded to **8** decimal places (`MidpointRounding.AwayFromZero`) and expose a
`DisplayValue` rounded to **2**:

```csharp
new Price(1.23456789012m).Value;   // 1.23456789   (8 dp)
new Price(19.999m).DisplayValue;   // 20.00        (2 dp, away from zero)
new Price(19.999m).ToString();     // "20.00" on en-US, "20,00" on pl-PL
new Price(19.999m).ToFullPrecisionString();   // "19.99900000" / "19,99900000"
```

`ToFullPrecisionString()` prints `Value` (the stored 8-dp number), **not** the rounded `DisplayValue` —
`19.999` stays `19.999`, it does not become `20`. It is also culture-sensitive, exactly like
`ToString()`: on a `pl-PL` host the separator is a comma. There is no provider overload, so set
`CultureInfo.CurrentCulture` (or use `Value.ToString(CultureInfo.InvariantCulture)`) when the output has
to be machine-readable.

Conversions between them:

```csharp
Money m = new Price(3m);           // implicit — Price widens to Money
Price p = m.ToPrice();             // InvalidOperationException if m is negative
m.TryToPrice(out var p2);          // false instead of throwing
Money.CreatePositive(-1m);         // ArgumentOutOfRangeException
Money.TryCreatePositive(-1m, out var m2);   // false
```

Both convert implicitly to and from `decimal`, so they compose with ordinary arithmetic:

```csharp
Price unit = 19.99m;
Price line = unit * 3;                       // 59.97
Price withVat = unit.ApplyPercentage(23);    // 24.5877  — value * (1 + pct/100)
Price vatOnly = unit.CalculatePercentage(23);// 4.5977   — value * pct/100
Money balance = Money.Zero - 12.5m;          // -12.50
```

`Price / 0m` and `Money / 0m` throw `DivideByZeroException` rather than producing infinity.

## Parsing user input

`Price.TryParse` / `Money.TryParse` (and every `IParsable` model bind and JSON string read that routes
through them) run a culture-free normalizer first. The `IFormatProvider` argument is accepted for
interface compatibility and **ignored**.

| input | result | why |
|---|---|---|
| `"19,99"` | `19.99` | single separator = decimal point |
| `"19.99"` | `19.99` | same |
| `"1.234,56"` | `1234.56` | last separator wins, earlier ones are thousands |
| `"1,234.56"` | `1234.56` | same |
| `"1 234,56"` | `1234.56` | whitespace and `_` are stripped |
| `"1.234"` | **`1.234`** | ambiguous — read as a fraction, not as 1234 |
| `"-5"` → `Price` | `false` | `Price.TryParse` rejects negatives |
| `"-5,50"` → `Money` | `-5.50` | `Money` accepts them |

The `"1.234"` row is the one that bites Polish users who mean *one thousand two hundred thirty-four*.
The unambiguous form always includes the cents: `"1.234,56"` and `"1,234.56"` both give `1234.56`.

### The input-length gate

Input longer than **512 characters** is rejected before a single character is copied:

```csharp
Price.TryParse(new string('1', 513), null, out _);   // false
Price.Parse(new string('1', 513), null);             // FormatException
```

This exists because the normalizer sizes a stack buffer from the caller's string and is reachable from
untrusted data — `MoneyJsonConverter.Read`, `PriceJsonConverter.Read` and every ASP.NET model bind. A
~400 KB numeric string used to produce an uncatchable `StackOverflowException` that killed the process.
A `decimal` holds at most 29 significant digits, so the cap only ever rejects input that could not have
parsed anyway.

## JSON

`Price` and `Money` serialize as **numbers** at full 8-decimal precision, and read either a number or a
string:

```csharp
JsonSerializer.Serialize(new Price(19.99m));        // 19.99
JsonSerializer.Deserialize<Price>("19.99");         // 19.99
JsonSerializer.Deserialize<Price>("\"19,99\"");     // 19.99  (string path, culture-free)
JsonSerializer.Deserialize<Price>("-5");            // Price.Zero — negatives are dropped, not thrown
```

Note the last line: a negative number in a JSON payload deserializes to `Price.Zero` silently. Use
`Money` if the field can legitimately be negative.

`Currency` serializes as its code string (`"PLN"`), and an unparseable value reads back as
`Currency.Empty`.

## Currency

An ISO 4217 alphabetic code, or a crypto ticker. 2-10 characters (`Currency.MinLength` /
`Currency.MaxLength`), letters and digits only, upper-cased on construction.

```csharp
var pln = Currency.PLN;             // predefined static — no allocation, no validation
Currency c  = "usd";                // implicit → Currency.USD, upper-cased
Currency c2 = "!!!";                // Currency.Empty — the implicit conversion never throws
new Currency("!!!");                // ArgumentException — the constructor does
Currency.TryCreate(input, out var c3);
Currency.IsValid(input);            // bool, no out parameter
```

Predefined statics cover the common fiat set (`USD EUR PLN GBP JPY CHF CAD AUD CZK HUF NOK SEK DKK CNY
INR RUB UAH TRY BRL MXN KRW SGD HKD NZD ZAR AED ILS RON BGN THB`) and crypto (`BTC ETH USDT USDC BNB XRP
ADA SOL DOGE DOT LTC MATIC`). `Currency.GetKnownCurrencies()` returns them all.

An unknown-but-syntactically-valid code is accepted and stored verbatim; only its metadata is missing:

```csharp
Currency x = "XYZ";
x.HasValue;   // true
x.IsKnown;    // false
x.Symbol;     // "" — fall back to Code
x.Name;       // ""
x.DecimalDigits;   // 2 (the default)
```

### `Format`

`Format(decimal amount, IFormatProvider? provider = null)` renders the amount with the currency's own
decimal-digit count and places the symbol on the side that currency conventionally uses. The provider
controls only the *number* formatting (group and decimal separators) and defaults to
`CultureInfo.CurrentCulture`.

```csharp
var inv = CultureInfo.InvariantCulture;

Currency.USD.Format(19.99m, inv);    // "$19.99"          symbol before
Currency.PLN.Format(19.99m, inv);    // "19.99 zł"        symbol after
Currency.JPY.Format(1999m, inv);     // "¥1,999"          0 decimal digits
Currency.BTC.Format(0.5m, inv);      // "0.50000000 ₿"    8 decimal digits
((Currency)"XYZ").Format(5m, inv);   // "5.00 XYZ"        no symbol -> code as suffix
Currency.Empty.Format(5m, inv);      // "5.00"            no currency at all

Currency.USD.ToDisplayString();      // "USD ($)"
Currency.USDT.ToDisplayString();     // "USDT"  — no symbol defined
```

`Format` takes a `decimal`, not a `Price`. Both `Price` and `Money` convert implicitly, so
`Currency.PLN.Format(order.Total)` compiles for either.

## Storage

```csharp
// EF Core
modelBuilder.Entity<Product>()
    .Property(p => p.Price)
    .HasConversion(v => v.Value, v => new Price(v))
    .HasPrecision(19, 8);

modelBuilder.Entity<Product>()
    .Property(p => p.Currency)
    .HasConversion(v => v.Code, v => VoRead.Currency(v))
    .HasMaxLength(Currency.MaxLength);
```

`VoRead.Currency` is a static helper wrapping `Currency.TryCreate` — `HasConversion` takes an
`Expression`, and an expression tree cannot contain `out var` (CS8198). See the EF Core section of
`.zonit/extensions/core/value-objects.md`.

`decimal(19,8)` matches the internal precision exactly; a narrower column silently rounds on write.
Use `Money` (and drop the constructor's non-negative check) for any column that can hold a debit.

## Known limitations

- `Price` arithmetic operators all pass `allowNegative: true` internally, so no operator on `Price` can
  throw for going negative. Guard the result yourself where the sign matters.
- `ApplyPercentage`/`CalculatePercentage` round to 8 decimal places at every step; chaining many of them
  accumulates the usual decimal rounding drift. Compute from the base amount rather than chaining.
- Neither `Price` nor `Money` carries a currency, so nothing prevents adding USD to PLN. Pair the amount
  with a `Currency` property and compare the codes before arithmetic if you handle more than one.
