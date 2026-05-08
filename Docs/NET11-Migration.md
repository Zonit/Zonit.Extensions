# Plan migracji do .NET 11

Lista miejsc do uaktualnienia, gdy projekt zostanie podbity z `net10.0` do `net11.0`.

## 1. AOT-safe JSON dla `PageViewBase<T>`

### Stan obecny (.NET 10)
`Microsoft.AspNetCore.Components.PersistentComponentState` w .NET 10 udostępnia tylko refleksyjne przeciążenia:

- `PersistAsJson<TValue>(string key, TValue instance)` — `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`
- `TryTakeFromJson<TValue>(string key, out TValue? instance)` — `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`

Dlatego w `PageViewBase<TViewModel>` używamy:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "...")]
[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "...")]
private Task PersistState() { /* PersistAsJson(key, model) */ }
```

Trimmer/AOT zachowuje członków `TViewModel` przez `[DynamicallyAccessedMembers(PublicProperties | PublicFields | PublicConstructors)]` na parametrze generycznym.

### Plan na .NET 11
.NET 11 ma dodać przeciążenia z `JsonTypeInfo<T>`:

- `PersistAsJson<TValue>(string key, TValue value, JsonTypeInfo<TValue> jsonTypeInfo)`
- `TryTakeFromJson<TValue>(string key, JsonTypeInfo<TValue> jsonTypeInfo, out TValue? instance)`

Po wydaniu .NET 11 należy:

1. **Re-enable JsonSerializerContext w generatorze**
   - Plik: `Source/Zonit.Extensions.Website.SourceGenerators/ViewModelMetadataGenerator.cs`
   - Sekcja oznaczona komentarzem `// NOTE: We intentionally do NOT emit a JsonSerializerContext here yet.`
   - Przywrócić `EmitJsonContext(sb, c)` per kandydat (kod był już napisany — patrz historia git).
   - Każda VM ma już `EmitJsonContext` flag w `ViewModelCandidate` i `IsJsonContextSafe()` filtruje typy nested/generic.

2. **Override `JsonTypeInfo` w wygenerowanej klasie metadata**
   - W `EmitMetadataClass` dodać:
     ```csharp
     public override global::System.Text.Json.Serialization.Metadata.JsonTypeInfo<{FQN}>? JsonTypeInfo
         => __ZonitVMJsonContext_{sanitized}.Default.{simpleName};
     ```
   - `ViewModelMetadata<T>.JsonTypeInfo` (Abstractions) jest już `virtual` zwracający null — nie wymaga zmian.

3. **Refactor `PageViewBase<TViewModel>`**
   - Plik: `Source/Zonit.Extensions.Website/Components/PageViewBase.cs`
   - `PersistState()` i `TryTakeModelFromState(...)`:
     ```csharp
     if (ViewModelMetadata<TViewModel>.Instance?.JsonTypeInfo is { } typeInfo)
         PersistentComponentState.PersistAsJson(StateKey, Model, typeInfo);   // AOT-safe path
     else
         PersistentComponentState.PersistAsJson(StateKey, Model);              // legacy fallback
     ```
   - **Usunąć** `[UnconditionalSuppressMessage]` na `PersistState`/`TryTakeModelFromState` — gdy generator dostarczy `JsonTypeInfo`, gałąź refleksyjna zamienia się w martwy kod (nadal opatrzony lokalnym suppress jako fallback gdy generatora nie ma).
   - Można rozważyć całkowite usunięcie gałęzi refleksyjnej, jeśli założymy, że generator zawsze obecny (ale ryzyko: konsument nie referuje pakietu generatora).

4. **Cleanup w PageEditBase**
   - Plik: `Source/Zonit.Extensions.Website/Components/PageEditBase.cs`
   - Pozostałe supresje `IL2026/IL3050` na `*Reflective` metodach **zostają** — dotyczą reflection-based fallbacku poza JSON (DataAnnotations Validator, property accessors).
   - Można rozważyć migrację `Validator.TryValidateObject` na `Microsoft.Extensions.Validation` (source-generated walidacja) w .NET 10.x/11 — wtedy zniknie potrzeba `[UnconditionalSuppressMessage]` na `TryValidate`/`CreateValidationContext`.

## 2. Bump TFM

- `Source/Directory.Build.props` linia ~9: `<TargetFramework>net10.0</TargetFramework>` → `net11.0`.
- `Source/Directory.Packages.props`: package versions dla `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `MudBlazor` itp. → `11.x`.
- `Example/ExampleWebsite/ExampleWebsite.csproj`: `MudBlazor` PackageReference → wersja kompatybilna z .NET 11.
- Source generator (`Zonit.Extensions.Website.SourceGenerators.csproj`) zostaje na `netstandard2.0` (Roslyn wymóg) — bez zmian.

## 3. Drobiazgi do sprawdzenia po migracji

- **`PublishTrimmed=true` z CLI**: w .NET 10 propaguje się przez `ProjectReference` do netstandard2.0 generatora i powoduje `NETSDK1124`. Sprawdzić, czy MSBuild w SDK .NET 11 respektuje `UndefineProperties` na `ProjectReference` (już ustawione w `Zonit.Extensions.Website.csproj` i `ExampleWebsite.csproj`). Jeśli tak — można usunąć opisowy komentarz w `Directory.Build.props`.
- **`Microsoft.Extensions.Validation` source-generated validator**: jeśli stabilny w .NET 11, rozważyć migrację z `DataAnnotations.Validator`.
- **`ValidationContext(object)`**: czy w .NET 11 nadal `[RequiresUnreferencedCode]`? Jeśli nie — usunąć `CreateValidationContext` helper i jego supresję.

## 4. Walidacja po migracji

```powershell
dotnet build Source\Extensions\Zonit.Extensions\Zonit.Extensions.sln -nologo
# oczekiwane: 0 warnings, 0 errors

dotnet publish Source\Extensions\Zonit.Extensions\Example\ExampleWebsite\ExampleWebsite.csproj -c Release /p:PublishTrimmed=true
# oczekiwane: 0 IL2026/IL3050 warnings dla TViewModel persistence path
```

Sprawdzić wygenerowany kod w `obj/Release/net11.0/generated/Zonit.Extensions.Website.SourceGenerators/`:
- Każda `__ZonitVMMetadata_*` powinna mieć override `JsonTypeInfo`.
- Każda VM powinna mieć osobny `__ZonitVMJsonContext_*` z `[JsonSerializable(typeof(...))]`.

## 5. Lokalizacja klucza fragmentów

| Cel | Plik | Sekcja |
|---|---|---|
| Generator JSON | `Source/Zonit.Extensions.Website.SourceGenerators/ViewModelMetadataGenerator.cs` | komentarz `NOTE: We intentionally do NOT emit a JsonSerializerContext here yet` |
| `JsonTypeInfo` virtual | `Source/Zonit.Extensions.Website.Abstractions/ViewModels/ViewModelMetadata.cs` | property `JsonTypeInfo` |
| Persist/restore | `Source/Zonit.Extensions.Website/Components/PageViewBase.cs` | metody `PersistState`, `TryTakeModelFromState` |
| Validator | `Source/Zonit.Extensions.Website/Components/PageEditBase.cs` | metody `TryValidate`, `CreateValidationContext` |
