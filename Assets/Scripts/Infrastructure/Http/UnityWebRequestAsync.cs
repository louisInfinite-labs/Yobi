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

            // Register before dispatching: if the token is (or becomes) canceled anywhere
            // between here and SendWebRequest below, the cancellation check right after
            // registration catches it and skips dispatch entirely, instead of leaving a gap
            // where cancellation during that window fails to abort anything.
            var registration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() =>
                {
                    if (!request.isDone)
                    {
                        request.Abort();
                    }
                })
                : default;

            if (cancellationToken.IsCancellationRequested)
            {
                registration.Dispose();
                request.Dispose();
                tcs.TrySetCanceled(cancellationToken);
                return tcs.Task;
            }

            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = request.SendWebRequest();
            }
            catch
            {
                registration.Dispose();
                throw;
            }

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
