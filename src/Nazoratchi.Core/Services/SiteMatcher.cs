using System.Text.RegularExpressions;
using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Advanced domain matching service supporting wildcards, exceptions (+), and keywords (~).
/// Inspired by LeechBlock NG matching rules.
/// </summary>
public static class SiteMatcher
{
    /// <summary>
    /// Checks whether a domain is blocked under the specified filter mode and site rules.
    /// </summary>
    public static bool IsBlocked(string queryDomain, FilterMode mode, SiteList siteList)
    {
        if (string.IsNullOrWhiteSpace(queryDomain))
            return false;

        var domain = queryDomain.Trim().ToLowerInvariant().TrimEnd('.');

        if (mode == FilterMode.BlackList)
        {
            var rules = siteList.BlacklistSites.Count > 0 ? siteList.BlacklistSites : siteList.Sites;
            if (rules.Count == 0) return false;

            // 1. Check exceptions (+prefix): if matches an exception, it is allowed
            foreach (var rule in rules)
            {
                var r = rule.Domain.Trim().ToLowerInvariant();
                if (r.StartsWith("+") && r.Length > 1)
                {
                    var exceptionRule = r.Substring(1).Trim();
                    if (MatchesRule(domain, exceptionRule))
                    {
                        return false; // Allowed by exception
                    }
                }
            }

            // 2. Check blacklist blocking rules
            foreach (var rule in rules)
            {
                var r = rule.Domain.Trim().ToLowerInvariant();
                if (r.StartsWith("+")) continue; // Skip exception rules here

                if (MatchesRule(domain, r))
                {
                    return true; // Blocked!
                }
            }

            return false;
        }
        else // WhiteList
        {
            var rules = siteList.WhitelistSites.Count > 0 ? siteList.WhitelistSites : siteList.Sites;
            if (rules.Count == 0) return true; // Empty whitelist blocks everything

            // In WhiteList mode: if domain matches ANY allowed rule, allow it
            foreach (var rule in rules)
            {
                var r = rule.Domain.Trim().ToLowerInvariant();
                if (MatchesRule(domain, r))
                {
                    return false; // Allowed!
                }
            }

            return true; // Blocked because not on whitelist
        }
    }

    /// <summary>
    /// Evaluates if a given domain matches a specific rule pattern.
    /// Supports:
    /// - '~keyword' (domain contains keyword)
    /// - '*' / '**' (wildcard pattern)
    /// - 'example.com' (matches example.com and *.example.com)
    /// </summary>
    public static bool MatchesRule(string domain, string rule)
    {
        if (string.IsNullOrWhiteSpace(rule) || string.IsNullOrWhiteSpace(domain))
            return false;

        rule = rule.Trim().ToLowerInvariant().TrimEnd('.');

        // 1. Keyword match (~keyword)
        if (rule.StartsWith("~") && rule.Length > 1)
        {
            var keyword = rule.Substring(1).Trim();
            return domain.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        // 2. Wildcard match (* or **)
        if (rule.Contains("*"))
        {
            try
            {
                var pattern = "^" + Regex.Escape(rule).Replace(@"\*", ".*") + "$";
                return Regex.IsMatch(domain, pattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // Fallback to simple contains if invalid regex
                return domain.Contains(rule.Replace("*", ""), StringComparison.OrdinalIgnoreCase);
            }
        }

        // 3. Exact domain or subdomain match (e.g. 'google.com' matches 'google.com' and 'sub.google.com')
        return domain == rule || domain.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase);
    }
}
