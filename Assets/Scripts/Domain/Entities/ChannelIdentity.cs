namespace Yobi.Domain.Entities
{
    public sealed class ChannelIdentity
    {
        public string ChannelId { get; }
        public string Name { get; }

        public ChannelIdentity(string channelId, string name)
        {
            ChannelId = channelId;
            Name = name;
        }
    }
}
