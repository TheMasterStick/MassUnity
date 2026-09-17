using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassRPG.Client.Presentation;
using MassRPG.Core.Content;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    /// <summary>
    /// Converts repository AssetLink source documents into the compact ScriptableObject catalog used
    /// by player builds. AssetLink JSON remains canonical authoring data; this generated Unity asset
    /// is merely a packaging index and can be rebuilt whenever linked assets move/change.
    /// </summary>
    public static class PresentationAssetCatalogBuilder
    {
        public const string GeneratedFolder = "Assets/MassRPG/Generated";
        public const string CatalogAssetPath = GeneratedFolder + "/PresentationAssetCatalog.asset";

        [MenuItem("MassRPG/Rebuild Presentation Asset Catalog", priority = 24)]
        public static void RebuildMenu() => Rebuild(true);

        public static PresentationAssetCatalog Rebuild(bool showDialog)
        {
            EnsureUnityFolder(GeneratedFolder);
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationAssetCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PresentationAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            var entries = new List<KeyValuePair<string, UnityEngine.Object>>();
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var root = RepositoryPresentationAssetLinkStore.AssetLinkRoot;
            if (Directory.Exists(root))
            {
                var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < files.Length; i++)
                {
                    RepositoryPresentationAssetLinkJson raw;
                    try
                    {
                        raw = JsonUtility.FromJson<RepositoryPresentationAssetLinkJson>(File.ReadAllText(files[i]));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Relative(files[i]) + ": " + ex.Message);
                        continue;
                    }

                    if (raw == null || !ContentId.TryCreate(raw.id, out var assetId)
                        || !PresentationAssetId.TryParse(assetId, out _, out _))
                    {
                        errors.Add(Relative(files[i]) + ": invalid stable presentation asset ID.");
                        continue;
                    }
                    if (!seen.Add(assetId.Value))
                    {
                        errors.Add(Relative(files[i]) + ": duplicate stable asset ID '" + assetId.Value + "'.");
                        continue;
                    }
                    if (!RepositoryPresentationAssetLinkStore.TryLoad(assetId, out var validated, out var validationError))
                    {
                        errors.Add(Relative(files[i]) + ": " + validationError);
                        continue;
                    }

                    var resolvedPath = AssetDatabase.GUIDToAssetPath(validated.unityGuid);
                    if (string.IsNullOrWhiteSpace(resolvedPath)) resolvedPath = validated.assetPath;
                    var asset = AssetDatabase.LoadMainAssetAtPath(resolvedPath);
                    if (asset == null)
                    {
                        errors.Add(Relative(files[i]) + ": linked Unity asset no longer resolves ('" + resolvedPath + "').");
                        continue;
                    }
                    entries.Add(new KeyValuePair<string, UnityEngine.Object>(assetId.Value, asset));
                }
            }

            catalog.ReplaceEntries(entries.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var summary = "Built " + entries.Count + " presentation asset binding(s) into " + CatalogAssetPath + ".";
            if (errors.Count > 0)
            {
                var details = string.Join("\n", errors.Take(20));
                if (errors.Count > 20) details += "\n… and " + (errors.Count - 20) + " more.";
                Debug.LogWarning("MassRPG presentation catalog built with " + errors.Count + " issue(s):\n" + details);
                if (showDialog) EditorUtility.DisplayDialog("MassRPG Presentation Catalog", summary + "\n\n" + errors.Count + " link issue(s) were skipped. See Console for details.", "OK");
            }
            else
            {
                Debug.Log("MassRPG: " + summary);
                if (showDialog) EditorUtility.DisplayDialog("MassRPG Presentation Catalog", summary, "OK");
            }
            return catalog;
        }

        private static string Relative(string path)
        {
            var root = RepositoryDraftItemStore.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length).Replace('\\', '/') : path;
        }

        private static void EnsureUnityFolder(string path)
        {
            var normalized = path.Replace('\\', '/');
            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") throw new ArgumentException("Generated Unity path must begin with Assets/.", nameof(path));
            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
