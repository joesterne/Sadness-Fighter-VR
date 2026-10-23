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
        // One chop: the view looks up, well clear of every web (so moving into position cuts nothing else), settles above the point,
        // and sweeps down through it. Yields game-time waits; zero means the next frame.
        public static IEnumerable<float> ChopAt(Vector3 point,float degrees=50,float seconds=.15f)
        {
            var head=D.player.Head;
            if(head.forward.y<.95f)foreach(var t in Raise())yield return t;
            var through=Through(point);
            head.rotation=through*Quaternion.Euler(-85,0,0);yield return .15f;
            head.rotation=through*Quaternion.Euler(-degrees/2,0,0);yield return .15f;
            float start=Time.time;
            while(Time.time<start+seconds){head.rotation=through*Quaternion.Euler(Mathf.Lerp(-degrees/2,degrees/2,(Time.time-start)/seconds),0,0);yield return 0;}
            head.rotation=through*Quaternion.Euler(degrees/2,0,0);yield return .1f;
            head.rotation=through*Quaternion.Euler(-85,0,0);yield return .1f;
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
        public static IEnumerable<float> Chop(int col)=>ChopAt(Between(col));
        // Chops every column once, left to right, in about seven seconds: well inside the first wait before a web grows back.
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
