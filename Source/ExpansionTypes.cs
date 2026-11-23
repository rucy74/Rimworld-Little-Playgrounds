using Verse;
using UnityEngine;

namespace LittlePlaygrounds;

// Shared rendering contract. X/east and Z/north are the map axes; no camera rotation.
public enum ExpansionKind { SpringRider, Roundabout, Playhouse, Drink, Hopscotch, SplashPad, SteppingStone, BalanceLog, Fence, Entrance, Fountain, SharedTrampoline, CompactSlide, FloorRocker, BallPit, BubbleTub, PullBar, Kite, JumpRope }
public sealed class ExpansionProperties:DefModExtension
{
    public ExpansionKind kind;
    public int seats=1;
    public bool woodenStep;
}

public static class ExpansionMotion
{
    // Contacts of the four metal nozzle pixels in ExpansionBoundaryAtlasV2.
    static readonly Vector3[] Nozzles={new(0,0,1.0812f),new(-1.1925f,0,.0212f),new(1.1819f,0,.0212f),new(0,0,-1.0653f)};
    public static Vector3 SplashNozzle(int nozzle)=>Nozzles[nozzle%4];
    public static float SplashPulse(float tick,int nozzle)=>.5f+.5f*Mathf.Sin((tick+nozzle*47)*.04f);
}

// ExpansionArt.Draw(kind, at, motion, activeMask, links) is owned by the visual implementation.
// motion: SpringRider=rock degrees, Roundabout=rotation degrees, other types=game tick.
// activeMask: bit per occupied/mounted slot. links: N/E/S/W neighbouring boundary/course pieces.
// Coordinates for pawn contacts (relative to building center):
// Spring contact is shared with RestyleArt.SpringContact; pawn proportions stay native.
// Roundabout circular plate radius1.30, riders radius.85, native pose height .25.
// Playhouse open-front3x3: keeper(.58,0,.1), customer(.65,.45,-.8); Y is local drawing depth in front of the counter.
// Drink3x1: users at x+/-.672, child z=.12/adult z=.22, facing north toward their respective bowls.
// Hopscotch 2x5: start(0,0,-2), ends(0,0,2); outlined cells/legible1-8.
// SplashPad3x3: four cardinal nozzle pixels use SplashNozzle for both rendering and avoidance.
// SteppingStone/BalanceLog 1x1: walkable center at0, height.08/.15; modular course links.
// Fence 1x1: low connected rails N/E/S/W. Entrance3x1: always-open passage, small sign.
// Fountain 3x3: central basin radius1.05 with visible gentle water; decorative.
