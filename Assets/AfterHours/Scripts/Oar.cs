using System.Collections;
using UnityEngine;
namespace AfterHours
{
    // An oar held in an oarlock on the side of the boat. It can only turn about the lock, never slide through it, so the handle
    // swings one way inside the boat while the blade swings the other way out over the water.
    // The player faces the bow, so the drive is a push: handles forward, blades sweeping back through the water. Drawing the
    // handles back toward the body lifts the blades clear and sets up the next stroke.
    // The oar's own transform sits at the lock; its local +Z runs out along the shaft toward the blade.
    public class Oar : MonoBehaviour
    {
        // -1 is the left (port) side, +1 the right (starboard) side.
        public int side=1;
        public const float InboardLength=.95f,OutboardLength=1.75f;
        // Angles in degrees. A sweep angle of 0 points straight out to the side, and plus turns the blade toward the bow.
        public const float RestAngle=-12f,RestPitch=26f,MaxSweep=40f,CatchAngle=8f,FinishAngle=-8f,MinPitch=22f,MaxPitch=38f;
        public float angle{get;private set;}=RestAngle;
        // How far the blade end dips toward the water; plus is down.
        public float pitch{get;private set;}=RestPitch;
        Grabbable grab;bool caught;float velocity;Coroutine playing;
        void Awake(){grab=GetComponent<Grabbable>();Apply();}
        // Leaving the room mid-stroke stops the sweep; the oar then eases back to rest when the room returns.
        void OnDisable(){playing=null;}
        public bool Driven=>grab&&grab.held;
        public bool Playing=>playing!=null;
        // Where the handle end is, in world space.
        public Vector3 HandleEnd=>transform.position-transform.forward*InboardLength;
        public Vector3 BladeCentre=>transform.position+transform.forward*(OutboardLength-.3f);

        // Points the oar the way a hand pulls it: the shaft always runs from the hand, through the lock, out over the water.
        public void Aim(Vector3 hand)
        {
            var parent=transform.parent;
            Vector3 offset=parent?parent.InverseTransformPoint(hand)-transform.localPosition:hand-transform.position;
            float outward=-offset.x*side,along=-offset.z,flat=Mathf.Sqrt(outward*outward+along*along);
            float target=Mathf.Clamp(Mathf.Atan2(along,Mathf.Max(outward,.05f))*Mathf.Rad2Deg,-MaxSweep,MaxSweep);
            // A raised handle puts the blade down in the water; a lowered one lifts it out.
            float lift=Mathf.Clamp(Mathf.Atan2(offset.y,Mathf.Max(flat,.2f))*Mathf.Rad2Deg,MinPitch,MaxPitch);
            float dt=Mathf.Max(Time.deltaTime,.0001f),before=angle;
            angle+=(target-angle)*(1-Mathf.Exp(-22*dt));
            velocity+=((angle-before)/dt-velocity)*(1-Mathf.Exp(-12*dt));
            // Sweeping back (the drive) presses the blade a little deeper; sweeping forward (the recovery) lifts it.
            float bias=Mathf.Clamp(-velocity/60f,-1,1)*5f;
            pitch+=(Mathf.Clamp(lift+bias,18f,42f)-pitch)*(1-Mathf.Exp(-12*dt));
            Apply();
        }
        // True once for each stroke: the blade went forward to the catch, then back through the water to the finish.
        public bool TakeStroke()
        {
            if(angle>=CatchAngle)caught=true;
            if(caught&&angle<=FinishAngle){caught=false;return true;}
            return false;
        }
        public void LetGo(){caught=false;velocity=0;}
        public void ResetPose(){if(playing!=null)StopCoroutine(playing);playing=null;caught=false;velocity=0;angle=RestAngle;pitch=RestPitch;Apply();}
        void Update()
        {
            // Let go, the oar eases back to rest with the blade just above the water.
            if(Driven||playing!=null)return;
            if(Mathf.Abs(angle-RestAngle)<.05f&&Mathf.Abs(pitch-RestPitch)<.05f)return;
            float k=1-Mathf.Exp(-5*Time.deltaTime);angle+=(RestAngle-angle)*k;pitch+=(RestPitch-pitch)*k;Apply();
        }
        // The accessible stroke (the ROW button, or a stroke made with the other hand): the oar sweeps a full stroke by itself.
        public void PlayStroke()
        {
            if(Driven||!isActiveAndEnabled)return;
            if(playing!=null)StopCoroutine(playing);
            playing=StartCoroutine(Sweep());
        }
        IEnumerator Sweep()
        {
            yield return Glide(18,30,.22f);
            yield return Glide(-22,37,.5f);
            yield return Glide(RestAngle,RestPitch,.32f);
            playing=null;
        }
        IEnumerator Glide(float toAngle,float toPitch,float seconds)
        {
            float a=angle,p=pitch;
            for(float t=0;t<1;)
            {
                t=Mathf.Min(1,t+Time.deltaTime/seconds);float s=Mathf.SmoothStep(0,1,t);
                angle=Mathf.Lerp(a,toAngle,s);pitch=Mathf.Lerp(p,toPitch,s);Apply();yield return null;
            }
        }
        void Apply()
        {
            float a=angle*Mathf.Deg2Rad,p=pitch*Mathf.Deg2Rad;
            var direction=new Vector3(side*Mathf.Cos(a)*Mathf.Cos(p),-Mathf.Sin(p),Mathf.Sin(a)*Mathf.Cos(p));
            var local=Quaternion.LookRotation(direction,Vector3.up);
            transform.rotation=(transform.parent?transform.parent.rotation:Quaternion.identity)*local;
        }
    }
}
