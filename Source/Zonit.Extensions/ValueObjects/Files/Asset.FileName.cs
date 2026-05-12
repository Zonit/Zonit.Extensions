using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions;

public readonly partial struct Asset
{
    /// <summary>
    /// Represents a file name with extension, with validation for invalid characters.
    /// Nested within Asset - not intended for standalone use.
    /// </summary>
    public readonly struct FileName : IEquatable<FileName>, IComparable<FileName>, IParsable<FileName>
    {
        /// <summary>
        /// Maximum allowed length for a file name.
        /// </summary>
        public const int MaxLength = 255;

        /// <summary>
        /// Invalid characters for file names (based on Windows restrictions).
        /// </summary>
        private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0'];

        /// <summary>
        /// Empty file name instance.
        /// </summary>
        public static readonly FileName Empty = default;

        private readonly string? _value;

        /// <summary>
        /// The file name value. Never null.
        /// </summary>
        public string Value => _value ?? string.Empty;

        /// <summary>
        /// Indicates whether the file name has a meaningful value.
        /// </summary>
        public bool HasValue => !string.IsNullOrWhiteSpace(_value);

        /// <summary>
        /// Gets the file name without extension.
        /// </summary>
        public string NameWithoutExtension => HasValue
            ? Path.GetFileNameWithoutExtension(_value!)
            : string.Empty;

        /// <summary>
        /// Gets the file extension (with dot, e.g., ".png").
        /// </summary>
        public string Extension => HasValue
            ? Path.GetExtension(_value!)
            : string.Empty;

        /// <summary>
        /// Gets the file extension without dot (e.g., "png").
        /// </summary>
        public string ExtensionWithoutDot => Extension.TrimStart('.');

        /// <summary>
        /// Checks if the file has an extension.
        /// </summary>
        public bool HasExtension => !string.IsNullOrEmpty(Extension);

        /// <summary>
        /// Gets the MIME type based on file extension.
        /// </summary>
        public MimeType MimeType => MimeType.FromExtension(Extension);

        /// <summary>
        /// Creates a new file name with the specified value.
        /// </summary>
        public FileName(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

            var normalizedValue = value.Trim();

            if (normalizedValue.Length > MaxLength)
                throw new ArgumentException($"File name cannot exceed {MaxLength} characters.", nameof(value));

            if (normalizedValue.IndexOfAny(InvalidChars) >= 0)
                throw new ArgumentException("File name contains invalid characters.", nameof(value));

            // Check for reserved names (Windows)
            var nameWithoutExt = Path.GetFileNameWithoutExtension(normalizedValue).ToUpperInvariant();
            string[] reservedNames = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
            if (reservedNames.Contains(nameWithoutExt))
                throw new ArgumentException($"'{normalizedValue}' is a reserved file name.", nameof(value));

            _value = normalizedValue;
        }

        /// <summary>
        /// Creates a new file name with the specified base name and extension.
        /// </summary>
        public FileName(string baseName, string extension)
            : this($"{baseName}{(extension.StartsWith('.') ? extension : $".{extension}")}")
        {
        }

        /// <summary>
        /// Creates a new file name with different extension.
        /// </summary>
        public FileName WithExtension(string newExtension)
        {
            if (!HasValue)
                throw new InvalidOperationException("Cannot change extension of empty file name.");

            var ext = newExtension.StartsWith('.') ? newExtension : $".{newExtension}";
            return new FileName($"{NameWithoutExtension}{ext}");
        }

        /// <summary>
        /// Creates a new file name with a suffix added before the extension.
        /// </summary>
        public FileName WithSuffix(string suffix)
        {
            if (!HasValue)
                throw new InvalidOperationException("Cannot add suffix to empty file name.");

            return new FileName($"{NameWithoutExtension}{suffix}{Extension}");
        }

        /// <summary>
        /// Creates a unique file name by adding a number suffix if needed.
        /// </summary>
        public FileName MakeUnique(IEnumerable<string> existingNames)
        {
            if (!HasValue)
                throw new InvalidOperationException("Cannot make unique from empty file name.");

            var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existing.Contains(Value))
                return this;

            var baseName = NameWithoutExtension;
            var ext = Extension;
            var counter = 1;

            string newName;
            do
            {
                newName = $"{baseName}-{counter}{ext}";
                counter++;
            } while (existing.Contains(newName));

            return new FileName(newName);
        }

        /// <summary>
        /// Sanitizes a string to be a valid file name.
        /// </summary>
        public static FileName Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new FileName("file");

            var sanitized = input.Trim();

            foreach (var c in InvalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            sanitized = sanitized.Trim('.', ' ');

            if (sanitized.Length > MaxLength)
            {
                var ext = Path.GetExtension(sanitized);
                var name = Path.GetFileNameWithoutExtension(sanitized);
                var maxNameLength = MaxLength - ext.Length;
                sanitized = name[..Math.Min(name.Length, maxNameLength)] + ext;
            }

            return string.IsNullOrEmpty(sanitized)
                ? new FileName("file")
                : new FileName(sanitized);
        }

        public static implicit operator string(FileName fileName) => fileName.Value;

        public static implicit operator FileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Empty;

            return TryCreate(value, out var fileName) ? fileName : Empty;
        }

        public bool Equals(FileName other) =>
            string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is FileName other && Equals(other);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

        public static bool operator ==(FileName left, FileName right) => left.Equals(right);
        public static bool operator !=(FileName left, FileName right) => !left.Equals(right);

        public int CompareTo(FileName other) =>
            string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

        public static bool operator <(FileName left, FileName right) => left.CompareTo(right) < 0;
        public static bool operator <=(FileName left, FileName right) => left.CompareTo(right) <= 0;
        public static bool operator >(FileName left, FileName right) => left.CompareTo(right) > 0;
        public static bool operator >=(FileName left, FileName right) => left.CompareTo(right) >= 0;

        public override string ToString() => Value;

        public static FileName Create(string value) => new(value);

        public static bool TryCreate(string? value, out FileName fileName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                fileName = Empty;
                return false;
            }

            try
            {
                fileName = new FileName(value);
                return true;
            }
            catch (ArgumentException)
            {
                fileName = Empty;
                return false;
            }
        }

        public static FileName Parse(string s, IFormatProvider? provider)
        {
            if (TryParse(s, provider, out var result))
                return result;

            throw new FormatException($"Cannot parse '{s}' as FileName.");
        }

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out FileName result)
            => TryCreate(s, out result);
    }
}
