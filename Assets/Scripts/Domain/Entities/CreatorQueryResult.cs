using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorQueryResult
    {
        // False when no creator mentioned in the query was found in the knowledge base at
        // all - the use case skips calling the LLM in that case (see
        // ParseCreatorQueryUseCase), so there is never a chance for it to answer from its own
        // unguided memory instead of the knowledge base.
        public bool HasKnowledgeBaseMatch { get; }
        public string Answer { get; }
        public IReadOnlyList<string> MatchedCreatorIds { get; }
        public IReadOnlyList<string> Sources { get; }

        public CreatorQueryResult(bool hasKnowledgeBaseMatch, string answer, IReadOnlyList<string> matchedCreatorIds, IReadOnlyList<string> sources)
        {
            HasKnowledgeBaseMatch = hasKnowledgeBaseMatch;
            Answer = answer;
            MatchedCreatorIds = matchedCreatorIds;
            Sources = sources;
        }
    }
}
