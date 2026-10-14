using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    public class KitchenRoom : MonoBehaviour
    {
        // The cabinet that hides the orange mug (counting doors from the left).
        public const int OrangeCabinet=4;
        // A broken mug is back on its shelf this long after it breaks.
        public const float ReturnAfter=2.5f;
        public GameDirector director;
        // "Your cup", the white mug from the start. It carries the saved pours.
        public Grabbable cup;
        // Every mug: the ones on the counter and the orange one in the cabinet.
        public Mug[] mugs;
        public Mug orangeMug;
        public CabinetDoor[] doors;
        public Transform nozzle;
        public GameObject[] crowd, spills;
        public Transform coffee;
        public ParticleSystem spillSpray;
        public TextMeshPro display;
        // A murmur that grows with the watching crowd and falls silent when it leaves.
        public AudioSource murmur;
        // A mug that breaks: shards (one system over the counter, one over the floor, each with its own collision plane), a puff of ceramic dust,
        // and a few splashes of coffee that stay where a full mug broke.
        public ParticleSystem shardsCounter, shardsFloor, dust;
        public Transform[] splashes;
        // The lever on the machine (it pulls when the machine brews) and the steam wand's puffs.
        public Transform lever;
        public ParticleSystem steam;
        public AudioClip shatterSound, cabinetSound, steamSound;
        public bool helped;
        public int mistakes, fills;
        // For tests: broken mugs, cabinets opened, steam blown, whether the orange cabinet has been opened, and whether the orange mug has been filled.
        public int shatters{get;private set;}
        public int opens{get;private set;}
        public int steams{get;private set;}
        public bool orangeSeen{get;private set;}
        public bool orangeFilled{get;private set;}
        float nextBrew, nextBrokenMessage, leverUntil, leverAmount, leverPull, steamUntil, steamCarry;
        int splashNext;
        readonly List<Mug> away=new List<Mug>();
        const float LeverRest=55, SteamRate=26;
        static readonly string[] Contents={"Plates, stacked and quiet.","Jars of beans and tea.","Bowls. Nothing is hiding in here.","A few tins. Nothing else.","Just a folded cloth in here now.","Folded cloths and an old kettle."};

        public Mug CupMug=>cup?cup.GetComponent<Mug>():null;
        // The mug nearest the spout, if one is close enough to catch the coffee.
        public Mug UnderSpout()
        {
            Mug best=null;float closest=.44f;
            if(mugs==null||!nozzle)return null;
            foreach(var m in mugs)
            {
                if(!m||!m.gameObject.activeInHierarchy)continue;
                float distance=Vector3.Distance(m.transform.position,nozzle.position);
                if(distance<closest){best=m;closest=distance;}
            }
            return best;
        }
        public bool IsAway(Mug m)=>away.Contains(m);
        public void Brew()
        {
            if(Time.time<nextBrew||director.currentRoom!=3)return;nextBrew=Time.time+1;
            var under=UnderSpout();
            // Once the three pours are done there is enough, with one gentle exception: the hidden orange mug can still be filled, once.
            bool bonus=fills>=3&&helped&&under&&under.orange&&under.level==0;
            if(fills>=3&&!bonus){director.Say("You have enough. Let this cup be enough.");return;}
            if(!helped||!under)
            {
                mistakes=Mathf.Min(8,mistakes+1);RefreshAudience();if(spillSpray)spillSpray.Play();director.PlayAt(director.spill,nozzle.position,.8f);
                if(spills.Length>0)spills[(mistakes-1)%spills.Length].SetActive(true);
                display.text=!helped?"PRESSURE FAULT\n<color=#EB997F>Support is available.</color>":"PLACE CUP\n<color=#ABC4C5>Below the brass spout.</color>";
                director.Say(!helped?"The machine is failing. That is not a verdict on you. ASK FOR HELP is beside the machine.":"Bring the cup under the spout, then try again. It is okay to adjust.");
                Pull(20,.45f);
                director.SaveCheckpoint();
                return;
            }
            bool counts=fills<3;
            if(counts){fills++;director.SmallStep();}
            under.Fill(under.level+1);director.PlayAt(director.pour,nozzle.position,.8f);Pull(60,.9f);Blow(.9f);
            if(under.orange)
            {
                orangeFilled=true;
                display.text="ORANGE, AT LAST\n<color=#ABC4C5>Someone kept this for you.</color>";
                director.Say("A hidden orange mug, filled at last. Someone kept it for you.");
                director.Earn("orange-pin");
            }
            else
            {
                display.text=fills<3?"A LITTLE AT A TIME\n"+fills+" / 3":"ENOUGH\n<color=#ABC4C5>Take a breath.</color>";
                director.Say("A little warmth. Press BREW again when you want more.");
            }
            if(counts&&fills>=3)director.CompleteRoom(3);
            director.SaveCheckpoint();
        }
        public void AskForHelp()
        {
            helped=true;RefreshAudience();display.text="PRESSURE RELEASED\n<color=#ABC4C5>You do not have to do this alone.</color>";
            director.Say("Someone says: 'That machine does this to everyone.' Put the cup below the spout and brew three small pours.");
            director.Earn("scarf");
            director.SaveCheckpoint();
        }
        // ----- Cabinets -----
        // Opens or shuts a cupboard. One of them hides an orange mug.
        public void ToggleCabinet(int index)
        {
            if(doors==null||index<0||index>=doors.Length||!doors[index]||director.currentRoom!=3)return;
            var door=doors[index];door.Toggle();director.PlayAt(cabinetSound,door.transform.position,.7f);
            if(!door.open)return;
            opens++;
            if(index==OrangeCabinet&&InCabinet(orangeMug)){orangeSeen=true;director.Say("An orange mug, set aside at the back. Take it, and try it under the espresso spout.");}
            else director.Say(Contents[Mathf.Clamp(index,0,Contents.Length-1)]);
        }
        // True when the mug is on the shelf of the orange cabinet.
        public bool InCabinet(Mug m)
        {
            if(!m||!m.gameObject.activeSelf)return false;
            var p=transform.InverseTransformPoint(m.transform.position);
            return p.z>4.5f&&p.y<.95f&&p.x>1.3f&&p.x<2.6f;
        }
        // ----- Mugs that break -----
        // Called by a mug as it breaks.
        public void Broke(Mug mug,Vector3 at,Vector3 normal)
        {
            shatters++;
            var local=transform.InverseTransformPoint(at);
            bool overCounter=local.z>4.35f&&Mathf.Abs(local.x)<4.1f&&local.y>.9f;
            Fire(overCounter?shardsCounter:shardsFloor,at,mug.body);Fire(dust,at,null);
            director.PlayAt(shatterSound,at,.9f);
            if(mug.level>0)Splash(at,normal);
            if(!away.Contains(mug)){away.Add(mug);StartCoroutine(ReturnLater(mug));}
            if(Time.time>nextBrokenMessage){director.Say("Crash. It is only a mug, and it is back on its shelf. Nothing is lost.");nextBrokenMessage=Time.time+10;}
        }
        static void Fire(ParticleSystem system,Vector3 at,Color? tint)
        {
            if(!system)return;
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.transform.position=at;
            if(tint.HasValue){var main=system.main;main.startColor=new ParticleSystem.MinMaxGradient(tint.Value,Color.Lerp(tint.Value,Color.white,.6f));}
            system.Play(true);
        }
        // A splash of coffee where a full mug broke, lying flat on whatever it hit.
        void Splash(Vector3 at,Vector3 normal)
        {
            if(splashes==null||splashes.Length==0)return;
            var s=splashes[splashNext++%splashes.Length];if(!s)return;
            s.gameObject.SetActive(true);s.position=at+normal*.004f;s.rotation=Quaternion.FromToRotation(Vector3.up,normal.sqrMagnitude>.01f?normal:Vector3.up);
        }
        IEnumerator ReturnLater(Mug m){yield return new WaitForSeconds(ReturnAfter);Return(m);}
        // A mug comes back whole and empty, set down gently on its shelf, as if it had never gone.
        void Return(Mug m)
        {
            away.Remove(m);
            if(!m||!m.item||m.gameObject.activeSelf)return;
            m.Empty();m.item.BringBack();
            if(director&&isActiveAndEnabled)director.PlayAt(director.place,m.transform.position,.4f);
        }
        // Leaving the room mid-wait stops the wait, so broken mugs are brought back when the room next opens.
        void OnEnable()
        {
            foreach(var m in away.ToArray())Return(m);
            if(steam&&!steam.isPlaying)steam.Play();
        }
        // ----- The machine's lever and steam wand -----
        void Pull(float degrees,float seconds){leverPull=degrees;leverUntil=Time.time+seconds;}
        void Blow(float seconds){steamUntil=Mathf.Max(steamUntil,Time.time+seconds);}
        // The steam wand: a hiss and a plume. It works even while the pressure is faulty.
        public void Steam()
        {
            if(director.currentRoom!=3||Time.time<steamUntil-.2f)return;
            steams++;Blow(2.2f);director.PlayAt(steamSound,steam?steam.transform.position:transform.position,.6f);
            if(steams==1)director.Say("The steam wand works fine. It is only the pressure that is faulty.");
        }
        void Update()
        {
            if(lever)
            {
                float goal=Time.time<leverUntil?leverPull:0;
                leverAmount=Mathf.MoveTowards(leverAmount,goal,(goal>leverAmount?300:90)*Time.deltaTime);
                lever.localRotation=Quaternion.Euler(0,0,-(LeverRest+leverAmount));
            }
            if(steam&&Time.time<steamUntil)
            {
                if(!steam.isPlaying)steam.Play();
                steamCarry+=Time.deltaTime*SteamRate;int n=(int)steamCarry;
                if(n>0){steam.Emit(n);steamCarry-=n;}
            }
        }
        public void RestoreProgress(bool help,int errors,int pours)
        {
            helped=help;mistakes=Mathf.Clamp(errors,0,8);fills=Mathf.Clamp(pours,0,3);RefreshAudience();
            for(int i=0;i<spills.Length;i++)spills[i].SetActive(i<mistakes);
            var own=CupMug;if(own)own.Fill(fills);
            display.text=fills>=3?"ENOUGH\n<color=#ABC4C5>Take a breath.</color>":fills>0?"A LITTLE AT A TIME\n"+fills+" / 3":helped?"PRESSURE RELEASED\n<color=#ABC4C5>Place your cup below the spout.</color>":"READY?\nOne cup. One ordinary task.";
        }
        public void RefreshAudience()
        {
            int watching=0;for(int i=0;i<crowd.Length;i++){bool on=!helped&&!director.gentleAudience&&i<mistakes;crowd[i].SetActive(on);if(on)watching++;}
            if(murmur)murmur.volume=watching==0?0:Mathf.Lerp(.12f,.5f,watching/(float)Mathf.Max(1,crowd.Length));
        }
        public void ResetRoom()
        {
            helped=false;mistakes=0;fills=0;nextBrew=0;RefreshAudience();foreach(var s in spills)s.SetActive(false);
            // Mugs that broke are back at once, every mug is empty, the cupboards are shut and the coffee splashes are mopped up.
            StopAllCoroutines();foreach(var m in away.ToArray())Return(m);away.Clear();
            if(mugs!=null)foreach(var m in mugs)if(m)m.Empty();
            if(doors!=null)foreach(var door in doors)if(door)door.Snap(false);
            if(splashes!=null)foreach(var s in splashes)if(s)s.gameObject.SetActive(false);
            foreach(var system in new[]{shardsCounter,shardsFloor,dust})if(system)system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            leverUntil=0;leverAmount=0;steamUntil=0;steamCarry=0;orangeSeen=false;
            display.text="READY?\nOne cup. One ordinary task.";
        }
    }
}
