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
        public Transform Holder { get; private set; }
        public Rigidbody Body { get; private set; }
        Vector3 homePosition; Quaternion homeRotation;
        Vector3 lastHand; float strokeTravel; float nextStroke;
        Vector3 holdPosition; Quaternion holdRotation; float holdYaw;
        void Awake() { Initialize(); }
        public void Initialize(){if(Body)return;Body=GetComponent<Rigidbody>();homePosition=transform.position;homeRotation=transform.rotation;}
        // With no offset the object stays exactly where the hand closed on it. A distance grab passes the world offset
        // from the hand at which to hold it. Either way it keeps its orientation relative to the hand.
        public bool Take(Transform holder,Vector3? worldOffset=null)
        {
            if(held || processed) return false;
            Holder=holder;held=true;if(!Body.isKinematic)Body.linearVelocity=Vector3.zero;Body.isKinematic=true;IgnorePlayer(true);
            var inverse=Quaternion.Inverse(holder.rotation);holdRotation=inverse*transform.rotation;holdPosition=inverse*(worldOffset??transform.position-holder.position);
            holdYaw=Mathf.DeltaAngle(holder.eulerAngles.y,transform.eulerAngles.y);
            lastHand=holder.position;strokeTravel=0;return true;
        }
        public void Release()
        {
            held=false;Holder=null;IgnorePlayer(false);
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
                if(paddle)transform.position=Holder.position+Holder.forward*.13f;
                else transform.SetPositionAndRotation(Holder.position+Holder.rotation*holdPosition,upright?Quaternion.Euler(0,Holder.eulerAngles.y+holdYaw,0):Holder.rotation*holdRotation);
            }
            if(!held&&!processed&&transform.position.y < -3) ResetObject();
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
            held=false;processed=false;Holder=null;IgnorePlayer(false);if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}Body.isKinematic=true;transform.SetPositionAndRotation(homePosition,homeRotation);
            if(!paddle)Body.isKinematic=false;
        }
    }
}


