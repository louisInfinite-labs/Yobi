namespace Yobi.Domain.Entities
{
    public sealed class CreatorSearchResult
    {
        public string ChannelId { get; }
        public string DisplayName { get; }

        public CreatorSearchResult(string channelId, string displayName)
        {
            ChannelId = channelId;
            DisplayName = displayName;
        }
    }
}
