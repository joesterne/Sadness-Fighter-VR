using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // The rage room: an old work computer on a desk, a bat, and a quiet moment when it is all broken.
    // The chapter completes after the quiet, and a new computer can be wheeled in to go again.
    // The desk things (extras) break too, but only the computer counts toward the chapter. Among them are the side desk with a
    // laptop and a work phone on it, and the copier, which has an easter egg: break it for good and it jams in a burst of paper.
    // Every thing broken, over every visit, counts toward the tools on the wall: the hammer, the handsaw, the nail bat and the axe.
    public class RageRoom : MonoBehaviour
    {
        public const int Room=7;
        public GameDirector director;
        public Breakable[] parts=Array.Empty<Breakable>(), extras=Array.Empty<Breakable>();
        public Grabbable bat;
        // The tools on the wall, in the order they unlock, how many broken things each needs, and what hangs in a tool's place
        // while it is locked (its outline, and a count of how many more things to break).
        public Grabbable[] tools=Array.Empty<Grabbable>();
        public int[] unlockAt=Array.Empty<int>();
        public GameObject[] toolLocks=Array.Empty<GameObject>();
        public TextMeshPro[] lockCounts=Array.Empty<TextMeshPro>();
        // The copier's easter egg, Paper Jam: a burst of paper from the copier, a shower from above, and its sound.
        public Breakable copier;
        public ParticleSystem paperBurst, paperShower;
        public AudioClip jamSound;
        public const string PaperJamPin="paperjam-pin";
        public ParticleSystem shards, sparks;
        public AudioClip hitPlastic, hitGlass, hitMetal, hitPaper, clatter, smash, swing, cart, calmSound;
        public AudioSource ambience;
        public float ambienceVolume=.5f;
        public GameObject calm, newComputer;
        bool quieting,spoke;Coroutine quiet;float nextClatter;
        // What has flown since the last new computer: hits, pieces thrown, the fewest any one hit threw, and knocks.
        [NonSerialized] public int Hits,Thrown,FewestThrown=int.MaxValue,Knocks;
        // Steps of breaking that came from falling with the side desk rather than from a hit.
        [NonSerialized] public int Carried;
        // Everything broken here, over every visit and every new computer (saved with the checkpoint), and how often the copier has jammed.
        [NonSerialized] public int BrokenEver,Jams;
        public IEnumerable<Grabbable> AllTools=>new[]{bat}.Concat(tools).Where(t=>t);
        public bool Unlocked(int tool)=>tool<0||tool>=unlockAt.Length||BrokenEver>=unlockAt[tool];
        public IEnumerable<Breakable> Everything=>parts.Concat(extras);
        public bool Quieting=>quieting;
        public int BrokenCount=>parts.Count(p=>p.Broken);
        public bool AllBroken=>parts.Length>0&&parts.All(p=>p.Broken);
        public bool Calm=>calm&&calm.activeSelf;

        // A swing reaches a part. Every hit counts the same, however short the swing: strength only changes the sound.
        // A heavier tool breaks a part two steps at a time.
        public void Hit(Breakable part,float speed,Vector3 point,Vector3 direction,Bat tool=null)
        {
            if(!part||part.Broken||quieting||Time.time<part.nextHit||director.currentRoom!=Room)return;
            part.nextHit=Time.time+.3f;
            var away=Away(part.transform.position);
            var push=(direction.sqrMagnitude>1e-4f?direction.normalized:away)*Mathf.Clamp(speed*.45f,.8f,3.2f);
            int thrown=0,power=tool?Mathf.Max(1,tool.power):1;
            for(int k=0;k<power&&!part.Broken;k++)thrown+=part.Strike(push,away);
            Hits++;Thrown+=thrown;FewestThrown=Mathf.Min(FewestThrown,thrown);
            bool extra=Array.IndexOf(extras,part)>=0;
            director.PlayAt(part.Broken&&!extra?smash:Clip(part.sound),point,Mathf.Clamp(.5f+speed/7f,.5f,1f));
            // Big, messy bursts: glass and plastic throw shards, metal throws sparks as well. Paper throws none.
            if(part.sound!=BreakSound.Paper)Burst(part.sound==BreakSound.Metal?sparks:shards,point,push,part.Broken?40:18);
            if(part.sound==BreakSound.Metal)Burst(shards,point,push,10);
            director.player.PulseFor(tool?tool.grabbable:bat,Mathf.Clamp(.35f+speed/6f,.35f,1f));
            if(!spoke){spoke=true;director.Say("Again, if you need to. As hard or as soft as you like. Short swings count.");}
            if(part.Broken)
            {
                Counted();
                // Whatever stood on it goes down with it, in pieces.
                foreach(var rider in part.riders)
                {
                    if(!rider||rider.Broken)continue;
                    for(int k=0;k<rider.HitsToBreak&&!rider.Broken;k++){Thrown+=rider.Strike(push,away);Carried++;}
                    Counted();if(rider==copier)PaperJam();
                }
                if(!extra)PartBroken(part);else if(!string.IsNullOrEmpty(part.brokenLine))director.Say(part.brokenLine);
                if(part==copier)PaperJam();
            }
            director.SaveCheckpoint();
        }
        AudioClip Clip(BreakSound sound)=>sound==BreakSound.Glass?hitGlass:sound==BreakSound.Metal?hitMetal:sound==BreakSound.Paper?hitPaper:hitPlastic;
        // Level, from the player to a point: the way pieces may fly.
        Vector3 Away(Vector3 point)
        {
            var a=Vector3.ProjectOnPlane(point-director.player.Head.position,Vector3.up);
            return a.sqrMagnitude>1e-4f?a.normalized:director.rooms[Room].root.transform.forward;
        }
        // A piece that has already flown, reached by a swing: it pops up and skids along the way the bat was going, with a
        // small clatter. A straight-down chop sends it away from the player. Heavy pieces barely move, and nothing is knocked
        // back toward the player.
        public void Knock(Rigidbody body,float speed,Vector3 point,Vector3 direction,Bat tool=null)
        {
            if(!body||body.isKinematic||director.currentRoom!=Room)return;
            var loose=body.GetComponent<Loose>();if(!loose||Time.time<loose.nextKnock)return;
            loose.nextKnock=Time.time+.25f;Knocks++;
            var away=Away(body.position);var flat=Vector3.ProjectOnPlane(direction,Vector3.up);
            flat=flat.sqrMagnitude>1e-4f?flat.normalized:Vector3.zero;
            float toward=Vector3.Dot(flat,away);if(toward<0)flat-=away*toward;
            if(flat.sqrMagnitude<.25f)flat+=away*.6f;flat.Normalize();
            float change=Mathf.Min(speed*.9f+.4f,1.6f/Mathf.Max(body.mass,.01f));
            body.linearVelocity+=flat*change+Vector3.up*Mathf.Min(change*.5f+.3f,1.5f);
            body.angularVelocity+=UnityEngine.Random.insideUnitSphere*Mathf.Min(change*4,12);
            if(Time.time>nextClatter){nextClatter=Time.time+.06f;director.PlayAt(loose.sound==BreakSound.Paper?hitPaper:clatter,point,Mathf.Clamp(.2f+speed/12f,.2f,.55f));}
            director.player.PulseFor(tool?tool.grabbable:bat,Mathf.Clamp(.12f+speed/15f,.12f,.35f));
        }
        // One more thing broken. A tool whose count is reached comes off its lock, and is announced after whatever was just said.
        void Counted()
        {
            BrokenEver++;
            for(int i=0;i<tools.Length&&i<unlockAt.Length;i++)
                if(BrokenEver==unlockAt[i]&&tools[i])director.Queue("Unlocked: "+tools[i].label.ToLowerInvariant()+". It is on the tool wall to your left.");
            RefreshTools();
        }
        // Each tool hangs on the wall once unlocked; until then its outline shows how many more things to break.
        // A tool in the hand is never taken away.
        public void RefreshTools()
        {
            for(int i=0;i<tools.Length;i++)
            {
                bool open=Unlocked(i);
                if(tools[i]&&!tools[i].held&&tools[i].gameObject.activeSelf!=open)tools[i].gameObject.SetActive(open);
                if(i<toolLocks.Length&&toolLocks[i])toolLocks[i].SetActive(!open);
                if(i<lockCounts.Length&&lockCounts[i])lockCounts[i].text=open?"":(unlockAt[i]-BrokenEver)+" MORE TO\nBREAK";
            }
        }
        // The easter egg. The copier, broken for good, jams: paper bursts out of it and drifts down, and the first time it earns
        // the Paper Jam pin. Every new copier can jam again.
        void PaperJam()
        {
            Jams++;var at=copier.transform.position+Vector3.up*.7f;
            Fire(paperBurst,at);Fire(paperShower,at+Vector3.up*1.5f);
            director.PlayAt(jamSound,at,1);
            director.Say("PAPER JAM. You found an easter egg. For once, the jam is not your problem.");
            director.Earn(PaperJamPin);
        }
        static void Fire(ParticleSystem system,Vector3 at)
        {
            if(!system)return;
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.transform.position=at;system.Play(true);
        }
        void PartBroken(Breakable part)
        {
            director.rooms[Room].accepted=BrokenCount;director.RefreshProgress();director.SmallStep();
            if(AllBroken)quiet=StartCoroutine(GoQuiet());
            else if(!string.IsNullOrEmpty(part.brokenLine))director.Say(part.brokenLine);
        }
        // When it is all broken the room goes quiet, and stays kind.
        IEnumerator GoQuiet()
        {
            quieting=true;
            yield return new WaitForSeconds(1.4f);
            float from=ambience?ambience.volume:0;
            for(float t=0;t<1;){t=Mathf.Min(1,t+Time.deltaTime/2.5f);if(ambience)ambience.volume=from*(1-t);yield return null;}
            if(calm)calm.SetActive(true);director.PlayUi(calmSound,.45f);
            director.Say("It is quiet now. Breathe in slowly, and out. The anger was real. It does not have to stay.");
            yield return new WaitForSeconds(6.5f);
            quieting=false;
            if(!director.rooms[Room].complete)director.CompleteRoom(Room);
            else director.Say("You can wheel in another one, or rest here. Both are fine.");
            if(newComputer)newComputer.SetActive(true);
            quiet=null;
        }
        // Coming back to a broken computer finishes the quiet; coming back after it shows the way to a new one.
        void OnEnable()
        {
            quieting=false;if(!director)return;
            if(AllBroken&&!(newComputer&&newComputer.activeSelf))quiet=StartCoroutine(GoQuiet());
            if(ambience)ambience.volume=AllBroken?0:ambienceVolume;
            RefreshTools();
        }
        // Puts every part back: a new computer, just like the old one. The chapter stays complete.
        public void ResetComputer()
        {
            // Only the quiet stops here: the wheel-in that calls this must keep running.
            if(quiet!=null){StopCoroutine(quiet);quiet=null;}quieting=false;
            foreach(var p in Everything)p.Mend();Hits=Thrown=Knocks=Carried=0;FewestThrown=int.MaxValue;
            if(shards)shards.Clear();if(sparks)sparks.Clear();
            if(paperBurst)paperBurst.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(paperShower)paperShower.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(calm)calm.SetActive(false);if(newComputer)newComputer.SetActive(false);
            if(ambience)ambience.volume=ambienceVolume;
            director.rooms[Room].accepted=0;
        }
        public void NewComputer()
        {
            if(!newComputer||!newComputer.activeSelf||director.player.busy)return;
            StartCoroutine(WheelIn());
        }
        IEnumerator WheelIn()
        {
            var player=director.player;player.busy=true;newComputer.SetActive(false);
            director.PlayAt(cart,transform.position+Vector3.up,.7f);
            yield return player.Fade(1,.35f);
            ResetComputer();director.RefreshProgress();director.SaveCheckpoint();
            yield return new WaitForSeconds(.5f);
            yield return player.Fade(0,.45f);player.busy=false;
            director.Say("A new one, just like the old one. Go again if you need to, or simply rest.");
        }
        // PUT IT BACK: every tool in either hand goes back to its place.
        public void PutBatBack(){foreach(var tool in AllTools)director.player.Drop(tool);}
        // How broken each part is, then each desk thing.
        public int[] Damage()=>Everything.Select(p=>p.damage).ToArray();
        // Brings back saved progress: how broken each part was. A finished room comes back quiet.
        public void Restore(int[] saved)
        {
            ResetComputer();var all=Everything.ToArray();
            if(saved!=null)for(int i=0;i<all.Length&&i<saved.Length;i++)all[i].Restore(saved[i]);
            director.rooms[Room].accepted=BrokenCount;
            if(AllBroken&&director.rooms[Room].complete){if(calm)calm.SetActive(true);if(newComputer)newComputer.SetActive(true);if(ambience)ambience.volume=0;}
            RefreshTools();
        }
        public void PlaySwing(Vector3 at,float speed){director.PlayAt(swing,at,Mathf.Clamp(.25f+speed/10f,.25f,.6f));}
        static void Burst(ParticleSystem ps,Vector3 at,Vector3 push,int count)
        {
            if(!ps)return;var p=new ParticleSystem.EmitParams{position=at,applyShapeToPosition=false};
            for(int i=0;i<count;i++){p.velocity=push*UnityEngine.Random.Range(.3f,.9f)+UnityEngine.Random.insideUnitSphere*1.4f+Vector3.up*1.1f;ps.Emit(p,1);}
        }
        // Test fixture: one hit on a part, as if the bat had landed on it.
        public void HitNow(Breakable part,Bat tool=null){if(!part)return;part.nextHit=0;Hit(part,3,part.transform.position,Vector3.forward,tool);}
    }
}
