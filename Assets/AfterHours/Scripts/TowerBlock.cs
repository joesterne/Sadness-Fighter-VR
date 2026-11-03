using UnityEngine;
namespace AfterHours
{
    // One wooden block of the rooftop tower. It is a Grabbable held by force rather than carried (see Grabbable.physical), and remembers
    // where it was stacked, so the tower can tell a block the player moved from one that fell.
    [RequireComponent(typeof(Grabbable))]
    public class TowerBlock : MonoBehaviour
    {
        public BlockTower tower;
        public int index;
        // The row it was stacked in, and its own height (a little under BlockTower.Height for most blocks).
        public int level;
        public float height=BlockTower.Height;
        // Held or pushed by the player since the tower was stacked.
        public bool touched;
        [System.NonSerialized] public Grabbable grab;
        [System.NonSerialized] public Rigidbody body;
        [System.NonSerialized] public BoxCollider box;
        Vector3 restPosition;Quaternion restRotation;float stillSince=-1;

        public void Prepare()
        {
            grab=GetComponent<Grabbable>();grab.Initialize();body=grab.Body;box=GetComponent<BoxCollider>();
        }
        // Puts the block in its place in a new tower. Its height is its scale, so its collider and its look both change.
        public void Stack(int row,float h,Vector3 position,Quaternion rotation)
        {
            level=row;height=h;touched=false;stillSince=-1;
            var s=transform.localScale;transform.localScale=new Vector3(s.x,h,s.z);
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.isKinematic=true;transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;
            restPosition=position;restRotation=rotation;
        }
        // Puts the block down somewhere off the tower, under physics and still. Where it was stacked is unchanged.
        public void Lay(Vector3 position,Quaternion rotation)
        {
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.isKinematic=true;transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;
            body.isKinematic=false;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
        }
        // Back under physics, at rest and asleep, so the new tower stands perfectly still until something touches it.
        public void Settle(){body.isKinematic=false;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.Sleep();}
        // Dropped by more than half its height, or tipped more than 25 degrees, from where it was stacked.
        public bool Displaced=>restPosition.y-transform.position.y>height*.5f||Quaternion.Angle(restRotation,transform.rotation)>25;
        // How far it has moved from where it was stacked.
        public float Drift=>Vector3.Distance(restPosition,transform.position);
        public Vector3 RestPosition=>restPosition;
        // Lying still on top of the tower (above its stacked rows and over its foot) for a moment.
        public bool OnTopOf(BlockTower t)
        {
            var local=t.foot.InverseTransformPoint(transform.position);
            bool there=local.y>t.TopHeight&&new Vector2(local.x,local.z).magnitude<BlockTower.Length*.75f;
            bool still=body.IsSleeping()||body.linearVelocity.sqrMagnitude<.0004f;
            if(!there||!still){stillSince=-1;return false;}
            if(stillSince<0)stillSince=Time.time;
            return Time.time-stillSince>.4f;
        }
        void OnCollisionEnter(Collision collision){if(tower)tower.Knock(this,collision);}
    }
}
