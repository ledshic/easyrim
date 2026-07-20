using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace Verse;

public class HediffDef : Def, IRenderNodePropertiesParent
{
    private class ReportStringOverride
    {
        public JobDef jobDef;

        [MustTranslate]
        public string reportString;
    }

    public Type hediffClass = typeof(Hediff);

    public List<HediffCompProperties> comps;

    [MustTranslate]
    public string descriptionShort;

    [NoTranslate]
    public string debugLabelExtra;

    public float initialSeverity = 0.5f;

    public float lethalSeverity = -1f;

    public List<HediffStage> stages;

    public bool tendable;

    public bool isBad = true;

    public ThingDef spawnThingOnRemoved;

    public float chanceToCauseNoPain;

    public bool canApplyDodChanceForCapacityChanges;

    public bool makesSickThought;

    public bool makesAlert = true;

    public NeedDef chemicalNeed;

    public float minSeverity;

    public float maxSeverity = float.MaxValue;

    public bool scenarioCanAdd;

    public List<HediffGiver> hediffGivers;

    public bool cureAllAtOnceIfCuredByItem;

    public TaleDef taleOnVisible;

    public bool recordDownedTale = true;

    public bool everCurableByItem = true;

    public List<string> tags;

    public bool priceImpact;

    public float priceOffset;

    public bool chronic;

    public bool keepOnBodyPartRestoration;

    public bool countsAsAddedPartOrImplant;

    public bool blocksSocialInteraction;

    public bool blocksSleeping;

    [MustTranslate]
    public string overrideTooltip;

    [MustTranslate]
    public string extraTooltip;

    [MustTranslate]
    public string inspectString;

    public bool levelIsQuantity;

    public bool removeOnDeathrestStart;

    public bool preventsCrawling;

    public bool preventsPregnancy;

    public bool preventsLungRot;

    public bool pregnant;

    public bool allowMothballIfLowPriorityWorldPawn;

    public List<string> removeWithTags;

    public List<BodyPartDef> onlyLifeThreateningTo;

    public bool canAffectBionicOrImplant = true;

    public bool alwaysShowSeverity;

    public bool showGizmosOnCorpse;

    public BodyPartDef defaultInstallPart;

    public Color? hairColorOverride;

    public List<HediffInfectionPathway> possiblePathways;

    public List<InfectionPathwayDef> givesInfectionPathways;

    public bool duplicationAllowed = true;

    public bool preventsDeath;

    public List<MeditationFocusDef> allowedMeditationFocusTypes;

    public List<AbilityDef> abilities;

    public bool isInfection;

    public bool forceRemoveOnResurrection;

    public bool organicAddedBodypart;

    public bool deprioritizeHealing;

    public bool clearsEgo;

    public List<Aptitude> aptitudes;

    public SimpleCurve removeOnRedressChanceByDaysCurve = new SimpleCurve
    {
        new CurvePoint(0f, 0f),
        new CurvePoint(1f, 0f)
    };

    public bool removeOnQuestLodgers;

    public List<PawnKindDef> removeOnRedressIfNotOfKind;

    public bool displayWound;

    public float? woundAnchorRange;

    public Color defaultLabelColor = Color.white;

    private List<PawnRenderNodeProperties> renderNodeProperties;

    public Color? skinColorOverride;

    public Color? skinColorTint;

    public float skinColorTintStrength = 0.5f;

    public ShaderTypeDef skinShader;

    public bool forceRenderTreeRecache;

    public InjuryProps injuryProps;

    public AddedBodyPartProps addedPartProps;

    private List<ReportStringOverride> reportStringOverrides;

    [MustTranslate]
    public string labelNoun;

    [MustTranslate]
    public string battleStateLabel;

    [MustTranslate]
    public string labelNounPretty;

    [MustTranslate]
    public string targetPrefix;

    private bool alwaysAllowMothballCached;

    private bool alwaysAllowMothball;

    private string descriptionCached;

    private Dictionary<JobDef, string> reportStringOverridesDict;

    private Hediff concreteExampleInt;

    public bool HasDefinedGraphicProperties
    {
        get
        {
            if (renderNodeProperties.NullOrEmpty())
            {
                return skinShader != null;
            }
            return true;
        }
    }

    public List<PawnRenderNodeProperties> RenderNodeProperties => renderNodeProperties ?? PawnRenderUtility.EmptyRenderNodeProperties;

    public bool IsAddiction => typeof(Hediff_Addiction).IsAssignableFrom(hediffClass);

    public bool AlwaysAllowMothball
    {
        get
        {
            if (!alwaysAllowMothballCached)
            {
                alwaysAllowMothball = true;
                if (comps != null && comps.Count > 0)
                {
                    alwaysAllowMothball = false;
                }
                if (stages != null)
                {
                    for (int i = 0; i < stages.Count; i++)
                    {
                        HediffStage hediffStage = stages[i];
                        if (hediffStage.deathMtbDays > 0f || (hediffStage.hediffGivers != null && hediffStage.hediffGivers.Count > 0))
                        {
                            alwaysAllowMothball = false;
                        }
                    }
                }
                alwaysAllowMothballCached = true;
            }
            return alwaysAllowMothball;
        }
    }

    public Hediff ConcreteExample => concreteExampleInt ?? (concreteExampleInt = HediffMaker.Debug_MakeConcreteExampleHediff(this));

    public string Description
    {
        get
        {
            if (descriptionCached == null)
            {
                if (!descriptionShort.NullOrEmpty())
                {
                    descriptionCached = descriptionShort;
                }
                else
                {
                    descriptionCached = description;
                }
            }
            return descriptionCached;
        }
    }
}