using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    [Serializable]
    public sealed class RepositoryPresentationJson
    {
        public string assetState;
        public string iconAssetId;
        public string modelAssetId;
        public string portraitAssetId;
        public string animationSetAssetId;
        public string notes;
    }

    [Serializable]
    public sealed class RepositoryPresentationEnvelopeJson
    {
        public int schemaVersion = 1;
        public string id;
        public string displayName;
        public string kind;
        public string editorState;
        public RepositoryPresentationJson presentation;
    }

    public sealed class RepositoryPresentationDraftRecord
    {
        public RepositoryPresentationDraftRecord(
            string category,
            string filePath,
            RepositoryPresentationEnvelopeJson document,
            string error)
        {
            Category = category ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Document = document;
            Error = error ?? string.Empty;
        }

        public string Category { get; }
        public string FilePath { get; }
        public RepositoryPresentationEnvelopeJson Document { get; }
        public string Error { get; }
        public bool IsValid => Document != null && string.IsNullOrEmpty(Error);
        public string Id => Document?.id ?? Path.GetFileNameWithoutExtension(FilePath);
        public string DisplayName => string.IsNullOrWhiteSpace(Document?.displayName) ? Id : Document.displayName;
        public string DefinitionKind => Document?.kind ?? string.Empty;
        public string AssetState
        {
            get
            {
                var state = Document?.presentation?.assetState;
                return string.IsNullOrWhiteSpace(state)
                    ? RepositoryPresentationDraftStore.DefaultAssetState(Category)
                    : state;
            }
        }
        public string Notes => Document?.presentation?.notes ?? string.Empty;
        public string IconAssetId => Document?.presentation?.iconAssetId ?? string.Empty;
        public string ModelAssetId => Document?.presentation?.modelAssetId ?? string.Empty;
        public string PortraitAssetId => Document?.presentation?.portraitAssetId ?? string.Empty;
        public string AnimationSetAssetId => Document?.presentation?.animationSetAssetId ?? string.Empty;
        public bool IsOpenAssetWork => IsValid && AssetState != "final" && AssetState != "not-required";
    }

    /// <summary>
    /// Minimal cross-category reader for presentation metadata. Gameplay draft stores remain strongly
    /// typed; this projection exists only so home/Unity work can see every model/icon/portrait/animation
    /// task authored from the browser without coupling those assets to gameplay data.
    /// </summary>
    public static class RepositoryPresentationDraftStore
    {
        private static readonly string[] Categories =
        {
            "items", "creatures", "resources", "recipes", "definitions"
        };

        public static string DraftRoot
            => Path.Combine(RepositoryDraftItemStore.RepositoryRoot, "ContentData", "Drafts");

        public static IReadOnlyList<RepositoryPresentationDraftRecord> LoadAll()
        {
            var result = new List<RepositoryPresentationDraftRecord>();
            for (var categoryIndex = 0; categoryIndex < Categories.Length; categoryIndex++)
            {
                var category = Categories[categoryIndex];
                var root = Path.Combine(DraftRoot, category);
                if (!Directory.Exists(root)) continue;

                var files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    var file = files[fileIndex];
                    RepositoryPresentationEnvelopeJson document = null;
                    try
                    {
                        document = JsonUtility.FromJson<RepositoryPresentationEnvelopeJson>(File.ReadAllText(file));
                        if (document == null) throw new InvalidDataException("JSON did not produce a draft document.");
                        if (document.schemaVersion != 1) throw new InvalidDataException("Unsupported schemaVersion " + document.schemaVersion + ".");
                        if (string.IsNullOrWhiteSpace(document.id)) throw new InvalidDataException("Permanent ID is missing.");

                        var fileId = Path.GetFileNameWithoutExtension(file);
                        if (!string.Equals(fileId, document.id, StringComparison.Ordinal))
                            throw new InvalidDataException("Filename '" + fileId + ".json' does not match permanent ID '" + document.id + "'.");

                        result.Add(new RepositoryPresentationDraftRecord(category, file, document, string.Empty));
                    }
                    catch (Exception ex)
                    {
                        result.Add(new RepositoryPresentationDraftRecord(category, file, document, ex.Message));
                    }
                }
            }

            return result
                .OrderBy(record => record.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Id, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<RepositoryPresentationDraftRecord> LoadOpenAssetBacklog()
            => LoadAll().Where(record => record.IsOpenAssetWork).ToArray();

        internal static string DefaultAssetState(string category)
            => string.Equals(category, "recipes", StringComparison.Ordinal) ? "not-required" : "needs-assets";
    }
}
