using Zonit.Extensions.Website.Abstractions.Navigations.Models;
using Zonit.Extensions.Website.Abstractions.Navigations.Types;

namespace Zonit.Extensions.Website.Navigations.Services;

internal class NavigationService : INavigationProvider
{
    readonly IList<NavGroupModel> _navigationModels = [];

    public IReadOnlyList<NavGroupModel>? Get(AreaType area, string? position)
        => _navigationModels?
            .Where(x => x.Area == area && x.Position == position)
            .OrderBy(x => x.Order)
            .ToList();

    public void Add(NavGroupModel model)
        => _navigationModels.Add(model);

}