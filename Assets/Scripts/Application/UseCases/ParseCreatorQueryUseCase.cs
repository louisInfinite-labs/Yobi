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

        // Loaded lazily rather than in the constructor: the repository now reads through
        // UnityWebRequest (the only approach that works for StreamingAssets on every Unity
        // platform, not just where File.ReadAllText happens to work), which is inherently
        // async - and constructors can't await.
        private CreatorKnowledgeBase _knowledgeBase;

        public ParseCreatorQueryUseCase(ICreatorKnowledgeRepository repository, ILocalAiClient aiClient)
        {
            _repository = repository;
            _aiClient = aiClient;
        }

        public async Task<CreatorQueryResult> AskAsync(string query, CancellationToken cancellationToken)
        {
            _knowledgeBase ??= await _repository.LoadAsync(cancellationToken);

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

            var prompt = BuildPrompt(query, mentionedCreators, collaborations);
            var answer = await _aiClient.AskAsync(prompt, cancellationToken);

            return new CreatorQueryResult(
                hasKnowledgeBaseMatch: true,
                answer: answer,
                matchedCreatorIds: mentionedCreators.Select(c => c.Id).ToList(),
                sources: collaborations.Select(c => c.Source).Distinct().ToList());
        }

        private string BuildPrompt(string query, IReadOnlyList<CreatorProfile> creators, IReadOnlyList<CreatorCollaboration> collaborations)
        {
            // Org/games are grounded facts already sitting on the matched profile itself - a
            // question like "邊個組織" needs these even when the creator has zero recorded
            // collaborations, which collaborations-only context could never answer.
            var profileLines = new StringBuilder();
            foreach (var creator in creators)
            {
                var name = creator.Names.FirstOrDefault() ?? creator.Id;
                var org = string.IsNullOrEmpty(creator.Org) || creator.Org == "unknown" ? "未知" : creator.Org;
                var games = creator.Games.Count > 0 ? string.Join("、", creator.Games) : "未知";
                profileLines.AppendLine($"- {name}:所屬={org},玩過嘅遊戲={games}");
            }

            var contextLines = new StringBuilder();
            foreach (var collab in collaborations)
            {
                var names = string.Join(" / ", collab.ParticipantIds.Select(id => _knowledgeBase.GetById(id)?.Names.FirstOrDefault() ?? id));
                var status = collab.NeedsManualReview ? "未核實" : "已核實";
                contextLines.AppendLine($"- [{collab.Game}][{status}] {collab.EventName}: {names}(source: {collab.Source})");
            }

            return
                "你係一個VTuber資料查詢助手。只可以根據下面提供嘅「已知資料」嚟回答,唔可以自己作資料。" +
                "如果資料入面冇答案,要老實講\"資料庫入面搵唔到\"。標咗「未核實」嘅記錄,回答嗰陣一定要註明係未核實,唔可以講到好似肯定確認咗咁。\n\n" +
                $"已知創作者資料:\n{profileLines}\n" +
                $"已知合作記錄:\n{contextLines}\n" +
                $"問題:{query}\n請用廣東話回答,列出人名同片段來源。";
        }
    }
}
