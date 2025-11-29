using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WindowsVersionsCore.Models;
using WindowsVersionsCore.Services;

namespace WindowsVersionsCore.Tools
{
    /// <summary>
    /// Utility for cleaning Windows update JSON files
    /// </summary>
    public static class WindowsJsonCleaner
    {
        /// <summary>
        /// Clean a Windows update JSON file
        /// </summary>
        /// <param name="inputFile">Path to the input JSON file</param>
        /// <param name="outputFile">Path to save the cleaned JSON file</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool CleanWindowsUpdateJson(string inputFile, string outputFile)
        {
            try
            {
                System.Console.WriteLine($"Reading updates from {inputFile}");
                string jsonContent = File.ReadAllText(inputFile);
                var updates = JsonSerializer.Deserialize<List<WindowsUpdate>>(jsonContent);
                
                if (updates == null)
                {
                    System.Console.WriteLine("Failed to deserialize updates from the input file");
                    return false;
                }
                
                System.Console.WriteLine($"Found {updates.Count} updates in the input file");
                
                var cleanedUpdates = UpdateDataCleaner.CleanWindowsUpdates(updates);
                
                System.Console.WriteLine($"Cleaned data to {cleanedUpdates.Count} unique updates");
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                string cleanedJson = JsonSerializer.Serialize(cleanedUpdates, options);
                
                File.WriteAllText(outputFile, cleanedJson);
                System.Console.WriteLine($"Saved cleaned data to {outputFile}");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
    }
}