using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace AfterHours
{
    // A quiet easter egg in the old résumé room. Throw a page onto the floor (or just drop it there) and it bursts into a shower of
    // multi-coloured confetti: a burst that flies up from where the page landed, a shower that falls from above, and a pop and a
    // flutter of paper. Nothing is lost. The page is back on its stand a moment later, so the chapter and its count are untouched.
    // A page let go over the desk, or sent or thrown into the shredder, does nothing special.
    // Lives on the room's "Confetti" group, so it is switched off with the room. Coming back into the room (or TRY AGAIN) puts any
    // page that was mid-burst back at once.
    public class ConfettiEgg : MonoBehaviour
    {
        public GameDirector director;
        public Grabbable[] pages;
        // The burst flies up and the shower falls; both are one-shot systems that are moved to where the page landed and played again.
        public ParticleSystem pop, shower;
        public AudioClip sound;
        // A page has to hit the floor at least this fast (a real throw or drop, not a nudge), and within this height of it (the desk does not count).
        public const float MinSpeed=1.5f, FloorHeight=.2f;
        // The page is back on its stand this long after it burst.
        public const float ReturnAfter=1.6f;
        // For tests: how many pages have burst, and where the last one landed.
        public int bursts{get;private set;}
        public Vector3 lastAt{get;private set;}
        public int returns{get;private set;}
        public float popTime{get;private set;}
        public float returnTime{get;private set;}
        readonly List<Grabbable> away=new List<Grabbable>();
        public bool IsAway(Grabbable page)=>away.Contains(page);

        // A page tells the egg whenever it hits something. Only a hard enough knock on the floor counts.
        public void Hit(Grabbable page,Collision collision)
        {
            if(!page||!director||director.currentRoom!=Shredder.Room||page.held||page.processed||page.sending||away.Contains(page))return;
            if(collision.contactCount==0||collision.relativeVelocity.magnitude<MinSpeed)return;
            var contact=collision.GetContact(0);
            if(Mathf.Abs(contact.normal.y)<.7f||contact.point.y-transform.position.y>FloorHeight)return;
            Pop(page,contact.point);
        }
        public void Pop(Grabbable page,Vector3 at)
        {
            if(!page||away.Contains(page))return;
            away.Add(page);bursts++;lastAt=at;popTime=Time.time;page.Vanish();
            Fire(pop,at+Vector3.up*.1f);Fire(shower,at+Vector3.up*2.1f);
            if(director){director.PlayAt(sound,at,.9f);director.Say("Confetti! That page is back on its stand.");}
            StartCoroutine(Wait(page));
        }
        static void Fire(ParticleSystem system,Vector3 at)
        {
            if(!system)return;
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.transform.position=at;system.Play(true);
        }
        IEnumerator Wait(Grabbable page){yield return new WaitForSeconds(ReturnAfter);Return(page);}
        // The page comes back to its stand, set down gently, as if it had never gone.
        void Return(Grabbable page)
        {
            away.Remove(page);
            if(!page||page.gameObject.activeSelf)return;
            returns++;returnTime=Time.time;
            page.BringBack();
            if(director&&isActiveAndEnabled)director.PlayAt(director.place,page.transform.position,.4f);
        }
        // Leaving the room mid-burst stops the wait, so the page is brought back when the room next opens.
        void OnEnable(){BringBack();}
        void BringBack(){foreach(var page in away.ToArray())Return(page);}
        // TRY AGAIN: any page that burst is back, and the confetti is cleared away.
        public void ResetRoom()
        {
            StopAllCoroutines();BringBack();
            if(pop)pop.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(shower)shower.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
