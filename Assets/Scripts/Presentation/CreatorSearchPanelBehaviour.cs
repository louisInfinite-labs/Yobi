using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Application.UseCases;
using Yobi.Domain.Entities;
using Yobi.Infrastructure.Api.Holodex;
using Yobi.Infrastructure.Config;

namespace Yobi.Presentation
{
    public sealed class CreatorSearchPanelBehaviour : MonoBehaviour
    {
        [SerializeField]
        private InputField searchInputField;

        [SerializeField]
        private Button searchButton;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private RectTransform resultsContainer;

        [SerializeField]
        private GameObject resultRowTemplate;

        [SerializeField]
        private RectTransform watchlistContainer;

        [SerializeField]
        private GameObject watchlistRowTemplate;

        private SearchCreatorsUseCase _searchCreatorsUseCase;
        private ManageWatchlistUseCase _watchlistUseCase;

        private readonly List<GameObject> _activeResultRows = new List<GameObject>();
        private readonly List<GameObject> _activeWatchlistRows = new List<GameObject>();

        private void Awake()
        {
            if (searchInputField == null || searchButton == null || resultsContainer == null ||
                resultRowTemplate == null || watchlistContainer == null || watchlistRowTemplate == null)
            {
                Debug.LogError("[CreatorSearchPanel] Required UI references are not assigned. Run Tools > Yobi > Setup Creator Search UI.");
            }

            if (resultRowTemplate != null)
            {
                resultRowTemplate.SetActive(false);
            }

            if (watchlistRowTemplate != null)
            {
                watchlistRowTemplate.SetActive(false);
            }

            _watchlistUseCase = new ManageWatchlistUseCase();

            try
            {
                var configProvider = new LocalFileChannelConfigProvider();
                var holodexClient = new HolodexApiClient(configProvider.GetHolodexApiKey());
                _searchCreatorsUseCase = new SearchCreatorsUseCase(holodexClient);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreatorSearchPanel] Failed to load Holodex configuration: {ex.Message}");
                SetStatus("Search unavailable: configuration error.");
                SetSearchInteractable(false);
                return;
            }

            if (searchButton != null)
            {
                searchButton.onClick.AddListener(OnSearchButtonClicked);
            }
        }

        private async void OnSearchButtonClicked()
        {
            var query = searchInputField != null ? searchInputField.text : string.Empty;

            SetSearchInteractable(false);
            SetStatus(string.Empty);
            ClearResultRows();

            try
            {
                var results = await _searchCreatorsUseCase.SearchAsync(query, CancellationToken.None);

                if (results.Count == 0)
                {
                    SetStatus("No matching creators found.");
                }
                else
                {
                    foreach (var result in results)
                    {
                        CreateResultRow(result);
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus("Search failed.");
                Debug.LogError($"[Holodex] Creator search failed: {ex.Message}");
            }
            finally
            {
                SetSearchInteractable(true);
            }
        }

        private void SetSearchInteractable(bool interactable)
        {
            if (searchButton != null)
            {
                searchButton.interactable = interactable;
            }
        }

        private void CreateResultRow(CreatorSearchResult result)
        {
            if (resultRowTemplate == null || resultsContainer == null)
            {
                return;
            }

            var row = Instantiate(resultRowTemplate, resultsContainer);
            row.SetActive(true);

            var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
            if (nameText != null)
            {
                nameText.text = $"{result.DisplayName}  ({result.ChannelId})";
            }

            var addButton = row.transform.Find("AddButton")?.GetComponent<Button>();
            if (addButton != null)
            {
                addButton.onClick.RemoveAllListeners();
                addButton.onClick.AddListener(() => OnAddButtonClicked(result));
            }

            _activeResultRows.Add(row);
        }

        private void OnAddButtonClicked(CreatorSearchResult result)
        {
            var addResult = _watchlistUseCase.Add(result.ChannelId, result.DisplayName);
            if (addResult == WatchlistAddResult.AlreadyExists)
            {
                SetStatus("Already added");
                return;
            }

            SetStatus(string.Empty);
            RefreshWatchlistUI();
        }

        private void RefreshWatchlistUI()
        {
            ClearWatchlistRows();

            if (watchlistRowTemplate == null || watchlistContainer == null)
            {
                return;
            }

            foreach (var creator in _watchlistUseCase.GetAll())
            {
                var row = Instantiate(watchlistRowTemplate, watchlistContainer);
                row.SetActive(true);

                var nameText = row.transform.Find("NameText")?.GetComponent<Text>();
                if (nameText != null)
                {
                    nameText.text = creator.DisplayName;
                }

                _activeWatchlistRows.Add(row);
            }
        }

        private void ClearResultRows()
        {
            foreach (var row in _activeResultRows)
            {
                Destroy(row);
            }

            _activeResultRows.Clear();
        }

        private void ClearWatchlistRows()
        {
            foreach (var row in _activeWatchlistRows)
            {
                Destroy(row);
            }

            _activeWatchlistRows.Clear();
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
