using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace AfterHours
{
    // The eighth chapter: The NDA web. Everything you agreed never to talk about has spun itself around you.
    // You arrive in a ring of cobwebs, each one a clause, with a machete already in your hand. In the far corner a spider the size of a
    // person sits and watches, its eyes glowing red. Everything here is done from the seat:
    //   - HACK AWAY: a swing of the machete through a web cuts it. Short chops count. Either hand can hold it: reach over with the other hand and take it.
    //   - THE WEBS GROW BACK: a cut web starts to grow back after a while. Clear every web before any grows back and the chapter is done.
    //     Each time a web grows back, the next one waits a little longer, so the room always gives in, however slow the swings.
    //   - KILL IT WITH FIRE: an unlabelled button on the wall. Pressing it burns the spider in a blast of fire, and earns the hat of flames.
    // Lives on the room's root, so it is switched off with the room.
    public class WebRoom : MonoBehaviour
    {
        public const int Room=9;
        // A cut web starts to grow back this long after it was cut, and takes GrowSeconds to grow whole.
        // Every time one grows back the wait grows by RegrowStep, up to RegrowMost.
        public const float RegrowAfter=12f,RegrowStep=3f,RegrowMost=30f,GrowSeconds=2.5f;
        // How long the spider takes to burn away.
        public const float BurnSeconds=1.6f;
        // The clauses the webs are spun from, one per column of webs, so the same words never stand side by side.
        public static readonly string[] Clauses={"DO NOT\nDISCUSS","NON-\nDISPARAGEMENT","CONFIDENTIAL","NON-\nCOMPETE","NO\nCOMMENT","STRICTLY\nPRIVATE","MUTUAL\nSILENCE","IN\nPERPETUITY","SIGN\nHERE"};
        static readonly string[] CutLines={"Snip. That clause is gone.","Cut through. You are allowed to talk about your own life.","Another one down. Short chops count.","Hacked away. Keep going, or rest. Both are fine."};

        public GameDirector director;
        // Everything that moves or switches on and off while playing is built under this one object, and kept out of the merged scenery.
        public Transform changing;
        public Cobweb[] webs=Array.Empty<Cobweb>();
        public Grabbable machete;
        // The spider: its body (which bobs and turns to watch you), its legs (which curl when it burns), its glowing eyes,
        // and the little heap of ash it leaves.
        public Transform spider;
        public Transform[] legs=Array.Empty<Transform>();
        public Renderer[] eyes=Array.Empty<Renderer>();
        public Renderer[] spiderBody=Array.Empty<Renderer>();
        public GameObject ash;
        public Material charred;
        // Bits of silk thrown from a cut, the fire, and the smoke after it.
        public ParticleSystem silk, flames, smoke;
        public AudioClip swingSound,cutSound,growSound,fireSound,hissSound,calmSound;

        // For tests and the heading.
        public int Cleared=>webs.Count(w=>w&&w.cut);
        public int Standing=>webs.Length-Cleared;
        public float regrowDelay{get;private set;}=RegrowAfter;
        public int cuts{get;private set;}
        public int regrowths{get;private set;}
        // True once every web was cut at the same time on this visit: the webs stop growing back.
        public bool beaten{get;private set;}
        public bool burned{get;private set;}
        public bool Burning=>burning!=null;
        Coroutine burning;
        Vector3 spiderHome;Quaternion spiderTurn;Vector3 spiderSize;Quaternion[] legHome;Material[] bodyLook;bool known;
        float nextGrowSound;bool spoke,warned;

        // ---- Coming and going
        // Called as the player arrives. Every visit starts with the webs whole, the spider in its corner and the machete in hand.
        public void OnArrive()
        {
            if(!director)return;
            Remember();StopBurning();
            beaten=false;regrowDelay=RegrowAfter;cuts=0;regrowths=0;spoke=false;warned=false;
            foreach(var w in webs)if(w){w.cut=false;w.grown=1;Show(w);}
            burned=false;ShowSpider();
            Count();Give();
        }
        void Remember()
        {
            if(known||!spider)return;known=true;
            spiderHome=spider.localPosition;spiderTurn=spider.localRotation;spiderSize=spider.localScale;
            legHome=legs.Select(l=>l?l.localRotation:Quaternion.identity).ToArray();
            bodyLook=spiderBody.Select(r=>r?r.sharedMaterial:null).ToArray();
        }
        // The machete is always in a hand while you are here. If neither hand can take it yet (tracking has not found one), it waits on its hook beside the seat.
        void Give(){if(machete&&!machete.held&&!machete.sending&&director.player&&!director.player.busy)director.player.Give(machete);}
        public string Line()
        {
            if(beaten)return "The webs are gone. Nothing here is holding you to silence. Stay a while, or go when you are ready.";
            return "Webs all round you, and something in the corner. The machete is already in your hand: swing it through the webs. Clear every one before any grows back.";
        }

        // ---- Each frame
        void Update()
        {
            if(!director||director.currentRoom!=Room)return;
            Give();
            if(!beaten)foreach(var w in webs)if(w&&w.cut&&Time.time>=w.regrowAt)Regrow(w);
            foreach(var w in webs)if(w&&!w.cut&&w.grown<1){w.grown=Mathf.Min(1,w.grown+Time.deltaTime/GrowSeconds);Show(w);}
            Watch();
        }
        // A cut web shows nothing; one growing back spreads out from its hub.
        void Show(Cobweb w)
        {
            if(!w.silk)return;
            bool visible=!w.cut;if(w.silk.gameObject.activeSelf!=visible)w.silk.gameObject.SetActive(visible);
            if(visible)w.silk.localScale=Vector3.one*Mathf.Max(.05f,Mathf.SmoothStep(0,1,w.grown));
        }
        // The spider breathes, shifts its weight and turns a little to keep you in view.
        void Watch()
        {
            if(!spider||burned||Burning||!known)return;
            float t=Time.time;
            spider.localPosition=spiderHome+Vector3.up*(Mathf.Sin(t*1.3f)*.02f);
            var head=director.player?director.player.Head:null;
            if(head)
            {
                var to=spider.parent?spider.parent.InverseTransformPoint(head.position)-spiderHome:head.position-spider.position;to.y=0;
                if(to.sqrMagnitude>.01f){var look=Quaternion.LookRotation(to.normalized,Vector3.up);spider.localRotation=Quaternion.RotateTowards(spider.localRotation,Quaternion.Slerp(spiderTurn,look,.5f),20*Time.deltaTime);}
            }
            for(int i=0;i<legs.Length&&i<legHome.Length;i++)if(legs[i])legs[i].localRotation=legHome[i]*Quaternion.Euler(0,0,Mathf.Sin(t*2.1f+i*1.7f)*2.5f);
        }

        // ---- Cutting
        public void PlaySwing(Vector3 at,float speed){if(director)director.PlayAt(swingSound,at,Mathf.Clamp(.25f+speed/10f,.25f,.6f));}
        public bool Cut(Cobweb web,float speed,Vector3 point)
        {
            if(!web||web.cut||!director||director.currentRoom!=Room)return false;
            // A web that is still growing back is cut as easily as a whole one.
            web.cut=true;web.regrowAt=Time.time+regrowDelay;web.grown=0;Show(web);cuts++;
            Burst(point);director.PlayAt(cutSound,point,Mathf.Clamp(.55f+speed/8f,.55f,1f));
            if(director.player)director.player.PulseFor(machete,Mathf.Clamp(.3f+speed/7f,.3f,.9f));
            Count();director.SmallStep();
            if(Standing==0&&!beaten){Beat();return true;}
            if(!spoke){spoke=true;director.Say(CutLines[0]+" Clear every web before any grows back.");}
            else if(cuts%6==0)director.Say(CutLines[(cuts/6)%CutLines.Length]+"  "+Standing+" to go.");
            return true;
        }
        void Regrow(Cobweb w)
        {
            w.cut=false;w.grown=0;Show(w);regrowths++;
            regrowDelay=Mathf.Min(RegrowMost,regrowDelay+RegrowStep);
            Count();
            if(Time.time>nextGrowSound){nextGrowSound=Time.time+.4f;director.PlayAt(growSound,w.transform.position,.55f);}
            if(!warned){warned=true;director.Say("One is growing back. Each time they do, they grow back a little slower. There is no rush.");}
        }
        void Count()
        {
            if(!director)return;
            director.rooms[Room].accepted=Cleared;director.RefreshProgress();
        }
        // Every web cut at once: the room goes quiet, the webs stay gone, and the chapter is done.
        void Beat()
        {
            beaten=true;director.PlayUi(calmSound,.5f);
            if(!director.rooms[Room].complete)director.CompleteRoom(Room);
            else director.Say("All of it, cut away again. Nothing here is holding you to silence.");
        }
        void Burst(Vector3 at)
        {
            if(!silk)return;
            var p=new ParticleSystem.EmitParams{position=at,applyShapeToPosition=true};
            silk.Emit(p,24);
        }

        // ---- Kill it with fire
        // The unlabelled button on the wall. The spider goes up in a blast of fire, and the hat of flames is yours.
        public bool Burn()
        {
            if(!director||director.currentRoom!=Room)return false;
            if(burned||Burning){director.Say("Click. There is nothing left in the corner to burn.");return false;}
            Remember();
            var at=spider?spider.position+Vector3.up*.5f:transform.position;
            if(flames){flames.transform.position=at;flames.Play(true);}
            director.PlayAt(fireSound,at,1f);director.PlayAt(hissSound,at,.7f);
            burning=StartCoroutine(BurnAway());
            director.Say("Kill it with fire. Whoomph. The corner is empty.");
            director.Earn("flame-hat");
            return true;
        }
        IEnumerator BurnAway()
        {
            if(charred)foreach(var r in spiderBody)if(r)r.sharedMaterial=charred;
            foreach(var e in eyes)if(e)e.enabled=false;
            for(float t=0;t<1;)
            {
                t=Mathf.Min(1,t+Time.deltaTime/BurnSeconds);float s=Mathf.SmoothStep(0,1,t);
                // The legs curl up over it and it shrivels down onto the floor. Each leg's local +x points away from the body, so turning about its z lifts it.
                for(int i=0;i<legs.Length&&i<legHome.Length;i++)if(legs[i])legs[i].localRotation=legHome[i]*Quaternion.Euler(0,0,75*s);
                if(spider){spider.localScale=spiderSize*Mathf.Lerp(1,.3f,s);spider.localPosition=spiderHome;}
                yield return null;
            }
            if(spider)spider.gameObject.SetActive(false);
            if(ash)ash.SetActive(true);
            if(smoke){smoke.transform.position=spider?spider.position+Vector3.up*.2f:transform.position;smoke.Play(true);}
            burned=true;burning=null;
        }
        void StopBurning(){if(burning!=null){StopCoroutine(burning);burning=null;}}
        void ShowSpider()
        {
            if(!spider)return;
            spider.gameObject.SetActive(true);spider.localPosition=spiderHome;spider.localRotation=spiderTurn;spider.localScale=spiderSize;
            for(int i=0;i<legs.Length&&i<legHome.Length;i++)if(legs[i])legs[i].localRotation=legHome[i];
            for(int i=0;i<spiderBody.Length&&i<bodyLook.Length;i++)if(spiderBody[i])spiderBody[i].sharedMaterial=bodyLook[i];
            foreach(var e in eyes)if(e)e.enabled=true;
            if(ash)ash.SetActive(false);
            if(flames)flames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(smoke)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ---- TRY AGAIN: the webs are whole again, the spider is back, and the chapter stays finished.
        public void ResetRoom()
        {
            if(silk)silk.Clear();
            if(director&&director.currentRoom==Room&&isActiveAndEnabled)OnArrive();
            else{beaten=false;burned=false;foreach(var w in webs)if(w){w.cut=false;w.grown=1;Show(w);}}
        }
        void OnDisable(){if(!Application.isPlaying)return;StopBurning();}
    }
}
