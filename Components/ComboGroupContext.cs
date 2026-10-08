using System.Collections.Generic;

namespace ComboBox.Components;

/// <summary>
/// Describes one group of results when <c>GroupBy</c> is set. Passed to <c>GroupFooterTemplate</c>.
/// </summary>
public sealed class ComboGroupContext<TItem>(string key, IReadOnlyList<TItem> items)
{
    /// <summary>The group key returned by <c>GroupBy</c>.</summary>
    public string Key { get; } = key;

    /// <summary>The items in this group, in the order they were loaded.</summary>
    public IReadOnlyList<TItem> Items { get; } = items;
}
