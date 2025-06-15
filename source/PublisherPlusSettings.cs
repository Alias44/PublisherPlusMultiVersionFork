using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PublisherPlus
{
    public class PublisherPlusSettings : ModSettings
    {

        public static bool useGitIgnore;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref useGitIgnore, "useGitIgnore", false);
        }
    }
}
