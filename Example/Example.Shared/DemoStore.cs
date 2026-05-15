using System.Collections.Concurrent;
using System.Collections.Frozen;
using Zonit.Extensions;
using Zonit.Extensions.Auth;
using Zonit.Extensions.Organizations;
using Zonit.Extensions.Projects;
using Zonit.Extensions.Tenants;

namespace Example.Shared;

/// <summary>
/// Singleton in-memory backing store for every "consumer-side" contract used by the demo.
/// A real host swaps the individual stubs (registered as scoped) for EF / Dapper / a remote
/// API; the store stays singleton because it represents the underlying data that those
/// stubs read/write.
/// </summary>
public sealed class DemoStore
{
    public static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserUserId  = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid AcmeOrgId   = Guid.Parse("aa000000-0000-0000-0000-000000000001");
    public static readonly Guid GlobexOrgId = Guid.Parse("aa000000-0000-0000-0000-000000000002");

    public static readonly Guid WebsiteProjectId = Guid.Parse("bb000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminProjectId   = Guid.Parse("bb000000-0000-0000-0000-000000000002");
    public static readonly Guid GlobexAppId      = Guid.Parse("bb000000-0000-0000-0000-000000000003");

    public IReadOnlyDictionary<Guid, UserModel> Users { get; }
    public IReadOnlyDictionary<Guid, OrganizationModel> Organizations { get; }
    public IReadOnlyDictionary<Guid, ProjectModel> Projects { get; }
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> UserOrganizations { get; }
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> OrganizationProjects { get; }
    public IReadOnlyDictionary<string, Tenant> TenantsByDomain { get; }

    /// <summary>token → userId. <see cref="DemoLoginService"/> mutates this map.</summary>
    public ConcurrentDictionary<string, Guid> SessionsByToken { get; } = new();

    /// <summary>userId → currently selected orgId.</summary>
    public ConcurrentDictionary<Guid, Guid> CurrentOrganizationByUser { get; } = new();

    /// <summary>(userId, orgId) → currently selected projectId.</summary>
    public ConcurrentDictionary<(Guid User, Guid Org), Guid> CurrentProjectByUserOrg { get; } = new();

    /// <summary>Plain-text creds (DEMO ONLY).</summary>
    public IReadOnlyDictionary<string, (Guid UserId, string Password)> Credentials { get; }

    public DemoStore()
    {
        var admin = new UserModel
        {
            Id = AdminUserId,
            Name = "admin",
            FirstName = "Ada",
            LastName = "Lovelace",
            DisplayRole = "Administrator",
            Roles = ["admin", "user"],
            Policy = ["users.read", "users.write", "settings.write", "*"],
        };
        var user = new UserModel
        {
            Id = UserUserId,
            Name = "user",
            FirstName = "Linus",
            LastName = "Pauling",
            DisplayRole = "Member",
            Roles = ["user"],
            Policy = ["users.read"],
        };
        Users = new Dictionary<Guid, UserModel>
        {
            [admin.Id] = admin,
            [user.Id]  = user,
        };

        Organizations = new Dictionary<Guid, OrganizationModel>
        {
            [AcmeOrgId] = new()
            {
                Id = AcmeOrgId, Name = "Acme Corp.", FullName = "Acme Corporation Sp. z o.o.",
                Country = "Poland", City = "Warsaw", PostalCode = "00-001", AddressLine1 = "Marszałkowska 1",
                Email = "billing@acme.example", TaxIdentification = "PL5260001246",
            },
            [GlobexOrgId] = new()
            {
                Id = GlobexOrgId, Name = "Globex Inc.", FullName = "Globex International Inc.",
                Country = "USA", Region = "California", City = "Springfield", PostalCode = "97477",
                AddressLine1 = "742 Evergreen Terrace", Email = "hello@globex.example",
                TaxIdentification = "EIN-1234567",
            },
        };

        Projects = new Dictionary<Guid, ProjectModel>
        {
            [WebsiteProjectId] = new() { Id = WebsiteProjectId, Name = "Acme — Website" },
            [AdminProjectId]   = new() { Id = AdminProjectId,   Name = "Acme — Admin Panel" },
            [GlobexAppId]      = new() { Id = GlobexAppId,      Name = "Globex — Mobile App" },
        };

        UserOrganizations = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [AdminUserId] = [AcmeOrgId, GlobexOrgId],
            [UserUserId]  = [AcmeOrgId],
        };

        OrganizationProjects = new Dictionary<Guid, IReadOnlyList<Guid>>
        {
            [AcmeOrgId]   = [WebsiteProjectId, AdminProjectId],
            [GlobexOrgId] = [GlobexAppId],
        };

        TenantsByDomain = new Dictionary<string, Tenant>(StringComparer.OrdinalIgnoreCase)
        {
            ["localhost"] = new()
            {
                Id = Guid.Parse("cc000000-0000-0000-0000-000000000001"),
                Domain = "localhost",
                Variables = new Dictionary<string, string>
                {
                    ["site"] = """{"Title":"Zonit.Extensions Demo","MetaDescription":"End-to-end Blazor sample","Language":"en-US"}""",
                    ["theme"] = """{"PrimaryColor":"#2563EB","SecondaryColor":"#7C3AED","AccentColor":"#DC2626","NeutralColor":"#F1F5F9","SurfaceColor":"#FFFFFF","ContentColor":"#0F172A"}""",
                }.ToFrozenDictionary(),
            },
        };

        // Default per-user current selections (admin → Acme/Website, user → Acme/Website).
        CurrentOrganizationByUser[AdminUserId] = AcmeOrgId;
        CurrentOrganizationByUser[UserUserId]  = AcmeOrgId;
        CurrentProjectByUserOrg[(AdminUserId, AcmeOrgId)] = WebsiteProjectId;
        CurrentProjectByUserOrg[(UserUserId,  AcmeOrgId)] = WebsiteProjectId;

        Credentials = new Dictionary<string, (Guid, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = (AdminUserId, "admin"),
            ["user"]  = (UserUserId,  "user"),
        };
    }

    public Identity HydrateIdentity(UserModel user)
    {
        var roles = new List<Role>();
        foreach (var r in user.Roles)
            if (Role.TryCreate(r, out var role)) roles.Add(role);

        var perms = new List<Permission>();
        foreach (var p in user.Policy)
            if (Permission.TryCreate(p, out var perm)) perms.Add(perm);

        var displayName = string.IsNullOrWhiteSpace(user.FullName?.Trim()) ? user.Name : user.FullName!;
        return new Identity(user.Id, new Title(displayName), roles, perms);
    }
}
