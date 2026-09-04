// © Mayanktaker Computers & Web Development | https://mayanktaker.com
namespace XDM.Core
{
    // Pure resume-selection decision used to keep batch resume processing independent
    public static class ResumeSelectionPolicy
    {
        // Returns true only for an item already being processed elsewhere
        public static bool ShouldSkip(bool isLive, bool isQueued)
        {
            return isLive || isQueued;
        }
    }
}
