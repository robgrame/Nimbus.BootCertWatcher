using System;
using System.Linq;

namespace SecureBootWatcher.Shared.Models
{
    /// <summary>
    /// Physical chassis types as reported by Win32_SystemEnclosure.ChassisTypes.
    /// Based on SMBIOS/DMTF specification.
    /// </summary>
    public enum ChassisType
    {
        /// <summary>Unknown or not specified.</summary>
        Unknown = 0,
        /// <summary>Other</summary>
        Other = 1,
        /// <summary>Desktop</summary>
        Desktop = 3,
        /// <summary>Low Profile Desktop</summary>
        LowProfileDesktop = 4,
        /// <summary>Pizza Box</summary>
        PizzaBox = 5,
        /// <summary>Mini Tower</summary>
        MiniTower = 6,
        /// <summary>Tower</summary>
        Tower = 7,
        /// <summary>Portable (generic portable)</summary>
        Portable = 8,
        /// <summary>Laptop</summary>
        Laptop = 9,
        /// <summary>Notebook</summary>
        Notebook = 10,
        /// <summary>Hand Held</summary>
        HandHeld = 11,
        /// <summary>Docking Station</summary>
        DockingStation = 12,
        /// <summary>All in One</summary>
        AllInOne = 13,
        /// <summary>Sub Notebook</summary>
        SubNotebook = 14,
        /// <summary>Space-Saving</summary>
        SpaceSaving = 15,
        /// <summary>Lunch Box</summary>
        LunchBox = 16,
        /// <summary>Main System Chassis</summary>
        MainSystemChassis = 17,
        /// <summary>Expansion Chassis</summary>
        ExpansionChassis = 18,
        /// <summary>Sub Chassis</summary>
        SubChassis = 19,
        /// <summary>Bus Expansion Chassis</summary>
        BusExpansionChassis = 20,
        /// <summary>Peripheral Chassis</summary>
        PeripheralChassis = 21,
        /// <summary>Storage Chassis</summary>
        StorageChassis = 22,
        /// <summary>Rack Mount Chassis (server)</summary>
        RackMountChassis = 23,
        /// <summary>Sealed-Case PC</summary>
        SealedCasePC = 24,
        /// <summary>Multi-system chassis</summary>
        MultiSystemChassis = 25,
        /// <summary>Compact PCI</summary>
        CompactPCI = 26,
        /// <summary>Advanced TCA</summary>
        AdvancedTCA = 27,
        /// <summary>Blade (server)</summary>
        Blade = 28,
        /// <summary>Blade Enclosure</summary>
        BladeEnclosure = 29,
        /// <summary>Tablet</summary>
        Tablet = 30,
        /// <summary>Convertible (2-in-1 laptop/tablet)</summary>
        Convertible = 31,
        /// <summary>Detachable (detachable tablet/laptop)</summary>
        Detachable = 32
    }

    /// <summary>
    /// Extension methods for ChassisType analysis.
    /// </summary>
    public static class ChassisTypeExtensions
    {
        /// <summary>
        /// Determines if the chassis type represents a portable device (laptop, notebook, tablet, etc.).
        /// </summary>
        public static bool IsPortable(this ChassisType chassisType)
        {
            return chassisType switch
            {
                ChassisType.Laptop => true,
                ChassisType.Notebook => true,
                ChassisType.Portable => true,
                ChassisType.HandHeld => true,
                ChassisType.SubNotebook => true,
                ChassisType.Tablet => true,
                ChassisType.Convertible => true,
                ChassisType.Detachable => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if the chassis type represents a server form factor.
        /// </summary>
        public static bool IsServer(this ChassisType chassisType)
        {
            return chassisType switch
            {
                ChassisType.RackMountChassis => true,
                ChassisType.Blade => true,
                ChassisType.BladeEnclosure => true,
                ChassisType.MainSystemChassis => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if the chassis type represents a desktop/tower form factor.
        /// </summary>
        public static bool IsDesktop(this ChassisType chassisType)
        {
            return chassisType switch
            {
                ChassisType.Desktop => true,
                ChassisType.LowProfileDesktop => true,
                ChassisType.MiniTower => true,
                ChassisType.Tower => true,
                ChassisType.PizzaBox => true,
                ChassisType.AllInOne => true,
                ChassisType.SpaceSaving => true,
                ChassisType.LunchBox => true,
                ChassisType.SealedCasePC => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets a user-friendly display name for the chassis type.
        /// </summary>
        public static string GetDisplayName(this ChassisType chassisType)
        {
            return chassisType switch
            {
                ChassisType.Desktop => "Desktop",
                ChassisType.Laptop => "Laptop",
                ChassisType.Notebook => "Notebook",
                ChassisType.Tower => "Tower",
                ChassisType.MiniTower => "Mini Tower",
                ChassisType.RackMountChassis => "Rack Server",
                ChassisType.Blade => "Blade Server",
                ChassisType.Tablet => "Tablet",
                ChassisType.Convertible => "2-in-1",
                ChassisType.AllInOne => "All-in-One",
                _ => chassisType.ToString()
            };
        }

        /// <summary>
        /// Gets a Font Awesome icon class for the chassis type.
        /// </summary>
        public static string GetIconClass(this ChassisType chassisType)
        {
            if (chassisType.IsPortable())
            {
                return "fa-laptop";
            }
            else if (chassisType.IsServer())
            {
                return "fa-server";
            }
            else if (chassisType.IsDesktop())
            {
                return "fa-desktop";
            }
            else
            {
                return "fa-question-circle";
            }
        }

        /// <summary>
        /// Gets the primary chassis type from an array of chassis types.
        /// Prioritizes portable > server > desktop > other.
        /// </summary>
        public static ChassisType GetPrimaryChassisType(int[]? chassisTypes)
        {
            if (chassisTypes == null || chassisTypes.Length == 0)
            {
                return ChassisType.Unknown;
            }

            // Convert to enum values
            var types = chassisTypes
                .Where(ct => Enum.IsDefined(typeof(ChassisType), ct))
                .Select(ct => (ChassisType)ct)
                .ToArray();

            if (types.Length == 0)
            {
                return ChassisType.Unknown;
            }

            // Priority order: Portable > Server > Desktop > Other
            var portable = types.FirstOrDefault(t => t.IsPortable());
            if (portable != ChassisType.Unknown)
            {
                return portable;
            }

            var server = types.FirstOrDefault(t => t.IsServer());
            if (server != ChassisType.Unknown)
            {
                return server;
            }

            var desktop = types.FirstOrDefault(t => t.IsDesktop());
            if (desktop != ChassisType.Unknown)
            {
                return desktop;
            }

            // Return first type if no specific category matches
            return types.First();
        }
    }
}
