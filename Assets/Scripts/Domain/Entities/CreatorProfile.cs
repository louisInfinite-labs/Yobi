using System.Collections.Generic;

namespace Yobi.Domain.Entities
{
    public sealed class CreatorProfile
    {
        public string Id { get; }
        public IReadOnlyList<string> Names { get; }
        public string Org { get; }
        public IReadOnlyList<string> Games { get; }

        public CreatorProfile(string id, IReadOnlyList<string> names, string org, IReadOnlyList<string> games)
        {
            Id = id;
            Names = names;
            Org = org;
            Games = games;
        }
    }
}
