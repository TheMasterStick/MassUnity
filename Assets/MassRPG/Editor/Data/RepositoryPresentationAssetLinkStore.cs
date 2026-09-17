using System;
using System.Collections.Generic;
using System.IO;
using MassRPG.Core.Content;
using MassRPG.EditorCore.Data;
using UnityEditor;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    [Serializable]
    public sealed class RepositoryPresentationAssetLinkJson
    {
        public int schemaVersion = 1;
        public string id = string.Empty;
        public string role = string.Empty;
        public string unityGuid = string.Empty;
        public string assetPath = string.Empty;
    }

    /// <summary>
    /// Repository mapping between stable presentation IDs used by MassRPG data and Unity's asset
    /// GUIDs. The stable ID survives asset moves/renames; Unity's .meta GUID resolves the actual file.
    /// </summary>
    public static class RepositoryPresentationAssetLinkStore
    {
        public static string AssetLinkRoot
            => Path.Combine(RepositoryDraftItemStore.RepositoryRoot, "ContentData", "AssetLinks");

        public static ContentId Link(ContentId contentId, PresentationAssetRole role, UnityEngine.Object asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Presentation assets must be saved project assets under Unity's Assets folder.");
            if (AssetDatabase.IsValidFolder(assetPath))
                throw new InvalidOperationException("A folder cannot be used as a presentation asset.");

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("Unity could not resolve a GUID for '" + assetPath + "'.");

            var assetId = PresentationAssetId.For(contentId, role);
            var document = new RepositoryPresentationAssetLinkJson
            {
                schemaVersion = 1,
                id = assetId.Value,
                role = PresentationAssetId.RoleSegment(role),
                unityGuid = guid,
                assetPath = assetPath
            };

            var filePath = DocumentPath(assetId);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AssetLinkRoot);
            File.WriteAllText(filePath, JsonUtility.ToJson(document, true) + Environment.NewLine);
            return assetId;
        }

        public static bool TryLoad(ContentId assetId, out RepositoryPresentationAssetLinkJson document, out string error)
        {
            document = null;
            error = string.Empty;
            try
            {
                var filePath = DocumentPath(assetId);
                if (!File.Exists(filePath))
                {
                    error = "Asset-link document is missing: " + MakeRepositoryRelative(filePath);
                    return false;
                }

                document = JsonUtility.FromJson<RepositoryPresentationAssetLinkJson>(File.ReadAllText(filePath));
                if (document == null) throw new InvalidDataException("Asset-link JSON did not produce a document.");
                if (document.schemaVersion != 1) throw new InvalidDataException("Unsupported asset-link schemaVersion " + document.schemaVersion + ".");
                if (!string.Equals(document.id, assetId.Value, StringComparison.Ordinal))
                    throw new InvalidDataException("Asset-link ID does not match its repository path.");
                if (string.IsNullOrWhiteSpace(document.unityGuid)) throw new InvalidDataException("Asset-link Unity GUID is missing.");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                document = null;
                return false;
            }
        }

        public static UnityEngine.Object LoadObject(string assetIdText, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(assetIdText)) return null;
            if (!ContentId.TryCreate(assetIdText, out var assetId))
            {
                error = "Presentation asset ID is invalid.";
                return null;
            }
            if (!TryLoad(assetId, out var link, out error)) return null;

            var resolvedPath = AssetDatabase.GUIDToAssetPath(link.unityGuid);
            if (string.IsNullOrWhiteSpace(resolvedPath) && !string.IsNullOrWhiteSpace(link.assetPath)) resolvedPath = link.assetPath;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                error = "Unity GUID no longer resolves to a project asset.";
                return null;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(resolvedPath);
            if (asset == null) error = "Unity asset could not be loaded from '" + resolvedPath + "'.";
            return asset;
        }

        public static string DocumentPath(ContentId assetId)
        {
            if (!PresentationAssetId.TryParse(assetId, out var role, out var contentId))
                throw new ArgumentException("Asset ID is not a MassRPG presentation asset ID.", nameof(assetId));

            var path = Path.Combine(AssetLinkRoot, PresentationAssetId.RoleSegment(role));
            var segments = SafeSegments(contentId.Value);
            for (var i = 0; i < segments.Count - 1; i++) path = Path.Combine(path, segments[i]);
            return Path.Combine(path, segments[segments.Count - 1] + ".json");
        }

        private static IReadOnlyList<string> SafeSegments(string value)
        {
            var parts = value.Split('/');
            if (parts.Length == 0) throw new InvalidDataException("Content ID has no path segments.");
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]) || parts[i] == "." || parts[i] == "..")
                    throw new InvalidDataException("Content ID contains an unsafe path segment.");
            }
            return parts;
        }

        private static string MakeRepositoryRelative(string path)
        {
            var root = RepositoryDraftItemStore.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
        }
    }

    /// <summary>
    /// Safely writes only the top-level `presentation` object of a draft. Generic-definition `data`
    /// and all gameplay fields remain untouched, even when Unity does not understand them yet.
    /// </summary>
    public static class RepositoryPresentationDraftLinker
    {
        public static void Save(
            RepositoryPresentationDraftRecord record,
            UnityEngine.Object icon,
            UnityEngine.Object model,
            UnityEngine.Object portrait,
            UnityEngine.Object animationSet,
            string assetState,
            string notes,
            bool clearMissingAssignments = false)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!record.IsValid) throw new InvalidOperationException("Cannot link presentation assets on an invalid draft.");
            if (!IsAssetState(assetState)) throw new ArgumentException("Unknown presentation asset state '" + assetState + "'.", nameof(assetState));

            if (!ContentId.TryCreate(record.Id, out var contentId))
                throw new InvalidOperationException("Draft permanent ID is invalid.");

            var presentation = new RepositoryPresentationJson
            {
                assetState = assetState,
                iconAssetId = Resolve(record.IconAssetId, contentId, PresentationAssetRole.Icon, icon, clearMissingAssignments),
                modelAssetId = Resolve(record.ModelAssetId, contentId, PresentationAssetRole.Model, model, clearMissingAssignments),
                portraitAssetId = Resolve(record.PortraitAssetId, contentId, PresentationAssetRole.Portrait, portrait, clearMissingAssignments),
                animationSetAssetId = Resolve(record.AnimationSetAssetId, contentId, PresentationAssetRole.AnimationSet, animationSet, clearMissingAssignments),
                notes = notes ?? string.Empty
            };

            var original = File.ReadAllText(record.FilePath);
            var replacement = JsonUtility.ToJson(presentation, true);
            var patched = JsonTopLevelPropertyPatcher.UpsertObjectProperty(original, "presentation", replacement);
            File.WriteAllText(record.FilePath, EnsureTrailingNewline(patched));
        }

        private static string Resolve(
            string existingAssetId,
            ContentId contentId,
            PresentationAssetRole role,
            UnityEngine.Object asset,
            bool clearMissingAssignments)
        {
            if (asset != null) return RepositoryPresentationAssetLinkStore.Link(contentId, role, asset).Value;
            return clearMissingAssignments ? null : EmptyToNull(existingAssetId);
        }

        private static string EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static bool IsAssetState(string value)
            => value == "needs-assets" || value == "placeholder" || value == "linked" || value == "final" || value == "not-required";

        private static string EnsureTrailingNewline(string value)
        {
            if (value.EndsWith("\r\n", StringComparison.Ordinal) || value.EndsWith("\n", StringComparison.Ordinal)) return value;
            return value + Environment.NewLine;
        }
    }
}
