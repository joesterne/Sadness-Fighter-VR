using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace AfterHours.Editor
{
    // The editor's own mouse and keyboard keep reporting while a test runs, and a button an earlier click left pressed stays
    // pressed in them. A test uses only its own devices, so the others are switched off until it finishes.
    public static class TestInput
    {
        static readonly List<InputDevice> off=new List<InputDevice>();
        public static void Claim(params InputDevice[] mine)
        {
            Release();
            foreach(var device in InputSystem.devices.Where(x=>x.enabled&&!mine.Contains(x)&&mine.Any(m=>(m is Mouse&&x is Mouse)||(m is Keyboard&&x is Keyboard))).ToList())
            {InputSystem.DisableDevice(device);off.Add(device);}
            foreach(var device in mine)device.MakeCurrent();
        }
        public static void Release(){foreach(var device in off)if(device!=null&&device.added)InputSystem.EnableDevice(device);off.Clear();}
    }
    // Exercises production input and physics. Editor stepping is used because this editor stalls its background player loop.
    public static class AfterHoursSmoke
    {
        static GameDirector d;static Mouse mouse;static Keyboard keyboard;static List<string> results;
        static bool running;static double next;static int oldSave;static bool hadSave;static string failure;
        static string oldCheckpoint,oldWardrobe;static bool hadCheckpoint,hadWardrobe,oldDark;
        public static string Status="Not run";
        public static void Start()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(running)return;d=GameDirector.Instance;results=new List<string>();failure=null;
            hadSave=PlayerPrefs.HasKey("AfterHours.Progress.v1");oldSave=PlayerPrefs.GetInt("AfterHours.Progress.v1",0);
            hadCheckpoint=PlayerPrefs.HasKey(CheckpointStore.Key);oldCheckpoint=PlayerPrefs.GetString(CheckpointStore.Key);
            hadWardrobe=PlayerPrefs.HasKey(Wardrobe.Key);oldWardrobe=PlayerPrefs.GetString(Wardrobe.Key);
            // The ambush opens in the dark by default; its own suite covers that. Here the lights are on from the start.
            oldDark=d.darkOpening;d.darkOpening=false;
            foreach(var room in d.rooms){room.complete=false;room.accepted=0;}d.RefreshProgress();if(d.intro)d.intro.Hide();
            mouse=InputSystem.AddDevice<Mouse>("AfterHoursTestMouse");keyboard=InputSystem.AddDevice<Keyboard>("AfterHoursTestKeyboard");TestInput.Claim(mouse,keyboard);
            running=true;Status="Running";EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.update+=Pump;Application.logMessageReceived+=OnLog;
            d.StartCoroutine(Guard(Run()));
        }
        static void OnPlayState(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish();}
        static void OnLog(string text,string stack,LogType type){if(type==LogType.Exception&&stack.Contains("AfterHours"))failure=text;}
        static void Pump()
        {
            if(failure!=null){Finish();return;}
            if(!running||!EditorApplication.isPlaying){Finish();return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;
            EditorApplication.Step();
        }
        static IEnumerator Guard(IEnumerator sequence)
        {
            while(true){bool more;object current=null;try{more=sequence.MoveNext();if(more)current=sequence.Current;}catch(Exception ex){failure=ex.Message;break;}if(!more)break;yield return current;}
            Finish();
        }
        // The editor's own mouse and keyboard become "current" whenever they move, so each test event claims the device first.
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool ok,string name){results.Add((ok?"PASS: ":"FAIL: ")+name);if(!ok)throw new Exception(name);}
        static IEnumerator Delay(float seconds=.2f){yield return new WaitForSeconds(seconds);}
        // The swing helpers yield game-time waits; zero means the next frame.
        static object Wait(float seconds)=>seconds>0?new WaitForSeconds(seconds):null;
        static void Aim(Vector3 point){d.player.Head.rotation=Quaternion.LookRotation(point-d.player.Head.position);}
        // Puts a loose object somewhere new, and its body there too, so physics does not draw it back to where it was.
        static void Put(Grabbable item,Vector3 point){item.Initialize();item.transform.position=point;item.Body.position=point;if(!item.Body.isKinematic){item.Body.linearVelocity=Vector3.zero;item.Body.angularVelocity=Vector3.zero;}}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        // Moves stand-in hands each frame, so a box in each is as fast as asked. The second hand may be missing. Ends after the time, or as soon as it is done.
        static IEnumerator Sweep(Transform first,Transform second,Vector3 firstWay,Vector3 secondWay,float speed,float seconds,Func<bool> done=null)
        {
            for(float end=Time.time+seconds;Time.time<end;)
            {
                first.position+=firstWay.normalized*speed*Time.deltaTime;if(second)second.position+=secondWay.normalized*speed*Time.deltaTime;
                yield return null;
                if(done!=null&&done())yield break;
            }
        }
        static IEnumerator Click(Interactable button)
        {Aim(button.transform.position);yield return Delay(.06f);Press(true);yield return Delay(.12f);Press(false);yield return Delay(.25f);}
        static Interactable Button(ActionKind kind,int value=-1)
        {return d.rooms[d.currentRoom].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==kind&&(value<0||x.value==value));}
        // Opens the comfort menu with its key, then selects one of its buttons with the pointer.
        static IEnumerator Menu(ActionKind kind)
        {
            // With a message showing, the first press only clears it.
            for(int attempt=0;attempt<2&&!d.menu.IsOpen;attempt++){Queue(keyboard,new KeyboardState(Key.Tab));yield return Delay(.1f);Queue(keyboard,new KeyboardState());yield return Delay(.15f);}
            Check(d.menu.IsOpen,"Menu key opens the comfort menu in room "+d.currentRoom);
            yield return Click(d.menu.Button(kind));
        }
        static IEnumerator Visit(int room)
        {
            if(d.currentRoom!=0){yield return Menu(ActionKind.Rest);yield return Delay(.8f);Check(d.currentRoom==0,"Menu LOBBY returns to the lobby");}
            var entry=Button(ActionKind.Travel,room);var approach=entry.transform.position-entry.transform.forward*1.7f;approach.y=.04f;d.player.Place(approach,0);yield return Delay();
            yield return Click(Button(ActionKind.Travel,room));yield return Delay(.85f);Check(d.currentRoom==room,"Portal input reaches room "+room);
        }
        static IEnumerator Run()
        {
            // The day's sky, light and haze, to check that the rooftop's sunrise comes and goes cleanly.
            var daySky=RenderSettings.skybox;var daySun=RenderSettings.sun.intensity;var daySunTurn=RenderSettings.sun.transform.rotation;var dayHaze=RenderSettings.fogColor;var dayAmbient=RenderSettings.ambientSkyColor;
            yield return Delay(.1f);Check(!d.player.IsXR,"Desktop fallback is active");Check(d.player.Head.position.y>1.5f,"Desktop camera remains at eye height");
            var initial=d.player.transform.position;Queue(keyboard,new KeyboardState(Key.W));yield return Delay(.5f);Queue(keyboard,new KeyboardState());yield return Delay();Check(Vector3.Distance(initial,d.player.transform.position)>.25f,"Movement input moves player");
            d.player.Place(new Vector3(0,.04f,-3.5f),0);yield return Delay();Aim(new Vector3(2,.015f,-1.9f));Press(true);yield return Delay();Press(false);yield return Delay(.7f);Check(d.player.transform.position.x>1.3f,"Floor ray press/release teleports player");
            yield return Visit(1);Capture("Ocean");
            for(int i=0;i<14;i++){yield return Click(Button(ActionKind.Row));yield return Delay(.6f);}
            Check(d.rooms[1].complete&&d.ocean.strokes==14,"Fourteen rowing actions complete ocean");Capture("Ocean-Complete");
            yield return Visit(2);Capture("Warehouse");
            var boxes=d.rooms[2].root.GetComponentsInChildren<Grabbable>();var bay=d.rooms[2].root.GetComponentInChildren<DropZone>();
            // A quiet easter egg: hold a box in each hand and slam them together, and both burst. Nothing is counted, and both are back on their stands.
            // Stand-in hands carry the boxes, so each scene is exactly as fast as it is written; the last one uses the player's own hand too.
            {
                var blast=d.boxBlast;var room=d.rooms[2].root.transform;var across=room.right;var centre=room.TransformPoint(new Vector3(0,1.35f,-1.6f));
                Check(blast&&blast.director==d&&blast.flash&&blast.sparks&&blast.smoke&&blast.chips&&blast.sound&&blast.boxes.Length==5&&boxes.All(x=>x.throwable&&blast.boxes.Contains(x)),"The heavy room has its box blast, and all five boxes are in it");
                var first=boxes[0];var second=boxes[1];var homeFirst=first.transform.position;var homeSecond=second.transform.position;
                var handA=new GameObject("Test hand A").transform;var handB=new GameObject("Test hand B").transform;
                d.player.Place(room.TransformPoint(new Vector3(0,.04f,-3.8f)),0);yield return Delay();
                // Both boxes into the stand-in hands, the given distance either side of the middle, and left there a moment so being picked up is not a slam.
                IEnumerator Grip(float half)
                {
                    first.Release();second.Release();handA.position=centre-across*half;handB.position=centre+across*half;Put(first,handA.position);Put(second,handB.position);
                    Check(first.Take(handA)&&second.Take(handB),"Two boxes can be held at once, one in each hand");yield return Delay(.4f);
                }
                yield return Grip(1.2f);Check(blast.blasts==0&&first.held&&second.held,"Two boxes held apart do nothing");
                yield return Sweep(handA,handB,across,-across,.8f,3f,()=>Vector3.Distance(handA.position,handB.position)<.4f);
                Check(blast.blasts==0&&first.held&&second.held&&Vector3.Distance(first.transform.position,second.transform.position)<.45f,"Pressing two boxes gently together does nothing");
                yield return Grip(.275f);yield return Sweep(handA,handB,across,across,4f,.5f);
                Check(blast.blasts==0&&first.held&&second.held&&Vector3.Distance(first.transform.position,second.transform.position)<.7f,"Two boxes carried side by side at speed do nothing");
                yield return Grip(1.2f);int blasted=blast.blasts,returned=blast.returns;
                yield return Sweep(handA,handB,across,-across,3.5f,1.5f,()=>blast.blasts>blasted);
                Check(blast.blasts==blasted+1&&!first.gameObject.activeSelf&&!second.gameObject.activeSelf&&blast.IsAway(first)&&blast.IsAway(second)&&!first.held&&!second.held,"Slamming two boxes together bursts both");
                yield return Delay(.06f);
                var bits=new ParticleSystem.Particle[400];int chipBits=blast.chips.GetParticles(bits);
                int shades=bits.Take(chipBits).Select(p=>ColorUtility.ToHtmlStringRGB(p.GetCurrentColor(blast.chips))).Distinct().Count();
                Check(blast.flash.particleCount>=1&&blast.sparks.particleCount>=50&&blast.smoke.particleCount>=6&&chipBits>=100&&shades>=5,"The burst is in the air: "+blast.flash.particleCount+" flash, "+blast.sparks.particleCount+" sparks, "+blast.smoke.particleCount+" smoke, "+chipBits+" chips in "+shades+" colours");
                Check(Vector3.Distance(blast.lastAt,centre)<.7f,"The burst is where the boxes met");
                Aim(blast.lastAt);Capture("Warehouse-Blast");
                while(Time.time<blast.blastTime+1.2f)yield return null;
                int settled=blast.chips.GetParticles(bits);float lowest=settled>0?bits.Take(settled).Min(p=>p.position.y):0;
                Check(settled>80&&lowest>-.03f,"The chips fall and land on the floor, not through it: "+settled+" chips, lowest at "+lowest.ToString("0.000")+" m");
                float wait=Time.time+4;while(blast.returns<returned+2&&Time.time<wait)yield return null;yield return Delay(.1f);
                Check(blast.returns==returned+2&&first.gameObject.activeSelf&&second.gameObject.activeSelf&&!blast.IsAway(first)&&!blast.IsAway(second)&&Vector3.Distance(first.transform.position,homeFirst)<.3f&&Vector3.Distance(second.transform.position,homeSecond)<.3f,"Both boxes are back on their stands");
                Check(!first.processed&&!second.processed&&!first.held&&!second.held&&d.rooms[2].accepted==0,"Nothing was counted, and neither box is held");
                // The player's own hand holds one box and a stand-in sweeps the other into it.
                var third=boxes[2];var fourth=boxes[3];var homeThird=third.transform.position;var homeFourth=fourth.transform.position;
                d.player.Place(third.transform.position+new Vector3(0,-third.transform.position.y+.04f,-1.8f),0);yield return Delay();Aim(third.transform.position);Press(true);yield return Delay(.5f);
                Check(third.held&&d.player.IsHolding(third),"The player's own hand holds a box");
                handB.position=third.transform.position+across*1.8f;Put(fourth,handB.position);Check(fourth.Take(handB),"A second box goes into the other hand");yield return Delay(.4f);
                blasted=blast.blasts;returned=blast.returns;
                yield return Sweep(handB,null,-across,Vector3.zero,4f,1.5f,()=>blast.blasts>blasted);
                Check(blast.blasts==blasted+1&&blast.IsAway(third)&&blast.IsAway(fourth)&&!d.player.IsHolding(third)&&!third.held,"A box in the player's hand and a box in the other hand burst together, and the player's hand lets go");
                Press(false);yield return Delay(.3f);
                var fifth=boxes[4];d.player.Place(fifth.transform.position+new Vector3(0,-fifth.transform.position.y+.04f,-1.8f),0);yield return Delay();Aim(fifth.transform.position);Press(true);yield return Delay(.4f);
                Check(fifth.held&&d.player.IsHolding(fifth),"The player's hand can pick up another box straight after");
                Press(false);yield return Delay(.2f);d.player.Drop(fifth);fifth.BringBack();
                wait=Time.time+4;while(blast.returns<returned+2&&Time.time<wait)yield return null;yield return Delay(.1f);
                Check(blast.returns==returned+2&&Vector3.Distance(third.transform.position,homeThird)<.3f&&Vector3.Distance(fourth.transform.position,homeFourth)<.3f&&!third.held&&!third.processed&&d.rooms[2].accepted==0,"Both boxes are back, and nothing was counted");
                // TRY AGAIN during a blast puts the boxes back at once and clears the smoke.
                blast.Blast(boxes[0],boxes[1],centre);yield return Delay(.2f);
                Check(blast.IsAway(boxes[0])&&blast.IsAway(boxes[1])&&blast.chips.particleCount>50,"A burst can be made by hand, and the chips fly");
                d.RestartCurrentRoom();yield return Delay(.1f);
                Check(boxes.All(x=>x.gameObject.activeSelf&&!blast.IsAway(x)&&!x.held&&!x.processed)&&blast.chips.particleCount==0&&blast.sparks.particleCount==0&&blast.smoke.particleCount==0,"TRY AGAIN brings the boxes back at once and clears the smoke");
                UnityEngine.Object.Destroy(handA.gameObject);UnityEngine.Object.Destroy(handB.gameObject);
            }
            foreach(var box in boxes)
            {
                d.player.Place(box.transform.position+new Vector3(0,-box.transform.position.y+.04f,-1.8f),0);yield return Delay();Aim(box.transform.position);Press(true);yield return Delay(.2f);Check(box.held,"Input grabs "+box.label);
                // Hold position over the real trigger; release uses the normal input edge and physics.
                var point=bay.transform.position+Vector3.up*.3f;d.player.Shift(new Vector3(point.x,.04f,point.z-1.35f)-d.player.transform.position);d.player.Head.rotation=Quaternion.identity;yield return Delay(.15f);Press(false);yield return Delay(.7f);Check(box.processed,"Truck accepts released "+box.label);
            }
            Check(d.rooms[2].complete&&d.rooms[2].accepted==5,"Warehouse counts each box exactly once");
            yield return Visit(3);d.player.Place(new Vector3(240,.04f,2.4f),0);yield return Delay();Capture("Kitchen");
            for(int i=0;i<3;i++){yield return Click(Button(ActionKind.Brew));yield return Delay(1);}
            Check(d.kitchen.mistakes==3&&d.kitchen.crowd.Count(x=>x.activeSelf)==3,"Coffee mistakes grow the crowd");Capture("Kitchen-Spill");
            yield return Menu(ActionKind.GentleAudience);Check(d.kitchen.crowd.All(x=>!x.activeSelf),"Menu KITCHEN CROWD hides the crowd");
            yield return Menu(ActionKind.CloseMenu);Check(!d.menu.IsOpen,"Menu CLOSE closes the menu");
            yield return Click(Button(ActionKind.Help));Check(d.kitchen.helped,"Help input releases machine pressure");
            var cup=d.kitchen.cup;d.player.Place(new Vector3(238.3f,.04f,2.9f),0);yield return Delay();Aim(cup.transform.position);Press(true);yield return Delay();Check(cup.held,"Input grabs the coffee cup");
            d.player.Shift(new Vector3(240,.04f,2.96f)-d.player.transform.position);d.player.Head.rotation=Quaternion.identity;yield return Delay();Press(false);yield return Delay(.7f);
            // The middle pour is made with the machine's lever, which pulls down as it brews.
            var kitchen=d.kitchen;var lever=kitchen.lever.GetComponentInChildren<Interactable>();float leverRest=kitchen.lever.localEulerAngles.z;
            for(int i=0;i<3;i++)
            {
                yield return Click(i==1?lever:Button(ActionKind.Brew));
                if(i==1)Check(Mathf.Abs(Mathf.DeltaAngle(kitchen.lever.localEulerAngles.z,leverRest))>20&&kitchen.fills==2,"Pointing at the lever brews, and the lever pulls down");
                yield return Delay(1);
            }
            Check(d.rooms[3].complete&&d.kitchen.fills==3,"Cup placement and three pours complete kitchen");
            yield return Delay(.7f);
            Check(Mathf.Abs(Mathf.DeltaAngle(kitchen.lever.localEulerAngles.z,leverRest))<5,"The lever goes back up");
            // The steam wand blows a plume, even though the machine was faulty.
            yield return Click(Button(ActionKind.Steam));Check(kitchen.steams==1&&kitchen.steam.particleCount>0,"Pointing at the steam wand blows steam");
            // A cupboard opens by pointing at it. The fifth hides an orange mug, which can be carried to the spout like any other cup and filled for a pin.
            d.player.Place(new Vector3(241.9f,.04f,2.9f),0);yield return Delay();
            Check(!d.wardrobe.Has("orange-pin")&&!kitchen.orangeSeen,"Nothing orange has been found yet");
            yield return Click(Button(ActionKind.Cabinet,KitchenRoom.OrangeCabinet));yield return Delay(.9f);
            var cupboard=kitchen.doors[KitchenRoom.OrangeCabinet];
            Check(cupboard.open&&cupboard.Settled&&Mathf.Abs(cupboard.angle)>=90&&kitchen.orangeSeen&&kitchen.opens==1,"Pointing at a cupboard opens its door, and the orange mug is inside");Capture("Kitchen-Cupboard");
            var hidden=kitchen.orangeMug.item;Aim(hidden.transform.position);Press(true);yield return Delay();Check(hidden.held,"Input takes the orange mug from the open cupboard");
            d.player.Shift(new Vector3(240,.04f,2.96f)-d.player.transform.position);d.player.Head.rotation=Quaternion.identity;yield return Delay();Press(false);yield return Delay(1.4f);
            Check(Vector3.Distance(hidden.transform.position,kitchen.nozzle.position)<.44f&&Vector3.Distance(kitchen.cup.transform.position,kitchen.transform.TransformPoint(new Vector3(-1.7f,1.23f,4.6f)))<.4f&&kitchen.mugs.All(m=>m.breaks==0),"The orange mug glides to the spout and the mug that was there goes back to the counter; nothing breaks");
            yield return Click(Button(ActionKind.Brew));
            Check(kitchen.orangeFilled&&kitchen.orangeMug.level==1&&kitchen.fills==3&&d.wardrobe.Has("orange-pin"),"Brewing into the orange mug earns the orange mug pin, and still counts as nothing more than a little warmth");
            yield return Visit(4);Capture("Archive");
            var notes=d.rooms[4].root.GetComponentsInChildren<Grabbable>();var zones=d.rooms[4].root.GetComponentsInChildren<DropZone>();
            foreach(var note in notes)
            {
                d.player.Place(new Vector3(note.transform.position.x,.04f,note.transform.position.z-1.65f),0);yield return Delay();Aim(note.transform.position);Press(true);yield return Delay();Check(note.held,"Input grabs archive note");
                var zone=zones.First(x=>x.category==note.category);d.player.Shift(new Vector3(zone.transform.position.x,.04f,zone.transform.position.z-1.35f)-d.player.transform.position);d.player.Head.rotation=Quaternion.identity;yield return Delay();Press(false);yield return Delay(.8f);Check(note.processed,"Matching tray accepts "+note.category);
            }
            Check(d.rooms[4].complete&&d.rooms[4].accepted==6,"Archive completes after six correct placements");
            yield return Visit(6);Capture("Old resume");
            // A quiet easter egg: a page thrown onto the floor bursts into confetti, then is back on its stand. Nothing is counted.
            {
                var egg=d.shredder.egg;var sheet=d.shredder.pages[4];var sheetHome=sheet.transform.position;int popped=egg.bursts;
                Check(egg&&egg.pop&&egg.shower&&egg.sound&&d.shredder.pages.All(x=>x.throwable&&!x.tossable),"The résumé room has its confetti, and its five pages can be thrown");
                // The editor is paused and stepped here, so no audio plays: the piece is checked by the clip the music has picked.
                Check(d.music&&(!d.music.on||d.music.source.clip==d.music.ClipFor(6)),"The chapter plays its own piece (unless MUSIC is off)");
                d.player.Place(d.rooms[6].spawn.position,0);yield return Delay();
                Put(sheet,d.rooms[6].root.transform.TransformPoint(new Vector3(3.4f,1.2f,-2f)));
                sheet.Throw(new Vector3(.4f,-1.5f,1.4f));yield return Delay(.6f);
                Check(egg.bursts==popped+1&&!sheet.gameObject.activeSelf&&egg.IsAway(sheet),"A page thrown onto the floor bursts into confetti and goes");
                yield return Delay(.4f);
                var bits=new ParticleSystem.Particle[400];int inBurst=egg.pop.GetParticles(bits);
                int shades=bits.Take(inBurst).Select(p=>ColorUtility.ToHtmlStringRGB(p.GetCurrentColor(egg.pop))).Distinct().Count();int inShower=egg.shower.particleCount;
                Check(inBurst>150&&inShower>20&&shades>=6,"The confetti is in the air: "+inBurst+" bits in the burst, "+inShower+" in the shower so far, in "+shades+" colours");
                Aim(egg.lastAt+Vector3.up*1.2f);yield return Delay(.1f);Capture("Old resume-Confetti");
                yield return Delay(1.4f);
                bool backHome=sheet.gameObject.activeSelf&&!egg.IsAway(sheet)&&Vector3.Distance(sheet.transform.position,sheetHome)<.3f&&!sheet.processed&&d.rooms[6].accepted==0&&d.shredder.shredded==0;
                Check(backHome,"The page is back on its stand a moment later, and nothing is counted"+(backHome?"":" [active "+sheet.gameObject.activeSelf+", away "+egg.IsAway(sheet)+", "+Vector3.Distance(sheet.transform.position,sheetHome).ToString("0.00")+" m from home, popped "+(Time.time-egg.popTime).ToString("0.00")+" s ago, returns "+egg.returns+", processed "+sheet.processed+", accepted "+d.rooms[6].accepted+", shredded "+d.shredder.shredded+"]"));
                yield return Delay(2.6f);
                inBurst=egg.pop.GetParticles(bits);float lowest=bits.Take(inBurst).Min(p=>p.position.y);
                Check(inBurst>100&&lowest>-.03f,"The confetti comes to rest on the floor, and none falls through it ("+inBurst+" bits, lowest "+lowest.ToString("0.000")+" m)");
                // A page dropped onto the desk is nothing special.
                sheet.ResetObject();Put(sheet,sheetHome+Vector3.up*.35f);yield return Delay(.8f);
                Check(egg.bursts==popped+1&&sheet.gameObject.activeSelf,"A page dropped onto the desk does not burst");
                sheet.ResetObject();yield return Delay(.3f);
                // TRY AGAIN in the room brings a burst page back at once and clears the confetti.
                egg.Pop(d.shredder.pages[3],egg.lastAt);yield return Delay(.2f);
                Check(!d.shredder.pages[3].gameObject.activeSelf&&egg.pop.particleCount>0,"Another page bursts");
                d.shredder.ResetRoom();yield return Delay(.1f);
                Check(d.shredder.pages.All(x=>x.gameObject.activeSelf)&&egg.pop.particleCount==0&&egg.shower.particleCount==0&&!egg.IsAway(d.shredder.pages[3]),"TRY AGAIN in this room brings a burst page back and clears the confetti");
            }
            var slot=d.Targets.First(x=>x.GetComponent<Shredder>());
            foreach(var page in d.shredder.pages)
            {
                d.player.Place(d.rooms[6].spawn.position,0);yield return Delay();Aim(page.transform.position);yield return Delay(.06f);Press(true);yield return Delay(.3f);
                Check(page.held,"Input picks up the résumé \""+page.label+"\"");
                Aim(slot.LandingPoint);yield return Delay(.25f);Check(slot.highlight.activeSelf,"Pointing at the shredder lights its slot");
                Press(false);yield return Delay(2.6f);
                Check(page.processed&&!page.gameObject.activeSelf,"The shredder takes \""+page.label+"\"");
                if(page==d.shredder.pages[0]){int inAir=d.shredder.bits.Sum(x=>x.particleCount);Check(inAir>100,"A flurry of paper bits is in the air over the shredder ("+inAir+")");}
            }
            Check(d.rooms[6].accepted==5&&!d.rooms[6].complete,"Five pages shredded once each; the chapter waits for a true line");
            yield return Delay(3.8f);Check(d.shredder.Offering,"A blank page offers four true lines");
            Check(d.shredder.litter.Length==5&&d.shredder.litter.All(x=>x.activeSelf),"Every shredded page has left its scraps on the floor, the desk and the shredder");Capture("Old resume-Choice");
            yield return Click(d.rooms[6].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==ActionKind.ChooseLine&&x.value==2));
            Check(d.rooms[6].complete&&d.shredder.line=="I keep going."&&d.wardrobe.State.line=="I keep going.","Choosing a line completes the chapter and keeps the line for the wardrobe");
            Check(d.CompletedCount==5,"Five chapters complete before the rage room");
            yield return Visit(RageRoom.Room);Capture("Rage room");
            var rage=d.rage;var bat=rage.bat;var batHome=bat.transform.position;var roomRoot=d.rooms[RageRoom.Room].root.transform;
            Aim(RageTesting.BatMiddle);yield return Delay(.06f);Press(true);yield return Delay(.3f);Press(false);yield return Delay(.3f);
            Check(bat.held&&d.player.IsHolding(bat),"Input picks up the bat, and it stays in hand after the press ends");
            Capture("Rage room-Bat in hand");
            foreach(var w in RageTesting.Swing(rage.parts[0],50,4))yield return Wait(w);
            Check(rage.parts[0].damage==0,"Moving the bat slowly through the monitor does not count as a hit");
            int pieceCount=RageTesting.Pieces.Count();
            int computerPieces=rage.parts.Sum(x=>x.stages.Sum(t=>t.debris.Length));
            Check(computerPieces>=4*32&&pieceCount>=200&&rage.extras.Length==9,"The computer holds "+computerPieces+" pieces that can fly off (32 before), and the nine other things "+(pieceCount-computerPieces)+" more");
            var swings=new List<int>();
            foreach(var extra in rage.extras)
            {
                foreach(var w in RageTesting.BreakPart(extra,swings))yield return Wait(w);
                Check(extra.Broken,"Short swings break "+extra.label+" ("+extra.HitsToBreak+(extra.HitsToBreak==1?" hit)":" hits)"));
            }
            Check(!d.rooms[RageRoom.Room].complete&&d.rooms[RageRoom.Room].accepted==rage.BrokenCount&&rage.BrokenCount<4,"The desk things are extras: breaking them does not count toward the chapter");
            Check(rage.Jams==1&&d.wardrobe.Has(RageRoom.PaperJamPin)&&(rage.paperBurst.isPlaying||rage.paperBurst.particleCount>0),"The copier, broken for good, jams in a burst of paper: the Paper Jam easter egg and its pin");
            Capture("Rage room-Paper jam");
            Check(rage.BrokenEver==9&&rage.tools[0].gameObject.activeSelf&&rage.tools[1].gameObject.activeSelf&&!rage.tools[2].gameObject.activeSelf&&!rage.toolLocks[0].activeSelf&&rage.toolLocks[2].activeSelf&&rage.lockCounts[2].text.StartsWith("1 MORE"),"Nine things broken unlock the hammer (3) and the handsaw (6); the nail bat waits for 1 more");
            for(int i=0;i<rage.parts.Length;i++)
            {
                var part=rage.parts[i];
                foreach(var w in RageTesting.BreakPart(part,swings))yield return Wait(w);
                Check(part.Broken,"Short swings break "+part.label+" ("+part.HitsToBreak+" hits"+(swings.Last()>0?" in "+swings.Last()+" swings aimed at it)":", all landed by swings aimed at its neighbour)"));
                Check(d.rooms[RageRoom.Room].accepted==rage.BrokenCount&&rage.BrokenCount>=i+1,"The room counts "+rage.BrokenCount+" of 4 broken");
            }
            Check(!d.rooms[RageRoom.Room].complete&&rage.Quieting,"The last part starts the quiet; the chapter waits for it");
            Check(rage.BrokenEver==13&&rage.tools[2].gameObject.activeSelf&&!rage.tools[3].gameObject.activeSelf&&rage.lockCounts[3].text.StartsWith("3 MORE"),"Thirteen things broken unlock the nail bat; the axe waits for 3 more");
            Check(d.music.Wanted==d.music.afterRage&&d.music.afterRage.name=="Music - Slow tide","The quiet eases the beat out for Slow tide");
            Check(rage.Hits+rage.Carried==rage.Everything.Sum(x=>x.HitsToBreak)&&rage.FewestThrown>=3&&rage.Thrown==pieceCount,"Every hit throws pieces: "+rage.Hits+" hits threw all "+rage.Thrown+", at least "+rage.FewestThrown+" at a time");
            var desk=roomRoot.TransformPoint(new Vector3(.1f,.85f,-.3f));
            yield return Delay(1);Aim(desk);yield return Delay(.2f);Capture("Rage room-Broken");
            Aim(roomRoot.TransformPoint(new Vector3(0,.2f,.9f)));yield return Delay(.2f);Capture("Rage room-Mess");
            yield return Delay(10.5f);
            Check(d.rooms[RageRoom.Room].complete&&rage.Calm&&rage.newComputer.activeSelf,"When it is all broken the room goes quiet, then the chapter completes");
            Check(rage.ambience.volume<.01f,"The room's hum fades to silence");
            var landed=RageTesting.Landed.ToList();
            // The side and back walls' inner faces are at 4.9 and 6.9; the entrance wall's is at -5, so a piece may rest against it.
            bool Inside(Rigidbody b){var p=roomRoot.InverseTransformPoint(b.position);return Mathf.Abs(p.x)<4.9f&&p.z>-5.02f&&p.z<6.9f&&p.y>-.02f&&p.y<4.6f;}
            var strays=landed.Where(b=>!Inside(b)).Take(3).Select(b=>b.name+" at "+roomRoot.InverseTransformPoint(b.position).ToString("0.00")+" (mass "+b.mass.ToString("0.00")+")").ToArray();
            Check(landed.Count==pieceCount&&strays.Length==0,"All "+landed.Count+" pieces stay inside the room, above the floor"+(strays.Length>0?": "+string.Join("; ",strays):""));
            int resting=landed.Count(b=>b.IsSleeping()||b.linearVelocity.magnitude<.05f);
            Check(resting>=landed.Count*.9f,resting+" of "+landed.Count+" pieces have come to rest by the end of the quiet");
            var onDesk=landed.Where(b=>{var p=roomRoot.InverseTransformPoint(b.position);return b.mass<.3f&&Mathf.Abs(p.x)<.7f&&p.z>-.55f&&p.z<0&&p.y>.74f&&p.y<.9f;})
                .OrderBy(b=>Vector3.Distance(roomRoot.InverseTransformPoint(b.position),new Vector3(0,.76f,-.4f))).FirstOrDefault();
            Check(onDesk,"Pieces lie on the desk after the quiet");
            if(onDesk)
            {
                var knockedFrom=onDesk.position;int knocks=rage.Knocks;
                foreach(var w in RageTesting.SwingAt(onDesk.position))yield return Wait(w);
                float moved=Vector3.Distance(onDesk.position,knockedFrom);
                Check(rage.Knocks>knocks&&moved>.03f,"A swing knocks a loose "+onDesk.name.ToLower()+" along the desk ("+(moved*100).ToString("0")+" cm)");
            }
            Aim(d.rooms[RageRoom.Room].root.transform.TransformPoint(new Vector3(0,1.5f,3)));yield return Delay(.2f);Capture("Rage room-Quiet");
            yield return Click(rage.newComputer.GetComponentInChildren<Interactable>());yield return Delay(1.8f);
            Check(rage.parts.All(x=>x.damage==0&&!x.Broken)&&d.rooms[RageRoom.Room].accepted==0&&!rage.Calm&&d.rooms[RageRoom.Room].complete,"WHEEL IN A NEW ONE brings back a whole computer, and the chapter stays complete");
            Check(Mathf.Abs(rage.ambience.volume-rage.ambienceVolume)<.01f,"The hum returns with the new computer");
            Check(d.music.Wanted.name=="Music - Overtime","The beat returns with the new computer");
            Check(rage.BrokenEver==13&&rage.tools[2].gameObject.activeSelf,"A new computer keeps the tools unlocked");
            Check(rage.Everything.All(x=>x.damage==0)&&RageTesting.Pieces.All(b=>b.isKinematic&&b.transform.parent!=roomRoot),"WHEEL IN A NEW ONE also brings fresh desk things, with every piece back in place");
            yield return Click(d.rooms[RageRoom.Room].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==ActionKind.PutBatBack));yield return Delay(1);
            Check(!bat.held&&!d.player.IsHolding(bat)&&Vector3.Distance(bat.transform.position,batHome)<.05f,"PUT IT BACK returns the bat to its rack");
            var hammer=rage.tools[0];var nailBat=rage.tools[2];
            Aim(hammer.transform.TransformPoint(0,0,.3f));yield return Delay(.06f);Press(true);yield return Delay(.3f);Press(false);yield return Delay(.3f);
            Check(hammer.held&&d.player.IsHolding(hammer),"An unlocked tool on the wall can be picked up: the hammer");
            Aim(nailBat.transform.TransformPoint(0,0,.6f));yield return Delay(.06f);Press(true);yield return Delay(.3f);Press(false);yield return Delay(.6f);
            Check(nailBat.held&&!hammer.held&&!d.player.IsHolding(hammer),"Pointing at another tool swaps them: the nail bat in hand, the hammer back on the wall");
            // Turning to the tool wall and back swings whatever is held, so the computer may have taken hits (and gone quiet) by now:
            // start from a whole one.
            rage.ResetComputer();rage.HitNow(rage.parts[0],nailBat.GetComponent<Bat>());
            Check(rage.parts[0].damage==2,"The nail bat breaks a part two steps at a time");
            rage.ResetComputer();
            yield return Click(d.rooms[RageRoom.Room].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==ActionKind.PutBatBack));yield return Delay(1);
            Check(!nailBat.held&&!d.player.IsHolding(nailBat),"PUT IT BACK puts the nail bat back too");
            Check(d.CompletedCount==6&&!d.wardrobe.Has("jacket"),"Six chapters complete before the ambush, and the sunrise jacket waits for the seventh");
            yield return Visit(AmbushRoom.Room);Capture("Ambush");
            var amb=d.ambush;
            Check(amb.lightsOn&&!amb.dark&&Mathf.Abs(amb.size-AmbushRoom.StartSize)<.05f&&amb.clowns.Length==2,"The ambush opens with two clowns far too large across the table");
            Aim(d.rooms[AmbushRoom.Room].root.transform.TransformPoint(new Vector3(0,2.6f,4)));yield return Delay(.2f);Capture("Ambush-Looming");
            yield return Click(Button(ActionKind.AmbushReply,2));
            Check(amb.replies==1&&amb.youLine.Contains("YEARS")&&!d.rooms[AmbushRoom.Room].complete,"Talking back is answered, and does not count toward the chapter");
            yield return Click(Button(ActionKind.AmbushBalloon,0));
            Check(amb.balloonsPopped==1,"Pointing at a balloon pops it");
            for(int i=0;i<AmbushRoom.Feelings;i++)
            {
                yield return Click(Button(ActionKind.AmbushFeeling,i));
                Check(amb.named==i+1&&d.rooms[AmbushRoom.Room].accepted==i+1,"Naming a feeling counts it: "+amb.named+" of 6");
            }
            Check(!d.rooms[AmbushRoom.Room].complete&&amb.standUpButton.activeSelf,"Six feelings named: STAND UP appears and the chapter waits for it");
            yield return Delay(2.2f);Aim(d.rooms[AmbushRoom.Room].root.transform.TransformPoint(new Vector3(0,1.6f,4)));yield return Delay(.2f);Capture("Ambush-Smaller");
            Check(Mathf.Abs(amb.size-AmbushRoom.EndSize)<.03f,"Both clowns have shrunk to "+amb.size.ToString("0.00")+" times a person");
            var notice=amb.papers[0];Aim(notice.transform.position);yield return Delay(.06f);Press(true);yield return Delay(.4f);
            Check(notice.held,"Input picks up the termination notice");
            Aim(amb.clowns[0].aim.LandingPoint);yield return Delay(.3f);Check(amb.clowns[0].aim.highlight.activeSelf,"Pointing at a clown lights its outline");
            Press(false);yield return Delay(1.5f);
            Check(amb.hits==1&&amb.IsAway(notice),"A page let go while pointing at a clown flies to it and hits");
            yield return Delay(2.6f);Check(!amb.IsAway(notice)&&notice.gameObject.activeSelf,"And is back on the table a moment later");
            yield return Click(Button(ActionKind.StandUp));yield return Delay(.5f);
            Check(amb.standing&&d.rooms[AmbushRoom.Room].complete&&amb.exitDoor.activeSelf,"STAND UP completes the chapter and opens the way out");
            yield return Delay(3.2f);Aim(d.rooms[AmbushRoom.Room].root.transform.TransformPoint(new Vector3(0,1.5f,4)));yield return Delay(.2f);Capture("Ambush-Standing");
            Check(d.CompletedCount==7&&!d.wardrobe.Has("jacket"),"Seven chapters complete before the NDA web, and the sunrise jacket waits for the eighth");
            yield return Visit(WebRoom.Room);Capture("NDA web");
            var web=d.web;var webRoot=d.rooms[WebRoom.Room].root.transform;
            Check(web.machete.held&&d.player.IsHolding(web.machete)&&web.Standing==18,"The NDA web opens with eighteen webs round the seat and the machete already in hand");
            foreach(var w in WebTesting.TurnTo(web.spider.position+Vector3.up*.7f))yield return Wait(w);yield return Delay(.2f);Capture("NDA web-Spider");
            foreach(var w in WebTesting.Chop(4))yield return Wait(w);
            Check(web.Cleared==2&&!d.rooms[WebRoom.Room].complete,"A chop through the webs straight ahead cuts both of them");
            foreach(var w in WebTesting.ClearAll())yield return Wait(w);
            Check(web.beaten&&web.Cleared==18&&d.rooms[WebRoom.Room].complete,"Chopping through every web before any grows back completes the chapter");
            Aim(webRoot.TransformPoint(new Vector3(0,1.4f,4)));yield return Delay(.2f);Capture("NDA web-Cleared");
            yield return Click(d.rooms[WebRoom.Room].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==ActionKind.KillItWithFire));
            Aim(web.spider.position+Vector3.up*.5f);yield return Delay(.3f);Capture("NDA web-Fire");
            yield return Delay(WebRoom.BurnSeconds+.3f);
            Check(web.burned&&web.ash.activeSelf&&d.wardrobe.Has("flame-hat"),"The unlabelled button burns the spider away and earns the hat of flames");
            Check(d.CompletedCount==8,"All eight chapters complete");Check(PlayerPrefs.GetInt("AfterHours.Progress.v1")==990,"Completion saved as all eight chapter flags");
            Check(new[]{"jumper","cap","cardigan","true-pin","lanyard","letitout-pin","standup-pin","jacket","step-pin","scarf","orange-pin",RageRoom.PaperJamPin,"flame-hat"}.All(d.wardrobe.Has),"Playing through earns every chapter piece, the sunrise jacket, the small step pin, the kind scarf, the Paper Jam pin and the hat of flames");
            yield return Visit(5);Check(d.ending.text.Contains("more"),"Completed journey changes rooftop ending");Capture("Rooftop");
            // The rooftop is a sunrise: its own gradient sky, golden light from low in front of the player, a warm haze and a lit-up ambient, all put back on leaving.
            var rooftop=d.rooftop;
            Check(rooftop&&rooftop.skybox&&RenderSettings.skybox==rooftop.skybox&&RenderSettings.skybox!=daySky,"The rooftop shows its own sunrise sky");
            Check(!Mathf.Approximately(RenderSettings.sun.intensity,daySun)&&RenderSettings.fogColor!=dayHaze&&RenderSettings.ambientSkyColor!=dayAmbient&&RenderSettings.sun.transform.rotation!=daySunTurn,"The rooftop sets its own light, haze and ambient colour");
            Check(rooftop.drifting.Length>=6&&rooftop.speeds.Length==rooftop.drifting.Length&&rooftop.balloon,"The rooftop has clouds to drift and a balloon to bob");
            var cloud=rooftop.drifting[0];var cloudAt=cloud.localPosition;var balloonAt=rooftop.balloon.localPosition;yield return Delay(2.5f);
            Check(cloud.localPosition.x>cloudAt.x&&rooftop.balloon.localPosition!=balloonAt,"The clouds drift and the balloon bobs while you rest");
            Queue(keyboard,new KeyboardState(Key.Escape));yield return Delay(.1f);Queue(keyboard,new KeyboardState());yield return Delay(.9f);
            Check(d.currentRoom==0,"The lobby key leaves the rooftop");
            Check(RenderSettings.skybox==daySky&&Mathf.Approximately(RenderSettings.sun.intensity,daySun)&&RenderSettings.sun.transform.rotation==daySunTurn&&RenderSettings.fogColor==dayHaze&&RenderSettings.ambientSkyColor==dayAmbient,"Leaving the rooftop puts the day's sky, sun and haze back");
            Status="Passed";
        }
        static void Capture(string label)
        {
            string path="Documentation/Previews/"+label+".png";Directory.CreateDirectory(Path.GetDirectoryName(path));var camera=d.player.Head.GetComponent<Camera>();var rt=new RenderTexture(1600,1000,24);var previous=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
        }
        static void Finish()
        {
            if(!running)return;running=false;EditorApplication.playModeStateChanged-=OnPlayState;EditorApplication.update-=Pump;Application.logMessageReceived-=OnLog;
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(d){d.saveEnabled=false;d.darkOpening=oldDark;}
            if(hadCheckpoint)PlayerPrefs.SetString(CheckpointStore.Key,oldCheckpoint);else PlayerPrefs.DeleteKey(CheckpointStore.Key);
            if(hadSave)PlayerPrefs.SetInt("AfterHours.Progress.v1",oldSave);else PlayerPrefs.DeleteKey("AfterHours.Progress.v1");
            if(hadWardrobe)PlayerPrefs.SetString(Wardrobe.Key,oldWardrobe);else PlayerPrefs.DeleteKey(Wardrobe.Key);PlayerPrefs.Save();
            if(failure!=null){results.Add("FAILURE: "+failure);Status="Failed: "+failure;}
            else if(Status!="Passed")Status="Stopped";
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Gameplay-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Desktop input + Unity physics, with editor frames advanced explicitly."}.Concat(results));
            Debug.Log("After Hours smoke test: "+Status);
        }
    }
}



