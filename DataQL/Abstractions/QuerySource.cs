namespace DataQL.Abstractions;

public sealed record QuerySource(
    string Provider,
    string Name);
