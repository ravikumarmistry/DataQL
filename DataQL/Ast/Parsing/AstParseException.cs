using System;

namespace DataQL.Ast.Parsing;

public sealed class AstParseException : Exception
{
    public string Path { get; }

    public AstParseException(string path, string message)
        : base(message)
    {
        Path = path;
    }
}
