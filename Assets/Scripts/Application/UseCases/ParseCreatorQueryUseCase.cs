using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;

namespace Yobi.Application.UseCases
{
    // Phase 1.5 "Query Parser": grounds the local LLM's answer in CreatorKnowledgeBase instead
    // of letting it answer from its own (untrustworthy, un-updatable) training data - matches
    // the roadmap's explicit "don't let the LLM memorize creator facts" rule.
    public sealed class ParseCreatorQueryUseCase
    {
        private const int MaxCollaborationsInContext = 15;

        private readonly ICreatorKnowledgeRepository _repository;
        private readonly ILocalAiClient _aiClient;
        private readonly CreatorKnowledgeBase _knowledgeBase;

        public ParseCreatorQueryUseCase(ICreatorKnowledgeRepository repository, ILocalAiClient aiClient)
        {
            _repository = repository;
            _aiClient = aiClient;
            _knowledgeBase = _repository.Load();
        }

        public async Task<CreatorQueryResult> AskAsync(string query, CancellationToken cancellationToken)
        {
            var mentionedCreators = _knowledgeBase.FindMentionedIn(query);
            if (mentionedCreators.Count == 0)
            {
                return new CreatorQueryResult(
                    hasKnowledgeBaseMatch: false,
                    answer: "資料庫入面搵唔到同呢個問題有關嘅創作者。",
                    matchedCreatorIds: new List<string>(),
                    sources: new List<string>());
            }

            var collaborations = mentionedCreators
                .SelectMany(c => _knowledgeBase.FindCollaborationsFor(c.Id))
                .GroupBy(c => c.EventName)
                .Select(g => g.First())
                .Take(MaxCollaborationsInContext)
                .ToList();

            var prompt = BuildPrompt(query, collaborations);
            var answer = await _aiClient.AskAsync(prompt, cancellationToken);

            return new CreatorQueryResult(
                hasKnowledgeBaseMatch: true,
                answer: answer,
                matchedCreatorIds: mentionedCreators.Select(c => c.Id).ToList(),
                sources: collaborations.Select(c => c.Source).Distinct().ToList());
        }

        private string BuildPrompt(string query, IReadOnlyList<CreatorCollaboration> collaborations)
        {
            var contextLines = new StringBuilder();
            foreach (var collab in collaborations)
            {
                var names = string.Join(" / ", collab.ParticipantIds.Select(id => _knowledgeBase.GetById(id)?.Names.FirstOrDefault() ?? id));
                var status = collab.NeedsManualReview ? "未核實" : "已核實";
                contextLines.AppendLine($"- [{collab.Game}][{status}] {collab.EventName}: {names}(source: {collab.Source})");
            }

            return
                "你係一個VTuber資料查詢助手。只可以根據下面提供嘅「已知合作記錄」嚟回答,唔可以自己作資料。" +
                "如果資料入面冇答案,要老實講\"資料庫入面搵唔到\"。標咗「未核實」嘅記錄,回答嗰陣一定要註明係未核實,唔可以講到好似肯定確認咗咁。\n\n" +
                $"已知合作記錄:\n{contextLines}\n" +
                $"問題:{query}\n請用廣東話回答,列出人名同片段來源。";
        }
    }
}
