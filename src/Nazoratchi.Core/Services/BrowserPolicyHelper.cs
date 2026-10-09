using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Nazoratchi.Core.Services;

/// <summary>
/// Manages enterprise browser policies for Google Chrome and Microsoft Edge (Chromium).
/// Enables URL-level blocking (URLBlocklist) for specific paths/pages that DNS cannot filter (e.g. HTTPS paths).
/// </summary>
public static class BrowserPolicyHelper
{
    private const string ChromePolicyPath = @"SOFTWARE\Policies\Google\Chrome\URLBlocklist";
    private const string EdgePolicyPath = @"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist";

    /// <summary>
    /// Extracts all URL-level blocking rules (prefixed with '!') from site rules.
    /// </summary>
    public static List<string> ExtractUrlRules(IEnumerable<string> rules)
    {
        var result = new List<string>();
        foreach (var raw in rules)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var r = raw.Trim();
            if (r.StartsWith("!"))
            {
                var pattern = r.Substring(1).Trim();
                if (pattern.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    pattern = pattern.Substring(7);
                else if (pattern.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    pattern = pattern.Substring(8);

                pattern = pattern.Trim().TrimEnd('*').TrimEnd('/').Trim();
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    result.Add(pattern);
                }
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Applies URLBlocklist to Google Chrome and Microsoft Edge via Windows Registry.
    /// If rules is empty, cleanly removes the policy subkeys so browsers have no restrictions.
    /// </summary>
    public static void ApplyUrlBlocklist(IEnumerable<string> urlRules)
    {
        var rules = ExtractUrlRules(urlRules);
        if (rules.Count == 0)
        {
            ClearUrlBlocklist();
            return;
        }

        ApplyToBrowser(ChromePolicyPath, rules);
        ApplyToBrowser(EdgePolicyPath, rules);
    }

    private static void ApplyToBrowser(string subKeyPath, List<string> rules)
    {
        try
        {
            // Open or create the policy key in HKLM
            using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, true);
            if (key != null)
            {
                // Delete existing values to avoid orphaned rules
                var existingValues = key.GetValueNames();
                foreach (var val in existingValues)
                {
                    key.DeleteValue(val, false);
                }

                // Add 1-based indexed rules
                for (int i = 0; i < rules.Count; i++)
                {
                    key.SetValue((i + 1).ToString(), rules[i], RegistryValueKind.String);
                }
            }
        }
        catch
        {
            // Fallback: Try HKCU if HKLM is read-only
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(subKeyPath, true);
                if (key != null)
                {
                    var existingValues = key.GetValueNames();
                    foreach (var val in existingValues)
                    {
                        key.DeleteValue(val, false);
                    }
                    for (int i = 0; i < rules.Count; i++)
                    {
                        key.SetValue((i + 1).ToString(), rules[i], RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Removes URLBlocklist policies from Google Chrome and Microsoft Edge.
    /// Immediately restores full browsing access.
    /// </summary>
    public static void ClearUrlBlocklist()
    {
        ClearFromBrowser(ChromePolicyPath);
        ClearFromBrowser(EdgePolicyPath);
    }

    private static void ClearFromBrowser(string subKeyPath)
    {
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(subKeyPath, false);
        }
        catch { }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, false);
        }
        catch { }
    }
}
