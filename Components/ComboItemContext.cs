using System;
using System.Collections.Generic;

namespace ComboBox.Components;

/// <summary>
/// The state of one row in the dropdown. Passed to <c>ItemContentTemplate</c>.
/// </summary>
public sealed class ComboItemContext<TItem>(TItem item, bool isSelected, bool isHighlighted, bool isDisabled, string searchText)
{
    /// <summary>The item the row shows.</summary>
    public TItem Item { get; } = item;

    /// <summary>True when the item is the selected <c>Value</c>, or one of the <c>Values</c> with <c>MultiSelect</c>.</summary>
    public bool IsSelected { get; } = isSelected;

    /// <summary>True when the row is the active option (keyboard or mouse).</summary>
    public bool IsHighlighted { get; } = isHighlighted;

    /// <summary>True when <c>ItemDisabledFunc</c> returns true for the item.</summary>
    public bool IsDisabled { get; } = isDisabled;

    /// <summary>
    /// The search text the shown results were loaded for. Empty when nothing is searched,
    /// and always empty in <see cref="Select{TItem}"/>.
    /// </summary>
    public string SearchText { get; } = searchText;

    /// <summary>
    /// Splits <paramref name="text"/> around every case-insensitive occurrence of <see cref="SearchText"/>,
    /// so the matching parts can be marked, e.g. with <c>&lt;mark&gt;</c>.
    /// Returns the whole text as one non-matching segment when nothing is searched.
    /// </summary>
    public IReadOnlyList<ComboTextSegment> SplitByMatch(string? text) => ComboTextSegment.Split(text, SearchText);
}

/// <summary>
/// A part of a text, as returned by <see cref="ComboItemContext{TItem}.SplitByMatch"/>.
/// </summary>
public readonly record struct ComboTextSegment(string Text, bool IsMatch)
{
    internal static IReadOnlyList<ComboTextSegment> Split(string? text, string? search)
    {
        text ??= "";
        if (string.IsNullOrWhiteSpace(search) || text.Length == 0)
            return [new ComboTextSegment(text, false)];

        var segments = new List<ComboTextSegment>();
        var position = 0;
        while (position < text.Length)
        {
            var match = text.IndexOf(search, position, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
                break;

            if (match > position)
                segments.Add(new ComboTextSegment(text[position..match], false));

            segments.Add(new ComboTextSegment(text.Substring(match, search.Length), true));
            position = match + search.Length;
        }

        if (position < text.Length)
            segments.Add(new ComboTextSegment(text[position..], false));

        return segments;
    }
}
