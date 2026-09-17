using System;

namespace MassRPG.Core.Content
{
    /// <summary>
    /// Permanent machine-facing identifier for published content.
    /// Display names are deliberately kept separate so renaming content never breaks saves.
    /// Existing browser ids such as "iron_sword" remain valid during migration; namespaced ids
    /// such as "item.iron_sword" are also supported.
    /// </summary>
    public readonly struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
    {
        private readonly string _value;

        public ContentId(string value)
        {
            if (!IsValid(value))
                throw new ArgumentException("Content ids must be non-empty lower-case identifiers using a-z, 0-9, '.', '_', '-', or '/'.", nameof(value));

            _value = value;
        }

        public string Value => _value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public static bool TryCreate(string value, out ContentId id)
        {
            if (IsValid(value))
            {
                id = new ContentId(value);
                return true;
            }

            id = default;
            return false;
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var valid = (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '.' || c == '_' || c == '-' || c == '/';
                if (!valid) return false;
            }

            return true;
        }

        public int CompareTo(ContentId other) => string.CompareOrdinal(Value, other.Value);
        public bool Equals(ContentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ContentId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static bool operator ==(ContentId left, ContentId right) => left.Equals(right);
        public static bool operator !=(ContentId left, ContentId right) => !left.Equals(right);
    }
}
