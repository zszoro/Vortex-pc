namespace VORTEX.Core
{
    public class UserProfile
    {
        public string Name { get; set; } = string.Empty;
        public string? Preferences { get; set; }
        public string? Treatment { get; set; }
        public bool IsSetupComplete { get; set; }
    }
}
