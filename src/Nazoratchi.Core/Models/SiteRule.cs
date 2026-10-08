namespace Nazoratchi.Core.Models;

/// <summary>
/// Represents a site blocking/allowing rule.
/// </summary>
public class SiteRule
{
    public string Domain { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.Now;
}
