// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core;

namespace XDM.Tests
{
    // Decision table for single-instance arbitration — TakeOverAsPrimary is the recovery
    // path for a defunct holder (mutex held, IPC dead) that previously locked users out
    [TestClass]
    public class SingleInstancePolicyTests
    {
        [TestMethod]
        public void MutexOwnerProceedsAsPrimary()
        {
            Assert.AreEqual(SingleInstanceAction.ProceedAsPrimary,
                SingleInstancePolicy.Decide(ownsMutex: true, argsDelivered: false, mutexRecovered: false));
        }

        [TestMethod]
        public void DeliveredArgsForwardAndExit()
        {
            Assert.AreEqual(SingleInstanceAction.ForwardAndExit,
                SingleInstancePolicy.Decide(ownsMutex: false, argsDelivered: true, mutexRecovered: false));
        }

        [TestMethod]
        public void DefunctHolderTriggersTakeover()
        {
            Assert.AreEqual(SingleInstanceAction.TakeOverAsPrimary,
                SingleInstancePolicy.Decide(ownsMutex: false, argsDelivered: false, mutexRecovered: false));
        }

        [TestMethod]
        public void RecoveredMutexProceedsAsPrimary()
        {
            Assert.AreEqual(SingleInstanceAction.ProceedAsPrimary,
                SingleInstancePolicy.Decide(ownsMutex: false, argsDelivered: false, mutexRecovered: true));
        }
    }
}
