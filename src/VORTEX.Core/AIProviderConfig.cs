namespace VORTEX.Core
{
    public class AIProviderConfig
    {
        public string ProviderName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }
}
