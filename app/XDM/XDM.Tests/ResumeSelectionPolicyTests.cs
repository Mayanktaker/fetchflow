// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core;

namespace XDM.Tests
{
    // Batch-resume selection: an item already live or queued must be skipped without
    // blocking the remaining selected stopped downloads (previously a plain `return`)
    [TestClass]
    public class ResumeSelectionPolicyTests
    {
        [TestMethod]
        public void LiveItemIsSkipped()
        {
            Assert.IsTrue(ResumeSelectionPolicy.ShouldSkip(isLive: true, isQueued: false));
        }

        [TestMethod]
        public void QueuedItemIsSkipped()
        {
            Assert.IsTrue(ResumeSelectionPolicy.ShouldSkip(isLive: false, isQueued: true));
        }

        [TestMethod]
        public void StoppedItemIsProcessed()
        {
            Assert.IsFalse(ResumeSelectionPolicy.ShouldSkip(isLive: false, isQueued: false));
        }
    }
}
