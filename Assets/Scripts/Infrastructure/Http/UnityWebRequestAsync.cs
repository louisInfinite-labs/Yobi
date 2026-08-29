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
            if (cancellationToken.IsCancellationRequested)
            {
                // Don't dispatch the request at all for a token that's already canceled -
                // starting it first and aborting a moment later would still consume API quota
                // for a call the caller no longer wants.
                request.Dispose();
                return Task.FromCanceled<string>(cancellationToken);
            }

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
