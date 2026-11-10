using System;
using System.Threading.Tasks;

namespace ComboBox.Components;

/// <summary>
/// Passed to <c>ErrorTemplate</c> when the <c>DataProvider</c> threw.
/// </summary>
public sealed class ComboErrorContext(Exception exception, Func<Task> retry)
{
    /// <summary>The exception the data provider threw.</summary>
    public Exception Exception { get; } = exception;

    /// <summary>Calls the data provider again with the current search text (the same as <c>RefreshAsync()</c>).</summary>
    public Func<Task> Retry { get; } = retry;
}
