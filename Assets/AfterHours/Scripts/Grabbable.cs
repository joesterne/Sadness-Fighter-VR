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
        public Transform Holder { get; private set; }
        // Gliding to a send target (or back home after a wrong tray). Drop zones wait until it lands.
        public bool sending { get; private set; }
        public Rigidbody Body { get; private set; }
        Vector3 homePosition; Quaternion homeRotation;
        Vector3 lastHand; float strokeTravel; float nextStroke;
        Vector3 holdPosition; Quaternion holdRotation; float holdYaw;
        Vector3 pullFrom, sendPoint; Quaternion pullFromRotation, sendRotation; float pullStart=-1;
        const float PullTime=.18f;
        float nextImpact;
        void Awake() { Initialize(); }
        public void Initialize(){if(Body)return;Body=GetComponent<Rigidbody>();homePosition=transform.position;homeRotation=transform.rotation;}
        // With no offset the object stays exactly where the hand closed on it. A distance grab passes the world offset
        // from the hand at which to hold it. Either way it keeps its orientation relative to the hand.
        public bool Take(Transform holder,Vector3? worldOffset=null,bool pull=false,Quaternion? worldRotation=null)
        {
            if(held || processed) return false;
            if(sending){StopAllCoroutines();sending=false;}
            Holder=holder;held=true;if(!Body.isKinematic)Body.linearVelocity=Vector3.zero;Body.isKinematic=true;IgnorePlayer(true);
            var inverse=Quaternion.Inverse(holder.rotation);holdRotation=inverse*(worldRotation??transform.rotation);holdPosition=inverse*(worldOffset??transform.position-holder.position);
            // A held tool must never catch the hand's own pointer ray.
            if(tool)SetLayer(2);
            holdYaw=Mathf.DeltaAngle(holder.eulerAngles.y,transform.eulerAngles.y);
            lastHand=holder.position;strokeTravel=0;
            // A distance grab draws the object to the hand over a moment instead of snapping it there.
            pullStart=pull?Time.time:-1;pullFrom=transform.position;pullFromRotation=transform.rotation;
            return true;
        }
        public void Release()
        {
            held=false;Holder=null;IgnorePlayer(false);
            if(tool){SetLayer(0);ReturnHome();return;}
            if(paddle) { transform.SetPositionAndRotation(homePosition,homeRotation);Body.isKinematic=true; }
            else if(!processed) Body.isKinematic=false;
        }
        void LateUpdate()
        {
            if(held && Holder)
            {
                if(paddle)
                {
                    // Row relative to the body, not the gaze: strokes still register while the player looks around.
                    float back=Vector3.Dot(lastHand-Holder.position,GameDirector.Instance.player.transform.forward);
                    if(back>0 && back<.2f)strokeTravel+=back;
                    if(strokeTravel>.16f && Time.time>nextStroke) { GameDirector.Instance.ocean.Stroke();strokeTravel=0;nextStroke=Time.time+.65f; }
                    lastHand=Holder.position;
                }
                Vector3 position;Quaternion rotation;
                if(paddle){position=Holder.position+Holder.forward*.13f;rotation=transform.rotation;}
                else{position=Holder.position+Holder.rotation*holdPosition;rotation=upright?Quaternion.Euler(0,Holder.eulerAngles.y+holdYaw,0):Holder.rotation*holdRotation;}
                if(pullStart>=0&&Time.time<pullStart+PullTime)
                {float s=Mathf.SmoothStep(0,1,(Time.time-pullStart)/PullTime);position=Vector3.Lerp(pullFrom,position,s);rotation=Quaternion.Slerp(pullFromRotation,rotation,s);}
                transform.SetPositionAndRotation(position,rotation);
            }
            if(!held&&!processed&&transform.position.y < -3) ResetObject();
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
        void OnDisable(){if(sending){StopAllCoroutines();transform.SetPositionAndRotation(sendPoint,sendRotation);Land();}}
        void OnCollisionEnter(Collision collision)
        {
            if(held||paddle||Time.time<nextImpact)return;float speed=collision.relativeVelocity.magnitude;if(speed<.6f)return;
            nextImpact=Time.time+.15f;var d=GameDirector.Instance;if(d)d.PlayAt(d.place,transform.position,Mathf.Clamp01(speed/3f)*.7f);
        }
        public void MarkProcessed(Transform destination)
        {
            Initialize();
            if(held||processed)return; processed=true;Body.isKinematic=true;transform.SetPositionAndRotation(destination.position,destination.rotation);
        }
        void IgnorePlayer(bool ignore) { var d=GameDirector.Instance; if(!d||!d.player)return; var playerCollider=d.player.GetComponent<CharacterController>(); foreach(var c in GetComponentsInChildren<Collider>())Physics.IgnoreCollision(c,playerCollider,ignore); }
        public void ResetObject()
        {
            Initialize();
            StopAllCoroutines();sending=false;pullStart=-1;
            held=false;processed=false;Holder=null;IgnorePlayer(false);if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}Body.isKinematic=true;transform.SetPositionAndRotation(homePosition,homeRotation);
            if(tool)SetLayer(0);else if(!paddle)Body.isKinematic=false;
        }
        void SetLayer(int layer){foreach(var t in GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
    }
}


