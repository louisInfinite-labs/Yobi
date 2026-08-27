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
                        tcs.SetException(new Exception($"HTTP request failed: {request.error} ({request.responseCode}) - {request.url}"));
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
