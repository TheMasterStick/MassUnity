using System;
using System.Collections.Generic;

namespace MassRPG.Data.Publishing
{
    public enum DataEnvironment
    {
        EditorDraft,
        LocalTest,
        LivePublished
    }

    public readonly struct PublishedDataVersion : IEquatable<PublishedDataVersion>, IComparable<PublishedDataVersion>
    {
        public PublishedDataVersion(long value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public long Value { get; }
        public int CompareTo(PublishedDataVersion other) => Value.CompareTo(other.Value);
        public bool Equals(PublishedDataVersion other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PublishedDataVersion other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(PublishedDataVersion left, PublishedDataVersion right) => left.Equals(right);
        public static bool operator !=(PublishedDataVersion left, PublishedDataVersion right) => !left.Equals(right);
    }

    /// <summary>
    /// Versioned manifest for published gameplay/world data. The actual files may be JSON/binary/
    /// compressed packages later; the manifest gives servers one immutable version to activate or
    /// roll back to without rebuilding the Unity client executable.
    /// </summary>
    public sealed class PublishedDataManifest
    {
        public PublishedDataManifest(
            PublishedDataVersion version,
            long createdUnixMilliseconds,
            string label,
            PublishedDataVersion? parentVersion = null,
            int schemaVersion = 1)
        {
            if (createdUnixMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdUnixMilliseconds));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            Version = version;
            ParentVersion = parentVersion;
            CreatedUnixMilliseconds = createdUnixMilliseconds;
            Label = label ?? string.Empty;
            SchemaVersion = schemaVersion;
            PackageHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public PublishedDataVersion Version { get; }
        public PublishedDataVersion? ParentVersion { get; }
        public long CreatedUnixMilliseconds { get; }
        public string Label { get; }
        public int SchemaVersion { get; }
        public Dictionary<string, string> PackageHashes { get; }

        public void SetPackageHash(string packageName, string hash)
        {
            if (string.IsNullOrWhiteSpace(packageName)) throw new ArgumentException("Package name is required.", nameof(packageName));
            if (string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("Package hash is required.", nameof(hash));
            PackageHashes[packageName] = hash;
        }
    }
}
