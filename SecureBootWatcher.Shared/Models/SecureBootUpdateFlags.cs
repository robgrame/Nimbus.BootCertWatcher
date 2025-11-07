using System;
using System.Collections.Generic;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Represents the bit flags used in the Secure Boot AvailableUpdates registry key.
    /// These flags control the deployment of Secure Boot certificates and boot managers.
    /// </summary>
    [Flags]
    public enum SecureBootUpdateFlags : uint
    {
        /// <summary>
        /// No updates available.
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// Apply the Windows UEFI CA 2023 certificate to the Secure Boot DB.
        /// This allows Windows to trust boot managers signed by this certificate.
        /// Processing order: 1
        /// </summary>
        WindowsUefiCA2023 = 0x0040,

        /// <summary>
        /// Apply the Microsoft UEFI CA 2023 to the DB.
        /// If ConditionalMicrosoftCAs is also set, only applies if Microsoft Corporation UEFI CA 2011 is already in the DB.
        /// Processing order: 2
        /// </summary>
        MicrosoftUefiCA2023 = 0x0800,

        /// <summary>
        /// Apply the Microsoft Option ROM CA 2023 to the DB.
        /// If ConditionalMicrosoftCAs is also set, only applies if Microsoft Corporation UEFI CA 2011 is already in the DB.
        /// Processing order: 3
        /// </summary>
        MicrosoftOptionRomCA2023 = 0x1000,

        /// <summary>
        /// Modifies the behavior of MicrosoftUefiCA2023 and MicrosoftOptionRomCA2023 flags.
        /// When set, these certificates are only applied if Microsoft Corporation UEFI CA 2011 is already in the DB.
        /// This ensures the device's security profile remains consistent.
        /// Processing order: Used with 2 and 3
        /// </summary>
        ConditionalMicrosoftCAs = 0x4000,

        /// <summary>
        /// Look for a Key Exchange Key (KEK) signed by the device's Platform Key (PK).
        /// The PK is managed by the OEM.
        /// Processing order: 4
        /// </summary>
        MicrosoftKEK2023 = 0x0004,

        /// <summary>
        /// Apply the boot manager signed by Windows UEFI CA 2023 to the boot partition.
        /// This replaces the Microsoft Windows Production PCA 2011 signed boot manager.
        /// Processing order: 5
        /// </summary>
        WindowsBootManager2023 = 0x0100
    }

    /// <summary>
    /// Provides interpretation of Secure Boot update flags.
    /// </summary>
    public static class SecureBootUpdateFlagsExtensions
    {
        /// <summary>
        /// Gets a human-readable description of the active update flags.
        /// </summary>
        public static IReadOnlyList<string> GetActiveFlags(uint? availableUpdates)
        {
            var flags = new List<string>();

            if (!availableUpdates.HasValue || availableUpdates.Value == 0)
            {
                return flags;
            }

            var value = (SecureBootUpdateFlags)availableUpdates.Value;

            if (value.HasFlag(SecureBootUpdateFlags.WindowsUefiCA2023))
            {
                flags.Add("Windows UEFI CA 2023 (0x0040) - Pending");
            }

            if (value.HasFlag(SecureBootUpdateFlags.MicrosoftUefiCA2023))
            {
                var conditional = value.HasFlag(SecureBootUpdateFlags.ConditionalMicrosoftCAs) ? " (conditional)" : "";
                flags.Add($"Microsoft UEFI CA 2023 (0x0800){conditional} - Pending");
            }

            if (value.HasFlag(SecureBootUpdateFlags.MicrosoftOptionRomCA2023))
            {
                var conditional = value.HasFlag(SecureBootUpdateFlags.ConditionalMicrosoftCAs) ? " (conditional)" : "";
                flags.Add($"Microsoft Option ROM CA 2023 (0x1000){conditional} - Pending");
            }

            if (value.HasFlag(SecureBootUpdateFlags.MicrosoftKEK2023))
            {
                flags.Add("Microsoft KEK 2023 (0x0004) - Pending");
            }

            if (value.HasFlag(SecureBootUpdateFlags.WindowsBootManager2023))
            {
                flags.Add("Windows Boot Manager 2023 (0x0100) - Pending");
            }

            return flags;
        }

        /// <summary>
        /// Gets the processing order for the given flag.
        /// </summary>
        public static int GetProcessingOrder(SecureBootUpdateFlags flag)
        {
            return flag switch
            {
                SecureBootUpdateFlags.WindowsUefiCA2023 => 1,
                SecureBootUpdateFlags.MicrosoftUefiCA2023 => 2,
                SecureBootUpdateFlags.MicrosoftOptionRomCA2023 => 3,
                SecureBootUpdateFlags.MicrosoftKEK2023 => 4,
                SecureBootUpdateFlags.WindowsBootManager2023 => 5,
                _ => 0
            };
        }

        /// <summary>
        /// Determines the expected progression state based on the current AvailableUpdates value.
        /// </summary>
        public static string GetProgressionState(uint? availableUpdates)
        {
            if (!availableUpdates.HasValue)
            {
                return "Unknown - No AvailableUpdates value";
            }

            var value = availableUpdates.Value;

            // Expected progression: 0x5944 ? 0x5904 ? 0x5104 ? 0x4104 ? 0x4100 ? 0x4000
            return value switch
            {
                0x5944 => "Initial state - All updates pending",
                0x5904 => "Windows UEFI CA 2023 applied",
                0x5104 => "Microsoft UEFI CA 2023 applied",
                0x4104 => "Microsoft Option ROM CA 2023 applied",
                0x4100 => "Microsoft KEK 2023 applied",
                0x4000 => "Windows Boot Manager 2023 applied - Deployment complete (conditional flag remains)",
                0x0000 => "All updates completed",
                _ => $"Custom state (0x{value:X4})"
            };
        }

        /// <summary>
        /// Calculates the deployment completion percentage.
        /// </summary>
        public static int GetCompletionPercentage(uint? availableUpdates)
        {
            if (!availableUpdates.HasValue)
            {
                return 0;
            }

            var value = (SecureBootUpdateFlags)availableUpdates.Value;
            var totalSteps = 5; // Total number of deployment steps
            var completedSteps = 0;

            // Check which flags are NOT set (meaning they were completed)
            if (!value.HasFlag(SecureBootUpdateFlags.WindowsUefiCA2023))
                completedSteps++;
            
            if (!value.HasFlag(SecureBootUpdateFlags.MicrosoftUefiCA2023))
                completedSteps++;
            
            if (!value.HasFlag(SecureBootUpdateFlags.MicrosoftOptionRomCA2023))
                completedSteps++;
            
            if (!value.HasFlag(SecureBootUpdateFlags.MicrosoftKEK2023))
                completedSteps++;
            
            if (!value.HasFlag(SecureBootUpdateFlags.WindowsBootManager2023))
                completedSteps++;

            return (completedSteps * 100) / totalSteps;
        }

        /// <summary>
        /// Gets detailed information about each deployment step.
        /// </summary>
        public static IReadOnlyList<SecureBootUpdateStepInfo> GetUpdateSteps(uint? availableUpdates)
        {
            var steps = new List<SecureBootUpdateStepInfo>();

            if (!availableUpdates.HasValue)
            {
                return steps;
            }

            var value = (SecureBootUpdateFlags)availableUpdates.Value;

            steps.Add(new SecureBootUpdateStepInfo(
                1,
                "Windows UEFI CA 2023",
                "0x0040",
                "Add Windows UEFI CA 2023 certificate to Secure Boot DB",
                !value.HasFlag(SecureBootUpdateFlags.WindowsUefiCA2023)));

            steps.Add(new SecureBootUpdateStepInfo(
                2,
                "Microsoft UEFI CA 2023",
                "0x0800",
                value.HasFlag(SecureBootUpdateFlags.ConditionalMicrosoftCAs)
                    ? "Apply Microsoft UEFI CA 2023 if Microsoft Corporation UEFI CA 2011 exists in DB"
                    : "Apply Microsoft UEFI CA 2023 to DB",
                !value.HasFlag(SecureBootUpdateFlags.MicrosoftUefiCA2023)));

            steps.Add(new SecureBootUpdateStepInfo(
                3,
                "Microsoft Option ROM CA 2023",
                "0x1000",
                value.HasFlag(SecureBootUpdateFlags.ConditionalMicrosoftCAs)
                    ? "Apply Microsoft Option ROM CA 2023 if Microsoft Corporation UEFI CA 2011 exists in DB"
                    : "Apply Microsoft Option ROM CA 2023 to DB",
                !value.HasFlag(SecureBootUpdateFlags.MicrosoftOptionRomCA2023)));

            steps.Add(new SecureBootUpdateStepInfo(
                4,
                "Microsoft KEK 2023",
                "0x0004",
                "Apply Key Exchange Key signed by device Platform Key",
                !value.HasFlag(SecureBootUpdateFlags.MicrosoftKEK2023)));

            steps.Add(new SecureBootUpdateStepInfo(
                5,
                "Windows Boot Manager 2023",
                "0x0100",
                "Apply boot manager signed by Windows UEFI CA 2023",
                !value.HasFlag(SecureBootUpdateFlags.WindowsBootManager2023)));

            return steps;
        }
    }

    /// <summary>
    /// Represents information about a Secure Boot update deployment step.
    /// </summary>
    public sealed class SecureBootUpdateStepInfo
    {
        public SecureBootUpdateStepInfo(
            int order,
            string name,
            string bitFlag,
            string description,
            bool isCompleted)
        {
            Order = order;
            Name = name;
            BitFlag = bitFlag;
            Description = description;
            IsCompleted = isCompleted;
        }

        /// <summary>
        /// Processing order of this step.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Name of the update step.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Hexadecimal bit flag value.
        /// </summary>
        public string BitFlag { get; }

        /// <summary>
        /// Description of what this step does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Whether this step has been completed.
        /// </summary>
        public bool IsCompleted { get; }
    }
}
