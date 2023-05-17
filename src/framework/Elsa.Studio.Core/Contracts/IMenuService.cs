using Elsa.Studio.Models;

namespace Elsa.Studio.Contracts;

public interface IMenuService
{
    /// <summary>
    /// Returns all menu items from all menu providers.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of <see cref="MenuItem"/> instances.</returns>
    ValueTask<IEnumerable<MenuItem>> GetMenuItemsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Returns all menu item groups from all menu group providers.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of <see cref="MenuItemGroup"/> instances.</returns>
    ValueTask<IEnumerable<MenuItemGroup>> GetMenuItemGroupsAsync(CancellationToken cancellationToken = default);
}