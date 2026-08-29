using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Yobi.Infrastructure.Http
{
    internal static class UnityWebRequestAsync
    {
        public static Task<string> SendAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<string>();
            var operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        var safeUrl = UrlSanitizer.Redact(request.url);
                        tcs.SetException(new Exception($"HTTP request failed: {request.error} ({request.responseCode}) - {safeUrl}"));
                    }
                    else
                    {
                        tcs.SetResult(request.downloadHandler.text);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };

            return tcs.Task;
        }
    }
}
