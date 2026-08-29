using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Yobi.Infrastructure.Http
{
    internal static class UnityWebRequestAsync
    {
        public static Task<string> SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<string>();
            var operation = request.SendWebRequest();

            var registration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() =>
                {
                    if (!request.isDone)
                    {
                        request.Abort();
                    }
                })
                : default;

            operation.completed += _ =>
            {
                registration.Dispose();

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                    else if (request.result != UnityWebRequest.Result.Success)
                    {
                        var safeUrl = UrlSanitizer.Redact(request.url);
                        tcs.TrySetException(new Exception($"HTTP request failed: {request.error} ({request.responseCode}) - {safeUrl}"));
                    }
                    else
                    {
                        tcs.TrySetResult(request.downloadHandler.text);
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
