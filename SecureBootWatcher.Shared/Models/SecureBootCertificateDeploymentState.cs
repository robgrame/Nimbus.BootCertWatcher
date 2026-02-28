using System;
using System.Collections.Generic;
using System.Linq;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Provides a structured view of the Secure Boot certificate deployment process
    /// based on the AvailableUpdates registry value and the documented bit processing order.
    /// </summary>
    public class SecureBootCertificateDeploymentState
    {
        /// <summary>
        /// Default initial value for the AvailableUpdates key when all steps are pending.
        /// </summary>
        public const uint DefaultInitialValue = 0x5944;

        public SecureBootCertificateDeploymentState(uint? availableUpdates)
        {
            AvailableUpdates = availableUpdates;
            Flags = availableUpdates.HasValue
                ? (SecureBootUpdateFlags)availableUpdates.Value
                : SecureBootUpdateFlags.None;

            Steps = SecureBootUpdateFlagsExtensions.GetUpdateSteps(availableUpdates);
            PendingSteps = Steps.Where(step => !step.IsCompleted)
                .OrderBy(step => step.Order)
                .ToArray();
            CompletedSteps = Steps.Where(step => step.IsCompleted)
                .OrderBy(step => step.Order)
                .ToArray();

            CompletionPercentage = SecureBootUpdateFlagsExtensions.GetCompletionPercentage(availableUpdates);
            ProgressionState = SecureBootUpdateFlagsExtensions.GetProgressionState(availableUpdates);
            NextPendingStep = PendingSteps.Count > 0 ? PendingSteps[0] : null;
            NextExpectedAvailableUpdatesValue = availableUpdates.HasValue
                ? CalculateNextValue(Flags)
                : (uint?)null;
        }

        /// <summary>
        /// Current raw AvailableUpdates value.
        /// </summary>
        public uint? AvailableUpdates { get; }

        /// <summary>
        /// Parsed flags for the current AvailableUpdates value.
        /// </summary>
        public SecureBootUpdateFlags Flags { get; }

        /// <summary>
        /// All deployment steps with completion state.
        /// </summary>
        public IReadOnlyList<SecureBootUpdateStepInfo> Steps { get; }

        /// <summary>
        /// Deployment steps that have not run yet (ordered by processing order).
        /// </summary>
        public IReadOnlyList<SecureBootUpdateStepInfo> PendingSteps { get; }

        /// <summary>
        /// Deployment steps already completed (ordered by processing order).
        /// </summary>
        public IReadOnlyList<SecureBootUpdateStepInfo> CompletedSteps { get; }

        /// <summary>
        /// The next step the scheduled task will process, if any.
        /// </summary>
        public SecureBootUpdateStepInfo? NextPendingStep { get; }

        /// <summary>
        /// Percentage (0-100) of deployment steps completed.
        /// </summary>
        public int CompletionPercentage { get; }

        /// <summary>
        /// Human-readable description of the current progression state.
        /// </summary>
        public string ProgressionState { get; }

        /// <summary>
        /// Indicates whether the conditional Microsoft CA behavior (0x4000) is enabled.
        /// </summary>
        public bool IsConditionalMicrosoftCaFlow => Flags.HasFlag(SecureBootUpdateFlags.ConditionalMicrosoftCAs);

        /// <summary>
        /// Indicates whether all deployment bits (except the conditional modifier) are cleared.
        /// </summary>
        public bool IsComplete => CompletionPercentage == 100;

        /// <summary>
        /// Expected next value of AvailableUpdates after the scheduled task processes
        /// the next pending bit. Returns null if no further progression is expected.
        /// </summary>
        public uint? NextExpectedAvailableUpdatesValue { get; }

        /// <summary>
        /// Generates the expected progression path starting from the provided value (defaults to 0x5944).
        /// </summary>
        public static IReadOnlyList<uint> GetExpectedProgression(uint startValue = DefaultInitialValue)
        {
            var progression = new List<uint>();
            var flags = (SecureBootUpdateFlags)startValue;

            progression.Add(startValue);

            var next = CalculateNextValue(flags);
            while (next.HasValue)
            {
                progression.Add(next.Value);
                flags = (SecureBootUpdateFlags)next.Value;
                next = CalculateNextValue(flags);
            }

            return progression;
        }

        private static uint? CalculateNextValue(SecureBootUpdateFlags flags)
        {
            // If only the conditional bit (0x4000) or nothing remains, no further progression occurs.
            if (flags == SecureBootUpdateFlags.None || flags == SecureBootUpdateFlags.ConditionalMicrosoftCAs)
            {
                return null;
            }

            var orderedFlags = new[]
            {
                SecureBootUpdateFlags.WindowsUefiCA2023,
                SecureBootUpdateFlags.MicrosoftUefiCA2023,
                SecureBootUpdateFlags.MicrosoftOptionRomCA2023,
                SecureBootUpdateFlags.MicrosoftKEK2023,
                SecureBootUpdateFlags.WindowsBootManager2023
            };

            foreach (var flag in orderedFlags)
            {
                if (flags.HasFlag(flag))
                {
                    var remaining = flags & ~flag;
                    return (uint)remaining;
                }
            }

            return null;
        }
    }
}
