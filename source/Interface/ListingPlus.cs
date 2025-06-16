using UnityEngine;
using Verse;

namespace PublisherPlus.Interface
{
  internal class Listing_StandardPlus : Listing_Standard
  {
    /// <summary>
    /// A CheckboxLabeled, but allows modifying the highlight rectangle
    /// </summary>
    /// <param name="color">The color to paint the highlight rectangle</param>
    public void CheckboxLabeled(string label, ref bool checkOn, string tooltip, Color? color = null)
    {
      var previousColor = GUI.color;
      if (color != null) { GUI.color = color.Value; }

      base.CheckboxLabeled(label, ref checkOn, tooltip);

      GUI.color = previousColor;
    }
  }
}
