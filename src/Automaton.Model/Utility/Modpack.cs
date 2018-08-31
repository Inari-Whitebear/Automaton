﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Automaton.Model.Errors;
using Automaton.Model.Extensions;
using Automaton.Model.ModpackBase;

namespace Automaton.Model.Utility
{
    public class Modpack
    {
        public delegate void ModpackLoaded();
        public static event ModpackLoaded ModpackLoadedEvent;

        /// <summary>
        /// Extracts and loads an archived modpack into the model
        /// </summary>
        /// <param name="modpackPath"></param>
        public static void LoadModpack(string modpackPath)
        {
            var modpackExtractionPath = string.Empty;
            var modpackHeader = new Header();
            var modpackMods = new List<Mod>();

            // Extract the modpack archive out to a temp folder
            using (var extractionHandler = new Archive(modpackPath))
            {
                extractionHandler.ExtractModpack();
                modpackExtractionPath = extractionHandler.ModpackExtractionPath;
            }

            Instance.Automaton.ModpackExtractionLocation = modpackExtractionPath;

            // Load the modpack header
            var modpackHeaderPath = System.IO.Path.Combine(modpackExtractionPath, $"modpack.json");

            if (!File.Exists(modpackHeaderPath))
            {
                GenericErrorHandler.Throw(GenericErrorType.ModpackStructure, "Valid modpack.json was not found.", new StackTrace());
                return;
            }

            // Load modpack header into Instance
            modpackHeader = Json.TryDeserializeJson<Header>(File.ReadAllText(modpackHeaderPath), out string parseError);

            // The json string was not parsed correctly, throw error
            if (parseError != string.Empty)
            {
                GenericErrorHandler.Throw(GenericErrorType.JSONParse, parseError, new StackTrace());
                return;
            }

            // Set global instances, these will update viewmodels automatically via the message service
            Instance.Automaton.ModpackHeader = modpackHeader;
            Instance.Automaton.ModpackMods = LoadModInstallParameters(modpackHeader, modpackExtractionPath);

            ModpackLoadedEvent();
        }

        /// <summary>
        /// Loads and returns list of Mod objects
        /// </summary>
        /// <param name="modpackHeader"></param>
        /// <param name="modpackExtractionPath"></param>
        /// <returns></returns>
        private static List<Mod> LoadModInstallParameters(Header modpackHeader, string modpackExtractionPath)
        {
            var modpackMods = new List<Mod>();

            // Detect for mod install directories outlined by ModInstallFolders
            var modInstallFolders = modpackHeader.ModInstallFolders
                .Select(x => System.IO.Path.Combine(modpackExtractionPath, x).StandardizePathSeparators());

            var existingModInstallFolders = modInstallFolders
                .Where(x => Directory.Exists(x) && Directory.GetFiles(x, $"*.json").Any());

            // Check for any valid values
            if (!existingModInstallFolders.ContainsAny())
            {
                GenericErrorHandler.Throw(GenericErrorType.ModpackStructure, "mod_install_folders not found in modpack.", new StackTrace());
                return null;
            }

            // Out to log or error handler, not a breaking issue, but may cause installation issues
            if (existingModInstallFolders.Count() != modInstallFolders.Count())
            {
                // TODO
            }

            // Parse existingModInstallFolders and return any valid mod structures
            foreach (var folder in existingModInstallFolders)
            {
                var modFiles = Directory.GetFiles(folder, $"*.json");

                foreach (var modFile in modFiles)
                {
                    var modObject = Json.TryDeserializeJson<Mod>(File.ReadAllText(modFile), out string parseError);

                    modObject.ModInstallParameterPath = modFile;

                    if (parseError != string.Empty)
                    {
                        GenericErrorHandler.Throw(GenericErrorType.JSONParse, parseError, new StackTrace());

                        return null;
                    }

                    modpackMods.Add(modObject);
                }
            }

            return modpackMods;
        }
    }
}