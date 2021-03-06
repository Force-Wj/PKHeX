﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PKHeX.Core
{
    public static partial class Util
    {
        private const string TranslationSplitter = " = ";
        private static readonly Assembly thisAssembly = typeof(Util).GetTypeInfo().Assembly;
        private static readonly string[] manifestResourceNames = thisAssembly.GetManifestResourceNames();
        private static readonly Dictionary<string, string> resourceNameMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, string[]> stringListCache = new Dictionary<string, string[]>();

        private static readonly object getStringListLoadLock = new object();

        #region String Lists

        /// <summary>
        /// Gets a list of all Pokémon species names.
        /// </summary>
        /// <param name="language">Language of the Pokémon species names to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each Pokémon species name.</returns>
        public static string[] GetSpeciesList(string language) => GetStringList("species", language);

        /// <summary>
        /// Gets a list of all move names.
        /// </summary>
        /// <param name="language">Language of the move names to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each move name.</returns>
        public static string[] GetMovesList(string language) => GetStringList("moves", language);

        /// <summary>
        /// Gets a list of all Pokémon ability names.
        /// </summary>
        /// <param name="language">Language of the Pokémon ability names to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each Pokémon ability name.</returns>
        public static string[] GetAbilitiesList(string language) => GetStringList("abilities", language);

        /// <summary>
        /// Gets a list of all Pokémon nature names.
        /// </summary>
        /// <param name="language">Language of the Pokémon nature names to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each Pokémon nature name.</returns>
        public static string[] GetNaturesList(string language) => GetStringList("natures", language);

        /// <summary>
        /// Gets a list of all Pokémon form names.
        /// </summary>
        /// <param name="language">Language of the Pokémon form names to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each Pokémon form name.</returns>
        public static string[] GetFormsList(string language) => GetStringList("forms", language);

        /// <summary>
        /// Gets a list of all Pokémon type names.
        /// </summary>
        /// <param name="language">Language of the Pokémon type names to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each Pokémon type name.</returns>
        public static string[] GetTypesList(string language) => GetStringList("types", language);

        /// <summary>
        /// Gets a list of all Pokémon characteristic.
        /// </summary>
        /// <param name="language">Language of the Pokémon characteristic to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each Pokémon characteristic.</returns>
        public static string[] GetCharacteristicsList(string language) => GetStringList("character", language);

        /// <summary>
        /// Gets a list of all items.
        /// </summary>
        /// <param name="language">Language of the items to select (e.g. "en", "fr", "jp", etc.)</param>
        /// <returns>An array of strings whose indexes correspond to the IDs of each item.</returns>
        public static string[] GetItemsList(string language) => GetStringList("items", language);

        #endregion

        public static string[] GetStringList(string f)
        {
            if (stringListCache.ContainsKey(f))
                return (string[])stringListCache[f].Clone();

            var txt = GetStringResource(f); // Fetch File, \n to list.
            if (txt == null)
                return Array.Empty<string>();
            string[] rawlist = txt.Split('\n');
            for (int i = 0; i < rawlist.Length; i++)
                rawlist[i] = rawlist[i].TrimEnd('\r');

            lock (getStringListLoadLock) // Make sure only one thread can write to the cache
            {
                if (!stringListCache.ContainsKey(f)) // Check cache again in case of race condition
                    stringListCache.Add(f, rawlist);
            }

            return (string[])rawlist.Clone();
        }

        public static string[] GetStringList(string f, string l, string type = "text") => GetStringList($"{type}_{f}_{l}");

        public static string[] GetNulledStringArray(string[] SimpleStringList)
        {
            int len = ToInt32(SimpleStringList.Last().Split(',')[0]) + 1;
            string[] newlist = new string[len];
            for (int i = 1; i < SimpleStringList.Length; i++)
            {
                var split = SimpleStringList[i].Split(',');
                newlist[ToInt32(split[0])] = split[1];
            }
            return newlist;
        }

        public static byte[] GetBinaryResource(string name)
        {
            using (var resource = thisAssembly.GetManifestResourceStream(
                $"PKHeX.Core.Resources.byte.{name}"))
            {
                var buffer = new byte[resource.Length];
                resource.Read(buffer, 0, (int)resource.Length);
                return buffer;
            }
        }

        public static string GetStringResource(string name)
        {
            if (!resourceNameMap.ContainsKey(name))
            {
                bool Match(string x) => x.StartsWith("PKHeX.Core.Resources.text.") && x.EndsWith($"{name}.txt", StringComparison.OrdinalIgnoreCase);
                var resname = Array.Find(manifestResourceNames, Match);
                resourceNameMap.Add(name, resname);
            }

            if (resourceNameMap[name] == null)
                return null;

            using (var resource = thisAssembly.GetManifestResourceStream(resourceNameMap[name]))
            using (var reader = new StreamReader(resource))
                return reader.ReadToEnd();
        }

        #region Non-Form Translation
        /// <summary>
        /// Gets the names of the properties defined in the given input
        /// </summary>
        /// <param name="input">Enumerable of translation definitions in the form "Property = Value".</param>
        private static string[] GetProperties(IEnumerable<string> input)
        {
            return input.Select(l => l.Substring(0, l.IndexOf(TranslationSplitter, StringComparison.Ordinal))).ToArray();
        }

        private static IEnumerable<string> DumpStrings(Type t)
        {
            var props = ReflectUtil.GetPropertiesStartWithPrefix(t, string.Empty);
            return props.Select(p => $"{p}{TranslationSplitter}{ReflectUtil.GetValue(t, p)}");
        }

        /// <summary>
        /// Gets the current localization in a static class containing language-specific strings
        /// </summary>
        /// <param name="t"></param>
        /// <param name="existingLines">Existing localization lines (if provided)</param>
        public static string[] GetLocalization(Type t, string[] existingLines = null)
        {
            var currentLines = DumpStrings(t).ToArray();
            if (existingLines == null)
                return currentLines;
            var existing = GetProperties(existingLines);
            var current = GetProperties(currentLines);

            var result = new string[currentLines.Length];
            for (int i = 0; i < current.Length; i++)
            {
                int index = Array.IndexOf(existing, current[i]);
                result[i] = index < 0 ? currentLines[i] : existingLines[index];
            }
            return result;
        }

        /// <summary>
        /// Applies localization to a static class containing language-specific strings.
        /// </summary>
        /// <param name="t">Type of the static class containing the desired strings.</param>
        /// <param name="lines">Lines containing the localized strings</param>
        private static void SetLocalization(Type t, IEnumerable<string> lines)
        {
            if (lines == null)
                return;
            foreach (var line in lines.Where(l => l != null))
            {
                var index = line.IndexOf(TranslationSplitter, StringComparison.Ordinal);
                if (index < 0)
                    continue;
                var prop = line.Substring(0, index);
                var value = line.Substring(index + TranslationSplitter.Length);

                try
                {
                    ReflectUtil.SetValue(t, prop, value);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Property not present: {prop} || Value written: {value}");
                    Debug.WriteLine(e.Message);
                }
            }
        }

        /// <summary>
        /// Applies localization to a static class containing language-specific strings.
        /// </summary>
        /// <param name="t">Type of the static class containing the desired strings.</param>
        /// <param name="languageFilePrefix">Prefix of the language file to use.  Example: if the target is legality_en.txt, <paramref name="languageFilePrefix"/> should be "legality".</param>
        /// <param name="currentCultureCode">Culture information</param>
        private static void SetLocalization(Type t, string languageFilePrefix, string currentCultureCode)
        {
            SetLocalization(t, GetStringList($"{languageFilePrefix}_{currentCultureCode}"));
        }

        /// <summary>
        /// Applies localization to a static class containing language-specific strings.
        /// </summary>
        /// <param name="t">Type of the static class containing the desired strings.</param>
        /// <remarks>The values used to translate the given static class are retrieved from [TypeName]_[CurrentLangCode2].txt in the resource manager of PKHeX.Core.</remarks>
        /// <param name="currentCultureCode">Culture information</param>
        public static void SetLocalization(Type t, string currentCultureCode)
        {
            SetLocalization(t, t.Name, currentCultureCode);
        }

        #endregion

        #region DataSource Providing
        public static List<ComboItem> GetCountryRegionList(string textfile, string lang)
        {
            // Set up
            string[] inputCSV = GetStringList(textfile);

            // Get Language we're fetching for
            int index = Array.IndexOf(new[] { "ja", "en", "fr", "de", "it", "es", "ko", "zh", }, lang);

            // Gather our data from the input file
            return inputCSV.Skip(1)
                .Select(entry => entry.Split(','))
                .Select(data => new ComboItem { Text = data[1 + index], Value = Convert.ToInt32(data[0]) })
                .OrderBy(z => z.Text)
                .ToList();
        }

        public static List<ComboItem> GetUnsortedCBList(string textfile)
        {
            string[] inputCSV = GetStringList(textfile);
            return inputCSV.Skip(1)
                .Select(entry => entry.Split(','))
                .Select(data => new ComboItem { Text = data[1], Value = Convert.ToInt32(data[0]) })
                .ToList();
        }

        public static List<ComboItem> GetCBList(IReadOnlyList<string> inStrings, params int[][] allowed)
        {
            if (allowed.Length == 0)
                allowed = new[] { Enumerable.Range(0, inStrings.Count).ToArray() };

            return allowed.SelectMany(list => list
                .Select(z => new ComboItem { Text = inStrings[z], Value = z })
                .OrderBy(z => z.Text))
                .ToList();
        }

        public static void AddCBWithOffset(List<ComboItem> cbList, IReadOnlyList<string> inStrings, int offset, IEnumerable<int> allowed)
        {
            var list = allowed
                .Select(z => new ComboItem {Text = inStrings[z - offset], Value = z})
                .OrderBy(z => z.Text);

            cbList.AddRange(list);
        }

        public static List<ComboItem> GetVariedCBListBall(string[] inStrings, int[] stringNum, int[] stringVal)
        {
            // First 3 Balls are always first
            var newlist = new List<ComboItem>(3 + stringNum.Length)
            {
                new ComboItem {Text = inStrings[4], Value = (int)Ball.Poke},
                new ComboItem {Text = inStrings[3], Value = (int)Ball.Great},
                new ComboItem {Text = inStrings[2], Value = (int)Ball.Ultra},
            };

            var ordered = stringNum
                .Select((z, i) => new ComboItem {Text = inStrings[z], Value = stringVal[i]})
                .OrderBy(z => z.Text);
            newlist.AddRange(ordered);
            return newlist;
        }
        #endregion
    }
}
