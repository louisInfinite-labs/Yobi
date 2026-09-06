using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yobi.Application.UseCases;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Ai;
using Yobi.Infrastructure.Knowledge;
using Yobi.Infrastructure.Storage;

namespace Yobi.Presentation
{
    // The single Google-homepage-style input field that replaced the separate Search and AI
    // Query dock buttons/panels. Submitting a query tries a Holodex/mock creator-name search
    // first (via CreatorSearchPanelBehaviour, so adds land in the same in-memory watchlist its
    // polling loop already reads from); if nothing matches, it falls back to the existing local
    // AI query parser unchanged.
    public sealed class MainSearchBarBehaviour : MonoBehaviour
    {
        private const int PillTextureSize = 64;
        private const float PillCornerRadius = 20f;

        [SerializeField]
        private InputField searchInputField;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private RectTransform resultsContainer;

        [SerializeField]
        private GameObject resultRowTemplate;

        [SerializeField]
        private Text answerText;

        [SerializeField]
        private string ollamaModel = "llama3.1:8b";

        private const int MaxHistoryEntries = 8;

        private CreatorSearchPanelBehaviour _searchPanel;
        private ParseCreatorQueryUseCase _queryUseCase;
        private IQueryHistoryRepository _queryHistoryRepository;
        private QueryHistory _queryHistory;
        private CancellationTokenSource _requestCts;

        private readonly List<GameObject> _activeResultRows = new List<GameObject>();

        private void Awake()
        {
            if (searchInputField == null || resultsContainer == null || resultRowTemplate == null || answerText == null)
            {
                Debug.LogError("[MainSearchBar] Required UI references are not assigned in the Inspector. Run Tools > Yobi > Setup Main Search Bar.");
                return;
            }

            resultRowTemplate.SetActive(false);
            HideResults();
            StartCoroutine(ApplyRoundedRectSpriteNextFrame());

            // Not constructed independently: SearchCreatorsAsync/AddToWatchlist route through
            // this specific instance so watchlist adds stay visible to its polling/reminder loop
            // - see CreatorSearchPanelBehaviour's own comments on those two methods for why.
            _searchPanel = FindFirstObjectByType<CreatorSearchPanelBehaviour>();

            var aiClient = new OllamaHttpClient(ollamaModel);
            var knowledgeRepository = new StreamingAssetsCreatorKnowledgeRepository();
            _queryUseCase = new ParseCreatorQueryUseCase(knowledgeRepository, aiClient);

            _queryHistoryRepository = new LocalFileQueryHistoryRepository();
            _queryHistory = _queryHistoryRepository.Load();

            // Fires on Enter and on the field losing focus either way - both count as "the user
            // is done typing this query" for a Google-style bar.
            searchInputField.onEndEdit.AddListener(SubmitQuery);

            // Google-style: focusing an empty box shows recent searches; typing anything hides
            // them again until the box is cleared back to empty. The legacy UI.InputField (unlike
            // TMP_InputField) has no onSelect event of its own, so an EventTrigger is the way to
            // observe focus-gained without subclassing InputField.
            var focusTrigger = searchInputField.gameObject.GetComponent<EventTrigger>();
            if (focusTrigger == null)
            {
                focusTrigger = searchInputField.gameObject.AddComponent<EventTrigger>();
            }

            var selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            selectEntry.callback.AddListener(_ => ShowHistoryIfEmpty());
            focusTrigger.triggers.Add(selectEntry);

            searchInputField.onValueChanged.AddListener(OnInputValueChanged);
        }

        private void OnDestroy()
        {
            _requestCts?.Cancel();
            _requestCts?.Dispose();
        }

        private async void SubmitQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            _queryHistory.Add(query, MaxHistoryEntries);
            try
            {
                _queryHistoryRepository.Save(_queryHistory);
            }
            catch (Exception ex)
            {
                // History is a nice-to-have, not load-bearing - a full disk or permissions
                // problem here must not stop the actual search/AI answer below from happening.
                Debug.LogError($"[MainSearchBar] Failed to save query history: {ex.Message}");
            }

            _requestCts?.Cancel();
            _requestCts?.Dispose();
            _requestCts = new CancellationTokenSource();
            var requestToken = _requestCts.Token;

            var searchResults = Array.Empty<CreatorSearchResult>() as IReadOnlyList<CreatorSearchResult>;

            if (_searchPanel != null)
            {
                try
                {
                    searchResults = await _searchPanel.SearchCreatorsAsync(query, requestToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Not fatal - fall through to the AI path below instead of dead-ending here.
                    Debug.LogError($"[MainSearchBar] Creator search failed, falling back to AI: {ex.Message}");
                }
            }

            // Cancelling _requestCts above doesn't guarantee the awaits below actually observe
            // it before completing - a stale request can still win the race and reach here after
            // a newer one has already started, so each terminal render is guarded once more
            // immediately before it runs.
            if (searchResults.Count > 0)
            {
                if (requestToken.IsCancellationRequested)
                {
                    return;
                }

                ShowMatches(searchResults);
                return;
            }

            using var tickerCts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
            RunThinkingTicker(tickerCts.Token);

            try
            {
                var result = await _queryUseCase.AskAsync(query, requestToken);
                tickerCts.Cancel();

                if (requestToken.IsCancellationRequested)
                {
                    return;
                }

                ShowAnswer(result.Answer);
            }
            catch (OperationCanceledException)
            {
                tickerCts.Cancel();
                // Superseded by a newer query - leave whatever is already showing.
            }
            catch (Exception ex)
            {
                tickerCts.Cancel();

                if (requestToken.IsCancellationRequested)
                {
                    return;
                }

                ShowAnswer("查詢失敗,check下Ollama有冇跑緊。");
                Debug.LogError($"[MainSearchBar] Query failed: {ex.Message}");
            }
        }

        private async void RunThinkingTicker(CancellationToken token)
        {
            var elapsedSeconds = 0;
            while (!token.IsCancellationRequested)
            {
                ShowAnswer($"諗緊...({elapsedSeconds}s)");
                elapsedSeconds++;

                try
                {
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void OnInputValueChanged(string newText)
        {
            if (string.IsNullOrEmpty(newText))
            {
                ShowHistoryIfEmpty();
            }
            else
            {
                HideResults();
            }
        }

        private void ShowHistoryIfEmpty()
        {
            if (!string.IsNullOrEmpty(searchInputField.text))
            {
                return;
            }

            if (_queryHistory.Entries.Count == 0)
            {
                return;
            }

            SetAnswerVisible(false);
            resultsContainer.gameObject.SetActive(true);
            ClearResultRows();

            foreach (var query in _queryHistory.Entries)
            {
                CreateHistoryRow(query);
            }
        }

        private void HideResults()
        {
            SetAnswerVisible(false);
            resultsContainer.gameObject.SetActive(false);
        }

        private void ShowAnswer(string text)
        {
            ClearResultRows();
            resultsContainer.gameObject.SetActive(false);
            SetAnswerVisible(true);
            answerText.text = text;
        }

        // answerText is wired to the child Text label, not the parent "AnswerText" container -
        // the container is what actually carries the translucent background Image, so toggling
        // answerText.gameObject directly (an earlier version of this did) only ever hid/showed
        // the text itself, leaving that background permanently visible from launch onward.
        private void SetAnswerVisible(bool visible)
        {
            answerText.transform.parent.gameObject.SetActive(visible);
        }

        private void ShowMatches(IReadOnlyList<CreatorSearchResult> results)
        {
            SetAnswerVisible(false);
            resultsContainer.gameObject.SetActive(true);
            ClearResultRows();

            foreach (var result in results)
            {
                CreateResultRow(result);
            }
        }

        private void CreateResultRow(CreatorSearchResult result)
        {
            var row = Instantiate(resultRowTemplate, resultsContainer);
            row.SetActive(true);

            var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
            {
                nameText.text = $"{result.DisplayName}  ({result.ChannelId})";
            }

            var addButton = row.transform.Find("AddButton")?.GetComponent<Button>();
            var addButtonLabel = addButton != null ? addButton.transform.Find("Text")?.GetComponent<Text>() : null;
            if (addButton != null)
            {
                addButton.onClick.RemoveAllListeners();
                addButton.onClick.AddListener(() => OnAddToWatchlistClicked(result, addButton, addButtonLabel));
            }

            _activeResultRows.Add(row);
        }

        // Reuses the same row template as a creator match, minus the add-to-watchlist button -
        // the whole row is clickable instead (a Button on the template's own root), re-filling
        // the box and re-submitting the same query, the way Google's recent-searches list works.
        private void CreateHistoryRow(string query)
        {
            var row = Instantiate(resultRowTemplate, resultsContainer);
            row.SetActive(true);

            var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
            {
                nameText.text = query;
            }

            var addButton = row.transform.Find("AddButton")?.GetComponent<Button>();
            if (addButton != null)
            {
                addButton.gameObject.SetActive(false);
            }

            var rowButton = row.GetComponent<Button>();
            if (rowButton != null)
            {
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(() =>
                {
                    searchInputField.text = query;
                    SubmitQuery(query);
                });
            }

            _activeResultRows.Add(row);
        }

        private void OnAddToWatchlistClicked(CreatorSearchResult result, Button addButton, Text addButtonLabel)
        {
            if (_searchPanel == null)
            {
                return;
            }

            var addResult = _searchPanel.AddToWatchlist(result);

            if (addButtonLabel != null)
            {
                addButtonLabel.text = addResult == WatchlistAddResult.AlreadyExists ? "已追蹤" : "已加落追蹤";
            }

            addButton.interactable = false;
        }

        private void ClearResultRows()
        {
            foreach (var row in _activeResultRows)
            {
                Destroy(row);
            }

            _activeResultRows.Clear();
        }

        // Generated in code rather than an imported art asset, same anti-aliased-texture
        // convention this project already uses for the dock's circular buttons - a rounded-rect
        // signed-distance field this time instead of a plain radial one. Baked at the bar's
        // actual final pixel size (Image.Type.Simple) rather than a small tile with
        // Image.Type.Sliced: 9-slicing this texture produced a visibly wrong (dimmer, hard-edged)
        // band across part of the bar - not worth chasing down further since a full-size bake
        // sidesteps it entirely and this bar's width never changes at runtime anyway.
        //
        // Deferred one frame: SearchInputField's size is resolved by the parent MainSearchBar's
        // VerticalLayoutGroup, which - this early in Awake(), even after
        // Canvas.ForceUpdateCanvases() + LayoutRebuilder.ForceRebuildLayoutImmediate() on that
        // parent - still measured rect.width/height at Unity's pre-layout default (100x100) in
        // testing, not the real ~500x44. Baking at 100x100 and letting Image.Type.Simple stretch
        // it to the real rect afterward is what produced the wrong-looking dimmer band down part
        // of the bar (a 100x100 square stretched 5x wide and squashed to under half height smears
        // the rounded-corner alpha falloff across a much wider strip than intended). Waiting a
        // frame is what the diagnostic that found this bug confirmed actually settles the layout.
        private IEnumerator ApplyRoundedRectSpriteNextFrame()
        {
            // yield return null resumes at the start of the NEXT frame's Update, which in
            // testing was still too early - LayoutGroup rebuilds happen later, tied to
            // Canvas.willRenderCanvases just before rendering. WaitForEndOfFrame resumes after
            // that, which is what actually observed the real ~500x44 rect instead of the
            // pre-layout 100x100 default.
            yield return new WaitForEndOfFrame();
            ApplyRoundedRectSprite();
        }

        private void ApplyRoundedRectSprite()
        {
            if (backgroundImage == null)
            {
                return;
            }

            var width = Mathf.Max(1, Mathf.RoundToInt(backgroundImage.rectTransform.rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(backgroundImage.rectTransform.rect.height));
            var cornerRadius = Mathf.Min(PillCornerRadius, width / 2f, height / 2f);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var halfWidth = width / 2f;
            var halfHeight = height / 2f;
            var centerX = (width - 1) / 2f;
            var centerY = (height - 1) / 2f;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var qx = Mathf.Abs(x - centerX) - (halfWidth - cornerRadius);
                    var qy = Mathf.Abs(y - centerY) - (halfHeight - cornerRadius);
                    var outsideDist = Mathf.Sqrt((Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f)) + (Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)));
                    var insideDist = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    var distance = outsideDist + insideDist - cornerRadius;

                    // Soft ~1px edge: alpha ramps from 1 to 0 as `distance` crosses the boundary
                    // (negative well inside, ~0 at the rounded edge, positive outside).
                    var alpha = Mathf.Clamp01(0.5f - distance);
                    var pixelColor = Color.white;
                    pixelColor.a *= alpha;
                    pixels[(y * width) + x] = pixelColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));

            backgroundImage.sprite = sprite;
            backgroundImage.type = Image.Type.Simple;
        }
    }
}
