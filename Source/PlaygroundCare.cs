using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

// Adults playing with children as Childcare work, the way vanilla Biotech handles "play with babies" and "teach child".
// Care jobs have their own JobDefs without a joyKind and reuse the ride drivers, so recreation checks never see them:
// the adult gains a little Social joy directly (as BabyPlayUtility.PlayTickCheckEnd does) and leaves when the child
// does. A child that starts to play can also call a Childcare adult over (PlaygroundCompanion).
public sealed class LittlePlaygroundsSettings:ModSettings
{
    public bool childcarePlay=true,recordPlay,facialPlay=true,sounds=true,runAround=true,runAroundIndoors,couplePlay=true,requireResearch=true,toddlerPlay=true;
    public float playgroundLearningWeight=PlaygroundLearning.DefaultWeight,soundVolume=1f,runAroundChance=PlaygroundRunning.DefaultChance;
    public HashSet<string> mutedRides=new();   // LPSound.Rides the player switched off
    public override void ExposeData()
    {
        base.ExposeData();Scribe_Values.Look(ref childcarePlay,"childcarePlay",true);Scribe_Values.Look(ref recordPlay,"recordPlay",false);
        Scribe_Values.Look(ref facialPlay,"facialPlay",true);
        Scribe_Values.Look(ref runAround,"runAround",true);Scribe_Values.Look(ref runAroundIndoors,"runAroundIndoors",false);
        Scribe_Values.Look(ref runAroundChance,"runAroundChance",PlaygroundRunning.DefaultChance);
        Scribe_Values.Look(ref couplePlay,"couplePlay",true);
        Scribe_Values.Look(ref requireResearch,"requireResearch",true);
        Scribe_Values.Look(ref toddlerPlay,"toddlerPlay",true);
        Scribe_Values.Look(ref sounds,"sounds",true);Scribe_Values.Look(ref soundVolume,"soundVolume",1f);
        Scribe_Collections.Look(ref mutedRides,"mutedRides",LookMode.Value);
        Scribe_Values.Look(ref playgroundLearningWeight,"playgroundLearningWeight",PlaygroundLearning.DefaultWeight);
        if(Scribe.mode==LoadSaveMode.PostLoadInit){playgroundLearningWeight=PlaygroundLearning.NormalizeWeight(playgroundLearningWeight);mutedRides??=new();}
    }
}

public sealed class LittlePlaygroundsMod:Mod
{
    public static LittlePlaygroundsSettings Settings{get;private set;}=new();
    public static ModContentPack Pack{get;private set;}
    // The ride sound list starts folded each game. The page scrolls when the open list no longer fits the window.
    public static bool SoundsOpen;
    public static float ContentHeight=560f;
    static Vector2 scroll;
    public LittlePlaygroundsMod(ModContentPack content):base(content){Pack=content;Settings=GetSettings<LittlePlaygroundsSettings>();}
    public override string SettingsCategory()=>"Little Playgrounds";
    public override void WriteSettings(){PlaygroundLearning.ApplyWeight();base.WriteSettings();}
    public override void DoSettingsWindowContents(Rect inRect)
    {
        var view=new Rect(0f,0f,inRect.width-20f,ContentHeight);
        Widgets.BeginScrollView(inRect,ref scroll,view);
        var list=new Listing_Standard{maxOneColumn=true};list.Begin(view);
        bool wasResearch=Settings.requireResearch;
        list.CheckboxLabeled("LP_Setting_RequireResearch".Translate(PlaygroundResearch.ProjectLabel),ref Settings.requireResearch,
            "LP_Setting_RequireResearchTip".Translate(PlaygroundResearch.ProjectLabel));
        if(wasResearch!=Settings.requireResearch)PlaygroundResearch.Apply();
        list.Gap(18f);
        if(ModsConfig.BiotechActive)
        {
            var weightLabel=list.Label("LP_Setting_LearningWeight".Translate(Settings.playgroundLearningWeight.ToString("0.0")));
            TooltipHandler.TipRegion(weightLabel,"LP_Setting_LearningWeightTip".Translate());
            var weight=PlaygroundLearning.NormalizeWeight(list.Slider(Settings.playgroundLearningWeight,PlaygroundLearning.MinWeight,PlaygroundLearning.MaxWeight));
            if(weight!=Settings.playgroundLearningWeight){Settings.playgroundLearningWeight=weight;PlaygroundLearning.ApplyWeight();}
            list.Label("LP_Setting_LearningWeightDescription".Translate());
            GUI.color=Color.gray;list.Label("LP_Setting_LearningWeightTiming".Translate());GUI.color=Color.white;
            list.Gap(18f);
        }
        list.CheckboxLabeled("LP_Setting_ChildcarePlay".Translate(),ref Settings.childcarePlay,"LP_Setting_ChildcarePlayTip".Translate());
        list.Gap(6f);GUI.color=Color.gray;list.Label("LP_Setting_ChildcarePlayTip".Translate());GUI.color=Color.white;
        list.Gap(18f);
        PlaygroundRunning.DrawSettings(list);
        list.Gap(18f);
        list.CheckboxLabeled("LP_Setting_CouplePlay".Translate(),ref Settings.couplePlay,"LP_Setting_CouplePlayTip".Translate());
        list.Gap(18f);
        ToddlerSetting(list);
        list.Gap(18f);
        bool wasFacial=Settings.facialPlay;
        list.CheckboxLabeled("LP_Setting_FacialPlay".Translate(),ref Settings.facialPlay,"LP_Setting_FacialPlayTip".Translate());
        if(wasFacial!=Settings.facialPlay)PlaygroundFaces.SettingChanged();
        if(PlaygroundFaces.Faulted)list.Label("LP_Setting_FacialPlayError".Translate());
        list.Gap(18f);
        SoundSettings(list);
        list.Gap(18f);
        bool wasRecording=Settings.recordPlay;
        list.CheckboxLabeled("LP_Setting_RecordPlay".Translate(),ref Settings.recordPlay,"LP_Setting_RecordPlayTip".Translate());
        if(wasRecording&&!Settings.recordPlay&&Current.Game!=null)
            foreach(var map in Find.Maps)map.GetComponent<PlaygroundRecords>().ClearTransient();
        list.Gap(6f);GUI.color=Color.gray;list.Label("LP_Setting_RecordPlayTip".Translate()+"\n"+"LP_Rec_File".Translate(PlaygroundRecords.FileShown));GUI.color=Color.white;
        if(Settings.recordPlay)
        {
            list.Gap(6f);
            if(Current.Game!=null&&Find.CurrentMap!=null)
            {if(list.ButtonText("LP_Rec_Open".Translate()))Dialog_PlaygroundRecords.Show();}
            else list.Label("LP_Rec_NoMap".Translate());
        }
        ContentHeight=list.CurHeight+8f;
        list.End();
        Widgets.EndScrollView();
    }
    // Greyed and shown off without the Toddlers mod; the saved choice is kept for when it is installed.
    static void ToddlerSetting(Listing_Standard list)
    {
        bool active=PlaygroundToddlers.Active;
        string name="LP_Setting_ToddlerPlay".Translate(),missing="LP_Setting_ToddlerPlayMissing".Translate();
        string label=active?name:name+" ("+missing+")";
        string rides=PlaygroundToddlers.RideNames.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)?.label).Where(l=>l!=null).ToCommaList(useAnd:true);
        var rect=list.GetRect(Mathf.Max(Text.LineHeight,Text.CalcHeight(label,list.ColumnWidth-24f)));list.Gap(list.verticalSpacing);
        bool shown=active&&Settings.toddlerPlay;
        if(!active)GUI.color=Color.gray;
        Widgets.CheckboxLabeled(rect,label,ref shown,disabled:!active);
        GUI.color=Color.white;
        TooltipHandler.TipRegion(rect,"LP_Setting_ToddlerPlayTip".Translate(rides));
        if(active)Settings.toddlerPlay=shown;
    }
    static void SoundSettings(Listing_Standard list)
    {
        var head=list.GetRect(Text.LineHeight);list.Gap(list.verticalSpacing);
        var line=new Rect(head.x+24f,head.y,head.width-24f,head.height);
        Widgets.CheckboxLabeled(line,"LP_Setting_Sounds".Translate(),ref Settings.sounds);
        TooltipHandler.TipRegion(line,"LP_Setting_SoundsTip".Translate());
        if(!Settings.sounds)return;
        if(Widgets.ButtonImage(new Rect(head.x+2f,head.y+3f,16f,16f),SoundsOpen?TexButton.Collapse:TexButton.Reveal))SoundsOpen=!SoundsOpen;
        if(!SoundsOpen)return;
        // Indent moves only the left edge, so the column narrows with it or the rows run past the page.
        list.Indent(24f);list.ColumnWidth-=24f;
        list.Label("LP_Setting_SoundVolume".Translate(Settings.soundVolume.ToStringPercent()));
        Settings.soundVolume=Mathf.Round(list.Slider(Settings.soundVolume,0f,1.5f)*20f)/20f;
        list.Gap(6f);list.Label("LP_Setting_SoundRides".Translate());
        const float rowH=26f;
        var grid=list.GetRect((LPSound.Rides.Length+1)/2*rowH);
        float cell=(grid.width-24f)/2f;
        for(int i=0;i<LPSound.Rides.Length;i++)
        {
            string ride=LPSound.Rides[i];
            var def=DefDatabase<ThingDef>.GetNamedSilentFail(ride);if(def==null)continue;
            bool on=!Settings.mutedRides.Contains(ride),was=on;
            Widgets.CheckboxLabeled(new Rect(grid.x+i%2*(cell+24f),grid.y+i/2*rowH,cell,rowH-2f),def.LabelCap,ref on);
            if(on==was)continue;
            if(on)Settings.mutedRides.Remove(ride);else Settings.mutedRides.Add(ride);
        }
        list.ColumnWidth+=24f;list.Outdent(24f);
    }
}

public sealed class PlaygroundCareExtension:DefModExtension{}

[DefOf]
public static class CareDefOf
{
    public static JobDef LP_CareSwing,LP_CareSeesaw,LP_CareSand,LP_CareRoundabout,LP_CarePlayhouse,LP_CareBounceTogether,LP_WatchChildPlay;
    public static ThoughtDef LP_PlayedWithMe;
    public static AnimationDef LP_WatchChildPose;
    static CareDefOf()=>DefOfHelper.EnsureInitializedInCtor(typeof(CareDefOf));
}

public static class PlaygroundCare
{
    // Vanilla BabyPlayUtility.PlayTickCheckEnd: adult joy 0.000144 per tick, Social kind (the normal recreation rate).
    public const float JoyPerTick=0.000144f;
    const int MemoryTicks=600;

    public static bool IsCare(JobDef d)=>d?.GetModExtension<PlaygroundCareExtension>()!=null;
    public static Pawn Child(Job job)=>job?.targetB.Thing as Pawn;

    // The seat someone is actually playing on right now, bench included (not running around, not waiting in line).
    public static Thing PlaySite(Pawn child)
    {
        var job=child?.CurJob;var driver=child?.jobs?.curDriver;
        if(job==null||driver==null||job.def==FreePlayDefOf.LP_RunAround)return null;
        bool ours=driver is JobDriver_RideSwing or JobDriver_RideSlide or JobDriver_RideSeesaw or JobDriver_ActiveRide or JobDriver_VisitPlayground
            ||driver is JobDriver_GardenVisit;
        return ours?job.targetA.Thing:null;
    }

    // Shared rides an adult can join as the child's partner.
    public static JobDef JoinFor(Thing site)=>site switch
    {
        Building_Swing=>CareDefOf.LP_CareSwing,Building_Seesaw=>CareDefOf.LP_CareSeesaw,Building_Sandbox=>CareDefOf.LP_CareSand,
        Building_PlaygroundFacility f=>f.Kind switch
        {
            ExpansionKind.Roundabout=>CareDefOf.LP_CareRoundabout,ExpansionKind.Playhouse=>CareDefOf.LP_CarePlayhouse,
            ExpansionKind.SharedTrampoline=>CareDefOf.LP_CareBounceTogether,
            ExpansionKind.FloorRocker=>CompactDefOf.LP_CareFloorRocker,ExpansionKind.BallPit=>CompactDefOf.LP_CareBallPit,
            ExpansionKind.BubbleTub=>MorePlayDefOf.LP_CareBubbles,_=>null
        },
        _=>null
    };

    // What an adult stands by: the ride a child is on, or the one it is running around.
    public static Thing WatchSite(Pawn child)=>PlaygroundRunning.RunSite(child)??PlaySite(child);

    // Solo rides: the adult stands beside and watches instead.
    public static bool Watchable(Thing site)=>site is Building_Slide or Building_ActiveRide
        ||site is Building_PlaygroundFacility f&&f.Kind is ExpansionKind.SpringRider or ExpansionKind.Hopscotch or ExpansionKind.SplashPad
            or ExpansionKind.SteppingStone or ExpansionKind.BalanceLog or ExpansionKind.CompactSlide or ExpansionKind.PullBar or ExpansionKind.Kite or ExpansionKind.JumpRope;

    internal static bool CaredForByOther(Pawn child,Pawn adult)=>child.Map.mapPawns.SpawnedPawnsInFaction(child.Faction)
        .Any(p=>p!=adult&&(IsCare(p.CurJobDef)||p.CurJobDef==CareDefOf.LP_WatchChildPlay)&&Child(p.CurJob)==child);

    // Decide what an adult with Childcare work would do for this child, if anything.
    public static bool TryPlan(Pawn adult,Pawn child,out JobDef def,out Thing site,out IntVec3 watchCell)
    {
        def=null;site=null;watchCell=IntVec3.Invalid;
        if(!LittlePlaygroundsMod.Settings.childcarePlay||adult==null||child==null||adult==child)return false;
        if(!child.Spawned||child.Map!=adult.Map||!child.DevelopmentalStage.Child()||child.Faction!=adult.Faction
            ||child.Drafted||child.Downed||child.InMentalState)return false;
        site=WatchSite(child);
        if(site==null||!site.Spawned||CaredForByOther(child,adult))return false;
        // Nobody joins a run. A child running alone gets a watcher; two running together have each other.
        if(PlaygroundRunning.RunSite(child)!=null)
        {
            if(PlaygroundRunning.HasFriend(child)||!TryWatchCell(adult,site,out watchCell))return false;
            def=CareDefOf.LP_WatchChildPlay;return true;
        }
        var occupants=PlaygroundSocialAdapter.Occupants(site).ToList();
        if(occupants.Count!=1||occupants[0]!=child)return false;
        var join=JoinFor(site);
        if(join!=null)
        {
            if(!PlaygroundSocialAdapter.CanUse(site,adult))return false;
            def=join;return true;
        }
        if(Watchable(site)&&TryWatchCell(adult,site,out watchCell)){def=CareDefOf.LP_WatchChildPlay;return true;}
        return false;
    }

    // The native rectangle overload stops at the ride's footprint, so the ride itself cannot obscure its rider.
    // Walls and closed doors outside that footprint still block the view.
    public static bool HasWatchSight(Thing site,IntVec3 cell)=>site?.Spawned==true&&cell.IsValid
        &&GenSight.LineOfSight(cell,site.OccupiedRect().CenterCell,site.Map,CellRect.SingleCell(cell),site.OccupiedRect());

    // A visible free cell two to three cells from the ride, off its entries and exits, reachable and reservable.
    public static bool TryWatchCell(Pawn adult,Thing site,out IntVec3 cell)
    {
        cell=IntVec3.Invalid;var map=site.Map;var rect=site.OccupiedRect();
        var keepClear=PlaygroundSocialAdapter.Entries(site).ToList();
        if(site is Building_Slide slide)keepClear.Add(slide.ExitCell);
        if(site is Building_ActiveRide ride)keepClear.Add(ride.ExitCell);
        float best=float.MaxValue;
        foreach(var c in rect.ExpandedBy(3).Cells)
        {
            if(rect.ExpandedBy(1).Contains(c)||!c.InBounds(map)||!c.Standable(map)||c.IsForbidden(adult))continue;
            if(keepClear.Any(k=>k.IsValid&&k.InHorDistOf(c,1.5f)))continue;
            if(c.GetThingList(map).Any(t=>t is Building||t is Pawn))continue;
            if(!HasWatchSight(site,c))continue;
            if(!adult.CanReserve(c)||!adult.CanReach(c,PathEndMode.OnCell,Danger.Some))continue;
            float score=(c-rect.CenterCell).LengthHorizontalSquared*4+(c-adult.Position).LengthHorizontalSquared*.01f;
            if(score<best){best=score;cell=c;}
        }
        return cell.IsValid;
    }

    // Shared "leave together" rule for every care job: stop when the child is no longer on that ride.
    public static bool ShouldStop(Pawn adult,Job job)
    {
        if(job==null||!(IsCare(job.def)||job.def==CareDefOf.LP_WatchChildPlay))return false;
        if(!LittlePlaygroundsMod.Settings.childcarePlay)return true;
        var child=Child(job);
        return child==null||child.Dead||!child.Spawned||child.Map!=adult.Map||child.Downed||child.Drafted
            ||(job.def==CareDefOf.LP_WatchChildPlay?WatchSite(child):PlaySite(child))!=job.targetA.Thing;
    }

    // 'Played with me' once the adult has been on the ride for MemoryTicks. Both facts come from saved state (the
    // ride driver's age, the child's memory age), so saving and loading neither loses nor repeats the memory.
    public static void GainSocialJoy(Pawn adult,int delta,Thing source,float factor,bool memory)
    {
        float gain=JoyPerTick*delta*factor*(source?.GetStatValue(StatDefOf.JoyGainFactor)??1f);
        adult.needs?.joy?.GainJoy(gain,JoyKindDefOf.Social);
        if(!memory)return;
        int age=RideAge(adult.jobs?.curDriver);
        if(age<MemoryTicks)return;
        var memories=Child(adult.CurJob)?.needs?.mood?.thoughts?.memories;
        // A memory from this adult younger than this ride was given on this ride already.
        if(memories==null||memories.Memories.Any(m=>m.def==CareDefOf.LP_PlayedWithMe&&m.otherPawn==adult&&m.age<=age))return;
        memories.TryGainMemory(CareDefOf.LP_PlayedWithMe,adult);
    }
    internal static int RideAge(JobDriver d)=>d switch
    {
        JobDriver_RideSwing s=>s.Age,JobDriver_RideSeesaw s=>s.Age,JobDriver_GardenVisit g=>g.Age,JobDriver_VisitPlayground v=>v.Age,_=>-1
    };
}

public sealed class WorkGiver_PlayWithChild:WorkGiver_Scanner
{
    public override PathEndMode PathEndMode=>PathEndMode.Touch;
    public override ThingRequest PotentialWorkThingRequest=>ThingRequest.ForGroup(ThingRequestGroup.Pawn);
    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        =>pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction).Where(p=>p.DevelopmentalStage.Child());
    public override bool ShouldSkip(Pawn pawn,bool forced=false)=>!LittlePlaygroundsMod.Settings.childcarePlay
        ||pawn.DevelopmentalStage!=DevelopmentalStage.Adult||!PlaygroundUse.CanParticipate(pawn)||pawn.IsBurning();
    public override bool HasJobOnThing(Pawn pawn,Thing t,bool forced=false)=>PlaygroundCare.TryPlan(pawn,t as Pawn,out _,out _,out _);
    public override Job JobOnThing(Pawn pawn,Thing t,bool forced=false)
    {
        if(!PlaygroundCare.TryPlan(pawn,t as Pawn,out var def,out var site,out var cell))return null;
        var job=def==CareDefOf.LP_WatchChildPlay?JobMaker.MakeJob(def,site,t,cell):JobMaker.MakeJob(def,site,t);
        PlaygroundRecords.Tag(job,PlaySource.CareWork);return job;
    }
}

// Standing beside a solo ride and watching the child, with a little Social joy (half the playing rate).
public sealed class JobDriver_WatchChildPlay:JobDriver
{
    const int MaxTicks=3000;
    Pawn Child=>job.targetB.Thing as Pawn;
    public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetC,job,1,-1,null,errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(()=>pawn.Drafted||pawn.Downed||pawn.InMentalState||PlaygroundCare.ShouldStop(pawn,job));
        AddFinishAction(_=>ClearPose());
        yield return Toils_Goto.GotoCell(TargetIndex.C,PathEndMode.OnCell);
        var watch=ToilMaker.MakeToil("WatchChildPlay");
        watch.handlingFacing=true;watch.socialMode=RandomSocialMode.Normal;
        watch.defaultCompleteMode=ToilCompleteMode.Delay;watch.defaultDuration=MaxTicks;
        watch.initAction=()=>
        {
            if(!PlaygroundCare.HasWatchSight(job.targetA.Thing,pawn.Position)){EndJobWith(JobCondition.Incompletable);return;}
            FaceChild();
        };
        watch.tickIntervalAction=delta=>
        {
            if(pawn.IsHashIntervalTick(120,delta)&&!PlaygroundCare.HasWatchSight(job.targetA.Thing,pawn.Position))
            {EndJobWith(JobCondition.Incompletable);return;}
            FaceChild();
            PlaygroundCare.GainSocialJoy(pawn,delta,job.targetA.Thing,.5f,false);
        };
        watch.AddFinishAction(ClearPose);
        yield return watch;
    }
    void FaceChild()
    {
        // Native pose ownership lets other animation mods defer during this job, without patching their global code.
        var renderer=pawn.Drawer.renderer;
        if(renderer.CurAnimation==null)renderer.SetAnimation(CareDefOf.LP_WatchChildPose);
        if(Child!=null)pawn.rotationTracker.FaceTarget(Child);
    }
    void ClearPose()
    {
        var renderer=pawn.Drawer?.renderer;
        if(renderer?.CurAnimation==CareDefOf.LP_WatchChildPose)renderer.SetAnimation(null);
    }
}
