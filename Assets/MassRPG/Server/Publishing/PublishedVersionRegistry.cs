using System;
using System.Collections.Generic;
using MassRPG.Data.Publishing;

namespace MassRPG.Server.Publishing
{
    /// <summary>
    /// Server-side activation pointer for immutable published data versions. Activating or rolling
    /// back changes which manifest the server serves; it does not rewrite old versions in place.
    /// </summary>
    public sealed class PublishedVersionRegistry
    {
        private readonly Dictionary<long, PublishedDataManifest> _manifests = new Dictionary<long, PublishedDataManifest>();

        public PublishedDataManifest Active { get; private set; }
        public int Count => _manifests.Count;

        public void Register(PublishedDataManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (_manifests.ContainsKey(manifest.Version.Value))
                throw new InvalidOperationException($"Published data version {manifest.Version} already exists and is immutable.");
            if (manifest.ParentVersion.HasValue && !_manifests.ContainsKey(manifest.ParentVersion.Value.Value))
                throw new InvalidOperationException($"Parent version {manifest.ParentVersion.Value} has not been registered.");
            _manifests.Add(manifest.Version.Value, manifest);
        }

        public bool TryGet(PublishedDataVersion version, out PublishedDataManifest manifest)
            => _manifests.TryGetValue(version.Value, out manifest);

        public bool TryActivate(PublishedDataVersion version)
        {
            if (!_manifests.TryGetValue(version.Value, out var manifest)) return false;
            Active = manifest;
            return true;
        }

        public bool TryRollbackToParent()
        {
            if (Active == null || !Active.ParentVersion.HasValue) return false;
            return TryActivate(Active.ParentVersion.Value);
        }
    }
}
