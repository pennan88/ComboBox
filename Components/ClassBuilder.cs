using System;
using System.Collections.Generic;
using System.Linq;

namespace ComboBox.Components;

public class ClassBuilder
{
    private readonly List<string> _classes = [];

    /// <summary>
    /// Adds the given class if the optional condition is true (or un-specified).
    /// </summary>
    public ClassBuilder AddClass(string? className, bool condition)
    {
        if (condition && !string.IsNullOrWhiteSpace(className))
            _classes.Add(className.Trim());
        return this;
    }

    /// <summary>
    /// Add multiple classes at once (space-separated).
    /// </summary>
    public ClassBuilder AddClass(string? classNames)
    {
        if (!string.IsNullOrWhiteSpace(classNames))
            foreach (var c in classNames.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                _classes.Add(c);
        return this;
    }

    /// <summary>
    /// Builds the final space-separated string of classes.
    /// </summary>
    public string Build() => string.Join(" ", _classes.Distinct());
}