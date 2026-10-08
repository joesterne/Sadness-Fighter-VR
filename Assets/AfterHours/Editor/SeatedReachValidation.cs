using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // The "airplane seat test": the first-visit guide, then every chapter completed from its arrival point without ever
    // moving the player. Only pointer presses and the menu key are used, the way a seated, hands-only player would.
    public static class SeatedReachValidation
    {
        static IEnumerator<float> run;static double next,deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette",IntroGuide.Key,Wardrobe.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("SeatedReachMouse");keyboard=InputSystem.AddDevice<Keyboard>("SeatedReachKeyboard");
            lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static void Look(Vector3 at){P.Head.rotation=Quaternion.LookRotation(at-P.Head.position);}
        // Aims at a button or object and confirms the pointer would really reach it from the seat.
        static void AimAt(Component target)
        {
            Look(target.transform.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,14,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||!hit.collider.transform.IsChildOf(target.transform))
                throw new Exception("Pointer target obstructed from the seat: "+target.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        static IEnumerable<float> Click(Component button){AimAt(button);Press(true);yield return .12f;Press(false);yield return .25f;}
        static IEnumerable<float> Key(Key key){Queue(keyboard,new KeyboardState(key));yield return .1f;Queue(keyboard,new KeyboardState());yield return .15f;}
        static Interactable Button(ActionKind kind)=>D.rooms[D.currentRoom].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==kind);
        static SendTarget Target(Func<SendTarget,bool> match)=>D.Targets.First(match);
        // Pick an object up from across the room, point at where it belongs and let go.
        static IEnumerable<float> Send(Grabbable item,SendTarget target)
        {
            float away=Vector3.Distance(P.Head.position,item.transform.position);
            AimAt(item);Press(true);yield return .3f;
            Check(item.held,"Pinch picks up "+item.label.Replace("\n"," ")+" from the seat ("+away.ToString("0.0")+" m away)");
            Look(target.LandingPoint);yield return .25f;
            Check(target.highlight&&target.highlight.activeSelf,"Pointing at the destination lights it up");
            Press(false);yield return 1.2f;
        }
        static float Moved(Vector3 seat)=>Vector2.Distance(new Vector2(seat.x,seat.z),new Vector2(P.transform.position.x,P.transform.position.z));
        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return 1f;
            // First visit: the guide is taught by doing.
            Check(D.intro&&D.intro.IsShowing&&D.intro.Step==0,"A first visit opens the guide with its content note");
            foreach(var t in Click(D.intro.next.GetComponentInChildren<Interactable>()))yield return t;
            Check(D.intro.Step==1,"Pinching CONTINUE moves the guide on");
            foreach(var t in Click(D.intro.next.GetComponentInChildren<Interactable>()))yield return t;
            Check(D.intro.Step==2&&!D.intro.next.activeSelf,"The menu step waits for the menu itself");
            foreach(var t in Key(UnityEngine.InputSystem.Key.Tab))yield return t;
            Check(D.menu.IsOpen&&D.intro.Step==3,"Opening the menu completes that step");
            foreach(var t in Key(UnityEngine.InputSystem.Key.Tab))yield return t;
            foreach(var t in Click(D.intro.next.GetComponentInChildren<Interactable>()))yield return t;
            Check(!D.intro.IsShowing&&PlayerPrefs.GetInt(IntroGuide.Key)==1,"START closes the guide and it stays closed next time");
            // Seated view on, then every chapter from its arrival point.
            foreach(var t in Key(UnityEngine.InputSystem.Key.Tab))yield return t;
            foreach(var t in Click(D.menu.Button(ActionKind.SeatedMode)))yield return t;
            foreach(var t in Key(UnityEngine.InputSystem.Key.Tab))yield return t;
            Check(P.seatedMode&&!D.menu.IsOpen,"Seated view is on");

            D.Travel(1);yield return 1f;var seat=P.transform.position;
            for(int i=0;i<14;i++){foreach(var t in Click(Button(ActionKind.Row)))yield return t;yield return .7f;}
            Check(D.rooms[1].complete,"Ocean: fourteen strokes from the bench complete the crossing");
            Check(Moved(seat)<.05f,"Ocean: done without leaving the seat");

            D.Travel(2);yield return 1f;seat=P.transform.position;
            var bay=Target(x=>x.GetComponent<DropZone>());
            foreach(var box in D.Grabbables.Where(g=>!g.processed).ToArray())
            {
                foreach(var t in Send(box,bay))yield return t;
                Check(box.processed,"Heavy things: "+box.label+" goes to the truck from the seat");
            }
            Check(D.rooms[2].complete&&D.rooms[2].accepted==5,"Heavy things: all five boxes accepted once each");
            Check(Moved(seat)<.05f,"Heavy things: done without leaving the seat");

            D.Travel(3);yield return 1f;seat=P.transform.position;
            for(int i=0;i<2;i++){foreach(var t in Click(Button(ActionKind.Brew)))yield return t;yield return .9f;}
            Check(D.kitchen.mistakes==2&&D.kitchen.crowd.Count(c=>c.activeSelf)==2,"A small spill: the crowd appears where a seated player can see it");
            // Every watcher, not only the ones showing yet, stands within 60 degrees of the machine as seen from the seat.
            float widest=D.kitchen.crowd.Max(c=>Vector3.Angle(Vector3.ProjectOnPlane(D.kitchen.nozzle.position-P.Head.position,Vector3.up),Vector3.ProjectOnPlane(c.transform.position-P.Head.position,Vector3.up)));
            Check(widest<60,"A small spill: all eight watchers stand within "+widest.ToString("0")+" degrees of the machine, never behind the seat");
            Check(D.kitchen.murmur&&D.kitchen.murmur.volume>0,"A small spill: the murmur grows with the crowd");
            foreach(var t in Click(Button(ActionKind.Help)))yield return t;
            Check(D.kitchen.helped&&D.kitchen.murmur.volume==0,"A small spill: asking for help quiets the room");
            var spout=Target(x=>x.only==D.kitchen.cup);
            foreach(var t in Send(D.kitchen.cup,spout))yield return t;
            Check(Vector3.Distance(D.kitchen.cup.transform.position,D.kitchen.nozzle.position)<.44f&&Vector3.Angle(D.kitchen.cup.transform.up,Vector3.up)<10,"A small spill: the cup lands upright under the spout");
            for(int i=0;i<3;i++){foreach(var t in Click(Button(ActionKind.Brew)))yield return t;yield return 1.1f;}
            Check(D.rooms[3].complete&&D.kitchen.fills==3,"A small spill: three pours complete the kitchen from the seat");
            Check(Moved(seat)<.05f,"A small spill: done without leaving the seat");

            D.Travel(4);yield return 1f;seat=P.transform.position;
            var notes=D.Grabbables.Where(g=>!g.processed).ToArray();
            var wrong=notes.First();var wrongTray=Target(x=>x.GetComponent<DropZone>()&&x.GetComponent<DropZone>().category!=wrong.category);
            var home=wrong.transform.position;
            foreach(var t in Send(wrong,wrongTray))yield return t;yield return 1f;
            Check(!wrong.processed&&Vector3.Distance(wrong.transform.position,home)<.3f,"The infinite archive: a note sent to the wrong tray drifts back to the desk");
            foreach(var note in notes)
            {
                var tray=Target(x=>x.GetComponent<DropZone>()&&x.GetComponent<DropZone>().category==note.category);
                foreach(var t in Send(note,tray))yield return t;
                Check(note.processed,"The infinite archive: \""+note.label.Replace("\n"," ")+"\" filed under "+note.category+" from the seat");
            }
            Check(D.rooms[4].complete,"The infinite archive: all six notes filed");
            Check(Moved(seat)<.05f,"The infinite archive: done without leaving the seat");

            D.Travel(Shredder.Room);yield return 1f;seat=P.transform.position;
            var slot=Target(x=>x.GetComponent<Shredder>());
            foreach(var page in D.shredder.pages)
            {
                foreach(var t in Send(page,slot))yield return t;yield return 1.5f;
                Check(page.processed&&!page.gameObject.activeSelf,"The old résumé: \""+page.label+"\" shredded from the seat");
            }
            yield return 3.8f;
            Check(D.shredder.Offering,"The old résumé: a blank page offers four true lines");
            foreach(var t in Click(Button(ActionKind.ChooseLine)))yield return t;
            Check(D.rooms[Shredder.Room].complete&&D.shredder.line=="I learn fast.","The old résumé: choosing a line from the seat completes the chapter");
            Check(Moved(seat)<.05f,"The old résumé: done without leaving the seat");

            D.Travel(RageRoom.Room);yield return 1f;seat=P.transform.position;var rage=D.rage;
            // Every part is within a short bat swing of the right hand, and the monitor and keyboard of either hand.
            var forward=D.rooms[RageRoom.Room].spawn.forward;
            float widestRight=rage.parts.Max(x=>RageTesting.SeatedReach(P.Head.position,forward,.2f,x));
            float widestLeft=rage.parts.Take(2).Max(x=>RageTesting.SeatedReach(P.Head.position,forward,-.2f,x));
            Check(widestRight<.7f&&widestLeft<.7f,"The rage room: every part is within "+Mathf.Max(widestRight,widestLeft).ToString("0.00")+" m of a seated hand, inside the bat's 0.85 m");
            Look(RageTesting.BatMiddle);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var batHit,14,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||!batHit.collider.transform.IsChildOf(rage.bat.transform))
                throw new Exception("Pointer target obstructed from the seat: the bat by "+(batHit.collider?batHit.collider.name:"nothing"));
            Press(true);yield return .3f;Press(false);yield return .3f;
            Check(rage.bat.held,"The rage room: pinch picks up the bat from the seat ("+Vector3.Distance(P.Head.position,RageTesting.BatMiddle).ToString("0.0")+" m away), and it stays in hand");
            var swings=new List<int>();
            foreach(var part in rage.parts)
            {
                foreach(var t in RageTesting.BreakPart(part,swings))yield return t;
                Check(part.Broken,"The rage room: short swings from the seat break "+part.label);
            }
            yield return 11.5f;
            Check(D.rooms[RageRoom.Room].complete&&rage.Calm,"The rage room: the quiet comes, and the chapter completes");
            foreach(var t in Click(rage.newComputer.GetComponentInChildren<Interactable>()))yield return t;yield return 1.8f;
            Check(rage.parts.All(x=>x.damage==0),"The rage room: WHEEL IN A NEW ONE can be chosen from the seat");
            foreach(var t in Click(Button(ActionKind.PutBatBack)))yield return t;yield return 1f;
            Check(!rage.bat.held,"The rage room: PUT THE BAT BACK can be chosen from the seat");
            Check(Moved(seat)<.05f,"The rage room: done without leaving the seat");
            Check(D.CompletedCount==6,"All six chapters completed seated, without moving");

            // The mirror: MIRROR on the desk brings a seated player to it, and every wardrobe button is in reach from there.
            D.Travel(0);yield return 1f;
            foreach(var t in Click(Button(ActionKind.GoToMirror)))yield return t;yield return .8f;
            Check(Vector2.Distance(new Vector2(P.Head.position.x,P.Head.position.z),new Vector2(D.mirrorSpot.position.x,D.mirrorSpot.position.z))<.15f&&Mathf.Abs(Mathf.DeltaAngle(P.Head.eulerAngles.y,D.mirrorSpot.eulerAngles.y))<1,"Mirror: MIRROR on the desk brings you to the glass, facing it");
            seat=P.transform.position;
            Check(D.wardrobe.avatar.reflection.gameObject.activeInHierarchy,"Mirror: your reflection appears");
            var wardrobeButtons=D.rooms[0].root.GetComponentsInChildren<Interactable>().Where(x=>x.kind==ActionKind.WardrobePrev||x.kind==ActionKind.WardrobeNext).ToArray();
            foreach(var b in wardrobeButtons)AimAt(b);
            Check(wardrobeButtons.Length==16,"Mirror: all 16 wardrobe buttons can be pointed at from the mirror spot");
            Look(D.wardrobe.avatar.head.position);
            Check(Vector3.Angle(P.Head.forward,D.mirrorSpot.forward)<25,"Mirror: your reflection's face is straight ahead (within 25 degrees)");
            var top=wardrobeButtons.First(x=>x.kind==ActionKind.WardrobeNext&&x.value==4);
            foreach(var t in Click(top))yield return t;
            Check(D.wardrobe.State.top=="shirt"&&D.wardrobe.avatar.Wearing("shirt"),"Mirror: TOP > puts on the next top, and the reflection wears it");
            Check(Moved(seat)<.05f,"Mirror: trying things on needs no movement");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
            if(Time.time<deadline)return;
            try{if(!run.MoveNext()){Finish("Passed");return;}deadline=Time.time+run.Current;}catch(Exception e){Finish("Failed: "+e.Message);}
        }
        static void Finish(string result)
        {
            if(run==null)return;run=null;Status=result;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.isPaused=false;
            if(D)D.saveEnabled=false;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Seated-reach-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"The airplane-seat test: the first-visit guide, then every chapter completed from its arrival point using only pointer presses and the menu key. The player never moves."}.Concat(lines));
            Debug.Log("Seated reach validation: "+Status);
        }
    }
}
