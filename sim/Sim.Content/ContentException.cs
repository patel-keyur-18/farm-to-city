using System;

namespace Sim.Content
{
    /// <summary>A content file failed to parse or validate. <see cref="FieldPath"/> names the field, e.g. <c>clock.weatherSpellHours</c>.</summary>
    public sealed class ContentException : Exception
    {
        public string FieldPath { get; }

        public ContentException(string fieldPath, string message)
            : base(fieldPath + ": " + message)
        {
            FieldPath = fieldPath;
        }
    }
}
