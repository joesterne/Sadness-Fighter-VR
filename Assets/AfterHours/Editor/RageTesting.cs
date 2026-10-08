using System.Collections.Generic;
using UnityEngine;
namespace AfterHours.Editor
{
    // Desktop stand-ins for swinging the bat. On desktop the bat is held low and to the right of the view, so turning
    // the view swings it: the tests look just above a part, then flick the view down so the barrel sweeps through it.
    public static class RageTesting
    {
        static GameDirector D=>GameDirector.Instance;
        public static Bat Bat=>D.rage.bat.GetComponent<Bat>();
        // Where a part's hit volume is centred.
        public static Vector3 Centre(Breakable part){var box=(BoxCollider)part.hitVolumes[0];return part.transform.TransformPoint(box.center);}
        // A point on the bat's barrel, for pointing at the bat on its rack.
        public static Vector3 BatMiddle=>D.rage.bat.transform.TransformPoint(0,0,.45f);
        // The view rotation at which the held bat's barrel passes through a point.
        public static Quaternion Through(Vector3 point)
        {
            var head=D.player.Head;var bat=Bat;
            var grip=head.InverseTransformPoint(bat.transform.position);var axis=head.InverseTransformDirection(bat.transform.forward);
            // The point along the barrel that is as far from the eye as the target.
            float r=Vector3.Distance(head.position,point),best=bat.length,error=float.MaxValue;
            for(float s=bat.barrelFrom;s<=bat.length;s+=.01f){float e=Mathf.Abs((grip+axis*s).magnitude-r);if(e<error){error=e;best=s;}}
            var local=grip+axis*best;
            return Quaternion.LookRotation(point-head.position,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(local,Vector3.up));
        }
        // One swing: the view first looks well away (so moving into position cannot brush the part), settles clear above it,
        // then sweeps down through it. A 50 degree sweep starts and ends about 40 cm from the part. Yields game-time waits.
        public static IEnumerable<float> Swing(Breakable part,float degrees=50,float seconds=.15f)
        {
            var head=D.player.Head;var through=Through(Centre(part));
            head.rotation=through*Quaternion.Euler(-85,0,0);yield return .3f;
            head.rotation=through*Quaternion.Euler(-degrees/2,0,0);yield return .35f;
            float start=Time.time;
            while(Time.time<start+seconds){head.rotation=through*Quaternion.Euler(Mathf.Lerp(-degrees/2,degrees/2,(Time.time-start)/seconds),0,0);yield return 0;}
            head.rotation=through*Quaternion.Euler(degrees/2,0,0);yield return .15f;
            head.rotation=through*Quaternion.Euler(-85,0,0);yield return .2f;
        }
        // Swings at a part until it breaks. Returns the number of swings taken through the out parameter's list.
        public static IEnumerable<float> BreakPart(Breakable part,List<int> swings)
        {
            int n=0;
            while(!part.Broken&&n<part.HitsToBreak*3){n++;foreach(var t in Swing(part))yield return t;}
            swings.Add(n);
        }
        // How far a seated player's hand (about 45 cm below the eye and 35 cm in front) is from the nearest point of a part.
        public static float SeatedReach(Vector3 eye,Vector3 forward,float side,Breakable part)
        {
            var flat=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,flat);
            var hand=eye+Vector3.down*.45f+flat*.35f+right*side;
            return Vector3.Distance(hand,part.hitVolumes[0].ClosestPoint(hand));
        }
    }
}
