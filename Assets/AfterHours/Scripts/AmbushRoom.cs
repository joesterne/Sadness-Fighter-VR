using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // The seventh chapter: the sudden meeting where you were let go.
    // You arrive in the dark. The lights come on (with a flicker) and you are sat at one end of a very long table. Across it, two clowns stand far too tall, leaning in,
    // smiling a smile that never changes, and saying hollow things. Everything here is done from the seat, by pointing:
    //   - NAME YOUR FEELINGS: six cards on the table. Each one you name makes both clowns a little smaller and a little sillier, until they are life-sized and quiet.
    //   - TALK BACK: five replies, from confused to furious. The clowns answer with the kind of thing a company says.
    //   - THE PAPERWORK: pages and a severance packet on the table can be carried, sent at a clown, tossed, or shaken hard to tear. Nothing is lost: they come back.
    //   - BALLOONS and noses: pop a balloon, honk a nose.
    //   - STAND UP: once all six are named, a button lets you stand. The chair rolls back, the light turns warm and a door opens. That finishes the chapter.
    // The dark opening and the clowns' size are options in the menu (DARK OPENING, GENTLE AMBUSH). Opening the menu in the dark ends the dark at once.
    // Lives on the room's root, so it is switched off with the room.
    public class AmbushRoom : MonoBehaviour
    {
        public const int Room=8,Feelings=6,Replies=5;
        // How big the clowns are at the start, in a gentle ambush, and once every feeling is named (1 is a person).
        public const float StartSize=2.8f,GentleSize=1.9f,EndSize=.8f;
        // How far the giants lean in over the table at the start. A gentle ambush stands them upright.
        public const float StartLean=11f;
        // How long the room stays dark before the lights flicker on.
        public const float DarkTime=3f;
        // A page that was hit or torn is back on the table this long afterwards.
        public const float ReturnAfter=1.8f;
        // Tearing: shake a held page side to side, at least this fast, this many times in a row, each within this long of the last.
        public const float ShakeSpeed=1.6f,ShakeGap=.7f,ShakeWindow=.1f;public const int ShakeFlips=4;
        // How fast the clowns grow smaller, in size per second.
        const float SizeRate=.9f;
        public static readonly float[] Flicker={.07f,.14f,.06f,.30f,.45f,.09f};

        public static readonly string[] FeelingNames={"CONFUSED","BLINDSIDED","HURT","ANGRY","I WANT\nA REASON","WORRIED ABOUT\nWHAT'S NEXT"};
        static readonly string[] FeelingLines=
        {
            "Confused is allowed. It all happened so fast.",
            "Blindsided is allowed. Nobody gave you time to get ready.",
            "Hurt makes sense. You cared about this place.",
            "Angry is allowed. It says something mattered.",
            "Wanting a reason is fair. Sometimes there is no good one.",
            "Worry about what comes next is allowed. It does not have to be solved today."
        };
        public static readonly string[] ReplyNames={"WAIT. WHAT JUST\nHAPPENED?","CAN YOU\nEXPLAIN THAT?","I GAVE YOU\nYEARS.","THAT IS\nNOT OKAY.","I AM\nFURIOUS."};
        static readonly string[] ReplyLeft={"This isn't personal.","It is a restructuring.","We value your contribution.","We hear you.","We cannot discuss that."};
        static readonly string[] ReplyRight={"Mm-hm.","It is a pattern, really.","Truly. Honk.","We have a form for that.","Would anyone like a balloon?"};
        // Which clown answers a reply: 0 the left one, 1 the right one, 2 both.
        static readonly int[] ReplyWho={0,2,2,1,2};
        static readonly string[] ReplyHints=
        {
            "Of course you are confused. It happened fast.",
            "Asking for a reason is fair, even if nobody here has one.",
            "Years of real work. That was yours.",
            "You can say it is not okay. It is not.",
            "Furious is allowed here. Nothing in this room is hurt by you saying it."
        };
        // What the clowns say as they get smaller, from the first moment to the last: the left clown, then the right one.
        static readonly string[] SayLeft={"Thank you for coming in.","We value your contribution.","Your position has been eliminated.","We wish you every success.","We cannot discuss the reasons.","Honestly, we are not sure either.","..."};
        static readonly string[] SayRight={"This isn't personal.","It is a restructuring.","Please do not read into it.","Sign here. And here. Honk.","There is a snack in the break room.","We were told to say it was a restructuring.","..."};
        static readonly string[] HitLines={"Thud. Right in the middle of the meeting.","The clown did not expect paperwork back.","A page, delivered. They look worried.","That one was for them to read."};
        static readonly string[] TearLines={"Rrrip. It is only paper.","Torn in two. It will be back on the table.","Shaken to bits. Nothing here is lost."};

        [Serializable] public class Giant
        {
            public Transform root,nose;
            // Where a page sent at this clown comes to rest, on its chest.
            public SendTarget aim;
            public GameObject[] balloons;
            // The sillier things that appear as feelings are named.
            public GameObject propeller,bowtie;
            [NonSerialized] public float flinchUntil;
        }

        public GameDirector director;
        // Everything in the room that moves, changes or switches on and off while playing is built under this one object, and kept out of the merged scenery.
        public Transform changing;
        public Giant[] clowns;
        // The black-to-light switch for the lights, the button that appears once everything is named, and the door that opens when you stand.
        public GameObject standUpButton,exitDoor;
        public GameObject[] warmThings;
        public Transform chair;
        // The screen on the table: how many feelings are named, what you said, what they said.
        public TextMeshPro board;
        public Renderer[] cardPlates;
        public TextMeshPro[] cardLabels;
        public Material cardOpen,cardNamed;
        public Grabbable[] papers;
        // One-shot systems that are moved to where the paper or the balloon was and played again.
        public ParticleSystem puff,balloonBits;
        public AudioClip lightsSound,squeakSound,balloonSound,tearSound,standSound;

        // For tests and the menu.
        public int named{get;private set;}
        public int namedMask{get;private set;}
        public int replies{get;private set;}
        public int repliesMask{get;private set;}
        public int hits{get;private set;}
        public int tears{get;private set;}
        public int honks{get;private set;}
        public int balloonsPopped{get;private set;}
        public int poppedMask{get;private set;}
        public bool standing{get;private set;}
        // True while the room is dark (including the flicker). lightsOn becomes true once the lights are up for good.
        public bool dark{get;private set;}
        public bool lightsOn{get;private set;}
        public float size{get;private set;}
        public string leftLine{get;private set;}="";
        public string rightLine{get;private set;}="";
        public string youLine{get;private set;}="";
        public Vector3 lastHitAt{get;private set;}
        public bool IsAway(Grabbable page)=>away.Contains(page);

        readonly List<Grabbable> away=new List<Grabbable>();
        class Shake{public readonly List<Vector2> trail=new List<Vector2>();public float velocity;public int direction,flips;public float flipTime;}
        readonly Dictionary<Grabbable,Shake> shakes=new Dictionary<Grabbable,Shake>();
        float[] floorTime;
        Coroutine opening;bool flashing;
        Vector3 chairHome;Quaternion chairRotation;bool chairKnown;
        float chairBack,chairTarget;
        // Light and colour of the room: cold and harsh at first, warm once you stand. Sky, light and fog are shared by the whole scene, so the room sets them
        // as the player arrives and puts them back as they leave.
        struct Look{public Color sky,equator,ground,sun;public float sunIntensity;}
        Look day,lookFrom,lookTo;bool haveDay,changedLook;float lookTime=1,lookLength=1;
        static readonly Look Cool=new Look{sky=new Color32(0x8D,0xA1,0xB4,255),equator=new Color32(0x7A,0x8C,0x9C,255),ground=new Color32(0x4C,0x59,0x64,255),sun=new Color32(0xD8,0xE6,0xF5,255),sunIntensity=1.1f};
        static readonly Look Warm=new Look{sky=new Color32(0xE8,0xC7,0x9C,255),equator=new Color32(0xD4,0xA9,0x78,255),ground=new Color32(0x8F,0x6E,0x52,255),sun=new Color32(0xFF,0xC9,0x8A,255),sunIntensity=1.5f};
        static Look Capture()
        {
            var light=RenderSettings.sun;
            return new Look{sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,sun=light?light.color:Color.white,sunIntensity=light?light.intensity:1};
        }
        static void Apply(Look look)
        {
            RenderSettings.ambientSkyColor=look.sky;RenderSettings.ambientEquatorColor=look.equator;RenderSettings.ambientGroundColor=look.ground;
            var light=RenderSettings.sun;if(light){light.color=look.sun;light.intensity=look.sunIntensity;}
        }
        static Look Mix(Look a,Look b,float t)=>new Look{sky=Color.Lerp(a.sky,b.sky,t),equator=Color.Lerp(a.equator,b.equator,t),ground=Color.Lerp(a.ground,b.ground,t),sun=Color.Lerp(a.sun,b.sun,t),sunIntensity=Mathf.Lerp(a.sunIntensity,b.sunIntensity,t)};
        void SetLook(Look target,float seconds)
        {
            if(!haveDay)return;
            lookFrom=Capture();lookTo=target;lookTime=0;lookLength=Mathf.Max(.01f,seconds);changedLook=true;
        }
        void RestoreLook(){if(haveDay&&changedLook){Apply(day);changedLook=false;lookTime=1;}}
        void OnApplicationQuit(){RestoreLook();}

        public float Top=>director&&director.gentleAmbush?GentleSize:StartSize;
        public float TargetSize=>Mathf.Lerp(Top,EndSize,named/(float)Feelings);
        public bool Gentle=>director&&director.gentleAmbush;

        // ---- Coming and going
        // Called as the player arrives (after the fade-in has started), once the sea's light is put back if they came from the ocean.
        public void OnArrive()
        {
            if(!director)return;
            if(!changedLook){day=Capture();haveDay=true;}
            if(floorTime==null||floorTime.Length!=(papers==null?0:papers.Length))floorTime=new float[papers==null?0:papers.Length];
            standing=director.rooms[Room].complete&&named>=Feelings;
            Begin(false);
        }
        // Starts the room from where it stands: a fresh room opens in the dark (if that is on), anything else lights up at once.
        void Begin(bool announce)
        {
            StopOpening();ReturnPapers();ResetProps();ApplyAll(true);
            bool fresh=named==0&&!standing;
            if(fresh&&director.darkOpening&&isActiveAndEnabled)opening=StartCoroutine(Opening());
            else LightsUp(announce);
        }
        IEnumerator Opening()
        {
            dark=true;lightsOn=false;flashing=false;
            SetLook(Cool,.01f);
            yield return new WaitForSeconds(DarkTime);
            if(director)director.PlayUi(lightsSound,.8f);
            for(int k=0;k<Flicker.Length;k++){flashing=k%2==0;yield return new WaitForSeconds(Flicker[k]);}
            opening=null;LightsUp(true);
        }
        void StopOpening(){if(opening!=null){StopCoroutine(opening);opening=null;}flashing=false;}
        // Ends the dark at once (the menu was opened, or something was pressed).
        public void SkipDark(){if(!dark)return;StopOpening();LightsUp(true);}
        void LightsUp(bool announce)
        {
            StopOpening();dark=false;lightsOn=true;ClearFade();
            SetLook(standing?Warm:Cool,standing?.01f:.6f);
            if(announce)Announce();
        }
        void ClearFade()
        {
            var player=director?director.player:null;if(!player||!player.fade)return;
            var c=player.fade.color;player.fade.color=new Color(c.r,c.g,c.b,0);
        }
        // While the room is dark the view is held black over the usual fade, and flashes clear for each flicker.
        void LateUpdate()
        {
            if(!dark||!director||director.currentRoom!=Room)return;
            var player=director.player;if(!player||!player.fade)return;
            var c=player.fade.color;player.fade.color=new Color(c.r,c.g,c.b,flashing?0:1);
        }
        public string ArrivalMessage()=>dark?"":Line();
        // What the room says as the lights come up, or when you come back to a room that is already lit.
        public void Announce()
        {
            if(!director||director.currentRoom!=Room||dark)return;
            director.Say(Line());
        }
        string Line()
        {
            if(standing)return "The room is quiet and warm. The door at the back is open. Go when you are ready, or stay a while.";
            if(named>=Feelings)return "They are quiet now. STAND UP when you are ready.";
            if(named>0)return "Name a feeling: point at a card and "+Wording.PutBack+". "+(Feelings-named)+" more, in any order.";
            return "The lights are on. Two clowns across the table. Name how you feel: point at a card and "+Wording.PutBack+". Each one makes them smaller.";
        }
        void OnDisable()
        {
            if(!Application.isPlaying)return;
            StopOpening();dark=false;lightsOn=false;RestoreLook();
        }

        // ---- Each frame
        void Update()
        {
            if(!director||director.currentRoom!=Room)return;
            if(dark&&director.menu&&director.menu.IsOpen)SkipDark();
            if(lookTime<1){lookTime=Mathf.Min(1,lookTime+Time.deltaTime/lookLength);Apply(Mix(lookFrom,lookTo,Mathf.SmoothStep(0,1,lookTime)));}
            float target=TargetSize;
            if(!Mathf.Approximately(size,target)){size=Mathf.MoveTowards(size,target,SizeRate*Time.deltaTime);}
            Place();Chair();
            if(!lightsOn)return;
            PollPapers();DetectTears();KeepPapers();
        }
        // The giants: size, how close they stand, how far they lean, a slow sway that quiets as they shrink, and a recoil when something hits them.
        void Place()
        {
            if(clowns==null)return;
            float top=Top;float t=Mathf.Clamp01(Mathf.InverseLerp(EndSize,top,size));
            float lean=Gentle?0:StartLean*t;
            for(int i=0;i<clowns.Length;i++)
            {
                var c=clowns[i];if(c==null||!c.root)continue;
                float side=i==0?-1:1;float x=side*Mathf.Lerp(1.3f,2.6f,t),z=2.7f+size;
                float yaw=Mathf.Atan2(-x,-1.9f-z)*Mathf.Rad2Deg;
                float calm=Mathf.Clamp01(1-named/(float)Feelings);
                float roll=Mathf.Sin(Time.time*(.7f+.17f*i))*1.1f*calm;
                float hit=Mathf.Clamp01((c.flinchUntil-Time.time)/.5f);
                c.root.localPosition=new Vector3(x,0,z);
                c.root.localRotation=Quaternion.Euler(lean-7f*hit,yaw,roll+Mathf.Sin(Time.time*38f)*3f*hit);
                c.root.localScale=Vector3.one*size;
                if(c.aim)c.aim.radius=.6f+.5f*size;
            }
        }
        void Chair()
        {
            if(!chair)return;
            if(!chairKnown){chairHome=chair.localPosition;chairRotation=chair.localRotation;chairKnown=true;}
            chairBack=Mathf.MoveTowards(chairBack,chairTarget,Time.deltaTime/1.3f);
            float s=Mathf.SmoothStep(0,1,chairBack);
            chair.localPosition=chairHome+new Vector3(0,0,-.9f*s);chair.localRotation=Quaternion.Euler(0,24*s,0)*chairRotation;
        }
        // The state of the room at once: the clowns' size and sillier props, the cards, the screen, the stand-up button and door, the chair and the warm lamps.
        void ApplyAll(bool immediate)
        {
            if(immediate)size=TargetSize;
            if(!chairKnown&&chair){chairHome=chair.localPosition;chairRotation=chair.localRotation;chairKnown=true;}
            chairTarget=standing?1:0;if(immediate)chairBack=chairTarget;
            if(clowns!=null)foreach(var c in clowns){if(c==null)continue;if(c.propeller)c.propeller.SetActive(named>=2);if(c.bowtie)c.bowtie.SetActive(named>=4);}
            if(warmThings!=null)foreach(var w in warmThings)if(w)w.SetActive(standing);
            if(standUpButton)standUpButton.SetActive(named>=Feelings&&!standing);
            if(exitDoor)exitDoor.SetActive(standing);
            SetLines();RefreshCards();Place();Chair();
        }
        void SetLines()
        {
            int stage=Mathf.Clamp(named,0,SayLeft.Length-1);
            leftLine=SayLeft[stage];rightLine=SayRight[stage];youLine=standing?"I AM STANDING UP.":"";
            if(standing){leftLine="...";rightLine="...";}
            ShowBoard();
        }
        void ShowBoard()
        {
            if(!board)return;
            string you=string.IsNullOrEmpty(youLine)?"":"<color=#E4BD7E>YOU  "+youLine.Replace("\n"," ")+"</color>\n";
            board.text="<size=62%><color=#ABC4C5>FEELINGS NAMED  "+named+" / "+Feelings+"</color></size>\n"+you+"<size=62%><color=#ABC4C5>LEFT</color></size>  "+leftLine+"\n<size=62%><color=#ABC4C5>RIGHT</color></size>  "+rightLine;
        }
        void RefreshCards()
        {
            for(int i=0;i<Feelings;i++)
            {
                bool done=(namedMask&(1<<i))!=0;
                if(cardPlates!=null&&i<cardPlates.Length&&cardPlates[i])cardPlates[i].sharedMaterial=done?cardNamed:cardOpen;
                if(cardLabels!=null&&i<cardLabels.Length&&cardLabels[i]){cardLabels[i].text=done?FeelingNames[i]+"\n<size=55%>NAMED</size>":FeelingNames[i];cardLabels[i].color=done?new Color(.07f,.12f,.18f):Color.white;}
            }
        }
        // The screen says all of it again: used after naming, replying or restoring.
        void ResetProps()
        {
            if(clowns!=null)foreach(var c in clowns)
            {
                if(c==null||c.balloons==null)continue;
                foreach(var b in c.balloons)if(b)b.SetActive(true);
                c.flinchUntil=0;
            }
            poppedMask=0;
        }

        // ---- Feelings, replies, balloons and noses
        public bool Feel(int i)
        {
            if(!director||director.currentRoom!=Room||i<0||i>=Feelings)return false;
            if(dark){SkipDark();return false;}
            if((namedMask&(1<<i))!=0){director.Say("You already named that one. Pick another card when you are ready.");return false;}
            namedMask|=1<<i;named++;
            SetLines();RefreshCards();ApplyStage();
            if(clowns!=null&&clowns.Length>0){var c=clowns[named%clowns.Length];if(c!=null&&c.nose)director.PlayAt(squeakSound,c.nose.position,.6f);}
            Flinch(2);
            string more=named>=Feelings?"  They are quiet now. STAND UP when you are ready.":"  "+(Feelings-named)+" more, in any order.";
            director.AcceptItem(Room,FeelingNames[i].Replace("\n"," "),FeelingLines[i]+more);
            return true;
        }
        void ApplyStage()
        {
            if(clowns!=null)foreach(var c in clowns){if(c==null)continue;if(c.propeller)c.propeller.SetActive(named>=2);if(c.bowtie)c.bowtie.SetActive(named>=4);}
            if(standUpButton)standUpButton.SetActive(named>=Feelings&&!standing);
        }
        public bool Reply(int i)
        {
            if(!director||director.currentRoom!=Room||i<0||i>=Replies)return false;
            if(dark){SkipDark();return false;}
            repliesMask|=1<<i;replies++;
            youLine=ReplyNames[i].Replace("\n"," ");
            if(named>=Feelings||standing){leftLine=rightLine="...";}else{leftLine=ReplyLeft[i];rightLine=ReplyRight[i];}
            ShowBoard();Flinch(ReplyWho[i]);
            director.Say(ReplyHints[i]);director.SmallStep();
            return true;
        }
        public bool PopBalloon(int index)
        {
            if(!director||director.currentRoom!=Room||clowns==null)return false;
            if(dark){SkipDark();return false;}
            int c=index/3,b=index%3;if(c<0||c>=clowns.Length||clowns[c]==null||clowns[c].balloons==null||b>=clowns[c].balloons.Length)return false;
            var balloon=clowns[c].balloons[b];if(!balloon||!balloon.activeSelf)return false;
            var at=balloon.transform.position;balloon.SetActive(false);poppedMask|=1<<index;balloonsPopped++;
            Fire(balloonBits,at);director.PlayAt(balloonSound,at,.9f);Flinch(c);
            director.Say(balloonsPopped>=clowns.Length*3?"Every balloon is gone. The clowns look a little relieved.":"Pop. The clown says: please do not do that.");
            return true;
        }
        public bool Honk(int clown)
        {
            if(!director||director.currentRoom!=Room||clowns==null||clown<0||clown>=clowns.Length||clowns[clown]==null)return false;
            if(dark){SkipDark();return false;}
            var nose=clowns[clown].nose;honks++;director.PlayAt(squeakSound,nose?nose.position:transform.position,.9f);Flinch(clown);
            if(honks==1)director.Say("Honk. They did not expect that.");
            return true;
        }
        public void Flinch(int who)
        {
            if(clowns==null)return;
            for(int i=0;i<clowns.Length;i++)if(clowns[i]!=null&&(who==2||who==i))clowns[i].flinchUntil=Time.time+.5f;
        }

        // ---- The paperwork
        // A page that is not in a hand and is inside a clown (sent to its chest, tossed or thrown at it) hits it.
        bool Inside(Giant c,Vector3 world)
        {
            if(c==null||!c.root)return false;
            var l=c.root.InverseTransformPoint(world);
            return Mathf.Abs(l.x)<.95f&&l.y>-.05f&&l.y<2.6f&&Mathf.Abs(l.z)<1f;
        }
        void PollPapers()
        {
            if(papers==null||clowns==null)return;
            foreach(var p in papers)
            {
                if(!p||!p.gameObject.activeInHierarchy||p.held||p.sending||p.processed||away.Contains(p))continue;
                for(int i=0;i<clowns.Length;i++)if(Inside(clowns[i],p.transform.position)){Hit(p,i);break;}
            }
        }
        public void Hit(Grabbable page,int clown)
        {
            if(!page||away.Contains(page))return;
            var at=page.transform.position;away.Add(page);hits++;lastHitAt=at;
            if(director.player)director.player.Drop(page);
            page.Vanish();Fire(puff,at);
            director.PlayAt(director.send,at,.7f);
            if(clowns!=null&&clown>=0&&clown<clowns.Length&&clowns[clown]!=null&&clowns[clown].nose)director.PlayAt(squeakSound,clowns[clown].nose.position,.5f);
            Flinch(clown);director.Say(HitLines[(hits-1)%HitLines.Length]);
            StartCoroutine(Wait(page));
        }
        // Shaking a held page hard from side to side tears it. Only the speed along the line between the shoulders counts, and it has to turn round several times in a row.
        void DetectTears()
        {
            if(papers==null||director.player==null)return;
            var axis=director.player.Head?director.player.Head.right:Vector3.right;axis.y=0;axis=axis.sqrMagnitude<.01f?Vector3.right:axis.normalized;
            foreach(var p in papers)
            {
                if(!p)continue;
                if(!p.held||!p.gameObject.activeInHierarchy||away.Contains(p)){if(shakes.ContainsKey(p))shakes.Remove(p);continue;}
                if(!shakes.TryGetValue(p,out var s)){s=new Shake();shakes[p]=s;}
                // Speed along the shoulder line over the last tenth of a second: steadier than frame-to-frame jumps, which spike when frames are uneven.
                float now=Time.time;var trail=s.trail;trail.Add(new Vector2(now,Vector3.Dot(p.transform.position,axis)));
                while(trail.Count>2&&now-trail[0].x>ShakeWindow)trail.RemoveAt(0);
                if(trail.Count<2||now-trail[0].x<ShakeWindow*.4f)continue;
                s.velocity=(trail[trail.Count-1].y-trail[0].y)/(now-trail[0].x);
                if(s.flips>0&&Time.time-s.flipTime>ShakeGap){s.flips=0;s.direction=0;}
                int dir=s.velocity>ShakeSpeed?1:s.velocity<-ShakeSpeed?-1:0;
                if(dir!=0&&dir!=s.direction){s.direction=dir;s.flips++;s.flipTime=Time.time;}
                if(s.flips>=ShakeFlips){shakes.Remove(p);Tear(p);}
            }
        }
        public void Tear(Grabbable page)
        {
            if(!page||away.Contains(page))return;
            var at=page.transform.position;away.Add(page);tears++;lastHitAt=at;
            if(director.player)director.player.Drop(page);
            page.Vanish();Fire(puff,at);director.PlayAt(tearSound,at,.9f);
            director.Say(TearLines[(tears-1)%TearLines.Length]);
            StartCoroutine(Wait(page));
        }
        IEnumerator Wait(Grabbable page){yield return new WaitForSeconds(ReturnAfter);Return(page);}
        // The page is back on the table, set down gently, as if nothing had happened.
        void Return(Grabbable page)
        {
            away.Remove(page);
            if(!page||page.gameObject.activeSelf)return;
            page.BringBack();
            if(director&&isActiveAndEnabled)director.PlayAt(director.place,page.transform.position,.4f);
        }
        void ReturnPapers(){foreach(var page in away.ToArray())Return(page);shakes.Clear();}
        // A page that has come to rest on the floor, where a seated player cannot reach it, glides back to the table after a while.
        void KeepPapers()
        {
            if(papers==null||floorTime==null)return;
            for(int i=0;i<papers.Length&&i<floorTime.Length;i++)
            {
                var p=papers[i];
                if(p&&p.gameObject.activeInHierarchy&&!p.held&&!p.sending&&!p.thrown&&!away.Contains(p)&&p.transform.position.y-transform.position.y<.5f)floorTime[i]+=Time.deltaTime;else floorTime[i]=0;
                if(floorTime[i]>4){floorTime[i]=0;p.ReturnHome();}
            }
        }
        static void Fire(ParticleSystem system,Vector3 at)
        {
            if(!system)return;
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);system.transform.position=at;system.Play(true);
        }

        // ---- Standing up
        public bool StandUp()
        {
            if(!director||director.currentRoom!=Room||standing||named<Feelings)return false;
            if(dark){SkipDark();return false;}
            standing=true;
            director.PlayAt(standSound,chair?chair.position:transform.position,.8f);
            youLine="I AM STANDING UP.";leftLine="...";rightLine="...";ShowBoard();
            chairTarget=1;SetLook(Warm,2.5f);
            if(warmThings!=null)foreach(var w in warmThings)if(w)w.SetActive(true);
            if(standUpButton)standUpButton.SetActive(false);
            if(exitDoor)exitDoor.SetActive(true);
            director.CompleteRoom(Room);
            return true;
        }

        // ---- Stills
        // For pictures made outside Play Mode: the room with some feelings named, or after standing up, lit as it would be. EndPreview puts everything back.
        bool previewing,previewGentle;
        public void Preview(int namedCount,bool stood,bool gentle)
        {
            if(!director)return;
            if(!previewing){previewGentle=director.gentleAmbush;day=Capture();haveDay=true;}
            previewing=true;director.gentleAmbush=gentle;
            named=Mathf.Clamp(namedCount,0,Feelings);namedMask=(1<<named)-1;standing=stood;
            ApplyAll(true);Apply(stood?Warm:Cool);
            foreach(var text in director.rooms[Room].root.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate();
        }
        public void EndPreview()
        {
            if(!previewing)return;previewing=false;
            if(director)director.gentleAmbush=previewGentle;
            named=0;namedMask=0;standing=false;ApplyAll(true);Apply(day);haveDay=false;changedLook=false;
        }

        // ---- Options and saving
        // GENTLE AMBUSH changed: the clowns are smaller and upright from the start, or big again. Shown at once if the room is open.
        public void OptionChanged()
        {
            if(director&&director.currentRoom==Room&&isActiveAndEnabled)ApplyAll(false);
        }
        // Saved progress: which feelings are named. A finished room shows its warm ending.
        public void Restore(int mask)
        {
            mask&=(1<<Feelings)-1;namedMask=mask;named=0;for(int i=0;i<Feelings;i++)if((mask&(1<<i))!=0)named++;
            if(director){director.rooms[Room].accepted=named;standing=director.rooms[Room].complete&&named>=Feelings;}
        }
        // TRY AGAIN: everything named is un-named, the clowns are big again, the paperwork is back, and the room opens in the dark once more (if that is on).
        public void ResetRoom()
        {
            StopAllCoroutines();opening=null;
            namedMask=0;named=0;replies=0;repliesMask=0;hits=0;tears=0;honks=0;balloonsPopped=0;poppedMask=0;standing=false;
            away.Clear();shakes.Clear();
            if(papers!=null)foreach(var p in papers)if(p){p.BringBack();}
            if(puff)puff.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(balloonBits)balloonBits.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(director&&director.currentRoom==Room&&isActiveAndEnabled)Begin(false);else{ResetProps();ApplyAll(true);}
        }
    }
}
