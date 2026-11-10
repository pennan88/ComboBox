namespace ComboBox.Demo;

public sealed record Person(int Id, string Name, string Email, string Team, bool Inactive)
{
    private static readonly string[] FirstNames =
        ["Anna", "Albin", "Alva", "Bertil", "Cecilia", "David", "Elin", "Fredrik", "Greta", "Hugo", "Ida", "Johan", "Karin", "Lars", "Maja", "Nils", "Olivia", "Per", "Sara", "Tove"];

    private static readonly string[] LastNames =
        ["Andersson", "Berg", "Ek", "Holm", "Johansson", "Karlsson", "Lind", "Nilsson", "Svensson", "Åberg"];

    private static readonly string[] Teams = ["Ekonomi", "Försäljning", "IT", "Support"];

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
