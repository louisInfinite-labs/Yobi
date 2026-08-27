using System;
using System.Collections.Generic;
using Yobi.Application.Models;
using Yobi.Domain.Entities;

namespace Yobi.Application.UseCases
{
    public sealed class EvaluateLivestreamRemindersUseCase
    {
        private readonly Dictionary<string, LivestreamReminderState> _states = new Dictionary<string, LivestreamReminderState>();

        public IReadOnlyList<LivestreamReminderEvent> Evaluate(
            IReadOnlyList<ChannelLivestreamResult> channelResults,
            IReadOnlyList<ReminderThreshold> thresholds,
            DateTime nowUtc)
        {
            var dueEvents = new List<LivestreamReminderEvent>();

            foreach (var channelResult in channelResults)
            {
                foreach (var livestream in channelResult.Livestreams)
                {
                    if (!_states.TryGetValue(livestream.VideoId, out var state))
                    {
                        state = new LivestreamReminderState(livestream.VideoId);
                        _states[livestream.VideoId] = state;
                    }

                    var dueThresholds = state.Evaluate(nowUtc, livestream.ScheduledStartUtc, thresholds);
                    foreach (var threshold in dueThresholds)
                    {
                        dueEvents.Add(new LivestreamReminderEvent(
                            channelResult.Channel.Name,
                            livestream.Title,
                            livestream.VideoId,
                            livestream.ScheduledStartUtc,
                            threshold));
                    }
                }
            }

            return dueEvents;
        }
    }
}
