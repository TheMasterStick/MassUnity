using System;
using System.Collections.Generic;
using MassRPG.Core.Content;
using UnityEngine;

namespace MassRPG.Client.Presentation
{
    /// <summary>
    /// Build/runtime-facing lookup from stable MassRPG presentation IDs to packaged Unity assets.
    /// The repository AssetLink JSON is editor/source data; builds consume this compact catalog so
    /// gameplay/content never depends on Unity AssetDatabase paths or GUID APIs.
    /// </summary>
    [CreateAssetMenu(menuName = "MassRPG/Presentation Asset Catalog", fileName = "PresentationAssetCatalog")]
    public sealed class PresentationAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string assetId = string.Empty;
            [SerializeField] private UnityEngine.Object asset;

            public string AssetId => assetId;
            public UnityEngine.Object Asset => asset;

            public void Set(string id, UnityEngine.Object value)
            {
                assetId = id ?? string.Empty;
                asset = value;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, UnityEngine.Object> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryResolve(string assetId, out UnityEngine.Object asset)
        {
            EnsureLookup();
            if (string.IsNullOrWhiteSpace(assetId))
            {
                asset = null;
                return false;
            }
            return _lookup.TryGetValue(assetId, out asset) && asset != null;
        }

        public bool TryResolve<T>(string assetId, out T asset) where T : UnityEngine.Object
        {
            if (TryResolve(assetId, out var raw) && raw is T typed)
            {
                asset = typed;
                return true;
            }
            asset = null;
            return false;
        }

        public bool TryResolve(ContentId contentId, PresentationAssetRole role, out UnityEngine.Object asset)
            => TryResolve(PresentationAssetId.For(contentId, role).Value, out asset);

        public bool TryResolve<T>(ContentId contentId, PresentationAssetRole role, out T asset) where T : UnityEngine.Object
            => TryResolve(PresentationAssetId.For(contentId, role).Value, out asset);

        /// <summary>Editor/build tooling replaces the serialized list atomically.</summary>
        public void ReplaceEntries(IEnumerable<KeyValuePair<string, UnityEngine.Object>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            entries.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (!ContentId.TryCreate(pair.Key, out var assetId)
                    || !PresentationAssetId.TryParse(assetId, out _, out _))
                    throw new ArgumentException("Catalog entry has invalid presentation asset ID '" + pair.Key + "'.", nameof(source));
                if (!seen.Add(pair.Key)) throw new ArgumentException("Duplicate presentation asset ID '" + pair.Key + "'.", nameof(source));
                if (pair.Value == null) continue;
                var entry = new Entry();
                entry.Set(pair.Key, pair.Value);
                entries.Add(entry);
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.AssetId, b.AssetId));
            _lookup = null;
        }

        private void OnEnable() => _lookup = null;
        private void OnValidate() => _lookup = null;

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.AssetId) || entry.Asset == null) continue;
                _lookup[entry.AssetId] = entry.Asset;
            }
        }
    }
}
