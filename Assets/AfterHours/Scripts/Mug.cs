using UnityEngine;
namespace AfterHours
{
    // A mug in the kitchen. They can be carried, sent under the espresso spout, set down, and thrown. A hard knock breaks one: thrown, dropped
    // from more than a hand's height, or flung into something. A gentle set-down never does, so ordinary use is safe.
    // A broken mug bursts into shards and comes back whole and empty on its shelf a moment later (see KitchenRoom), so nothing is ever lost and
    // nothing is counted against anyone.
    // Lives next to the mug's Grabbable.
    public class Mug : MonoBehaviour
    {
        // How hard a knock has to be (relative speed at impact, m/s). A fall of about a third of a metre onto the counter is 2.6 m/s, so setting a mug down
        // is safe and letting go of it higher up is not. A drop from the counter to the floor is about 4.5 m/s, and a flick of the wrist is 2.2 m/s and up.
        public const float ShatterSpeed=2.6f;
        // After it lands from a send, for this long it cannot break (it settles under physics, and may nudge a mug already resting there).
        public const float Settling=.4f;
        public Grabbable item;
        // The coffee in this mug: a flat disc on its surface, shown once something has been poured.
        public Transform coffee;
        // The colour of the shards.
        public Color body=Color.white;
        // The hidden mug in the cabinet. Pouring into it earns the orange mug pin.
        public bool orange;
        public int level{get;private set;}
        // How many times this mug has broken (for tests).
        public int breaks{get;private set;}
        float calmUntil;
        Vector3 coffeeHome;bool coffeeKnown;

        // Two mugs pressed into each other part gently rather than flinging each other apart (a fling would read as a knock). This is not saved with the scene, so it is set here.
        void Awake(){if(!item)item=GetComponent<Grabbable>();var body=GetComponent<Rigidbody>();if(body)body.maxDepenetrationVelocity=1.5f;}
        void FixedUpdate(){if(item&&item.sending)calmUntil=Time.time+Settling;}

        // Level 0 is empty; each pour raises it a little.
        public void Fill(int to)
        {
            level=Mathf.Clamp(to,0,3);
            if(!coffee)return;
            if(!coffeeKnown){coffeeHome=coffee.localPosition;coffeeKnown=true;}
            coffee.gameObject.SetActive(level>0);
            coffee.localScale=new Vector3(.76f+level*.045f,.015f,.76f+level*.045f);coffee.localPosition=new Vector3(coffeeHome.x,1.018f,coffeeHome.z);
        }
        public void Empty(){Fill(0);}

        void OnCollisionEnter(Collision collision)
        {
            if(!isActiveAndEnabled||!item||item.held||item.processed||item.sending||!item.Body||item.Body.isKinematic||Time.time<calmUntil)return;
            // A hand sweeping through another mug, or a mug gliding in beside it, is not a knock.
            var other=collision.collider?collision.collider.GetComponentInParent<Grabbable>():null;
            if(other&&other!=item&&(other.held||other.sending))return;
            if(collision.relativeVelocity.magnitude<ShatterSpeed||collision.contactCount==0)return;
            var contact=collision.GetContact(0);
            Shatter(contact.point,contact.normal);
        }

        // Breaks the mug here and now. Public so a test can break one.
        public bool Shatter(Vector3 at,Vector3 normal)
        {
            if(!item||!gameObject.activeInHierarchy)return false;
            breaks++;
            var d=GameDirector.Instance;
            if(d&&d.player&&item.held)d.player.Drop(item);
            var kitchen=d?d.kitchen:null;
            if(kitchen)kitchen.Broke(this,at,normal);
            Empty();
            item.Vanish();
            return true;
        }
    }
}
