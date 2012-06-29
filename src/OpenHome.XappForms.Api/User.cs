namespace OpenHome.XappForms
{
    public class User
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string IconUrl { get; private set; }
        public User(string aId, string aDisplayName, string aIconUrl)
        {
            Id = aId;
            DisplayName = aDisplayName;
            IconUrl = aIconUrl;
        }
    }
}