using System;

namespace MassRPG.EditorCore.Data
{
    /// <summary>
    /// Replaces or inserts one top-level JSON property without deserializing the rest of the file.
    /// This is used by the Unity presentation linker so flexible generic definition `data` objects
    /// remain byte-for-byte untouched even when Unity does not know their schema yet.
    /// </summary>
    public static class JsonTopLevelPropertyPatcher
    {
        public static string UpsertObjectProperty(string json, string propertyName, string objectJson)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON text is required.", nameof(json));
            if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("Property name is required.", nameof(propertyName));
            if (propertyName.IndexOf('"') >= 0 || propertyName.IndexOf('\\') >= 0)
                throw new ArgumentException("This patcher accepts simple unescaped property names only.", nameof(propertyName));

            var replacement = (objectJson ?? string.Empty).Trim();
            if (replacement.Length < 2 || replacement[0] != '{' || replacement[replacement.Length - 1] != '}')
                throw new ArgumentException("Replacement value must be a JSON object.", nameof(objectJson));

            var open = SkipWhitespace(json, 0);
            if (open >= json.Length || json[open] != '{') throw new FormatException("Root JSON value must be an object.");
            var rootClose = FindMatchingContainerEnd(json, open);
            if (rootClose < 0) throw new FormatException("Root JSON object is not closed.");
            if (SkipWhitespace(json, rootClose + 1) != json.Length) throw new FormatException("Unexpected text follows the root JSON object.");

            var index = open + 1;
            while (index < rootClose)
            {
                index = SkipWhitespaceAndCommas(json, index, rootClose);
                if (index >= rootClose) break;
                if (json[index] != '"') throw new FormatException("Expected a top-level JSON property name.");

                var nameEnd = FindStringEnd(json, index);
                var name = json.Substring(index + 1, nameEnd - index - 1);
                var colon = SkipWhitespace(json, nameEnd + 1);
                if (colon >= rootClose || json[colon] != ':') throw new FormatException("Expected ':' after a top-level JSON property name.");
                var valueStart = SkipWhitespace(json, colon + 1);
                if (valueStart >= rootClose) throw new FormatException("Top-level JSON property is missing its value.");
                var valueEndExclusive = FindValueEndExclusive(json, valueStart, rootClose);

                if (string.Equals(name, propertyName, StringComparison.Ordinal))
                    return json.Substring(0, valueStart) + replacement + json.Substring(valueEndExclusive);

                index = valueEndExclusive;
            }

            var hasProperties = false;
            for (var i = open + 1; i < rootClose; i++)
            {
                if (!char.IsWhiteSpace(json[i]))
                {
                    hasProperties = true;
                    break;
                }
            }

            var newline = json.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            var insertion = (hasProperties ? "," : string.Empty)
                + newline
                + "  \"" + propertyName + "\": " + replacement
                + newline;
            return json.Substring(0, rootClose) + insertion + json.Substring(rootClose);
        }

        private static int FindValueEndExclusive(string json, int start, int rootClose)
        {
            var c = json[start];
            if (c == '"') return FindStringEnd(json, start) + 1;
            if (c == '{' || c == '[')
            {
                var end = FindMatchingContainerEnd(json, start);
                if (end < 0 || end > rootClose) throw new FormatException("Nested JSON container is not closed.");
                return end + 1;
            }

            var index = start;
            while (index < rootClose && json[index] != ',') index++;
            while (index > start && char.IsWhiteSpace(json[index - 1])) index--;
            if (index == start) throw new FormatException("JSON property has an empty primitive value.");
            return index;
        }

        private static int FindMatchingContainerEnd(string json, int open)
        {
            var opener = json[open];
            var closer = opener == '{' ? '}' : opener == '[' ? ']' : '\0';
            if (closer == '\0') throw new ArgumentException("Character is not a JSON container opener.", nameof(open));

            var stack = 0;
            var inString = false;
            var escaped = false;
            for (var i = open; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == opener) stack++;
                else if (c == closer)
                {
                    stack--;
                    if (stack == 0) return i;
                }
            }
            return -1;
        }

        private static int FindStringEnd(string json, int quoteStart)
        {
            var escaped = false;
            for (var i = quoteStart + 1; i < json.Length; i++)
            {
                var c = json[i];
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') return i;
            }
            throw new FormatException("JSON string is not closed.");
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            return index;
        }

        private static int SkipWhitespaceAndCommas(string text, int index, int limit)
        {
            while (index < limit && (char.IsWhiteSpace(text[index]) || text[index] == ',')) index++;
            return index;
        }
    }
}
