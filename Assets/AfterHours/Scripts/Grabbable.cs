using System.Collections;
using UnityEngine;
namespace AfterHours
{
    public class Grabbable : MonoBehaviour
    {
        public string label;
        public string category;
        public int room;
        public bool held;
        public bool processed;
        public bool paddle;
        // Held level, turning only with the hand's heading. A cup of coffee stays upright and lands standing.
        public bool upright;
        // A tool (the bat) is held in a fixed grip, stays in the hand when the pinch opens, and goes back to its rack when put down.
        public bool tool;
        // The boxes in the heavy room can be thrown: let go while the hand is moving and the box flies on, helped toward the truck.
        public bool throwable;
        public Transform Holder { get; private set; }
        // Gliding to a send target (or back home after a wrong tray). Drop zones wait until it lands.
        public bool sending { get; private set; }
        // In the air or rolling after a throw, until it is caught, picked up again or sent home.
        public bool thrown { get; private set; }
        public Rigidbody Body { get; private set; }
        Vector3 homePosition; Quaternion homeRotation;
        // Where the held box has been over the last moments, for the speed of a throw.
        const int Trail=24;
        readonly Vector3[] trailPosition=new Vector3[Trail];readonly float[] trailTime=new float[Trail];int trailHead,trailCount;
        Transform catchSlot; static float nextMissMessage;
        float nextStroke;Oar oar;
        Vector3 holdPosition; Quaternion holdRotation; float holdYaw;
        Vector3 pullFrom, sendPoint; Quaternion pullFromRotation, sendRotation; float pullStart=-1;
        const float PullTime=.18f;
        float nextImpact;
        void Awake() { Initialize(); }
        public void Initialize(){if(Body)return;Body=GetComponent<Rigidbody>();oar=GetComponent<Oar>();homePosition=transform.position;homeRotation=transform.rotation;}
        // With no offset the object stays exactly where the hand closed on it. A distance grab passes the world offset
        // from the hand at which to hold it. Either way it keeps its orientation relative to the hand.
        public bool Take(Transform holder,Vector3? worldOffset=null,bool pull=false,Quaternion? worldRotation=null)
        {
            if(held || processed) return false;
            if(sending){StopAllCoroutines();sending=false;}
            thrown=false;trailCount=0;
            Holder=holder;held=true;if(!Body.isKinematic)Body.linearVelocity=Vector3.zero;Body.isKinematic=true;IgnorePlayer(true);
            var inverse=Quaternion.Inverse(holder.rotation);holdRotation=inverse*(worldRotation??transform.rotation);holdPosition=inverse*(worldOffset??transform.position-holder.position);
            // A held tool must never catch the hand's own pointer ray.
            if(tool)SetLayer(2);
            holdYaw=Mathf.DeltaAngle(holder.eulerAngles.y,transform.eulerAngles.y);
            // A distance grab draws the object to the hand over a moment instead of snapping it there.
            pullStart=pull?Time.time:-1;pullFrom=transform.position;pullFromRotation=transform.rotation;
            return true;
        }
        public void Release()
        {
            held=false;Holder=null;IgnorePlayer(false);
            if(tool){SetLayer(0);ReturnHome();return;}
            // An oar stays in its lock and eases back to rest by itself.
            if(paddle) { if(oar)oar.LetGo();Body.isKinematic=true; }
            else if(!processed) Body.isKinematic=false;
        }
        void LateUpdate()
        {
            if(held && Holder)
            {
                // An oar turns about its lock to follow the hand. Each push through the water is one stroke.
                if(paddle&&oar)
                {
                    oar.Aim(Holder.position);
                    if(oar.TakeStroke()&&Time.time>nextStroke){GameDirector.Instance.ocean.Stroke();nextStroke=Time.time+.65f;}
                    return;
                }
                Vector3 position;Quaternion rotation;
                position=Holder.position+Holder.rotation*holdPosition;rotation=upright?Quaternion.Euler(0,Holder.eulerAngles.y+holdYaw,0):Holder.rotation*holdRotation;
                if(pullStart>=0&&Time.time<pullStart+PullTime)
                {float s=Mathf.SmoothStep(0,1,(Time.time-pullStart)/PullTime);position=Vector3.Lerp(pullFrom,position,s);rotation=Quaternion.Slerp(pullFromRotation,rotation,s);}
                transform.SetPositionAndRotation(position,rotation);
                // The first moments of a distance grab are the box being drawn in, not a throw.
                if(throwable){if(pullStart>=0&&Time.time<pullStart+PullTime)trailCount=0;else Record();}
            }
            if(!held&&!processed&&transform.position.y < -3) ResetObject();
        }
        void Record()
        {
            trailPosition[trailHead]=transform.position;trailTime[trailHead]=Time.time;
            trailHead=(trailHead+1)%Trail;if(trailCount<Trail)trailCount++;
        }
        int TrailIndex(int j)=>(trailHead-trailCount+j+Trail*2)%Trail;
        // How fast the hand was moving in the last moments before letting go: the quickest stretch of about two frames in the
        // last fifth of a second. Zero when nothing was recorded (a distance grab just begun, or a box that is not throwable).
        public Vector3 HandVelocity()
        {
            Vector3 best=Vector3.zero;float bestSpeed=0,now=Time.time;
            for(int j=2;j<trailCount;j++)
            {
                int a=TrailIndex(j-2),b=TrailIndex(j);float span=trailTime[b]-trailTime[a];
                if(span<.015f||now-trailTime[b]>.2f)continue;
                var v=(trailPosition[b]-trailPosition[a])/span;float speed=v.magnitude;
                if(speed>bestSpeed){bestSpeed=speed;best=v;}
            }
            return best;
        }
        // Lets go with a velocity. The box flies under physics, ignoring the player's own body for a moment so it clears the chest.
        public void Throw(Vector3 velocity)
        {
            Initialize();if(processed)return;
            StopAllCoroutines();held=false;Holder=null;sending=false;pullStart=-1;thrown=true;
            Body.isKinematic=false;Body.linearVelocity=velocity;
            var spin=Vector3.Cross(Vector3.up,velocity);Body.angularVelocity=spin.sqrMagnitude>.001f?spin.normalized*Mathf.Min(2.5f,velocity.magnitude*.25f):Vector3.zero;
            StartCoroutine(Flight());
        }
        IEnumerator Flight()
        {
            float start=Time.time,rest=-1;
            yield return new WaitForSeconds(Throwing.IgnoreBody);
            if(held||processed||sending){thrown=false;yield break;}
            IgnorePlayer(false);
            while(true)
            {
                yield return null;
                // Caught by the truck, picked up again, or sent somewhere: the throw is over.
                if(held||processed||sending){thrown=false;yield break;}
                bool still=Time.time-start>.8f&&Body.linearVelocity.sqrMagnitude<.09f;
                if(!still)rest=-1;else if(rest<0)rest=Time.time;else if(Time.time-rest>=Throwing.RestToReturn)break;
                if(Time.time-start>14f)break;
            }
            // It missed. Back to its stand, with no penalty.
            thrown=false;
            var d=GameDirector.Instance;
            if(d&&Time.time>nextMissMessage){d.Say("Not quite. That one is back on its stand. There is no penalty, and you can still point and let go.");nextMissMessage=Time.time+12;}
            ReturnHome();
        }
        // A thrown box that reaches the truck is drawn into its place in a moment, rather than snapping there.
        public void Catch(Transform slot)
        {
            Initialize();if(processed||held)return;
            StopAllCoroutines();Holder=null;thrown=false;IgnorePlayer(false);
            if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            Body.isKinematic=true;processed=true;sending=true;catchSlot=slot;
            StartCoroutine(Settle());
        }
        IEnumerator Settle()
        {
            Vector3 from=transform.position;Quaternion fromRotation=transform.rotation;var slot=catchSlot;
            for(float t=0;t<1;)
            {
                t=Mathf.Min(1,t+Time.deltaTime/.22f);float s=Mathf.SmoothStep(0,1,t);
                Snap(Vector3.Lerp(from,slot.position,s),Quaternion.Slerp(fromRotation,slot.rotation,s));
                yield return null;
            }
            Snap(slot.position,slot.rotation);
            sending=false;catchSlot=null;
            var d=GameDirector.Instance;if(d&&isActiveAndEnabled)d.PlayAt(d.place,transform.position,.5f);
        }
        // Glides along a low arc to a resting point, then hands the object back to physics.
        public void SendTo(Vector3 point,Quaternion rotation)
        {
            Initialize();if(processed)return;
            StopAllCoroutines();held=false;Holder=null;IgnorePlayer(false);
            sending=true;sendPoint=point;sendRotation=rotation;Body.isKinematic=true;StartCoroutine(Glide());
        }
        public void ReturnHome(){SendTo(homePosition,homeRotation);}
        IEnumerator Glide()
        {
            Vector3 from=transform.position;Quaternion fromRotation=transform.rotation;
            float distance=Vector3.Distance(from,sendPoint),duration=Mathf.Clamp(.25f+distance*.06f,.3f,.8f),arc=Mathf.Clamp(distance*.12f,.08f,.9f);
            for(float t=0;t<1;)
            {
                t=Mathf.Min(1,t+Time.deltaTime/duration);float s=Mathf.SmoothStep(0,1,t);
                transform.SetPositionAndRotation(Vector3.Lerp(from,sendPoint,s)+Vector3.up*(arc*4*s*(1-s)),Quaternion.Slerp(fromRotation,sendRotation,s));
                yield return null;
            }
            Land();
        }
        void Land()
        {
            sending=false;if(processed)return;
            // A tool rests on its rack; everything else lands and settles under physics.
            if(tool){Body.isKinematic=true;}else{Body.isKinematic=false;Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            var d=GameDirector.Instance;if(d&&isActiveAndEnabled)d.PlayAt(d.place,transform.position,.45f);
        }
        // Leaving the room mid-glide: finish the move at once so the object never stays frozen in the air.
        void OnDisable()
        {
            if(catchSlot){StopAllCoroutines();Snap(catchSlot.position,catchSlot.rotation);catchSlot=null;sending=false;}
            else if(sending){StopAllCoroutines();transform.SetPositionAndRotation(sendPoint,sendRotation);Land();}
            // A box still in the air when the room closes keeps nothing of its flight.
            if(thrown){thrown=false;IgnorePlayer(false);if(Body&&!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}}
        }
        void OnCollisionEnter(Collision collision)
        {
            if(held||paddle||Time.time<nextImpact)return;float speed=collision.relativeVelocity.magnitude;if(speed<.6f)return;
            nextImpact=Time.time+.15f;var d=GameDirector.Instance;if(d)d.PlayAt(d.place,transform.position,Mathf.Clamp01(speed/3f)*.7f);
        }
        public void MarkProcessed(Transform destination)
        {
            Initialize();
            if(held||processed)return; processed=true;Body.isKinematic=true;Snap(destination.position,destination.rotation);
        }
        void IgnorePlayer(bool ignore) { var d=GameDirector.Instance; if(!d||!d.player)return; var playerCollider=d.player.GetComponent<CharacterController>(); if(!playerCollider)return; foreach(var c in GetComponentsInChildren<Collider>())Physics.IgnoreCollision(c,playerCollider,ignore); }
        public void ResetObject()
        {
            Initialize();
            StopAllCoroutines();sending=false;pullStart=-1;thrown=false;catchSlot=null;trailCount=0;
            held=false;processed=false;Holder=null;IgnorePlayer(false);if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}Body.isKinematic=true;transform.SetPositionAndRotation(homePosition,homeRotation);if(oar)oar.ResetPose();
            if(tool)SetLayer(0);else if(!paddle)Body.isKinematic=false;
        }
        // A body that draws itself between its last two physics poses must be given a pose set by hand as well, or it springs back to the older pose a moment later.
        void Snap(Vector3 position,Quaternion rotation){transform.SetPositionAndRotation(position,rotation);if(Body){Body.position=position;Body.rotation=rotation;}}
        void SetLayer(int layer){foreach(var t in GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
    }
}


