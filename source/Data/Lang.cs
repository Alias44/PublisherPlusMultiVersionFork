using Verse;

namespace PublisherPlus.Data
{
  internal static class Lang
    {
      public static string Get(string key, params object[] args) => string.Format((Mod.Id + "." + key).Translate(), args);


      /// <summary>
      /// Wrapper function to prepend the mod id before calling the game's Translate() function
      /// </summary>
      /// <remarks>Does the same thing as Lang.Get(), but is a bit cleaner to read</remarks>
      /// <seealso cref="Lang.Get(string, object[])"/>
      public static string PrefixTranslate(this string key, params object[] args) => string.Format((Mod.Id + "." + key).Translate(), args);
    }
}
