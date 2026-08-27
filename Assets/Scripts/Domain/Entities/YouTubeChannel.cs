namespace Yobi.Domain.Entities
{
    public sealed class YouTubeChannel
    {
        public string Name { get; }
        public string Handle { get; }
        public string ChannelId { get; }

        public YouTubeChannel(string name, string handle, string channelId)
        {
            Name = name;
            Handle = handle;
            ChannelId = channelId;
        }
    }
}
