using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Regression test for the seventh chapter, The ambush: the dark opening and the flicker, the two giant clowns and how they shrink,
    // naming six feelings, the five replies, balloons and noses, the paperwork (sent, thrown, torn), standing up, the way out,
    // the two menu options, TRY AGAIN, and saving.
    public static class AmbushValidation
    {
        static IEnumerator<float> run;static double next;static float deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key,Wardrobe.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;static bool oldDark,oldGentle;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        static AmbushRoom A=>D.ambush;
        static Transform Root=>D.rooms[AmbushRoom.Room].root.transform;
        static Color Sky=>RenderSettings.ambientSkyColor;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            oldDark=D.darkOpening;oldGentle=D.gentleAmbush;
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("AmbushValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("AmbushValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Info(string text){lines.Add("INFO: "+text);}
        static void Aim(Interactable b)
        {
            P.Head.rotation=Quaternion.LookRotation(b.transform.position-P.Head.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Interactable>()!=b)
                throw new Exception("Pointer target obstructed: "+b.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        static bool Reachable(Interactable b)
        {
            P.Head.rotation=Quaternion.LookRotation(b.transform.position-P.Head.position);
            return Physics.Raycast(P.Head.position,P.Head.forward,out var hit,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<Interactable>()==b;
        }
        static void Look(Vector3 point){P.Head.rotation=Quaternion.LookRotation(point-P.Head.position);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static IEnumerable<float> Click(Interactable b){Check(b,"The button to press exists");Aim(b);Press(true);yield return .12f;Press(false);yield return .2f;}
        static IEnumerable<float> Click(ActionKind kind)=>Click(D.menu.Button(kind));
        static IEnumerable<float> Hold(Key key,float seconds){Queue(keyboard,new KeyboardState(key));yield return seconds;Queue(keyboard,new KeyboardState());yield return .15f;}
        static IEnumerable<float> Tab(){foreach(var t in Hold(Key.Tab,.1f))yield return t;}
        static IEnumerable<float> OpenMenu(){for(int i=0;i<2&&!D.menu.IsOpen;i++)foreach(var t in Tab())yield return t;}
        static IEnumerable<float> CloseMenu(){for(int i=0;i<2&&D.menu.IsOpen;i++)foreach(var t in Tab())yield return t;}
        static IEnumerable<float> Take(Grabbable page)
        {
            Look(page.transform.position);yield return .06f;Press(true);yield return .4f;
            Check(page.held&&P.IsHolding(page),"Pointing at \""+page.label+"\" and pinching picks it up");
        }
        // Moves a stand-in hand to and fro along a line, each stroke at the given speed, until the check says stop.
        static IEnumerable<float> Shake(Transform hand,Vector3 way,float speed,int strokes,float strokeTime,Func<bool> stop)
        {
            // The editor can tick more than once per game frame, so the hand only moves when a new frame has begun (otherwise it would move at twice the speed asked for).
            int frame=Time.frameCount;
            for(int i=0;i<strokes&&!stop();i++)
            {
                float sign=i%2==0?1:-1;
                for(float t=0;t<strokeTime&&!stop();)
                {
                    if(Time.frameCount==frame){yield return 0f;continue;}
                    frame=Time.frameCount;
                    float dt=Mathf.Max(Time.deltaTime,.001f);hand.position+=way*(sign*speed*dt);t+=dt;
                    yield return 0f;
                }
            }
            yield return .1f;
        }
        static Vector3 Flat(Vector3 v)=>Vector3.ProjectOnPlane(v,Vector3.up).normalized;
        static Vector3 Local(float x,float y,float z)=>Root.TransformPoint(new Vector3(x,y,z));
        static bool Near(Color a,Color b,float e=.03f)=>Mathf.Abs(a.r-b.r)<e&&Mathf.Abs(a.g-b.g)<e&&Mathf.Abs(a.b-b.b)<e;
        static readonly Color CoolSky=new Color(0x8D/255f,0xA1/255f,0xB4/255f),WarmSky=new Color(0xE8/255f,0xC7/255f,0x9C/255f);
        // The top of the clown's own body (the balloons float higher).
        static float Top(AmbushRoom.Giant c)=>c.root.GetComponentsInChildren<Renderer>().Where(r=>!c.balloons.Any(b=>b&&r.transform.IsChildOf(b.transform))).Max(r=>r.bounds.max.y)-Root.position.y;
        static float Lean(AmbushRoom.Giant c)=>Mathf.DeltaAngle(0,c.root.localEulerAngles.x);
        static Interactable Find(ActionKind kind,int value=-1)=>Root.GetComponentsInChildren<Interactable>().FirstOrDefault(x=>x.kind==kind&&(value<0||x.value==value));
        static bool Lit(AmbushRoom a)=>a.lightsOn&&!a.dark&&P.fade.color.a<.01f;

        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;
            D.darkOpening=true;D.gentleAmbush=false;
            var a=A;var lobby=D.rooms[0].root;
            // ---- What the scene holds
            Check(a&&D.rooms.Length==10&&D.rooms[AmbushRoom.Room].title=="The ambush"&&a.director==D,"The game has ten rooms, and room 8 is The ambush");
            Check(GameDirector.Chapters.Length==8&&GameDirector.IsChapter(AmbushRoom.Room)&&D.rooms[AmbushRoom.Room].total==AmbushRoom.Feelings,"The ambush is the seventh chapter, with six feelings to name");
            Check(a.clowns.Length==2&&a.clowns.All(c=>c.root&&c.nose&&c.aim&&c.balloons.Length==3&&c.balloons.All(b=>b)&&c.propeller&&c.bowtie),"Two clowns, each with a nose, three balloons, a chest to aim at, a propeller beanie and a bowtie");
            Check(a.papers.Length==6&&a.cardPlates.Length==6&&a.cardLabels.Length==6&&a.cardOpen&&a.cardNamed&&a.chair&&a.board&&a.standUpButton&&a.exitDoor&&a.warmThings.Length==3&&a.puff&&a.balloonBits&&a.changing,"Six papers, six feeling cards, a chair, the screen, the stand-up button, the way out, warm lights and two particle systems");
            Check(a.lightsSound&&a.squeakSound&&a.balloonSound&&a.tearSound&&a.standSound,"All five of the room's sounds are loaded");
            var door=lobby.GetComponentsInChildren<Interactable>().FirstOrDefault(x=>x.kind==ActionKind.Travel&&x.value==AmbushRoom.Room);
            Check(door&&D.lobbyProgress.text.Contains("0 / 8"),"The lobby has a door to the ambush, and counts eight rooms");
            Check(!a.gameObject.activeInHierarchy&&!D.gentleAmbush&&D.darkOpening,"The ambush is closed until you walk in, with the clowns full size and the dark on");
            var homes=a.papers.Select(p=>p.transform.position).ToArray();
            var daySky=Sky;

            // ---- The dark opening
            float started=Time.time;D.Travel(AmbushRoom.Room);yield return .7f;
            Check(D.currentRoom==AmbushRoom.Room&&a.dark&&!a.lightsOn,"Walking in, the room is dark");
            Check(!P.busy&&P.fade.color.a>.99f,"The view is held black, not just fading in");
            yield return 1.5f;
            Check(a.dark&&P.fade.color.a>.99f,"It stays dark for the first few seconds");
            bool sawClear=false,sawBlack=false;
            for(int i=0;i<200&&!a.lightsOn;i++)
            {
                yield return .03f;
                if(a.dark&&!a.lightsOn){if(P.fade.color.a<.01f)sawClear=true;else if(sawClear)sawBlack=true;}
            }
            float took=Time.time-started;
            Check(a.lightsOn&&!a.dark,"Then the lights come on");
            Check(sawClear&&sawBlack,"They flicker first: the view flashes clear and goes black again before they stay on");
            Check(took>3.8f&&took<5.8f,"The dark lasts about four seconds ("+took.ToString("0.0")+" s)");
            yield return .8f;
            Check(Lit(a),"Once the lights are on the view is clear");
            Check(P.HintVisible&&P.hint.text.Contains("Two clowns"),"The room says what to do as the lights come up");
            Check(Near(Sky,CoolSky)&&!Near(Sky,daySky,.01f),"The room is lit cool and harsh, unlike the lobby");
            Info("the room has "+Root.GetComponentsInChildren<Renderer>().Length+" renderers and "+Root.GetComponentsInChildren<Collider>().Length+" colliders");

            // ---- Two clowns, much too large
            var c0=a.clowns[0];var c1=a.clowns[1];
            var p0=Root.InverseTransformPoint(c0.root.position);var p1=Root.InverseTransformPoint(c1.root.position);
            Check(Mathf.Abs(a.size-AmbushRoom.StartSize)<.05f&&Mathf.Abs(c0.root.localScale.x-a.size)<.01f,"The clowns start "+a.size.ToString("0.0")+" times life size");
            Check(Top(c0)>5f&&Top(c1)>5f&&P.Head.position.y<2f,"Each clown's head is over five metres up, far above the seat ("+Top(c0).ToString("0.0")+" m)");
            Check(Mathf.Abs(p0.x+2.6f)<.1f&&Mathf.Abs(p1.x-2.6f)<.1f&&p0.z>4.5f&&p1.z>4.5f,"One stands each side of the head of the room, across the table");
            Check(Mathf.Abs(Lean(c0)-AmbushRoom.StartLean)<1.5f&&Mathf.Abs(Lean(c1)-AmbushRoom.StartLean)<1.5f,"They lean in over the table ("+Lean(c0).ToString("0")+" degrees)");
            Check(Vector3.Dot(c0.root.forward,(P.Head.position-c0.root.position).normalized)>.85f&&Vector3.Dot(c1.root.forward,(P.Head.position-c1.root.position).normalized)>.85f,"They face the player");
            Check(!c0.propeller.activeSelf&&!c0.bowtie.activeSelf&&c0.balloons.All(b=>b.activeSelf)&&c1.balloons.All(b=>b.activeSelf),"At first they wear nothing silly and every balloon is up");
            Check(a.leftLine=="Thank you for coming in."&&a.rightLine=="This isn't personal."&&a.board.text.Contains("0 / 6"),"The screen shows what each clown says, and 0 of 6 feelings");

            // ---- The menu, or doing anything, ends the dark at once
            D.Travel(0);yield return .9f;
            Check(D.currentRoom==0&&!a.lightsOn&&!a.dark&&Near(Sky,daySky,.005f),"Leaving puts the lobby's light back");
            D.Travel(AmbushRoom.Room);yield return .7f;
            Check(a.dark&&P.fade.color.a>.99f,"Back in, the room is dark again");
            foreach(var t in OpenMenu())yield return t;
            Check(D.menu.IsOpen&&Lit(a),"Opening the menu in the dark brings the lights up at once");
            foreach(var t in CloseMenu())yield return t;
            D.Travel(0);yield return .9f;D.Travel(AmbushRoom.Room);yield return .7f;
            Check(a.dark&&!a.Feel(0),"A card cannot be named in the dark");
            Check(Lit(a)&&a.named==0,"But trying ends the dark, and nothing is named yet");

            // ---- DARK OPENING (menu)
            D.Travel(0);yield return .9f;
            foreach(var t in OpenMenu())yield return t;
            Check(D.menu.dark.text.Contains("ON")&&D.menu.gentle.text.Contains("OFF"),"The menu offers DARK OPENING (on) and GENTLE AMBUSH (off)");
            foreach(var t in Click(ActionKind.DarkOpening))yield return t;
            Check(!D.darkOpening&&D.menu.dark.text.Contains("OFF"),"DARK OPENING switches off");
            foreach(var t in CloseMenu())yield return t;
            D.Travel(AmbushRoom.Room);yield return .9f;
            Check(!a.dark&&Lit(a)&&D.currentRoom==AmbushRoom.Room,"With the dark off, the lights are on as soon as you arrive");
            Check(P.HintVisible&&P.hint.text.Contains("Two clowns"),"And the room says what to do");
            foreach(var t in OpenMenu())yield return t;
            foreach(var t in Click(ActionKind.DarkOpening))yield return t;
            Check(D.darkOpening&&D.menu.dark.text.Contains("ON"),"DARK OPENING switches back on, from inside the room");
            foreach(var t in CloseMenu())yield return t;

            // ---- Talking back
            var chairHome=a.chair.localPosition;
            Check(a.replies==0,"Nothing has been said yet");
            float t0=Time.time;
            foreach(var t in Click(Find(ActionKind.AmbushReply,0)))yield return t;
            Check(a.replies==1&&a.youLine.Contains("WHAT JUST")&&a.leftLine=="This isn't personal."&&a.rightLine=="Mm-hm.","\"Wait. What just happened?\" is answered by the left clown with the kind of thing a company says");
            Check(c0.flinchUntil>t0&&c1.flinchUntil<t0,"Only the clown who was spoken to flinches");
            Check(a.board.text.Contains("YOU")&&a.board.text.Contains("WAIT. WHAT JUST HAPPENED?"),"The screen shows what you said");
            for(int i=1;i<AmbushRoom.Replies;i++)
            {
                foreach(var t in Click(Find(ActionKind.AmbushReply,i)))yield return t;
                Check(a.replies==i+1&&(a.repliesMask&(1<<i))!=0&&a.youLine==AmbushRoom.ReplyNames[i].Replace("\n"," "),"Reply "+(i+1)+" of 5: "+AmbushRoom.ReplyNames[i].Replace("\n"," "));
            }
            Check(a.replies==5&&a.repliesMask==31&&a.named==0&&D.rooms[AmbushRoom.Room].accepted==0&&!D.rooms[AmbushRoom.Room].complete,"All five replies can be said, and none of them counts toward the chapter");
            Check(a.leftLine=="We cannot discuss that."&&a.rightLine=="Would anyone like a balloon?","Both clowns answer the angriest reply with the kind of thing a company says");
            Check(D.wardrobe.Has("step-pin"),"Saying something is a small step");

            // ---- Balloons and noses
            var balloon=Find(ActionKind.AmbushBalloon,0);
            foreach(var t in Click(balloon))yield return t;
            Check(a.balloonsPopped==1&&a.poppedMask==1&&!c0.balloons[0].activeSelf&&c0.balloons[1].activeSelf&&(a.balloonBits.isPlaying||a.balloonBits.particleCount>0),"Pointing at a balloon pops it, in a burst of bits");
            Check(!balloon.gameObject.activeInHierarchy&&Find(ActionKind.AmbushBalloon,0)==null,"A popped balloon is gone from the room");
            // Balloons float in clusters, so one can hide another from the pointer: pop whichever the pointer can reach, nearest first (the player's own pointer does the same).
            for(int n=1;n<6;n++)
            {
                Interactable pick=null;
                foreach(var b in Root.GetComponentsInChildren<Interactable>().Where(x=>x.kind==ActionKind.AmbushBalloon&&x.gameObject.activeInHierarchy).OrderBy(x=>Vector3.Distance(x.transform.position,P.Head.position)))
                    if(Reachable(b)){pick=b;break;}
                Check(pick!=null,"While balloons are up, the pointer can always reach one ("+(6-n)+" left)");
                foreach(var t in Click(pick))yield return t;
            }
            Check(a.balloonsPopped==6&&a.poppedMask==63&&P.hint.text.Contains("Every balloon"),"All six balloons can be popped, and the clowns look relieved");
            int honks=a.honks;
            foreach(var t in Click(Find(ActionKind.AmbushNose,0)))yield return t;
            foreach(var t in Click(Find(ActionKind.AmbushNose,1)))yield return t;
            Check(a.honks==honks+2,"Each clown's nose can be honked");

            // ---- The paperwork: sent at a clown
            var page=a.papers[0];
            foreach(var t in Take(page))yield return t;
            Look(c0.aim.LandingPoint);yield return .3f;
            Check(c0.aim.highlight.activeSelf&&!c1.aim.highlight.activeSelf,"Pointing at the left clown with a page in hand lights its outline, and only its");
            float sent=Time.time;Press(false);yield return 1.3f;
            Check(a.hits==1&&a.IsAway(page)&&!page.gameObject.activeSelf&&c0.flinchUntil>sent,"Let go, the page flies to the clown's chest, hits it, and the clown flinches");
            Check(Vector3.Distance(a.lastHitAt,c0.aim.LandingPoint)<4f,"The hit is at the clown's chest");
            yield return 2.4f;
            Check(!a.IsAway(page)&&page.gameObject.activeSelf&&Vector3.Distance(page.transform.position,homes[0])<.15f&&!page.held,"A moment later the page is back on the table, as if nothing had happened");
            // Thrown
            var thrown=a.papers[1];thrown.Throw(new Vector3(.8f,2f,9f));
            for(int i=0;i<30&&a.hits<2;i++)yield return .05f;
            Check(a.hits==2&&a.IsAway(thrown),"A page thrown toward a clown hits it too");
            yield return 2.6f;
            Check(!a.IsAway(thrown)&&thrown.gameObject.activeSelf&&Vector3.Distance(thrown.transform.position,homes[1])<.15f,"And comes back");
            // Dropped inside a clown
            var dropped=a.papers[3];dropped.Initialize();dropped.transform.position=c1.root.TransformPoint(new Vector3(0,1.2f,.3f));dropped.Body.position=dropped.transform.position;
            yield return .3f;
            Check(a.hits==3&&a.IsAway(dropped),"A page that ends up in a clown's chest counts as a hit");
            yield return 2.4f;
            Check(!a.IsAway(dropped)&&Vector3.Distance(dropped.transform.position,homes[3])<.15f,"And comes back");
            // On the floor
            var floor=a.papers[4];floor.Initialize();floor.transform.position=Local(1.2f,.1f,-.6f);floor.Body.position=floor.transform.position;
            yield return 1f;
            Check(floor.transform.position.y-Root.position.y<.5f,"A page dropped off the table lies on the floor");
            yield return 5.2f;
            Check(Vector3.Distance(floor.transform.position,homes[4])<.2f&&floor.transform.position.y-Root.position.y>.7f,"Four seconds later it glides back to the table, out of a seated player's reach");

            // ---- The paperwork: torn
            var tear=a.papers[2];int tears=a.tears;
            var hand=new GameObject("Test hand").transform;hand.position=tear.transform.position;
            Check(tear.Take(hand),"A hand takes hold of a page");yield return .2f;
            var along=Flat(P.Head.right);
            foreach(var t in Shake(hand,Vector3.up,5f,10,.15f,()=>a.tears>tears))yield return t;
            Check(a.tears==tears&&tear.held,"Moving a page quickly up and down does not tear it"+(a.tears==tears&&tear.held?"":" [tears "+(a.tears-tears)+", held "+tear.held+"]"));
            foreach(var t in Shake(hand,along,1f,10,.35f,()=>a.tears>tears))yield return t;
            Check(a.tears==tears&&tear.held,"Moving it slowly from side to side does not tear it"+(a.tears==tears&&tear.held?"":" [tears "+(a.tears-tears)+", held "+tear.held+"]"));
            foreach(var t in Shake(hand,along,4f,12,.12f,()=>a.tears>tears))yield return t;
            Check(a.tears==tears+1&&a.IsAway(tear)&&!tear.gameObject.activeSelf&&!tear.held,"Shaking it hard from side to side tears it");
            Check((a.puff.isPlaying||a.puff.particleCount>0)&&(P.hint.text.Contains("paper")||P.hint.text.Contains("Torn")||P.hint.text.Contains("Rrrip")||P.hint.text.Contains("Shaken")),"It bursts into bits of paper, and the room says what happened");
            yield return 2.4f;
            Check(!a.IsAway(tear)&&tear.gameObject.activeSelf&&Vector3.Distance(tear.transform.position,homes[2])<.15f,"A torn page is back on the table a moment later");
            UnityEngine.Object.Destroy(hand.gameObject);
            // The thick severance packet is sent at the right clown, with real input
            var packet=a.papers[5];
            foreach(var t in Take(packet))yield return t;
            Look(c1.aim.LandingPoint);yield return .3f;
            Check(c1.aim.highlight.activeSelf&&!c0.aim.highlight.activeSelf,"Pointing at the right clown with the severance packet lights its outline instead");
            int before=a.hits;Press(false);yield return 1.3f;
            Check(a.hits==before+1&&a.IsAway(packet)&&c1.flinchUntil>sent,"The packet hits the right clown");
            yield return 2.4f;
            Check(!a.IsAway(packet)&&Vector3.Distance(packet.transform.position,homes[5])<.15f,"And comes back");

            // ---- Naming feelings
            Check(a.named==0&&!a.standUpButton.activeSelf,"No feeling is named, and there is nothing to stand up from yet");
            Check(D.rooms[AmbushRoom.Room].progress.text.Contains("0 / 6")&&D.rooms[AmbushRoom.Room].progress.text.Contains("NAMED"),"The heading counts feelings named: 0 / 6");
            float previous=a.size;
            for(int i=0;i<AmbushRoom.Feelings;i++)
            {
                var card=Find(ActionKind.AmbushFeeling,i);
                foreach(var t in Click(card))yield return t;
                Check(a.named==i+1&&(a.namedMask&(1<<i))!=0&&D.rooms[AmbushRoom.Room].accepted==i+1,"Naming \""+AmbushRoom.FeelingNames[i].Replace("\n"," ")+"\" counts it ("+a.named+" of 6)");
                Check(a.cardLabels[i].text.Contains("NAMED")&&a.cardPlates[i].sharedMaterial==a.cardNamed,"The card changes colour and says NAMED");
                Check(P.hint.text.Contains("allowed")||P.hint.text.Contains("makes sense")||P.hint.text.Contains("fair"),"The room answers with a kind line");
                Check(a.board.text.Contains((i+1)+" / 6"),"The screen counts "+(i+1)+" of 6");
                for(int w=0;w<40&&Mathf.Abs(a.size-a.TargetSize)>.02f;w++)yield return .05f;
                Check(Mathf.Abs(a.size-a.TargetSize)<.03f&&a.size<previous-.2f,"Both clowns shrink: "+previous.ToString("0.00")+" to "+a.size.ToString("0.00"));
                previous=a.size;
                bool props=a.named>=2,bow=a.named>=4;
                Check(c0.propeller.activeSelf==props&&c1.propeller.activeSelf==props&&c0.bowtie.activeSelf==bow&&c1.bowtie.activeSelf==bow,"Silly things appear as they shrink (propeller from 2, bowtie from 4)");
                Check(!D.rooms[AmbushRoom.Room].complete,"The chapter is not complete yet");
                if(i==0)
                {
                    foreach(var t in Click(card))yield return t;
                    Check(a.named==1&&D.rooms[AmbushRoom.Room].accepted==1&&P.hint.text.Contains("already"),"Naming the same feeling again does not count twice");
                }
            }
            Check(Mathf.Abs(a.size-AmbushRoom.EndSize)<.03f&&Top(c0)<3.2f&&Top(c1)<3.2f,"All six named: the clowns are smaller than a person ("+a.size.ToString("0.00")+" times, head at "+Top(c0).ToString("0.0")+" m)");
            yield return .7f; // a flinch (from a hit or a kind word) leans them back for half a second
            Check(Mathf.Abs(Lean(c0))<1.5f&&Mathf.Abs(Root.InverseTransformPoint(c0.root.position).x+1.3f)<.1f,"They stand upright and have come in close [lean "+Lean(c0).ToString("0.0")+", x "+Root.InverseTransformPoint(c0.root.position).x.ToString("0.00")+", size "+a.size.ToString("0.00")+", hit "+(c0.flinchUntil-Time.time).ToString("0.00")+"]");
            Check(a.leftLine=="..."&&a.rightLine=="...","They have nothing left to say");
            Check(a.standUpButton.activeSelf&&!a.standing&&!D.rooms[AmbushRoom.Room].complete&&D.rooms[AmbushRoom.Room].accepted==6,"STAND UP appears, and the chapter waits for it");
            Check(P.hint.text.Contains("STAND UP"),"The room says so");
            // Replies at the end are met with silence
            foreach(var t in Click(Find(ActionKind.AmbushReply,4)))yield return t;
            Check(a.leftLine=="..."&&a.rightLine=="...","Once everything is named, even the angriest reply gets only quiet");
            // The big pose from the table, as a reference picture
            Look(Local(0,2.2f,5f));yield return .3f;

            // ---- Standing up
            foreach(var t in Click(Find(ActionKind.StandUp)))yield return t;
            Check(a.standing&&D.rooms[AmbushRoom.Room].complete&&D.CompletedCount==1,"STAND UP finishes the chapter");
            Check(PlayerPrefs.GetInt("AfterHours.Progress.v1")==(1<<AmbushRoom.Room),"Completion is saved as chapter 8's flag");
            Check(!a.standUpButton.activeSelf&&a.exitDoor.activeSelf&&a.warmThings.All(w=>w.activeSelf),"The button goes, the way out opens, and the warm lights come on");
            Check(a.youLine=="I AM STANDING UP."&&a.board.text.Contains("STANDING UP"),"The screen says you stood up");
            yield return 2.2f;
            Check(chairHome.z-a.chair.localPosition.z>.8f,"The chair rolls back to let you stand");
            yield return 1.5f;
            Check(Near(Sky,WarmSky,.04f),"The light turns warm");
            Check(D.wardrobe.Has("standup-pin"),"Finishing the chapter earns the stood-up pin");
            Check(D.lobbyProgress.text.Contains("1 / 8"),"The lobby counts one of eight rooms");
            var way=a.exitDoor.GetComponentsInChildren<Interactable>().FirstOrDefault(x=>x.kind==ActionKind.Travel);
            Check(way&&way.value==0,"The way out leads to the lobby");

            // ---- The way out, and coming back
            foreach(var t in Click(way))yield return t;
            yield return 1f;
            Check(D.currentRoom==0&&Near(Sky,daySky,.005f),"The way out returns to the lobby, in the lobby's own light");
            D.Travel(AmbushRoom.Room);yield return .9f;
            Check(D.currentRoom==AmbushRoom.Room&&a.standing&&Lit(a)&&!a.dark,"Coming back to a finished chapter: lights on at once, no dark");
            Check(Mathf.Abs(a.size-AmbushRoom.EndSize)<.03f&&a.exitDoor.activeSelf&&!a.standUpButton.activeSelf&&chairHome.z-a.chair.localPosition.z>.8f,"The clowns are small and quiet, the door is open and the chair is back");
            yield return 3f;
            Check(Near(Sky,WarmSky,.04f)&&P.hint.text.Contains("quiet and warm"),"The room is warm again, and says it is quiet");

            // ---- TRY AGAIN on a finished chapter
            foreach(var t in OpenMenu())yield return t;
            foreach(var t in Click(ActionKind.ResetRoom))yield return t;
            Check(a.named==0&&a.namedMask==0&&!a.standing&&D.rooms[AmbushRoom.Room].accepted==0,"TRY AGAIN un-names everything");
            Check(D.rooms[AmbushRoom.Room].complete&&D.CompletedCount==1,"The finished chapter stays finished");
            Check(a.dark,"And the room opens in the dark once more");
            yield return 5.2f;
            Check(Lit(a)&&Mathf.Abs(a.size-AmbushRoom.StartSize)<.05f,"Then the lights come up on full-size clowns");
            Check(a.cardLabels.All(l=>!l.text.Contains("NAMED"))&&a.cardPlates.All(p=>p.sharedMaterial==a.cardOpen),"Every card is open again");
            Check(!c0.propeller.activeSelf&&!c1.bowtie.activeSelf&&c0.balloons.All(b=>b.activeSelf)&&c1.balloons.All(b=>b.activeSelf),"The beanies and bowties are gone and every balloon is back");
            Check(a.papers.Select((p,i)=>Vector3.Distance(p.transform.position,homes[i])<.15f&&p.gameObject.activeSelf).All(x=>x),"Every page is back where it started");
            Check(!a.exitDoor.activeSelf&&a.warmThings.All(w=>!w.activeSelf)&&Mathf.Abs(a.chair.localPosition.z-chairHome.z)<.05f&&Near(Sky,CoolSky),"The door is shut, the lamps are off, the chair is back and the light is cold");
            Check(a.replies==0&&a.hits==0&&a.tears==0&&a.balloonsPopped==0,"The counts start again");

            // ---- GENTLE AMBUSH
            foreach(var t in OpenMenu())yield return t;
            foreach(var t in Click(ActionKind.GentleAmbush))yield return t;
            Check(D.gentleAmbush&&D.menu.gentle.text.Contains("ON"),"GENTLE AMBUSH switches on");
            foreach(var t in CloseMenu())yield return t;
            yield return 1.6f;
            Check(Mathf.Abs(a.size-AmbushRoom.GentleSize)<.05f&&Mathf.Abs(Lean(c0))<1.5f&&Top(c0)>3.5f&&Top(c0)<5.6f,"The clowns shrink to "+a.size.ToString("0.0")+" times and stand upright, head at "+Top(c0).ToString("0.0")+" m");
            Check(Mathf.Abs(Root.InverseTransformPoint(c1.root.position).x-2.6f)<.1f,"They keep their distance");
            D.Travel(0);yield return .9f;D.Travel(AmbushRoom.Room);yield return 5.4f;
            Check(Lit(a)&&Mathf.Abs(a.size-AmbushRoom.GentleSize)<.05f,"Arriving again, the gentle size is there from the first moment");
            foreach(var t in Click(Find(ActionKind.AmbushFeeling,3)))yield return t;
            for(int w=0;w<40&&Mathf.Abs(a.size-a.TargetSize)>.02f;w++)yield return .05f;
            Check(Mathf.Abs(a.size-Mathf.Lerp(AmbushRoom.GentleSize,AmbushRoom.EndSize,1/6f))<.04f,"Naming a feeling still shrinks them, from the gentle size ("+a.size.ToString("0.00")+")");
            foreach(var t in OpenMenu())yield return t;
            foreach(var t in Click(ActionKind.GentleAmbush))yield return t;
            foreach(var t in CloseMenu())yield return t;
            yield return 2f;
            Check(!D.gentleAmbush&&Mathf.Abs(a.size-a.TargetSize)<.05f&&Mathf.Abs(Lean(c0)-AmbushRoom.StartLean*Mathf.InverseLerp(AmbushRoom.EndSize,AmbushRoom.StartSize,a.size))<1.5f,"GENTLE AMBUSH switches off and they grow back, leaning in again");

            // ---- Saving: partly named, and both options
            foreach(var t in OpenMenu())yield return t;
            foreach(var t in Click(ActionKind.GentleAmbush))yield return t;
            foreach(var t in Click(ActionKind.DarkOpening))yield return t;
            foreach(var t in CloseMenu())yield return t;
            Check(a.Feel(1)&&a.namedMask==(1<<3|1<<1)&&D.gentleAmbush&&!D.darkOpening,"Two feelings named (ANGRY, BLINDSIDED), gentle on, dark off");
            D.SaveCheckpoint();
            SceneManager.LoadScene("AfterHours");yield return .8f;
            a=A;c0=a.clowns[0];c1=a.clowns[1];
            Check(a.namedMask==(1<<3|1<<1)&&a.named==2&&D.gentleAmbush&&!D.darkOpening,"The named feelings and both options come back after a restart");
            Check(D.rooms[AmbushRoom.Room].complete&&!a.standing,"The chapter is still finished, but this attempt is not");
            D.Travel(AmbushRoom.Room);yield return .9f;
            Check(Lit(a)&&!a.dark&&Mathf.Abs(a.size-Mathf.Lerp(AmbushRoom.GentleSize,AmbushRoom.EndSize,2/6f))<.03f,"A half-named room opens lit, with the clowns where you left them");
            Check(a.cardLabels[1].text.Contains("NAMED")&&a.cardLabels[3].text.Contains("NAMED")&&!a.cardLabels[0].text.Contains("NAMED")&&c0.propeller.activeSelf&&!c0.bowtie.activeSelf,"The right cards say NAMED, and the propeller is on");
            // Finish from the restored state and restart: the whole ending comes back.
            foreach(var i in new[]{0,2,4,5})foreach(var t in Click(Find(ActionKind.AmbushFeeling,i)))yield return t;
            Check(a.named==6&&a.standUpButton.activeSelf,"The other four can still be named");
            foreach(var t in Click(Find(ActionKind.StandUp)))yield return t;
            Check(a.standing&&D.CompletedCount==1,"And STAND UP finishes it again");
            yield return .5f;
            D.SaveCheckpoint();
            SceneManager.LoadScene("AfterHours");yield return .8f;
            a=A;
            Check(a.named==6&&D.rooms[AmbushRoom.Room].complete&&a.standing,"A finished chapter is finished after a restart");
            D.Travel(AmbushRoom.Room);yield return 1f;
            Check(Lit(a)&&a.exitDoor.activeSelf&&a.warmThings.All(w=>w.activeSelf)&&Mathf.Abs(a.size-AmbushRoom.EndSize)<.03f&&a.leftLine=="...","It opens on the warm ending: small clowns, a lamp, an open door");
            Check(D.wardrobe.Has("standup-pin"),"The pin is kept");
            D.Travel(0);yield return .9f;
            Check(D.currentRoom==0&&Near(Sky,daySky,.005f),"Back in the lobby, in the lobby's own light");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
            // Wait in game time so every input edge spans rendered game frames, even when the editor stalls.
            if(Time.time<deadline)return;
            try{if(!run.MoveNext()){Finish("Passed");return;}deadline=Time.time+run.Current;}catch(Exception e){Finish("Failed: "+e.Message);}
        }
        static void Finish(string result)
        {
            if(run==null)return;run=null;Status=result;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.isPaused=false;
            if(D){D.saveEnabled=false;D.darkOpening=oldDark;D.gentleAmbush=oldGentle;}
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Ambush-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"The dark opening, the two giant clowns and how they shrink, naming six feelings, the five replies, balloons and noses, the paperwork (sent, thrown, torn), standing up, the way out, GENTLE AMBUSH and DARK OPENING, TRY AGAIN, and saving."}.Concat(lines));Debug.Log("Ambush validation: "+Status);
        }
    }
}
