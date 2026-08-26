using System.Collections.Generic;
using Yobi.Domain.Entities;

namespace Yobi.Domain.Interfaces
{
    public interface IChannelConfigProvider
    {
        IReadOnlyList<YouTubeChannel> GetChannels();
    }
}
