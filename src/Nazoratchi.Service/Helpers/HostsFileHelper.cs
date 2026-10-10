using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace Nazoratchi.Service.Helpers;

/// <summary>
/// Manages appending blocked domains to the Windows HOSTS file.
/// This enforces blocking at the OS kernel level, bypassing QUIC, DoH, and IPv6 DNS leaks.
/// </summary>
public static class HostsFileHelper
{
    private static readonly string HostsFilePath = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
    private const string Marker = "# NazoratchiBlock";
    private static readonly ConcurrentDictionary<string, bool> _blockedDomains = new();
    private static readonly object _fileLock = new();

    public static void Initialize()
    {
        ClearNazoratchiBlocks();
    }

    public static void ClearNazoratchiBlocks()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(HostsFilePath)) return;

                var lines = File.ReadAllLines(HostsFilePath);
                var cleanLines = lines.Where(l => !l.EndsWith(Marker)).ToList();
                
                if (lines.Length != cleanLines.Count)
                {
                    File.WriteAllLines(HostsFilePath, cleanLines);
                }
                
                _blockedDomains.Clear();
            }
            catch
            {
                // Ignore file lock issues during clear
            }
        }
    }

    public static void BlockDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;
        
        domain = domain.ToLowerInvariant().Trim();
        
        // Fast path check to avoid lock contention
        if (_blockedDomains.ContainsKey(domain)) return;

        lock (_fileLock)
        {
            if (_blockedDomains.ContainsKey(domain)) return;

            try
            {
                // Prepend newline in case the last line in hosts file doesn't have one
                var appendText = $"{Environment.NewLine}0.0.0.0 {domain} {Marker}";
                File.AppendAllText(HostsFilePath, appendText);
                _blockedDomains[domain] = true;
            }
            catch
            {
                // If it fails (e.g., file locked by another process), we will just catch it next time
            }
        }
    }
}
