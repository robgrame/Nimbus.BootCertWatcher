using System;
using System.Linq;
using System.Management;
using Microsoft.Extensions.Logging;
using SecureBootWatcher.Shared.Models;

namespace SecureBootWatcher.Client.Services
{
    /// <summary>
    /// Helper class for detecting virtualization and enriching DeviceIdentity with hardware information.
    /// </summary>
    internal static class DeviceIdentityEnricher
    {
        /// <summary>
        /// Enriches DeviceIdentity with OS information from WMI.
        /// </summary>
        public static void EnrichWithOSInfo(DeviceIdentity identity, ILogger logger)
        {
            try
            {
                using var osSearcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber, ProductType FROM Win32_OperatingSystem");
                using var osCollection = osSearcher.Get();
                
                foreach (ManagementObject os in osCollection)
                {
                    try
                    {
                        identity.OperatingSystem = os["Caption"]?.ToString();
                        identity.OSVersion = os["Version"]?.ToString();
                        identity.OSBuildNumber = os["BuildNumber"]?.ToString();
                        
                        var productType = os["ProductType"];
                        if (productType != null)
                        {
                            identity.OSProductType = Convert.ToInt32(productType);
                        }
                    }
                    catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
                    {
                        logger.LogDebug("Win32_OperatingSystem property not found: {Message}", ex.Message);
                    }
                    finally
                    {
                        os?.Dispose();
                    }
                    break; // Only process first result
                }
            }
            catch (ManagementException ex)
            {
                logger.LogDebug(ex, "Failed to query Win32_OperatingSystem. OS information will be unavailable.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unexpected error querying Win32_OperatingSystem.");
            }
        }

        /// <summary>
        /// Enriches DeviceIdentity with chassis information from WMI.
        /// </summary>
        public static void EnrichWithChassisInfo(DeviceIdentity identity, ILogger logger)
        {
            try
            {
                using var chassisSearcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
                using var chassisCollection = chassisSearcher.Get();
                
                foreach (ManagementObject chassis in chassisCollection)
                {
                    try
                    {
                        var chassisTypesObj = chassis["ChassisTypes"];
                        if (chassisTypesObj is ushort[] chassisTypesArray)
                        {
                            identity.ChassisTypes = chassisTypesArray.Select(ct => (int)ct).ToArray();
                        }
                        else if (chassisTypesObj is int[] chassisTypesIntArray)
                        {
                            identity.ChassisTypes = chassisTypesIntArray;
                        }
                    }
                    catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
                    {
                        logger.LogDebug("Win32_SystemEnclosure property not found: {Message}", ex.Message);
                    }
                    finally
                    {
                        chassis?.Dispose();
                    }
                    break; // Only process first result
                }
            }
            catch (ManagementException ex)
            {
                logger.LogDebug(ex, "Failed to query Win32_SystemEnclosure. Chassis information will be unavailable.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unexpected error querying Win32_SystemEnclosure.");
            }
        }

        /// <summary>
        /// Detects if the device is a virtual machine and determines the hypervisor platform.
        /// Uses multiple detection methods for accuracy.
        /// </summary>
        public static void DetectVirtualMachine(DeviceIdentity identity, ILogger logger)
        {
            try
            {
                // Method 1: Check Model and Manufacturer (already collected by TryPopulateHardwareInfo)
                var model = identity.Model?.ToLowerInvariant() ?? "";
                var manufacturer = identity.Manufacturer?.ToLowerInvariant() ?? "";

                // Common VM indicators in Model/Manufacturer
                if (model.Contains("virtual") ||
                    model.Contains("vmware") ||
                    model.Contains("virtualbox") ||
                    model.Contains("kvm") ||
                    model.Contains("qemu") ||
                    model.Contains("xen") ||
                    manufacturer.Contains("vmware") ||
                    (manufacturer.Contains("microsoft corporation") && model.Contains("virtual")) ||
                    manufacturer.Contains("qemu") ||
                    manufacturer.Contains("xen"))
                {
                    identity.IsVirtualMachine = true;
                    identity.VirtualizationPlatform = DetermineVMPlatform(model, manufacturer);
                    return; // VM detected, no need for further checks
                }

                // Method 2: Check Win32_BIOS for VM-specific indicators
                if (CheckBIOSForVM(identity, logger))
                {
                    return; // VM detected
                }

                // Method 3: Check Win32_BaseBoard for hypervisor indicators
                if (CheckBaseBoardForVM(identity, logger))
                {
                    return; // VM detected
                }

                // If we reach here, it's likely physical hardware
                identity.IsVirtualMachine = false;
                identity.VirtualizationPlatform = null;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to detect virtual machine status");
                // Leave as null if detection fails
            }
        }

        private static string DetermineVMPlatform(string model, string manufacturer)
        {
            if (model.Contains("vmware") || manufacturer.Contains("vmware"))
            {
                return "VMware";
            }
            else if (model.Contains("virtualbox"))
            {
                return "VirtualBox";
            }
            else if (model.Contains("virtual") && manufacturer.Contains("microsoft"))
            {
                return "Hyper-V";
            }
            else if (model.Contains("kvm") || model.Contains("qemu"))
            {
                return "KVM/QEMU";
            }
            else if (model.Contains("xen") || manufacturer.Contains("xen"))
            {
                return "Xen";
            }
            else
            {
                return "Unknown VM";
            }
        }

        private static bool CheckBIOSForVM(DeviceIdentity identity, ILogger logger)
        {
            try
            {
                using var biosSearcher = new ManagementObjectSearcher("SELECT SerialNumber, Manufacturer FROM Win32_BIOS");
                using var biosCollection = biosSearcher.Get();
                
                foreach (ManagementObject bios in biosCollection)
                {
                    try
                    {
                        var serialNumber = bios["SerialNumber"]?.ToString()?.ToLowerInvariant() ?? "";
                        var biosManufacturer = bios["Manufacturer"]?.ToString()?.ToLowerInvariant() ?? "";

                        // VM-specific BIOS indicators
                        if (serialNumber.Contains("vmware") ||
                            serialNumber.Contains("virtual") ||
                            biosManufacturer.Contains("vmware") ||
                            biosManufacturer.Contains("innotek") || // VirtualBox
                            biosManufacturer.Contains("qemu") ||
                            biosManufacturer.Contains("xen"))
                        {
                            identity.IsVirtualMachine = true;
                            
                            if (serialNumber.Contains("vmware") || biosManufacturer.Contains("vmware"))
                            {
                                identity.VirtualizationPlatform = "VMware";
                            }
                            else if (biosManufacturer.Contains("innotek"))
                            {
                                identity.VirtualizationPlatform = "VirtualBox";
                            }
                            else if (biosManufacturer.Contains("qemu"))
                            {
                                identity.VirtualizationPlatform = "KVM/QEMU";
                            }
                            else if (biosManufacturer.Contains("xen"))
                            {
                                identity.VirtualizationPlatform = "Xen";
                            }
                            else
                            {
                                identity.VirtualizationPlatform = "Unknown VM";
                            }
                            
                            return true; // VM detected
                        }
                    }
                    finally
                    {
                        bios?.Dispose();
                    }
                    break; // Only check first result
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to check BIOS for VM indicators");
            }

            return false; // Not a VM based on BIOS
        }

        private static bool CheckBaseBoardForVM(DeviceIdentity identity, ILogger logger)
        {
            try
            {
                using var baseBoardSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                using var baseBoardCollection = baseBoardSearcher.Get();
                
                foreach (ManagementObject baseBoard in baseBoardCollection)
                {
                    try
                    {
                        var boardManufacturer = baseBoard["Manufacturer"]?.ToString()?.ToLowerInvariant() ?? "";
                        var boardProduct = baseBoard["Product"]?.ToString()?.ToLowerInvariant() ?? "";

                        if (boardManufacturer.Contains("vmware") ||
                            (boardManufacturer.Contains("microsoft corporation") && boardProduct.Contains("virtual")) ||
                            boardManufacturer.Contains("qemu") ||
                            boardProduct.Contains("virtualbox"))
                        {
                            identity.IsVirtualMachine = true;
                            
                            if (boardManufacturer.Contains("vmware"))
                            {
                                identity.VirtualizationPlatform = "VMware";
                            }
                            else if (boardManufacturer.Contains("microsoft") && boardProduct.Contains("virtual"))
                            {
                                identity.VirtualizationPlatform = "Hyper-V";
                            }
                            else if (boardProduct.Contains("virtualbox"))
                            {
                                identity.VirtualizationPlatform = "VirtualBox";
                            }
                            else if (boardManufacturer.Contains("qemu"))
                            {
                                identity.VirtualizationPlatform = "KVM/QEMU";
                            }
                            else
                            {
                                identity.VirtualizationPlatform = "Unknown VM";
                            }
                            
                            return true; // VM detected
                        }
                    }
                    finally
                    {
                        baseBoard?.Dispose();
                    }
                    break; // Only check first result
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to check BaseBoard for VM indicators");
            }

            return false; // Not a VM based on BaseBoard
        }
    }
}
