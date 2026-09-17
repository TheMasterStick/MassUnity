using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MassRPG.Editor.Data
{
    [Serializable]
    public sealed class GenericDefinitionDraftJson
    {
        public int schemaVersion = 1;
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string kind = "Other";
        public string description = string.Empty;
        public string[] tags = Array.Empty<string>();
        public string notes = string.Empty;
        public string editorState = "draft";
        public RepositoryPresentationJson presentation;
    }

    public sealed class RepositoryGenericDefinitionRecord
    {
        public RepositoryGenericDefinitionRecord(string filePath, GenericDefinitionDraftJson document, string rawJson, string error)
        {
            FilePath = filePath ?? string.Empty;
            Document = document;
            RawJson = rawJson ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public string FilePath { get; }
        public GenericDefinitionDraftJson Document { get; }
        public string RawJson { get; }
        public string Error { get; }
        public bool IsValid => Document != null && string.IsNullOrEmpty(Error);
        public string Id => Document?.id ?? Path.GetFileNameWithoutExtension(FilePath);
        public string DisplayName => string.IsNullOrWhiteSpace(Document?.displayName) ? Id : Document.displayName;
        public string Kind => string.IsNullOrWhiteSpace(Document?.kind) ? "Other" : Document.kind;
        public bool ReadyForReview => string.Equals(Document?.editorState, "ready-for-review", StringComparison.Ordinal);
    }

    /// <summary>
    /// Read-only Unity projection of flexible browser-authored definitions. The arbitrary `data`
    /// object is deliberately preserved as raw JSON instead of being deserialized/reserialized by
    /// JsonUtility, so Unity inspection can never accidentally destroy fields it does not understand.
    /// </summary>
    public static class RepositoryGenericDefinitionStore
    {
        public static string DefinitionDraftRoot
            => Path.Combine(RepositoryDraftItemStore.RepositoryRoot, "ContentData", "Drafts", "definitions");

        public static IReadOnlyList<RepositoryGenericDefinitionRecord> LoadAll()
        {
            if (!Directory.Exists(DefinitionDraftRoot)) return Array.Empty<RepositoryGenericDefinitionRecord>();
            var files = Directory.GetFiles(DefinitionDraftRoot, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var result = new List<RepositoryGenericDefinitionRecord>(files.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                GenericDefinitionDraftJson document = null;
                var raw = string.Empty;
                try
                {
                    raw = File.ReadAllText(file);
                    document = JsonUtility.FromJson<GenericDefinitionDraftJson>(raw);
                    if (document == null) throw new InvalidDataException("JSON did not produce a generic definition document.");
                    if (document.schemaVersion != 1) throw new InvalidDataException("Unsupported schemaVersion " + document.schemaVersion + ".");
                    if (string.IsNullOrWhiteSpace(document.id)) throw new InvalidDataException("Permanent ID is missing.");
                    if (string.IsNullOrWhiteSpace(document.displayName)) throw new InvalidDataException("Display name is missing.");
                    if (string.IsNullOrWhiteSpace(document.kind)) throw new InvalidDataException("Definition kind is missing.");
                    if (!seen.Add(document.id)) throw new InvalidDataException("Duplicate permanent ID '" + document.id + "'.");
                    var fileId = Path.GetFileNameWithoutExtension(file);
                    if (!string.Equals(fileId, document.id, StringComparison.Ordinal))
                        throw new InvalidDataException("Filename '" + fileId + ".json' does not match permanent ID '" + document.id + "'.");
                    result.Add(new RepositoryGenericDefinitionRecord(file, document, raw, string.Empty));
                }
                catch (Exception ex)
                {
                    result.Add(new RepositoryGenericDefinitionRecord(file, document, raw, ex.Message));
                }
            }

            return result
                .OrderBy(record => record.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(record => record.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
