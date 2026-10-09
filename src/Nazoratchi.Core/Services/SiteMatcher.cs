using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Nazoratchi.Core.Models;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Advanced domain matching service supporting wildcards, exceptions (+), and keywords (~).
/// Includes URL sanitization, regex caching, and ReDoS protection.
/// </summary>
public static class SiteMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Normalizes raw domain or URL inputs (e.g. "https://example.com/path" -> "example.com").
    /// Preserves rule prefixes like '+' (exception) and '~' (keyword).
    /// </summary>
    public static string NormalizeRule(string rawRule)
    {
        if (string.IsNullOrWhiteSpace(rawRule))
            return string.Empty;

        var rule = rawRule.Trim();

        // Preserve '!' URL-level block exception prefix (e.g. !scratch.mit.edu/explore*)
        if (rule.StartsWith("!"))
        {
            var content = rule.Substring(1).Trim();
            if (content.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                content = content.Substring(7);
            else if (content.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                content = content.Substring(8);

            return "!" + content.Trim().ToLowerInvariant();
        }

        // Preserve '+' exception or '~' keyword prefix
        string prefix = string.Empty;
        if (rule.StartsWith("+") || rule.StartsWith("~"))
        {
            prefix = rule.Substring(0, 1);
            rule = rule.Substring(1).Trim();
        }

        // If user entered a full URL, extract hostname
        if (rule.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rule.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(rule, UriKind.Absolute, out var uri))
            {
                rule = uri.Host;
            }
        }

        // Remove path or query string if pasted (e.g. "facebook.com/messages")
        var slashIndex = rule.IndexOf('/');
        if (slashIndex >= 0)
        {
            rule = rule.Substring(0, slashIndex);
        }

        // Remove port (e.g. "example.com:443")
        var colonIndex = rule.IndexOf(':');
        if (colonIndex >= 0)
        {
            rule = rule.Substring(0, colonIndex);
        }

        // Strip leading dots and spaces
        rule = rule.Trim().TrimStart('.').TrimEnd('.');

        return prefix + rule.ToLowerInvariant();
    }

    /// <summary>
    /// Checks whether a domain is blocked under the specified filter mode and site rules.
    /// When blockAiChatbots is true, all AI chatbot services are strictly blocked regardless of whitelist/blacklist!
    /// </summary>
    public static bool IsBlocked(string queryDomain, FilterMode mode, SiteList siteList, bool blockAiChatbots = false)
    {
        if (string.IsNullOrWhiteSpace(queryDomain))
            return false;

        var domain = queryDomain.Trim().ToLowerInvariant().TrimEnd('.');

        // 0. Strict Priority: Block AI chatbots if active, regardless of whitelist or blacklist
        if (blockAiChatbots && IsAiChatbot(domain))
        {
            return true;
        }

        if (mode == FilterMode.BlackList)
        {
            var rules = siteList.BlacklistSites ?? new List<SiteRule>();
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
                if (r.StartsWith("+") || r.StartsWith("!")) continue; // Skip exceptions and URL-level rules

                if (MatchesRule(domain, r))
                {
                    return true; // Blocked!
                }
            }

            return false;
        }
        else // WhiteList
        {
            var rules = siteList.WhitelistSites ?? new List<SiteRule>();
            if (rules.Count == 0) return true; // Empty whitelist blocks everything

            // In WhiteList mode: if domain matches ANY allowed rule, allow it
            foreach (var rule in rules)
            {
                var r = rule.Domain.Trim().ToLowerInvariant();
                if (r.StartsWith("!")) continue; // URL-level block rule, handled by browser policy

                if (r.StartsWith("+") && r.Length > 1)
                {
                    r = r.Substring(1).Trim();
                }

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

        // URL-level block rule: never matches DNS domain
        if (rule.StartsWith("!"))
            return false;

        // 1. Keyword match (~keyword)
        if (rule.StartsWith("~") && rule.Length > 1)
        {
            var keyword = rule.Substring(1).Trim();
            return domain.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        // 2. Wildcard match (* or **)
        if (rule.Contains("*"))
        {
            // If rule is of form *.domain.com, match both domain.com and subdomains
            if (rule.StartsWith("*."))
            {
                var baseDomain = rule.Substring(2).TrimStart('.');
                if (!string.IsNullOrEmpty(baseDomain) && 
                    (domain == baseDomain || domain.EndsWith("." + baseDomain, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            try
            {
                var regex = _regexCache.GetOrAdd(rule, r =>
                {
                    // Escape regex special chars and replace consecutive wildcards with '.*'
                    var escaped = Regex.Escape(r);
                    var pattern = "^" + Regex.Replace(escaped, @"(\\?\*)+", ".*") + "$";
                    return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
                });

                return regex.IsMatch(domain);
            }
            catch
            {
                // Fallback to simple contains on error or regex timeout
                return domain.Contains(rule.Replace("*", ""), StringComparison.OrdinalIgnoreCase);
            }
        }

        // 3. Universal keyword match for terms without dots (e.g. "notebooklm", "teachablemachine", "casino")
        // Matches any domain containing the term anywhere in its name
        if (!rule.Contains('.') && rule.Length > 1)
        {
            return domain.Contains(rule, StringComparison.OrdinalIgnoreCase);
        }

        // 4. Exact domain or subdomain match (e.g. 'google.com' matches 'google.com' and 'sub.google.com')
        rule = rule.TrimStart('.');
        if (domain == rule || domain.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase))
            return true;

        // If rule has www., match against base rule
        if (rule.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && rule.Length > 4)
        {
            var baseRule = rule.Substring(4);
            if (domain == baseRule || domain.EndsWith("." + baseRule, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // If query domain has www., match against base domain
        if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && domain.Length > 4)
        {
            var baseDomain = domain.Substring(4);
            if (baseDomain == rule || baseDomain.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a domain belongs to a known AI chatbot service.
    /// </summary>
    public static bool IsAiChatbot(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return false;
        domain = domain.Trim().ToLowerInvariant().TrimEnd('.');

        foreach (var ai in Constants.AiChatbotDomains)
        {
            if (domain == ai || domain.EndsWith("." + ai, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (domain.Contains("chatgpt") || 
            domain.Contains("deepseek") || 
            domain.Contains("perplexity") || 
            domain.Contains("anthropic") || 
            domain.Contains("claude.ai") || 
            domain.Contains("youtube") || 
            domain.Contains("googlevideo") || 
            domain.Contains("ytimg") || 
            domain == "youtu.be" || 
            domain.EndsWith(".youtu.be") || 
            domain == "gemini.google.com" || 
            domain.EndsWith(".gemini.google.com") ||
            domain == "bard.google.com" || 
            domain.EndsWith(".bard.google.com") ||
            domain == "copilot.microsoft.com" ||
            domain == "meta.ai" ||
            domain.EndsWith(".meta.ai") ||
            domain == "grok.com" ||
            domain.EndsWith(".grok.com"))
        {
            return true;
        }

        return false;
    }
}
