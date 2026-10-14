using System;
using System.Collections.Generic;
using System.Linq;

namespace ComboBox.Components;

/// <summary>
/// The selection logic shared by <see cref="Select{TItem}"/> and <see cref="Combobox{TItem}"/> when MultiSelect is on.
/// </summary>
internal static class MultiSelection
{
    public static bool Contains<TItem>(IReadOnlyList<TItem>? values, TItem item, Func<TItem?, TItem?, bool> equals)
    {
        return values is not null && values.Any(value => equals(value, item));
    }

    /// <summary>
    /// Returns a new list with <paramref name="item"/> removed if it is selected, otherwise appended,
    /// so the list stays in the order the items were picked.
    /// </summary>
    public static IReadOnlyList<TItem> Toggle<TItem>(IReadOnlyList<TItem>? values, TItem item, Func<TItem?, TItem?, bool> equals)
    {
        var updated = values?.ToList() ?? [];
        var index = updated.FindIndex(value => equals(value, item));

        if (index >= 0)
            updated.RemoveAt(index);
        else
            updated.Add(item);

        return updated;
    }

    public static string DefaultText<TItem>(IReadOnlyList<TItem> values, Func<TItem, string> toString)
    {
        return string.Join(", ", values.Select(toString));
    }
}
