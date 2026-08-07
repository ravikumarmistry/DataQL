using DataQL.Ast.Model;

namespace DataQL.Abstractions;

public interface IQueryProviderTranslator
{
    string Provider { get; }
    ProviderCapabilities Capabilities { get; }
    object Translate(QueryAst queryAst, QuerySource source);
}
