using System.Text.Json.Serialization;
using Zonit.Extensions.Organizations;

namespace Zonit.Extensions;

/// <summary>
/// AOT-safe <see cref="JsonSerializerContext"/> for the organizations workspace
/// snapshot persisted by <see cref="ZonitOrganizationsExtension"/>.
/// </summary>
/// <remarks>
/// <para>STJ's source generator walks the type graph from <see cref="StateModel"/> and
/// emits metadata for every reachable type (<see cref="WorkspaceModel"/>,
/// <see cref="OrganizationModel"/>, their primitive properties). The accessor used at
/// runtime is <see cref="OrganizationsStateJsonContext.Default"/>'s <c>StateModel</c>
/// property — strongly typed, zero reflection.</para>
///
/// <para>The accessor is not yet consumed in .NET 10 because
/// <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/> exposes only
/// reflective <c>PersistAsJson</c> / <c>TryTakeFromJson</c>. Once .NET 11 adds the
/// <c>JsonTypeInfo</c>-accepting overloads, <see cref="ZonitOrganizationsExtension"/>
/// will switch to them and the suppressions on its persist / restore methods can be
/// removed — see <c>Docs/NET11-Migration.md</c>.</para>
/// </remarks>
[JsonSerializable(typeof(StateModel))]
internal sealed partial class OrganizationsStateJsonContext : JsonSerializerContext;
