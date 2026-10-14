using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AfterHours
{
    // A quiet easter egg in the heavy room. Hold a box in each hand and slam them together, and both burst in a flash of sparks, a puff of
    // smoke and a spray of chips in the boxes' own colours, with a boom. Nothing is lost. Both boxes are back on their stands a moment later and
    // nothing is counted, so the chapter and its count are untouched.
    // A held box is not a physics body (it is carried by the hand), so two held boxes never "collide". This watches how fast each carried box is
    // moving, and when two boxes held in different hands touch while closing quickly, it bursts them. Boxes held side by side, carried together,
    // or only pressed lightly together do nothing.
    // Lives on the room's "Box blast" group, so it is switched off with the room. Coming back into the room (or TRY AGAIN) puts any box that
    // was mid-blast back at once.
    // Runs after every Grabbable has followed its hand this frame (they move in LateUpdate), so the speed it works out is the speed of the hand
    // between the last two frames, divided by the time between them.
    [DefaultExecutionOrder(1000)]
    public class BoxBlast : MonoBehaviour
    {
        public const int Room=2;
        public GameDirector director;
        public Grabbable[] boxes;
        // One-shot systems that are moved to where the boxes met and played again: a flash, sparks, a puff of smoke and chips of box.
        public ParticleSystem flash, sparks, smoke, chips;
        public AudioClip sound;
        // The two boxes must be closing on each other at least this fast (m/s, smoothed over a few frames, so a jittery hand is not a slam).
        public const float MinClosing=2.4f;
        // A box has to have been in the hand at least this long, so being drawn in from across the room is not a slam.
        public const float Settle=.3f;
        // Both boxes are back on their stands this long after the blast.
        public const float ReturnAfter=2f;
        const float Smoothing=.05f;
        // For tests: how many blasts, where the last one was, how many boxes have come back, and when.
        public int blasts{get;private set;}
        public Vector3 lastAt{get;private set;}
        public int returns{get;private set;}
        public float blastTime{get;private set;}
        readonly List<Grabbable> away=new List<Grabbable>();
        Vector3[] last,velocity;bool[] seen;Collider[] bodies;
        public bool IsAway(Grabbable box)=>away.Contains(box);

        void Setup()
        {
            if(boxes==null||(last!=null&&last.Length==boxes.Length))return;
            last=new Vector3[boxes.Length];velocity=new Vector3[boxes.Length];seen=new bool[boxes.Length];bodies=new Collider[boxes.Length];
            for(int i=0;i<boxes.Length;i++)if(boxes[i])bodies[i]=boxes[i].GetComponent<Collider>();
        }
        // A box that counts: carried in a hand, not being drawn in, not already on its way somewhere.
        bool Carried(Grabbable box)=>box&&box.gameObject.activeInHierarchy&&box.held&&box.Holder&&!box.processed&&!box.sending&&Time.time-box.TakenAt>=Settle&&!away.Contains(box);

        void LateUpdate()
        {
            if(!director||director.currentRoom!=Room||boxes==null)return;
            float dt=Time.deltaTime;if(dt<=0)return;
            Setup();
            for(int i=0;i<boxes.Length;i++)
            {
                if(!Carried(boxes[i])){seen[i]=false;continue;}
                var at=boxes[i].transform.position;
                if(seen[i])velocity[i]=Vector3.Lerp(velocity[i],(at-last[i])/dt,1-Mathf.Exp(-dt/Smoothing));else velocity[i]=Vector3.zero;
                last[i]=at;seen[i]=true;
            }
            for(int i=0;i<boxes.Length;i++)for(int j=i+1;j<boxes.Length;j++)
                if(seen[i]&&seen[j]&&Slammed(i,j,out var where)){Blast(boxes[i],boxes[j],where);return;}
        }
        // Two boxes in different hands, touching, and closing quickly.
        bool Slammed(int i,int j,out Vector3 where)
        {
            where=default;
            var a=boxes[i];var b=boxes[j];var ca=bodies[i];var cb=bodies[j];
            if(a.Holder==b.Holder||!ca||!cb)return false;
            var gap=b.transform.position-a.transform.position;float distance=gap.magnitude;
            var relative=velocity[i]-velocity[j];
            float closing=distance>.01f?Vector3.Dot(relative,gap/distance):relative.magnitude;
            if(closing<MinClosing)return false;
            if(!Physics.ComputePenetration(ca,ca.transform.position,ca.transform.rotation,cb,cb.transform.position,cb.transform.rotation,out _,out _))return false;
            where=(a.transform.position+b.transform.position)*.5f;return true;
        }
        public void Blast(Grabbable a,Grabbable b,Vector3 at)
        {
            if(!a||!b||away.Contains(a)||away.Contains(b))return;
            blasts++;lastAt=at;blastTime=Time.time;
            foreach(var box in new[]{a,b})
            {
                away.Add(box);
                if(director&&director.player)director.player.Drop(box);
                box.Vanish();
            }
            Fire(flash,at);Fire(sparks,at);Fire(smoke,at);Fire(chips,at);
            if(director){director.PlayAt(sound,at,.9f);director.Say("Boom! Both boxes are back on their stands.");}
            StartCoroutine(Wait(a,b));
        }
        static void Fire(ParticleSystem system,Vector3 at)
        {
            if(!system)return;
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.transform.position=at;system.Play(true);
        }
        IEnumerator Wait(Grabbable a,Grabbable b){yield return new WaitForSeconds(ReturnAfter);Return(a);Return(b);}
        // A box comes back to its stand, set down gently, as if it had never gone.
        void Return(Grabbable box)
        {
            away.Remove(box);
            if(!box||box.gameObject.activeSelf)return;
            returns++;
            box.BringBack();
            if(director&&isActiveAndEnabled)director.PlayAt(director.place,box.transform.position,.4f);
        }
        // Leaving the room mid-blast stops the wait, so the boxes are brought back when the room next opens.
        void OnEnable(){BringBack();}
        void BringBack(){foreach(var box in away.ToArray())Return(box);}
        // TRY AGAIN: any box that burst is back, and the smoke and sparks are cleared away.
        public void ResetRoom()
        {
            StopAllCoroutines();BringBack();
            foreach(var system in new[]{flash,sparks,smoke,chips})if(system)system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
