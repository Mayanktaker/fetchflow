// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;

namespace XDM.Core
{
    // Outcome of the single-instance arbitration for a launching process
    public enum SingleInstanceAction
    {
        // Mutex owned: normal primary startup
        ProceedAsPrimary,
        // A healthy running instance accepted our arguments
        ForwardAndExit,
        // Mutex is held but IPC is dead: the holder is defunct, recover by becoming primary
        TakeOverAsPrimary
    }

    // Pure decision table for single-instance arbitration (dependency-free for testability)
    public static class SingleInstancePolicy
    {
        // Chooses the launch outcome from mutex ownership, arg delivery and recovery state
        public static SingleInstanceAction Decide(bool ownsMutex, bool argsDelivered, bool mutexRecovered)
        {
            if (ownsMutex || mutexRecovered)
            {
                return SingleInstanceAction.ProceedAsPrimary;
            }
            return argsDelivered ? SingleInstanceAction.ForwardAndExit : SingleInstanceAction.TakeOverAsPrimary;
        }
    }
}
