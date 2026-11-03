namespace Rts.Data;

/// <summary>
/// Collects names that must resolve after every file is parsed: prerequisites and effect targets. A name is valid
/// when it is a type id, a provides name, or <c>class:vehicle|infantry|building</c>.
/// </summary>
internal sealed class RulesReferences
{
    private readonly List<(string File, string Key, string Name)> _refs = [];

    /// <summary>Adds one reference from <c>Id.key</c>.</summary>
    public void Add(RulesFields f, string key, string name) => _refs.Add((f.File, f.Id + "." + key, name));

    /// <summary>Adds several references from <c>Id.key</c>.</summary>
    public void AddAll(RulesFields f, string key, IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            Add(f, key, name);
        }
    }

    /// <summary>Reports every reference that resolves to nothing.</summary>
    public void Validate(IReadOnlySet<string> typeIds, IReadOnlySet<string> provides, List<RulesProblem> problems)
    {
        foreach (var r in _refs)
        {
            if (typeIds.Contains(r.Name) || provides.Contains(r.Name))
            {
                continue;
            }
            if (r.Name.StartsWith(RulesEnums.ClassPrefix, StringComparison.Ordinal))
            {
                string cls = r.Name[RulesEnums.ClassPrefix.Length..];
                if (Array.IndexOf(RulesEnums.ClassNames, cls) >= 0)
                {
                    continue;
                }
                problems.Add(new RulesProblem(r.File, r.Key,
                    $"unknown class '{cls}'; must be one of {RulesEnums.Join(RulesEnums.ClassNames)}"));
                continue;
            }
            problems.Add(new RulesProblem(r.File, r.Key, $"unknown type, provides name or class reference '{r.Name}'"));
        }
    }
}
