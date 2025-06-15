using RimWorld;
using Verse;
using UnityEngine;
using PublisherPlus.Data;

namespace PublisherPlus
{
    public class PublisherPlus : Verse.Mod
    {
        public static PublisherPlusSettings settings;

        public PublisherPlus(ModContentPack content) : base(content)
        {
            settings = GetSettings<PublisherPlusSettings>();
        }

        public override string SettingsCategory() => "Settings.Title".PrefixTranslate();

        // Vanilla rendering, because the fancy checkbox in listingPlus isn't needed here
        public override void DoSettingsWindowContents(Rect inRect)
        {
            checked
            {
                Listing_Standard listing = new Listing_Standard();
                listing.Begin(inRect);

                listing.CheckboxLabeled("Settings.GitIgnore".PrefixTranslate(), ref PublisherPlusSettings.useGitIgnore, "Settings.GitIgnore.Tooltip".PrefixTranslate());
                listing.End();

                settings.Write();
            }
        }
    }
}
