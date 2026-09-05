using System.Threading;
using System.Threading.Tasks;

namespace Yobi.Domain.Interfaces
{
    // A port over "ask a local LLM a prompt, get text back" (e.g. Ollama's HTTP API).
    // Deliberately takes/returns plain text rather than anything model-specific, so swapping
    // the local runtime (Ollama / llama.cpp / MLX, per the roadmap) only touches the
    // Infrastructure implementation.
    public interface ILocalAiClient
    {
        Task<string> AskAsync(string prompt, CancellationToken cancellationToken);
    }
}
