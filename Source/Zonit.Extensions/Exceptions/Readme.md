# Exceptions

`BaseException` and `BaseException<TErrorCode>` — exceptions that keep the localization key, the message
template and the parameters separable, so the same error can be re-rendered in the user's language later.

Full documentation is in
[`Instruction/extensions/core/exceptions.md`](../../../Instruction/extensions/core/exceptions.md)
(installed into a consumer's repo as `.zonit/extensions/core/exceptions.md`).

The short version:

```csharp
// Never interpolate — the message must stay a template.
throw new WalletException("Wallets.NotFound", "Wallet {0} was not found", walletId);
```

| member | value |
|---|---|
| `ErrorKey` | `"Wallets.NotFound"` |
| `Template` | `"Wallet {0} was not found"` |
| `Parameters` | `[walletId]` — `null` when none were supplied |
| `Message` | `"Wallet 7f3b… was not found"` (template already formatted) |

`BaseException<TErrorCode>` derives `ErrorKey` from `[Display(Name = …)]` and `Template` from
`[Display(Description = …)]` on the enum member, and exposes the enum as `Code` so handlers can `switch`
on it.

Known limitation: that attribute lookup is reflective and is not annotated for trimming, so under
`PublishTrimmed` / `PublishAot` a trimmed enum silently degrades to `code.ToString()` for both key and
template. Use the non-generic form with literal keys in a trimmed app.
