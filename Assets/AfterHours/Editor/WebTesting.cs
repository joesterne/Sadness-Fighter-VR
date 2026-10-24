using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace AfterHours.Editor
{
    // Desktop stand-ins for swinging the machete in The NDA web. On desktop the machete is held low and to the right of the view,
    // so turning the view swings it: the tests look high above a column of webs, then flick the view down through both its webs.
    public static class WebTesting
    {
        static GameDirector D=>GameDirector.Instance;
        public static Machete Machete=>D.web.machete.GetComponent<Machete>();
        // The two webs of a column, low then high, and the point between them that one chop passes through.
        public static Cobweb[] Column(int col)=>new[]{D.web.webs[col*2],D.web.webs[col*2+1]};
        public static Vector3 Between(int col)=>(Column(col)[0].transform.position+Column(col)[1].transform.position)*.5f;
        public static int Columns=>D.web.webs.Length/2;
        // The view rotation at which the held blade passes through a point.
        public static Quaternion Through(Vector3 point)
        {
            var head=D.player.Head;var m=Machete;
            var grip=head.InverseTransformPoint(m.transform.position);var axis=head.InverseTransformDirection(m.transform.forward);
            float r=Vector3.Distance(head.position,point),best=m.length,error=float.MaxValue;
            for(float s=m.bladeFrom;s<=m.length;s+=.01f){float e=Mathf.Abs((grip+axis*s).magnitude-r);if(e<error){error=e;best=s;}}
            var local=grip+axis*best;
            return Quaternion.LookRotation(point-head.position,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(local,Vector3.up));
        }
        // One chop: the view looks up, well clear of every web (so moving into position cuts nothing else), and sweeps straight down
        // through the point to degrees/2 past it, as fast as a chop of `degrees` in `seconds`. (Jumping to a pose just above the point
        // first would itself count as a swing, and that pose already has the blade in the high webs.) Yields game-time waits; zero
        // means the next frame.
        public static IEnumerable<float> ChopAt(Vector3 point,float degrees=50,float seconds=.15f)=>Sweep(()=>Through(point),degrees,seconds);
        static IEnumerable<float> Sweep(System.Func<Quaternion> aim,float degrees,float seconds)
        {
            var head=D.player.Head;
            if(head.forward.y<.95f)foreach(var t in Raise())yield return t;
            var through=aim();
            head.rotation=through*Quaternion.Euler(-85,0,0);yield return .15f;
            float start=Time.time,sweep=seconds*(85+degrees/2)/degrees;
            while(Time.time<start+sweep){head.rotation=through*Quaternion.Euler(Mathf.Lerp(-85,degrees/2,(Time.time-start)/sweep),0,0);yield return 0;}
            head.rotation=through*Quaternion.Euler(degrees/2,0,0);yield return .1f;
            // Back up the same way. Snapping straight up in one frame would count as a swing along the chord between the two poses,
            // which passes through the next column's high web.
            start=Time.time;
            while(Time.time<start+sweep){head.rotation=through*Quaternion.Euler(Mathf.Lerp(degrees/2,-85,(Time.time-start)/sweep),0,0);yield return 0;}
            // End looking straight up (the aim leans down a little), so the next chop starts without the slow raise.
            head.rotation=Quaternion.Euler(-85,through.eulerAngles.y,0);yield return .1f;
        }
        // Turns the view to look at a point slowly enough to cut nothing: snapping it round would swing the machete through the webs.
        public static IEnumerable<float> TurnTo(Vector3 point)
        {
            var head=D.player.Head;var look=Quaternion.LookRotation(point-head.position);
            while(Quaternion.Angle(head.rotation,look)>.5f){head.rotation=Quaternion.RotateTowards(head.rotation,look,SlowTurn*Time.deltaTime);yield return 0;}
            head.rotation=look;
        }
        // Degrees per second at which turning the view moves the blade tip well under the machete's cutting speed.
        public const float SlowTurn=20f;
        // At rest the blade reaches through the webs straight ahead, so the view first turns up to the ceiling slowly enough to cut nothing.
        public static IEnumerable<float> Raise()
        {
            var head=D.player.Head;var up=Quaternion.Euler(-85,head.eulerAngles.y,0);
            while(Quaternion.Angle(head.rotation,up)>.5f){head.rotation=Quaternion.RotateTowards(head.rotation,up,SlowTurn*Time.deltaTime);yield return 0;}
            head.rotation=up;yield return .1f;
        }
        // A chop through one column. The desktop machete hangs to the right of the eye, so a sweep aimed straight at the point between
        // the two webs crosses the ring at a slant: it clips the next column's high web on the right. Turned 1.5 degrees left it passes
        // through both of the column's webs and no others (3 degrees is too far: it catches the low web of the column on the left).
        public static IEnumerable<float> Chop(int col)=>Sweep(()=>Quaternion.AngleAxis(-1.5f,Vector3.up)*Through(Between(col)),50,.15f);
        // Chops every column once, left to right, in about nine seconds: inside the first wait before a web grows back.
        public static IEnumerable<float> ClearAll(){for(int col=0;col<Columns;col++)foreach(var t in Chop(col))yield return t;}
        // How far a seated player's hand (about 45 cm below the eye and 25 cm out toward the web) is from the nearest point of a web.
        public static float SeatedReach(Vector3 eye,Cobweb web)
        {
            var toward=Vector3.ProjectOnPlane(web.transform.position-eye,Vector3.up).normalized;
            var hand=eye+Vector3.down*.45f+toward*.25f;
            return Vector3.Distance(hand,web.volume.ClosestPoint(hand));
        }
    }
}
