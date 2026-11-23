using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LittlePlaygrounds;

public enum PlaySource{Unknown,Learning,Idle,Invitation,Queue,FriendCall,CareCall,WatchCall,RecreationCall,CareWork,Recreation,Ordered}

// One logged ride (or one play decision). Plain values only: a pawn or a ride that is gone later still reads fine.
public sealed class PlayRecord:IExposable
{
    public bool decision,child,shared,alone;
    public int tick,end,siteId=-1,pawnId=-1;
    public string siteLabel,siteKind,pawnLabel,source,mode,with,watchers,hold,why;
    public void ExposeData()
    {
        Scribe_Values.Look(ref decision,"d");Scribe_Values.Look(ref child,"c");Scribe_Values.Look(ref shared,"s");Scribe_Values.Look(ref alone,"a");
        Scribe_Values.Look(ref tick,"t");Scribe_Values.Look(ref end,"e");Scribe_Values.Look(ref siteId,"si",-1);Scribe_Values.Look(ref pawnId,"pi",-1);
        Scribe_Values.Look(ref siteLabel,"sl");Scribe_Values.Look(ref siteKind,"sk");Scribe_Values.Look(ref pawnLabel,"pl");
        Scribe_Values.Look(ref source,"src");Scribe_Values.Look(ref mode,"m");Scribe_Values.Look(ref with,"w");
        Scribe_Values.Look(ref watchers,"wa");Scribe_Values.Look(ref hold,"h");Scribe_Values.Look(ref why,"y");
    }
}

public sealed class PlaygroundRecords:MapComponent
{
    public const int Keep=600,SampleTicks=30,RecentTicks=3*GenDate.TicksPerDay;
    public static bool On=>LittlePlaygroundsMod.Settings.recordPlay;
    static int Now=>Find.TickManager.TicksGame;

    // Map-owned, never static: loadIDs are only unique within a game. Not saved; loaded rides show "unknown".
    sealed class Tagged{public PlaySource source;public string note;public Pawn companion;public int tick,held=-1;public string hold;
        public readonly HashSet<Pawn> awaited=new();}
    readonly Dictionary<int,Tagged> tags=new();
    static Tagged TagOf(Job job,bool create)
    {
        var tags=job?.targetA.Thing?.Map?.GetComponent<PlaygroundRecords>()?.tags;
        if(tags==null)return null;
        if(tags.TryGetValue(job.loadID,out var t)||!create)return t;
        if(tags.Count>400)foreach(var old in tags.OrderBy(kv=>kv.Value.tick).Take(100).Select(kv=>kv.Key).ToList())tags.Remove(old);
        return tags[job.loadID]=new Tagged{tick=Now};
    }
    public static void Tag(Job job,PlaySource source,string note=null)
    {if(!On||job==null)return;var t=TagOf(job,true);if(t==null)return;t.source=source;if(note!=null)t.note=t.note==null?note:note+" · "+t.note;}
    public static void Note(Job job,string note,Pawn companion=null)
    {if(!On||job==null)return;var t=TagOf(job,true);if(t==null)return;t.note=t.note==null?note:t.note+" · "+note;if(companion!=null)t.companion=companion;}
    // Whom a rider waits for at the entrance, and how the wait ended. A wait that ends because someone got on counts as
    // the companion arriving when that someone is one of the pawns it waited for.
    public static void Awaiting(Job job,IEnumerable<Pawn> walkers)
    {if(!On||job==null)return;TagOf(job,true)?.awaited.UnionWith(walkers);}
    public static void Held(Job job,int ticks,string outcome,IEnumerable<Pawn> riding=null)
    {
        if(!On||job==null)return;var t=TagOf(job,true);if(t==null||t.held>=0)return;
        var arrived=riding?.Where(t.awaited.Contains).ToList();
        t.held=ticks;t.hold=arrived?.Count>0?"LP_Rec_HoldArrivedName".Translate(string.Join(", ",arrived.Select(p=>p.LabelShort))).ToString():outcome;
    }

    // A child's decision is logged only once its job is really running (map components tick after the pawns).
    sealed class Pending{public Pawn child;public int job;public PlaySource source;public string text;}
    readonly List<Pending> pending=new();
    public static void Decided(Pawn child,Job job,PlaySource source)
    {
        if(!On||job==null||child?.Map==null)return;
        child.Map.GetComponent<PlaygroundRecords>().pending.Add(new Pending{child=child,job=job.loadID,source=source,text=TagOf(job,false)?.note});
    }

    sealed class Session{public Pawn pawn;public Thing site;public int job,start,last;public bool shared,ordered,care;
        public readonly Dictionary<Pawn,string> with=new();public readonly HashSet<Pawn> watchers=new();public Tagged tag;}
    readonly Dictionary<long,Session> open=new();
    List<PlayRecord> records=new();
    string logContext;
    public IReadOnlyList<PlayRecord> Records=>records;
    public PlaygroundRecords(Map map):base(map){}
    public void ClearTransient(){tags.Clear();open.Clear();pending.Clear();}

    public override void MapComponentTick()
    {
        if(!On){ClearTransient();return;}
        if(pending.Count>0)
        {
            foreach(var d in pending)if(d.child?.Spawned==true&&d.child.CurJob?.loadID==d.job)
                Add(new PlayRecord{decision=true,child=true,tick=Now,end=Now,pawnId=d.child.thingIDNumber,pawnLabel=d.child.LabelShort,
                    source=Label(d.source),why=d.text});
            pending.Clear();
        }
        if((Now+map.uniqueID)%SampleTicks==0)Sample();
    }

    public static bool Mounted(JobDriver d)=>d switch
    {
        JobDriver_RideSwing s=>s.Age>=0,JobDriver_RideSeesaw s=>s.Age>=0,JobDriver_GardenVisit g=>g.Age>=0,
        JobDriver_VisitPlayground v=>v.Age>=0,JobDriver_RideSlide s=>s.Age>=0,JobDriver_ActiveRide a=>a.Age>=0,_=>false
    };
    // The job's own report ("gardening in the corner"); a driver that throws falls back to the job's name.
    public static string JobText(Pawn p)
    {
        if(p?.CurJob==null)return "-";
        try{return p.jobs.curDriver?.GetReport()?.CapitalizeFirst()??p.CurJobDef.label;}
        catch{return p.CurJobDef?.label??"?";}
    }
    static string Role(Pawn p)=>PlaygroundToddlers.Walking(p)||p.DevelopmentalStage.Child()?"child":PlaygroundCare.IsCare(p.CurJobDef)?"care":"adult";

    void Sample()
    {
        int now=Now;
        var riders=new List<(Pawn p,JobDriver d,Thing site)>();var watching=new Dictionary<Pawn,List<Pawn>>();
        foreach(var p in map.mapPawns.AllPawnsSpawned)
        {
            if(!p.RaceProps.Humanlike||p.jobs?.curDriver is not {} d)continue;
            if(d is JobDriver_WatchChildPlay&&d.CurToilIndex>=1&&d.job.targetB.Thing is Pawn kid)
            {if(!watching.TryGetValue(kid,out var list))watching[kid]=list=new();list.Add(p);}
            else if(Mounted(d)&&d.job.targetA.Thing is {Spawned:true} site)riders.Add((p,d,site));
        }
        foreach(var r in riders)
        {
            long key=((long)r.p.thingIDNumber<<32)|(uint)r.d.job.loadID;
            if(!open.TryGetValue(key,out var s))
                open[key]=s=new Session{pawn=r.p,site=r.site,job=r.d.job.loadID,start=now,shared=PlaygroundSocialAdapter.Invitable(r.site),
                    ordered=r.d.job.playerForced,care=PlaygroundCare.IsCare(r.d.job.def),tag=TagOf(r.d.job,false)};
            s.last=now;
            foreach(var o in riders)if(o.p!=r.p&&o.site==r.site)s.with[o.p]=Role(o.p);
            if(watching.TryGetValue(r.p,out var w))s.watchers.UnionWith(w);
        }
        foreach(var key in open.Where(kv=>kv.Value.last!=now).Select(kv=>kv.Key).ToList()){Finish(open[key]);open.Remove(key);}
    }

    void Finish(Session s)
    {
        bool child=PlaygroundToddlers.Walking(s.pawn)||s.pawn.DevelopmentalStage.Child();
        var roles=s.with.Values.ToList();
        var modes=new List<string>();
        if(child)
        {
            if(roles.Contains("child"))modes.Add("Children");
            if(roles.Contains("care"))modes.Add("WithCare");
            if(roles.Contains("adult"))modes.Add("WithRec");
            if(s.watchers.Count>0)modes.Add("Watched");
            if(modes.Count==0)modes.Add("ChildAlone");
        }
        else modes.Add(roles.Contains("child")?(s.care?"AdultCare":"AdultWithChild"):roles.Count>0?"Adults":"AdultAlone");
        var source=s.tag?.source??(s.ordered?PlaySource.Ordered:s.care?PlaySource.CareWork:PlaySource.Unknown);
        var why=new List<string>();
        bool alone=s.with.Count==0;
        if(alone&&s.shared)
        {
            if(s.tag?.note!=null)why.Add(s.tag.note);
            if(s.tag?.companion is {} c&&!s.with.ContainsKey(c))
                why.Add("LP_Rec_CompanionMissed".Translate(c.LabelShort,JobText(c)));
            if(why.Count==0)why.Add("LP_Rec_WhyUnknown".Translate(Label(source)));
        }
        else if(s.tag?.note!=null)why.Add(s.tag.note);
        Add(new PlayRecord{tick=s.start,end=s.last,siteId=s.site.thingIDNumber,siteLabel=s.site.LabelCap,siteKind=s.site.def.defName,
            pawnId=s.pawn.thingIDNumber,pawnLabel=s.pawn.LabelShort,child=child,shared=s.shared,alone=alone,source=Label(source),
            mode=string.Join("+",modes),with=string.Join(", ",s.with.Select(kv=>kv.Key.LabelShort+"("+("LP_Rec_Role_"+kv.Value).Translate()+")")),
            watchers=string.Join(", ",s.watchers.Select(p=>p.LabelShort)),
            hold=s.tag?.held>0?"LP_Rec_Hold".Translate((s.tag.held/60f).ToString("0"),s.tag.hold).ToString():null,why=string.Join(" / ",why)});
        tags.Remove(s.job);
    }

    void Add(PlayRecord r)
    {
        records.Add(r);
        if(records.Count>Keep)records.RemoveRange(0,records.Count-Keep);
        Write(r);
    }

    // ---- shown to the player ----
    public static string Label(PlaySource s)=>("LP_Rec_Src_"+s).Translate();
    public static string ModeLabel(string mode)=>string.Join(" + ",mode.Split('+').Select(m=>("LP_Rec_Mode_"+m).Translate().ToString()));
    public static string Ago(int tick)=>Now-tick<GenDate.TicksPerHour/10?"LP_Rec_JustNow".Translate().ToString():"LP_Rec_Ago".Translate(PlaygroundCompanion.Hours(Now-tick)).ToString();

    // Info-panel line of a ride: rides in the last three days by how they were made.
    public static string InspectLine(Thing t)
    {
        if(!On||t?.Map==null)return null;
        var recent=t.Map.GetComponent<PlaygroundRecords>().records.Where(r=>!r.decision&&r.siteId==t.thingIDNumber&&r.end>=Now-RecentTicks).ToList();
        string line="LP_Rec_Inspect".Translate(recent.Count,recent.Count==0?"-":string.Join(", ",recent.GroupBy(r=>r.mode.Split('+')[0])
            .OrderByDescending(g=>g.Count()).Select(g=>("LP_Rec_Mode_"+g.Key).Translate()+" "+g.Count())));
        if(PlaygroundSocialAdapter.Invitable(t))line+="\n"+"LP_Rec_InspectAlone".Translate(recent.Count(r=>r.alone));
        return line;
    }

    // ---- file for the developer ----
    // Preserve the previous unversioned log. v2 adds a persisted map/world context and keeps two older segments.
    const long FileLimit=4*1024*1024;
    static string FilePath=>Path.Combine(GenFilePaths.SaveDataFolderPath,"LittlePlaygrounds","playlog-v2.tsv");
    public static string FileShown=>FilePath;
    static string Cell(string s)=>(s??"").Replace('\t',' ').Replace('\n',' ').Replace('\r',' ');
    void Write(PlayRecord r)
    {
        try
        {
            var path=FilePath;Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb=new StringBuilder();
            logContext??=Guid.NewGuid().ToString("N");
            int tile=map.Tile;Vector2 longLat=Find.WorldGrid.LongLatOf(tile);
            sb.Append(string.Join("\t",new[]{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),r.tick.ToString(),GenDate.DateFullStringWithHourAt(GenDate.TickGameToAbs(r.tick),longLat),
                logContext,map.uniqueID.ToString(),r.decision?"decision":"ride",Cell(r.siteLabel),r.siteId.ToString(),Cell(r.pawnLabel),r.child?"child":"adult",Cell(r.source),
                Cell(r.mode),Cell(r.with),Cell(r.watchers),((r.end-r.tick)/60f).ToString("0"),Cell(r.hold),Cell(r.why)})).Append('\n');
            AppendFile(path,sb.ToString());
        }
        catch(Exception e){Log.WarningOnce("[Little Playgrounds] play log file: "+e.Message,0x4C5050);}
    }

    static void AppendFile(string path,string row)
    {
        if(File.Exists(path)&&new FileInfo(path).Length+Encoding.UTF8.GetByteCount(row)>FileLimit)
        {
            if(File.Exists(path+".1"))MoveReplacing(path+".1",path+".2");
            MoveReplacing(path,path+".1");
        }
        const string header="realTime\ttick\tdate\tcontext\tmap\ttype\tride\trideId\tpawn\tgroup\tsource\tmode\twith\twatchers\tseconds\twaited\twhy\n";
        File.AppendAllText(path,(File.Exists(path)?"":header)+row);
    }

    // File.Move(source, destination, overwrite) is unavailable on RimWorld 1.6's Mono runtime.
    static void MoveReplacing(string source,string destination)
    {
        if(File.Exists(destination))File.Replace(source,destination,null);
        else File.Move(source,destination);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref records,"lpPlayLog",LookMode.Deep);
        Scribe_Values.Look(ref logContext,"lpPlayLogContext");
        if(Scribe.mode==LoadSaveMode.PostLoadInit){records??=new();ClearTransient();}
    }
}

// Debug-only viewer opened from mod settings; never occupies a slot in the colony's main button bar.
public sealed class Dialog_PlaygroundRecords:Window
{
    enum Page{Rides,Recent,Decisions,Children}
    Page page;Vector2 scroll;bool onlySelected;float height=400;
    public Dialog_PlaygroundRecords(){doCloseX=true;doCloseButton=true;forcePause=true;absorbInputAroundWindow=true;}
    public override Vector2 InitialSize=>new(1100,700);
    public static bool Show()
    {
        if(Current.Game==null||Find.CurrentMap==null||!PlaygroundRecords.On)return false;
        if(Find.WindowStack.WindowOfType<Dialog_PlaygroundRecords>()==null)Find.WindowStack.Add(new Dialog_PlaygroundRecords());
        return true;
    }
    static int Now=>Find.TickManager.TicksGame;

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font=GameFont.Medium;Widgets.Label(new Rect(inRect.x,inRect.y,inRect.width-30,32),"LP_Rec_Title".Translate());
        Text.Font=GameFont.Small;inRect.yMin+=38;inRect.yMax-=45;
        var map=Find.CurrentMap;
        if(map==null){Widgets.Label(inRect,"LP_Rec_NoMap".Translate());return;}
        if(!PlaygroundRecords.On){Widgets.Label(inRect,"LP_Rec_Off".Translate());return;}
        var log=map.GetComponent<PlaygroundRecords>();
        Text.Font=GameFont.Small;
        float x=0;
        foreach(Page p in Enum.GetValues(typeof(Page)))
        {
            var r=new Rect(inRect.x+x,inRect.y,150,30);
            if(Widgets.ButtonText(r,("LP_Rec_Page_"+p).Translate(),active:page!=p)){page=p;scroll=Vector2.zero;}
            x+=156;
        }
        var selected=Find.Selector.SingleSelectedThing;
        if(page==Page.Recent)Widgets.CheckboxLabeled(new Rect(inRect.x+x+10,inRect.y+4,260,24),"LP_Rec_OnlySelected".Translate(),ref onlySelected);
        Widgets.Label(new Rect(inRect.x,inRect.yMax-24,inRect.width,24),"LP_Rec_File".Translate(PlaygroundRecords.FileShown));
        var outer=new Rect(inRect.x,inRect.y+38,inRect.width,inRect.height-38-28);
        var view=new Rect(0,0,outer.width-18,height);
        Widgets.BeginScrollView(outer,ref scroll,view);
        float y=0;
        void Line(string text,float indent=0)
        {
            float h=Text.CalcHeight(text,view.width-indent);
            Widgets.Label(new Rect(indent,y,view.width-indent,h),text);y+=h+2;
        }
        switch(page)
        {
            case Page.Rides:
                // Most used first; rides never used stay listed (that is worth seeing), course pieces only when used.
                foreach(var site in RideThings(map).Select(s=>(s,n:log.Records.Count(r=>!r.decision&&r.siteId==s.thingIDNumber)))
                    .Where(x=>x.n>0||x.s is not Building_PlaygroundFacility {IsCourse:true}).OrderByDescending(x=>x.n).Select(x=>x.s))
                {
                    var all=log.Records.Where(r=>!r.decision&&r.siteId==site.thingIDNumber).ToList();
                    var recent=all.Where(r=>r.end>=Now-PlaygroundRecords.RecentTicks).ToList();
                    string modes=recent.Count==0?"-":string.Join(", ",recent.GroupBy(r=>r.mode).OrderByDescending(g=>g.Count())
                        .Select(g=>PlaygroundRecords.ModeLabel(g.Key)+" "+g.Count()));
                    string alone=PlaygroundSocialAdapter.Invitable(site)?" · "+"LP_Rec_InspectAlone".Translate(recent.Count(r=>r.alone)):"";
                    string last=all.Count==0?"LP_Rec_Never".Translate().ToString():PlaygroundRecords.Ago(all[all.Count-1].end);
                    Line($"<b>{site.LabelCap}</b> {site.Position} — "+"LP_Rec_RideLine".Translate(recent.Count,modes)+alone+" · "+"LP_Rec_Last".Translate(last));
                }
                break;
            case Page.Recent:
                foreach(var r in log.Records.Where(r=>!r.decision&&(!onlySelected||selected==null||r.siteId==selected.thingIDNumber)).Reverse().Take(150))
                {
                    Line($"<b>{PlaygroundRecords.Ago(r.end)}</b> {r.siteLabel} · {r.pawnLabel} · {r.source} · {PlaygroundRecords.ModeLabel(r.mode)}"
                        +(r.with.NullOrEmpty()?"":" · "+"LP_Rec_With".Translate(r.with))+(r.watchers.NullOrEmpty()?"":" · "+"LP_Rec_Watchers".Translate(r.watchers))
                        +" · "+"LP_Rec_Seconds".Translate(((r.end-r.tick)/60f).ToString("0")));
                    if(!r.hold.NullOrEmpty())Line(r.hold,24);
                    if(!r.why.NullOrEmpty())Line((r.alone&&r.shared?"LP_Rec_WhyAlone":"LP_Rec_HowCame").Translate(r.why),24);
                }
                break;
            case Page.Decisions:
                foreach(var r in log.Records.Where(r=>r.decision).Reverse().Take(150))
                    Line($"<b>{PlaygroundRecords.Ago(r.tick)}</b> {r.pawnLabel} ({r.source}): {r.why}");
                break;
            case Page.Children:
                var social=map.GetComponent<PlaygroundSocialMap>();var visits=map.GetComponent<PlaygroundMap>();
                foreach(var c in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Where(p=>p.RaceProps.Humanlike&&p.DevelopmentalStage.Child()))
                {
                    var last=log.Records.LastOrDefault(r=>!r.decision&&r.pawnId==c.thingIDNumber);
                    var hour=c.timetable?.CurrentAssignment??TimeAssignmentDefOf.Anything;
                    Line($"<b>{c.LabelShort}</b> ({c.ageTracker.AgeBiologicalYears}) — "+"LP_Rec_ChildLine".Translate(
                        c.needs?.learning==null?"-":c.needs.learning.CurLevelPercentage.ToStringPercent(),hour.LabelCap,
                        PlaygroundRecords.JobText(c),last==null?"LP_Rec_Never".Translate().ToString():PlaygroundRecords.Ago(last.end)+" "+last.siteLabel,
                        visits.Ready(c)?"LP_Rec_Ready".Translate().ToString():"LP_Rec_ReadyIn".Translate(PlaygroundCompanion.Hours(visits.TicksUntilReady(c))).ToString(),
                        social.MayCall(c)?"LP_Rec_Ready".Translate().ToString():"LP_Rec_ReadyIn".Translate(PlaygroundCompanion.Hours(social.CallCooldownLeft(c))).ToString()));
                }
                break;
        }
        if(Event.current.type==EventType.Layout)height=Mathf.Max(y,outer.height);
        Widgets.EndScrollView();
    }

    static IEnumerable<Thing> RideThings(Map map)=>map.listerThings.AllThings.Where(t=>t.Spawned&&t is Building b&&b.Faction==Faction.OfPlayer
        &&(t is Building_Swing or Building_Slide or Building_Seesaw or Building_GardenSeats or Building_ActiveRide
            ||t is Building_PlaygroundFacility f&&f.Kind is not (ExpansionKind.Fence or ExpansionKind.Entrance or ExpansionKind.Fountain)))
        .OrderBy(t=>t.def.label).ThenBy(t=>t.thingIDNumber);
}
