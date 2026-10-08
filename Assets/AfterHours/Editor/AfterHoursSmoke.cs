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
    // Exercises production input and physics. Editor stepping is used because this editor stalls its background player loop.
    public static class AfterHoursSmoke
    {
        static GameDirector d;static Mouse mouse;static Keyboard keyboard;static List<string> results;
        static bool running;static double next;static int oldSave;static bool hadSave;static string failure;
        static string oldCheckpoint,oldWardrobe;static bool hadCheckpoint,hadWardrobe;
        public static string Status="Not run";
        public static void Start()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(running)return;d=GameDirector.Instance;results=new List<string>();failure=null;
            hadSave=PlayerPrefs.HasKey("AfterHours.Progress.v1");oldSave=PlayerPrefs.GetInt("AfterHours.Progress.v1",0);
            hadCheckpoint=PlayerPrefs.HasKey(CheckpointStore.Key);oldCheckpoint=PlayerPrefs.GetString(CheckpointStore.Key);
            hadWardrobe=PlayerPrefs.HasKey(Wardrobe.Key);oldWardrobe=PlayerPrefs.GetString(Wardrobe.Key);
            foreach(var room in d.rooms){room.complete=false;room.accepted=0;}d.RefreshProgress();if(d.intro)d.intro.Hide();
            mouse=InputSystem.AddDevice<Mouse>("AfterHoursTestMouse");keyboard=InputSystem.AddDevice<Keyboard>("AfterHoursTestKeyboard");
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
        static void Aim(Vector3 point){d.player.Head.rotation=Quaternion.LookRotation(point-d.player.Head.position);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static IEnumerator Click(Interactable button)
        {Aim(button.transform.position);yield return Delay(.06f);Press(true);yield return Delay(.12f);Press(false);yield return Delay(.25f);}
        static Interactable Button(ActionKind kind,int value=-1)
        {return d.rooms[d.currentRoom].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==kind&&(value<0||x.value==value));}
        // Opens the comfort menu with its key, then selects one of its buttons with the pointer.
        static IEnumerator Menu(ActionKind kind)
        {
            if(!d.menu.IsOpen){Queue(keyboard,new KeyboardState(Key.Tab));yield return Delay(.1f);Queue(keyboard,new KeyboardState());yield return Delay(.15f);}
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
            yield return Delay(.1f);Check(!d.player.IsXR,"Desktop fallback is active");Check(d.player.Head.position.y>1.5f,"Desktop camera remains at eye height");
            var initial=d.player.transform.position;Queue(keyboard,new KeyboardState(Key.W));yield return Delay(.5f);Queue(keyboard,new KeyboardState());yield return Delay();Check(Vector3.Distance(initial,d.player.transform.position)>.25f,"Movement input moves player");
            d.player.Place(new Vector3(0,.04f,-3.5f),0);yield return Delay();Aim(new Vector3(2,.015f,-1.9f));Press(true);yield return Delay();Press(false);yield return Delay(.7f);Check(d.player.transform.position.x>1.3f,"Floor ray press/release teleports player");
            yield return Visit(1);Capture("Ocean");
            for(int i=0;i<14;i++){yield return Click(Button(ActionKind.Row));yield return Delay(.6f);}
            Check(d.rooms[1].complete&&d.ocean.strokes==14,"Fourteen rowing actions complete ocean");Capture("Ocean-Complete");
            yield return Visit(2);Capture("Warehouse");
            var boxes=d.rooms[2].root.GetComponentsInChildren<Grabbable>();var bay=d.rooms[2].root.GetComponentInChildren<DropZone>();
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
            for(int i=0;i<3;i++){yield return Click(Button(ActionKind.Brew));yield return Delay(1);}
            Check(d.rooms[3].complete&&d.kitchen.fills==3,"Cup placement and three pours complete kitchen");
            yield return Visit(4);Capture("Archive");
            var notes=d.rooms[4].root.GetComponentsInChildren<Grabbable>();var zones=d.rooms[4].root.GetComponentsInChildren<DropZone>();
            foreach(var note in notes)
            {
                d.player.Place(new Vector3(note.transform.position.x,.04f,note.transform.position.z-1.65f),0);yield return Delay();Aim(note.transform.position);Press(true);yield return Delay();Check(note.held,"Input grabs archive note");
                var zone=zones.First(x=>x.category==note.category);d.player.Shift(new Vector3(zone.transform.position.x,.04f,zone.transform.position.z-1.35f)-d.player.transform.position);d.player.Head.rotation=Quaternion.identity;yield return Delay();Press(false);yield return Delay(.8f);Check(note.processed,"Matching tray accepts "+note.category);
            }
            Check(d.rooms[4].complete&&d.rooms[4].accepted==6,"Archive completes after six correct placements");
            yield return Visit(6);Capture("Old resume");
            var slot=d.Targets.First(x=>x.GetComponent<Shredder>());
            foreach(var page in d.shredder.pages)
            {
                d.player.Place(d.rooms[6].spawn.position,0);yield return Delay();Aim(page.transform.position);yield return Delay(.06f);Press(true);yield return Delay(.3f);
                Check(page.held,"Input picks up the résumé \""+page.label+"\"");
                Aim(slot.LandingPoint);yield return Delay(.25f);Check(slot.highlight.activeSelf,"Pointing at the shredder lights its slot");
                Press(false);yield return Delay(2.6f);
                Check(page.processed&&!page.gameObject.activeSelf,"The shredder takes \""+page.label+"\"");
            }
            Check(d.rooms[6].accepted==5&&!d.rooms[6].complete,"Five pages shredded once each; the chapter waits for a true line");
            yield return Delay(3.8f);Check(d.shredder.Offering,"A blank page offers four true lines");Capture("Old resume-Choice");
            yield return Click(d.rooms[6].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==ActionKind.ChooseLine&&x.value==2));
            Check(d.rooms[6].complete&&d.shredder.line=="I keep going."&&d.wardrobe.State.line=="I keep going.","Choosing a line completes the chapter and keeps the line for the wardrobe");
            Check(d.CompletedCount==5,"All five chapters complete");Check(PlayerPrefs.GetInt("AfterHours.Progress.v1")==94,"Completion saved as all five chapter flags");
            Check(new[]{"jumper","cap","cardigan","true-pin","lanyard","jacket","step-pin","scarf"}.All(d.wardrobe.Has),"Playing through earns every chapter piece, the small step pin and the kind scarf");
            yield return Visit(5);Check(d.ending.text.Contains("more"),"Completed journey changes rooftop ending");Capture("Rooftop");
            Status="Passed";
        }
        static void Capture(string label)
        {
            string path="Documentation/Previews/"+label+".png";Directory.CreateDirectory(Path.GetDirectoryName(path));var camera=d.player.Head.GetComponent<Camera>();var rt=new RenderTexture(1600,1000,24);var previous=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(rt);
        }
        static void Finish()
        {
            if(!running)return;running=false;EditorApplication.playModeStateChanged-=OnPlayState;EditorApplication.update-=Pump;Application.logMessageReceived-=OnLog;
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(d)d.saveEnabled=false;
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



