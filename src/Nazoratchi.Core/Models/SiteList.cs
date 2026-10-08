namespace Nazoratchi.Core.Models;

/// <summary>
/// Container for site rules separated by filter mode.
/// </summary>
public class SiteList
{
    /// <summary>
    /// Legacy site rules for backward compatibility.
    /// </summary>
    public List<SiteRule> Sites { get; set; } = new();

    /// <summary>
    /// Rules for BlackList mode (sites to block).
    /// </summary>
    public List<SiteRule> BlacklistSites { get; set; } = new();

    /// <summary>
    /// Rules for WhiteList mode (sites to allow).
    /// </summary>
    public List<SiteRule> WhitelistSites { get; set; } = new();
}
