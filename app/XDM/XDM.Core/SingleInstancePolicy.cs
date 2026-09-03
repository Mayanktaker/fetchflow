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
        // Chooses the launch outcome: a verified live relay always outranks the mutex
        // hint (the mutex backing file can be wiped by the OS while an instance serves)
        public static SingleInstanceAction Decide(bool ownsMutex, bool argsDelivered, bool mutexRecovered)
        {
            if (argsDelivered)
            {
                return SingleInstanceAction.ForwardAndExit;
            }
            if (ownsMutex || mutexRecovered)
            {
                return SingleInstanceAction.ProceedAsPrimary;
            }
            return SingleInstanceAction.TakeOverAsPrimary;
        }
    }
}
