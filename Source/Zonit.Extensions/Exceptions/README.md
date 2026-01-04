# BaseException - Structured Exception Handling

A flexible, strongly-typed exception base classes for .NET applications with built-in support for localization (i18n), error codes, and structured error handling.

## Features

? **Two-tier architecture** - Generic and non-generic base classes  
? **i18n ready** - Built-in support for localization with `ErrorKey`, `Template`, and `Parameters`  
? **Strongly typed** - Use enums for error codes or create custom exceptions with typed properties  
? **Display attributes** - Automatic mapping from `[Display]` attributes to error messages  
? **Safe formatting** - Built-in error handling for message formatting  
? **Flexible** - Support for both simple and complex error scenarios

---

## Architecture

### `BaseException` (Non-generic)
Base class for creating custom exceptions with full control over all properties. Ideal for specific errors with custom properties.

```csharp
public abstract class BaseException : Exception
{
    public string ErrorKey { get; }      // Localization key (e.g., "Wallets.NotFound")
    public string Template { get; }      // Message template with placeholders (e.g., "Wallet {0} was not found")
    public object[]? Parameters { get; } // Parameters for formatting
}
```

### `BaseException<TErrorCode>` (Generic)
Inherits from `BaseException`. Uses enum-based error codes with automatic mapping from `[Display]` attributes.

```csharp
public abstract class BaseException<TErrorCode> : BaseException where TErrorCode : Enum
{
    public TErrorCode Code { get; } // Strongly-typed error code
}
```

---

## Usage Examples

### 1. Simple Exception with Enum (Generic approach)

**Step 1: Define error codes**
```csharp
public enum WalletErrorCode
{
    [Display(Name = "Wallets.InsufficientBalance", 
             Description = "Insufficient balance. Requested: {0}, Available: {1}")]
    InsufficientBalance,

    [Display(Name = "Wallets.NotFound", 
             Description = "Wallet {0} was not found")]
    NotFound
}
```

**Step 2: Create exception class**
```csharp
public class WalletException(WalletErrorCode code, params object[] args)
    : BaseException<WalletErrorCode>(code, args)
{
}
```

**Step 3: Throw and catch**
```csharp
// Throwing
throw new WalletException(WalletErrorCode.NotFound, "wallet-123");
throw new WalletException(WalletErrorCode.InsufficientBalance, 100.50m, 50.00m);

// Catching
try
{
    throw new WalletException(WalletErrorCode.NotFound, "wallet-123");
}
catch (WalletException ex)
{
    Console.WriteLine(ex.Code);       // WalletErrorCode.NotFound
    Console.WriteLine(ex.ErrorKey);   // "Wallets.NotFound"
    Console.WriteLine(ex.Template);   // "Wallet {0} was not found"
    Console.WriteLine(ex.Message);    // "Wallet wallet-123 was not found"
    Console.WriteLine(ex.Parameters?[0]); // "wallet-123"
}
```

---

### 2. Custom Exception with Typed Properties (Non-generic approach)

**Best for:** Single, specific errors that require strongly-typed data.

```csharp
public class InsufficientBalanceException : BaseException
{
    public decimal Requested { get; }
    public decimal Available { get; }

    public InsufficientBalanceException(decimal requested, decimal available)
        : base(
            errorKey: "Wallets.InsufficientBalance",
            template: "Insufficient balance. Requested: {0}, Available: {1}",
            parameters: [requested, available]
        )
    {
        Requested = requested;
        Available = available;
    }
}

// Usage
try
{
    throw new InsufficientBalanceException(100.50m, 50.00m);
}
catch (InsufficientBalanceException ex)
{
    // Strongly typed access
    Console.WriteLine($"Requested: {ex.Requested}, Available: {ex.Available}");
    
    // Still have i18n support
    Console.WriteLine(ex.ErrorKey);   // "Wallets.InsufficientBalance"
    Console.WriteLine(ex.Template);   // "Insufficient balance. Requested: {0}, Available: {1}"
    Console.WriteLine(ex.Message);    // "Insufficient balance. Requested: 100.50, Available: 50.00"
}
```

---

### 3. Simple Exception with ID

```csharp
public class WalletNotFoundException : BaseException
{
    public string WalletId { get; }

    public WalletNotFoundException(string walletId)
        : base(
            errorKey: "Wallets.NotFound",
            template: "Wallet {0} was not found",
            parameters: [walletId]
        )
    {
        WalletId = walletId;
    }
}

// Usage
throw new WalletNotFoundException("wallet-123");
```

---

## Localization (i18n)

All exceptions are designed for easy localization:

### Option 1: Re-format with translated template
```csharp
try
{
    throw new WalletException(WalletErrorCode.NotFound, "wallet-123");
}
catch (WalletException ex)
{
    // Get translated template
    var translatedTemplate = GetTranslation(ex.Template); 
    // e.g., "Portfel {0} nie zosta³ znaleziony"
    
    // Re-format with original parameters
    var localizedMessage = string.Format(translatedTemplate, ex.Parameters ?? []);
    // Result: "Portfel wallet-123 nie zosta³ znaleziony"
}
```

### Option 2: Use ErrorKey with localization service
```csharp
catch (BaseException ex)
{
    // Use ErrorKey as resource key
    var localizedMessage = localizer[ex.ErrorKey, ex.Parameters ?? []];
}
```

### Option 3: Custom message builder
```csharp
catch (InsufficientBalanceException ex)
{
    var message = $"You requested {ex.Requested} but only {ex.Available} is available.";
    // Or use a localization service with strongly-typed properties
}
```

---

## Error Handling Middleware Example

```csharp
app.UseExceptionHandler(builder =>
{
    builder.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        
        var response = ex switch
        {
            BaseException baseEx => new ErrorResponse
            {
                ErrorKey = baseEx.ErrorKey,
                Message = baseEx.Message,
                Template = baseEx.Template,
                Parameters = baseEx.Parameters,
                // Add Code if it's BaseException<T>
                Code = (baseEx as dynamic)?.Code?.ToString()
            },
            
            ValidationException validationEx => new ErrorResponse
            {
                ErrorKey = "Validation.Failed",
                Message = validationEx.Message
            },
            
            _ => new ErrorResponse
            {
                ErrorKey = "Internal.Error",
                Message = "An unexpected error occurred"
            }
        };
        
        context.Response.StatusCode = GetStatusCode(ex);
        await context.Response.WriteAsJsonAsync(response);
    });
});

private static int GetStatusCode(Exception ex) => ex switch
{
    WalletException { Code: WalletErrorCode.NotFound } => 404,
    WalletException { Code: WalletErrorCode.InsufficientBalance } => 409,
    ValidationException => 400,
    UnauthorizedAccessException => 401,
    _ => 500
};
```

---

## API Response Example

```json
{
  "errorKey": "Wallets.InsufficientBalance",
  "code": "InsufficientBalance",
  "message": "Insufficient balance. Requested: 100.50, Available: 50.00",
  "template": "Insufficient balance. Requested: {0}, Available: {1}",
  "parameters": [100.50, 50.00]
}
```

Client can use:
- `message` - Display directly (already formatted)
- `errorKey` + `parameters` - Translate on client side
- `template` + `parameters` - Re-format with client-side localization

---

## Best Practices

### ? DO:

1. **Always use placeholders** (`{0}`, `{1}`) instead of string interpolation in templates
   ```csharp
   // ? GOOD
   template: "Wallet {0} was not found",
   parameters: [walletId]
   
   // ? BAD
   message: $"Wallet {walletId} was not found"
   // This prevents re-formatting with translations!
   ```

2. **Use enum-based exceptions for modules with multiple error types**
   ```csharp
   public enum WalletErrorCode { NotFound, InsufficientBalance, ... }
   public class WalletException(WalletErrorCode code, params object[] args) 
       : BaseException<WalletErrorCode>(code, args) { }
   ```

3. **Use custom exceptions for specific errors with typed properties**
   ```csharp
   public class InsufficientBalanceException : BaseException
   {
       public decimal Requested { get; }
       public decimal Available { get; }
   }
   ```

4. **Use meaningful ErrorKey values** (follow a convention)
   ```csharp
   // Good: Module.ErrorType
   "Wallets.NotFound"
   "Wallets.InsufficientBalance"
   "Orders.Cancelled"
   ```

### ? DON'T:

1. Don't use string interpolation in templates (breaks i18n)
2. Don't mix both approaches in the same exception class
3. Don't leave `ErrorKey` empty or use generic values like "Error"

---

## When to Use Which?

| Scenario | Approach | Example |
|----------|----------|---------|
| **Module with multiple error types** | `BaseException<TErrorCode>` | `WalletException` with `WalletErrorCode` enum |
| **Single specific error with typed data** | `BaseException` (non-generic) | `InsufficientBalanceException` with `Requested`, `Available` properties |
| **Need strong typing for properties** | `BaseException` (non-generic) | Any exception where you catch and use specific properties |
| **Simple, consistent error handling** | `BaseException<TErrorCode>` | Quick setup with `[Display]` attributes |

---

## Performance Considerations

- **Reflection**: `BaseException<TErrorCode>` uses reflection to read `[Display]` attributes. This happens once per exception instance (not per throw).
- **Recommendation**: For high-performance scenarios, consider caching display attribute values if creating many exceptions with the same error code.

---

## Migration from Standard Exceptions

**Before:**
```csharp
throw new Exception("Wallet wallet-123 was not found");

catch (Exception ex)
{
    // No structured error information
    Log(ex.Message);
}
```

**After:**
```csharp
throw new WalletException(WalletErrorCode.NotFound, "wallet-123");

catch (WalletException ex)
{
    // Structured error information
    Log(ex.ErrorKey, ex.Code, ex.Parameters);
    
    // Can translate and re-format
    var translatedMessage = Translate(ex.Template, ex.Parameters);
}
```

---

## Summary

`BaseException` provides a **solid foundation** for structured exception handling in .NET applications with:

- **Flexibility** - Choose between enum-based or custom property-based exceptions
- **i18n support** - Built-in localization capabilities
- **Strong typing** - Type-safe error codes and parameters
- **Consistent API** - All exceptions share the same base properties
- **Easy integration** - Works seamlessly with middleware, logging, and error responses

Choose the approach that best fits your use case, and enjoy cleaner, more maintainable error handling! ??
