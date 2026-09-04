using System.Threading;
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

            askButton.interactable = false;
            answerText.text = "諗緊...";

            try
            {
                var result = await _queryUseCase.AskAsync(query, _requestCts.Token);
                answerText.text = result.Answer;
            }
            catch (System.OperationCanceledException)
            {
                // Superseded by a newer query - leave whatever text is already showing.
            }
            catch (System.Exception ex)
            {
                answerText.text = "查詢失敗,check下Ollama有冇跑緊。";
                Debug.LogError($"[AiQueryPanel] Query failed: {ex.Message}");
            }
            finally
            {
                askButton.interactable = true;
            }
        }
    }
}
