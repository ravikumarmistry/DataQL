using System.Collections.Generic;

namespace DataQL.Abstractions;

public sealed class ProviderCapabilities
{
    public string Provider { get; init; } = string.Empty;

    public string? Description { get; init; }

    public IReadOnlyList<CapabilityNote> Notes { get; init; } = [];

    public ISet<string> SupportedOperators { get; init; } = new HashSet<string>();

    public bool SupportsSelect { get; init; }

    public bool SupportsExclude { get; init; }

    public bool SupportsGrouping { get; init; }

    public bool SupportsHaving { get; init; }

    public bool SupportsNestedFields { get; init; }

    public bool SupportsDistinct { get; init; }

    public ISet<string> SupportedGroupOperations { get; init; } = new HashSet<string>();
}
