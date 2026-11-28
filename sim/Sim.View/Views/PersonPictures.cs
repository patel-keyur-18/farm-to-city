using System;

namespace Sim.View.Views
{
    /// <summary>The pictures of people in the UI (spec 032 §3.1, US-276).</summary>
    public static class PersonPictures
    {
        /// <summary>
        /// The picture the UI shows for <paramref name="picture"/>: wherever it names a person (<c>person_&lt;kind&gt;</c>, <c>people</c>) that is
        /// their portrait, because a 6-head body's face is too small to see as an icon; full bodies are the map's. Any other picture is itself.
        /// </summary>
        public static string InUi(string picture)
        {
            if (picture == "people" || picture == "person_founder") return "portrait_founder";
            return picture != null && picture.StartsWith("person_", StringComparison.Ordinal) ? "portrait_" + picture.Substring("person_".Length) : picture;
        }
    }
}
