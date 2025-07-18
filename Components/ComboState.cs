namespace ComboBox.Components;

public class ComboState(string? search, int take, int skip)
{
    /// <summary>Filter text.</summary>
    public string? Search { get; set; } = search;

    /// <summary>How many items to fetch.</summary>
    public int Take { get; set; } = take;

    /// <summary>How many items to skip.</summary>
    public int Skip { get; set; } = skip;
}