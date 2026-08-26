using System.Threading;
using UnityEngine;
using Yobi.Application.UseCases;
using Yobi.Infrastructure.Config;
using Yobi.Infrastructure.YouTube;

namespace Yobi.Presentation
{
    public sealed class YouTubeLivestreamCheckerBehaviour : MonoBehaviour
    {
        [SerializeField]
        private bool runOnStart = false;
        private async void Start(){
        if (!runOnStart)

        {
            Debug.Log("[YouTube] Auto check is disabled.");
            return;
        }

            await RunCheckAsync();

        }
        private async System.Threading.Tasks.Task RunCheckAsync()
        {
            try
            {
                var configProvider = new LocalFileChannelConfigProvider();
                var repository = new YouTubeDataApiLivestreamRepository(configProvider.GetApiKey());
                var useCase = new CheckUpcomingLivestreamsUseCase(configProvider, repository);

                var results = await useCase.ExecuteAsync(CancellationToken.None);

                foreach (var result in results)
                {
                    if (result.Livestreams.Count == 0)
                    {
                        Debug.Log($"[YouTube]\nChannel: {result.Channel.Name}\nNo upcoming livestreams within the next 24 hours.");
                        continue;
                    }

                    foreach (var livestream in result.Livestreams)
                    {
                        var localStart = livestream.ScheduledStartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                        Debug.Log($"[YouTube]\nChannel: {result.Channel.Name}\nTitle: {livestream.Title}\nScheduled Start: {localStart}\nVideo ID: {livestream.VideoId}\nURL: {livestream.Url}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[YouTube] Failed to check upcoming livestreams: {ex.Message}");
            }
        }
    }
}
