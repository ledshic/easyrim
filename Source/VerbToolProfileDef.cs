using System.Collections.Generic;
using Verse;

namespace EasyMode
{
    public class VerbToolProfileDef : Def
    {
        public List<VerbProperties> verbs;
        public List<Tool> tools;
    }

    public class HediffCompProperties_VerbGiverProfiled : HediffCompProperties_VerbGiver
    {
        public string verbToolProfileDef;
        public bool appendExisting = false;

        public HediffCompProperties_VerbGiverProfiled()
        {
            compClass = typeof(HediffComp_VerbGiver);
        }

        public override void PostLoad()
        {
            base.PostLoad();

            if (string.IsNullOrEmpty(verbToolProfileDef))
            {
                return;
            }

            VerbToolProfileDef profile = DefDatabase<VerbToolProfileDef>.GetNamedSilentFail(verbToolProfileDef);
            if (profile == null)
            {
                return;
            }

            if (appendExisting)
            {
                if (profile.verbs != null && profile.verbs.Count > 0)
                {
                    if (verbs == null)
                    {
                        verbs = new List<VerbProperties>();
                    }

                    verbs.AddRange(profile.verbs);
                }

                if (profile.tools != null && profile.tools.Count > 0)
                {
                    if (tools == null)
                    {
                        tools = new List<Tool>();
                    }

                    tools.AddRange(profile.tools);
                }
            }
            else
            {
                verbs = profile.verbs != null ? new List<VerbProperties>(profile.verbs) : null;
                tools = profile.tools != null ? new List<Tool>(profile.tools) : null;
            }

            if (tools != null)
            {
                for (int i = 0; i < tools.Count; i++)
                {
                    tools[i].id = i.ToString();
                }
            }
        }
    }
}
