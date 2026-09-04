using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Http;

namespace Yobi.Infrastructure.Ai
{
    // Talks to Ollama's local HTTP API (non-streaming /api/generate). Assumes `ollama serve`
    // is already running on this machine with the model already pulled - Yobi does not manage
    // the Ollama process or model downloads itself (see roadmap: that's a first-run/setup
    // concern, not something this client should silently trigger over the network).
    public sealed class OllamaHttpClient : ILocalAiClient
    {
        private const string DefaultBaseUrl = "http://localhost:11434";
        private const string DefaultModel = "llama3.1:8b";

        // UnityWebRequest.timeout defaults to 0 (never), and SendAsync only aborts on explicit
        // cancellation - without a deadline here, a hung/non-responsive Ollama process leaves
        // the caller (AiQueryPanelBehaviour's askButton) stuck disabled indefinitely. Generous
        // enough to cover a cold model load, which alone measured ~16s in local testing.
        private const int RequestTimeoutSeconds = 120;

        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaHttpClient(string model = DefaultModel, string baseUrl = DefaultBaseUrl)
        {
            _model = model;
            _baseUrl = baseUrl;
        }

        public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestDto = new GenerateRequestDto { model = _model, prompt = prompt, stream = false };
            var bodyBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestDto));

            using var request = new UnityWebRequest($"{_baseUrl}/api/generate", UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = RequestTimeoutSeconds;

            var responseJson = await UnityWebRequestAsync.SendAsync(request, cancellationToken);
            var responseDto = JsonUtility.FromJson<GenerateResponseDto>(responseJson);
            return responseDto?.response ?? string.Empty;
        }

        [Serializable]
        private sealed class GenerateRequestDto
        {
            public string model;
            public string prompt;
            public bool stream;
        }

        [Serializable]
        private sealed class GenerateResponseDto
        {
            public string response;
        }
    }
}
