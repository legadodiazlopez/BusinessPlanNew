namespace CrownBuilders.Diagnostics
{
    public class WatchdogOptions
    {
        public string OpenAiApiKey { get; set; } = "";
        public string ProjectRootPath { get; set; } = "";
        public long MemoryThresholdMb { get; set; } = 2048;
        public double FreezeThresholdMs { get; set; } = 3000;
    }
}
