namespace ComboBox.Demo;

public sealed record Person(int Id, string Name, string Email, string Team, bool Inactive)
{
    private static readonly string[] FirstNames =
        ["Alice", "Adam", "Amelia", "Ben", "Chloe", "Daniel", "Emma", "Felix", "Grace", "Henry", "Isla", "Jack", "Kate", "Liam", "Mia", "Noah", "Olivia", "Peter", "Sophie", "Tom"];

    private static readonly string[] LastNames =
        ["Anderson", "Baker", "Clarke", "Davis", "Evans", "Fisher", "Grant", "Harris", "Johnson", "Walker"];

    private static readonly string[] Teams = ["Finance", "Sales", "IT", "Support"];

    /// <summary>200 people; every seventh one is inactive (shown as a disabled option).</summary>
    public static IReadOnlyList<Person> All { get; } = Enumerable.Range(0, 200)
        .Select(i =>
        {
            var first = FirstNames[i % FirstNames.Length];
            var last = LastNames[(i / FirstNames.Length) % LastNames.Length];
            return new Person(i + 1, $"{first} {last}", $"{first}.{last}{i + 1}@example.com".ToLowerInvariant(), Teams[i % Teams.Length], i % 7 == 3);
        })
        .ToList();
}
