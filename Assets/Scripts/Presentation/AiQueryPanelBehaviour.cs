using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Application.UseCases;
using Yobi.Infrastructure.Ai;
using Yobi.Infrastructure.Knowledge;

namespace Yobi.Presentation
{
    // Phase 1.5 "development/search UI" for trying the local AI query parser - deliberately
    // minimal (no auto-generated UI hierarchy like CreatorSearchPanelBehaviour has): wire
    // queryInputField/askButton/answerText to existing UI elements in the Inspector.
    public sealed class AiQueryPanelBehaviour : MonoBehaviour
    {
        [SerializeField]
        private InputField queryInputField;

        [SerializeField]
        private Button askButton;

        [SerializeField]
        private Text answerText;

        [SerializeField]
        private string ollamaModel = "llama3.1:8b";

        private ParseCreatorQueryUseCase _queryUseCase;
        private CancellationTokenSource _requestCts;

        private void Awake()
        {
            if (queryInputField == null || askButton == null || answerText == null)
            {
                Debug.LogError("[AiQueryPanel] Required UI references are not assigned in the Inspector.");
                return;
            }

            var aiClient = new OllamaHttpClient(ollamaModel);
            var knowledgeRepository = new StreamingAssetsCreatorKnowledgeRepository();
            _queryUseCase = new ParseCreatorQueryUseCase(knowledgeRepository, aiClient);

            askButton.onClick.AddListener(OnAskButtonClicked);
        }

        private void OnDestroy()
        {
            _requestCts?.Cancel();
            _requestCts?.Dispose();
        }

        private async void OnAskButtonClicked()
        {
            var query = queryInputField.text;
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            _requestCts?.Cancel();
            _requestCts?.Dispose();
            _requestCts = new CancellationTokenSource();
            var requestToken = _requestCts.Token;

            askButton.interactable = false;

            // Ollama's non-streaming /api/generate gives no progress signal at all, so a fake
            // percentage would just be a lie - an elapsed-seconds counter is the most this can
            // honestly show while waiting.
            using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
            RunThinkingTicker(tickerCts.Token);

            try
            {
                var result = await _queryUseCase.AskAsync(query, requestToken);
                tickerCts.Cancel();
                answerText.text = result.Answer;
            }
            catch (System.OperationCanceledException)
            {
                tickerCts.Cancel();
                // Superseded by a newer query - leave whatever text is already showing.
            }
            catch (System.Exception ex)
            {
                tickerCts.Cancel();
                answerText.text = "查詢失敗,check下Ollama有冇跑緊。";
                Debug.LogError($"[AiQueryPanel] Query failed: {ex.Message}");
            }
            finally
            {
                askButton.interactable = true;
            }
        }

        private async void RunThinkingTicker(CancellationToken token)
        {
            var elapsedSeconds = 0;
            while (!token.IsCancellationRequested)
            {
                answerText.text = $"諗緊...({elapsedSeconds}s)";
                elapsedSeconds++;

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (System.OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
