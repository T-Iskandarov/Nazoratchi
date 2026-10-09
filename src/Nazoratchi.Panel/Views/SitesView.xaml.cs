using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nazoratchi.Core.Models;
using Nazoratchi.Core.Services;

namespace Nazoratchi.Panel.Views;

public partial class SitesView : UserControl
{
    private readonly ConfigManager _configManager;
    private List<SiteRule> _blacklistRules = new();
    private List<SiteRule> _whitelistRules = new();
    private bool _isInitializing = true;

    public SitesView()
    {
        InitializeComponent();
        _configManager = new ConfigManager();
        Loaded += (s, e) => LoadData();
    }

    private void LoadData()
    {
        try
        {
            _isInitializing = true;

            // Load active filter mode
            var config = _configManager.LoadConfig();
            if (config.FilterMode == FilterMode.BlackList)
            {
                BlacklistRadio.IsChecked = true;
                SitesTabControl.SelectedIndex = 0;
                UpdateActivePills(true);
            }
            else
            {
                WhitelistRadio.IsChecked = true;
                SitesTabControl.SelectedIndex = 1;
                UpdateActivePills(false);
            }

            // Load sites
            var siteList = _configManager.LoadSites();
            _blacklistRules = siteList.BlacklistSites ?? new List<SiteRule>();
            _whitelistRules = siteList.WhitelistSites ?? new List<SiteRule>();

            // Populate Blacklist UI
            BlacklistTextBox.Text = string.Join(Environment.NewLine, _blacklistRules.Select(r => r.Domain));
            UpdateBlacklistGrid();

            // Populate Whitelist UI
            WhitelistTextBox.Text = string.Join(Environment.NewLine, _whitelistRules.Select(r => r.Domain));
            UpdateWhitelistGrid();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void UpdateActivePills(bool isBlacklist)
    {
        if (BlacklistActivePill != null)
            BlacklistActivePill.Visibility = isBlacklist ? Visibility.Visible : Visibility.Collapsed;
        if (WhitelistActivePill != null)
            WhitelistActivePill.Visibility = isBlacklist ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SitesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || e.Source != SitesTabControl) return;
        if (SitesTabControl.SelectedIndex == 0 && BlacklistRadio.IsChecked != true)
        {
            BlacklistRadio.IsChecked = true;
        }
        else if (SitesTabControl.SelectedIndex == 1 && WhitelistRadio.IsChecked != true)
        {
            WhitelistRadio.IsChecked = true;
        }
    }

    private void ActiveMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var config = _configManager.LoadConfig();
        if (BlacklistRadio.IsChecked == true)
        {
            config.FilterMode = FilterMode.BlackList;
            SitesTabControl.SelectedIndex = 0;
            UpdateActivePills(true);
        }
        else
        {
            config.FilterMode = FilterMode.WhiteList;
            SitesTabControl.SelectedIndex = 1;
            UpdateActivePills(false);
        }

        _configManager.SaveConfig(config);
    }

    #region Blacklist Actions
    private void SaveBlacklistButton_Click(object sender, RoutedEventArgs e)
    {
        var lines = BlacklistTextBox.Text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => SiteMatcher.NormalizeRule(l))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingDict = _blacklistRules
            .GroupBy(r => r.Domain.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().AddedAt);

        var newRules = lines.Select(domain => new SiteRule
        {
            Domain = domain,
            AddedAt = existingDict.ContainsKey(domain) ? existingDict[domain] : DateTime.Now
        }).ToList();

        var siteList = _configManager.LoadSites();
        siteList.Sites.Clear();
        siteList.BlacklistSites = newRules;
        _configManager.SaveSites(siteList);

        _blacklistRules = newRules;
        UpdateBlacklistGrid();

        BlacklistNotificationText.Text = $"Qora ro'yxat saqlandi ({newRules.Count} ta sayt/qoida)";
        BlacklistNotificationBadge.Visibility = Visibility.Visible;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, args) =>
        {
            BlacklistNotificationBadge.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void SortBlacklistButton_Click(object sender, RoutedEventArgs e)
    {
        var lines = BlacklistTextBox.Text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        BlacklistTextBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void ClearBlacklistText_Click(object sender, RoutedEventArgs e)
    {
        BlacklistTextBox.Clear();
    }

    private void DeleteBlacklistSite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var dynamicItem = button.DataContext;
            var domainProp = dynamicItem.GetType().GetProperty("Domain");
            var domain = domainProp?.GetValue(dynamicItem)?.ToString();

            if (!string.IsNullOrEmpty(domain))
            {
                var siteList = _configManager.LoadSites();
                siteList.Sites.Clear();
                siteList.BlacklistSites.RemoveAll(r => r.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
                _configManager.SaveSites(siteList);

                _blacklistRules = siteList.BlacklistSites;
                BlacklistTextBox.Text = string.Join(Environment.NewLine, _blacklistRules.Select(r => r.Domain));
                UpdateBlacklistGrid();
            }
        }
    }

    private void SearchBlacklist_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBlacklistTextBox.Text;
        if (SearchBlacklistPlaceholder != null)
            SearchBlacklistPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        if (ClearSearchBlacklistBtn != null)
            ClearSearchBlacklistBtn.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        UpdateBlacklistGrid(text.Trim());
    }

    private void ClearSearchBlacklist_Click(object sender, RoutedEventArgs e)
    {
        SearchBlacklistTextBox.Clear();
        SearchBlacklistTextBox.Focus();
    }

    private void UpdateBlacklistGrid(string filter = "")
    {
        var query = _blacklistRules.AsEnumerable();
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(r => r.Domain.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var items = query.Select(r => new
        {
            Domain = r.Domain,
            AddedDate = r.AddedAt.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        BlacklistGrid.ItemsSource = items;
        if (BlacklistCountBadge != null)
        {
            BlacklistCountBadge.Text = $"{_blacklistRules.Count} ta";
        }
        if (BlacklistEmptyState != null)
        {
            BlacklistEmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BlacklistGrid.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
    #endregion

    #region Whitelist Actions
    private void SaveWhitelistButton_Click(object sender, RoutedEventArgs e)
    {
        var lines = WhitelistTextBox.Text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => SiteMatcher.NormalizeRule(l))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingDict = _whitelistRules
            .GroupBy(r => r.Domain.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().AddedAt);

        var newRules = lines.Select(domain => new SiteRule
        {
            Domain = domain,
            AddedAt = existingDict.ContainsKey(domain) ? existingDict[domain] : DateTime.Now
        }).ToList();

        var siteList = _configManager.LoadSites();
        siteList.Sites.Clear();
        siteList.WhitelistSites = newRules;
        _configManager.SaveSites(siteList);

        _whitelistRules = newRules;
        UpdateWhitelistGrid();

        WhitelistNotificationText.Text = $"Oq ro'yxat saqlandi ({newRules.Count} ta sayt/qoida)";
        WhitelistNotificationBadge.Visibility = Visibility.Visible;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (s, args) =>
        {
            WhitelistNotificationBadge.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void SortWhitelistButton_Click(object sender, RoutedEventArgs e)
    {
        var lines = WhitelistTextBox.Text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        WhitelistTextBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void ClearWhitelistText_Click(object sender, RoutedEventArgs e)
    {
        WhitelistTextBox.Clear();
    }

    private void DeleteWhitelistSite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext != null)
        {
            var dynamicItem = button.DataContext;
            var domainProp = dynamicItem.GetType().GetProperty("Domain");
            var domain = domainProp?.GetValue(dynamicItem)?.ToString();

            if (!string.IsNullOrEmpty(domain))
            {
                var siteList = _configManager.LoadSites();
                siteList.Sites.Clear();
                siteList.WhitelistSites.RemoveAll(r => r.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
                _configManager.SaveSites(siteList);

                _whitelistRules = siteList.WhitelistSites;
                WhitelistTextBox.Text = string.Join(Environment.NewLine, _whitelistRules.Select(r => r.Domain));
                UpdateWhitelistGrid();
            }
        }
    }

    private void SearchWhitelist_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchWhitelistTextBox.Text;
        if (SearchWhitelistPlaceholder != null)
            SearchWhitelistPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        if (ClearSearchWhitelistBtn != null)
            ClearSearchWhitelistBtn.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        UpdateWhitelistGrid(text.Trim());
    }

    private void ClearSearchWhitelist_Click(object sender, RoutedEventArgs e)
    {
        SearchWhitelistTextBox.Clear();
        SearchWhitelistTextBox.Focus();
    }

    private void UpdateWhitelistGrid(string filter = "")
    {
        var query = _whitelistRules.AsEnumerable();
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(r => r.Domain.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        var items = query.Select(r => new
        {
            Domain = r.Domain,
            AddedDate = r.AddedAt.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        WhitelistGrid.ItemsSource = items;
        if (WhitelistCountBadge != null)
        {
            WhitelistCountBadge.Text = $"{_whitelistRules.Count} ta";
        }
        if (WhitelistEmptyState != null)
        {
            WhitelistEmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            WhitelistGrid.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
    #endregion
}
