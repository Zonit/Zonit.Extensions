using System.Text.Json.Serialization;
using Zonit.Extensions.Projects;

namespace Zonit.Extensions;

/// <summary>
/// AOT-safe <see cref="JsonSerializerContext"/> for the projects catalog snapshot
/// persisted by <see cref="ZonitProjectsExtension"/>.
/// </summary>
/// <remarks>
/// <para>STJ's source generator walks the type graph from <see cref="StateModel"/> and
/// emits metadata for <see cref="CatalogModel"/> + <see cref="ProjectModel"/>.
/// The accessor used at runtime is <see cref="ProjectsStateJsonContext.Default"/>'s
/// <c>StateModel</c> property — strongly typed, zero reflection.</para>
///
/// <para>The accessor is not yet consumed in .NET 10 because
/// <see cref="Microsoft.AspNetCore.Components.PersistentComponentState"/> exposes only
/// reflective <c>PersistAsJson</c> / <c>TryTakeFromJson</c>. Once .NET 11 adds the
/// <c>JsonTypeInfo</c>-accepting overloads, <see cref="ZonitProjectsExtension"/> will
/// switch to them and the suppressions on its persist / restore methods can be removed
/// — see <c>Docs/NET11-Migration.md</c>.</para>
/// </remarks>
[JsonSerializable(typeof(StateModel))]
internal sealed partial class ProjectsStateJsonContext : JsonSerializerContext;
