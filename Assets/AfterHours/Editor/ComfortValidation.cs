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
    // Focused regression test for the comfort menu, seated movement and persistence across an actual scene reload.
    public static class ComfortValidation
    {
        static IEnumerator<float> run;static double next,deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette",IntroGuide.Key,Wardrobe.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("SeatedValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("SeatedValidationKeyboard");lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        // The editor's own mouse and keyboard become "current" whenever they move, so each test event claims the device first.
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Aim(Interactable b)
        {
            D.player.Head.rotation=Quaternion.LookRotation(b.transform.position-D.player.Head.position);
            if(!Physics.Raycast(D.player.Head.position,D.player.Head.forward,out var hit,14,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Interactable>()!=b)
                throw new Exception("Pointer target obstructed: "+b.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        static Interactable Button(ActionKind kind)=>D.rooms[D.currentRoom].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==kind);
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static float EyeHeight=>D.player.Head.position.y-D.player.transform.position.y;
        static float Yaw=>D.player.transform.eulerAngles.y;
        static VRPlayer P=>D.player;
        // Selects a menu button with the pointer, exactly as a player would.
        static IEnumerable<float> Click(ActionKind kind){Aim(D.menu.Button(kind));Press(true);yield return .12f;Press(false);yield return .2f;}
        static IEnumerable<float> Hold(Key key,float seconds){Queue(keyboard,new KeyboardState(key));yield return seconds;Queue(keyboard,new KeyboardState());yield return .15f;}
        static IEnumerable<float> MenuKey(bool open){if(D.menu.IsOpen!=open)foreach(var t in Hold(Key.Tab,.1f))yield return t;}
        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;
            Check(!P.seatedMode,"Fresh profile starts with tracked standing height");
            Check(!D.menu.IsOpen,"The menu starts closed");
            Check(!D.intro.IsShowing,"A returning player sees no first-visit guide");
            foreach(var t in MenuKey(true))yield return t;
            float reach=Vector3.Distance(P.Head.position,D.menu.transform.position);
            Check(D.menu.IsOpen&&reach>.6f&&reach<.75f,"Menu key opens the menu at arm's length ("+reach.ToString("0.00")+" m)");
            Check(!D.menu.lobby.activeSelf&&!D.menu.tryAgain.activeSelf,"The lobby menu hides LOBBY and TRY AGAIN");
            Check(D.rooms[0].root.GetComponentsInChildren<Interactable>(true).All(x=>x.kind==ActionKind.Travel||x.kind==ActionKind.Resume||x.kind==ActionKind.IntroNext||x.kind==ActionKind.IntroSkip||x.kind==ActionKind.GoToMirror||x.kind==ActionKind.WardrobePrev||x.kind==ActionKind.WardrobeNext),"Only doors, CONTINUE, the mirror, its wardrobe and the first-visit guide remain as lobby signs");
            foreach(var t in Click(ActionKind.SeatedMode))yield return t;
            Check(P.seatedMode&&Mathf.Abs(EyeHeight-1.65f)<.03f,"Menu SEATED VIEW enables seated view at 1.65 m");
            foreach(var t in Click(ActionKind.SeatedHeight))yield return t;
            Check(Mathf.Abs(EyeHeight-1.85f)<.03f,"Menu SEATED HEIGHT changes eye height to 1.85 m");
            Check(Mathf.Abs(P.Head.position.y-D.menu.transform.position.y-.2f*D.menu.transform.localScale.y)<.02f,"The open menu moves up with the new eye height");
            foreach(var t in Click(ActionKind.SeatedHeight))yield return t;
            Check(Mathf.Abs(EyeHeight-1.45f)<.03f,"Height cycles to 1.45 m");
            foreach(var t in Click(ActionKind.SeatedHeight))yield return t;
            Check(Mathf.Abs(EyeHeight-1.65f)<.03f,"Height cycles back to 1.65 m");
            Check(D.menu.height.text.Contains("1.65"),"The height button shows the current eye height");
            float yaw=Yaw;foreach(var t in Click(ActionKind.TurnRight))yield return t;
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,Yaw)-30)<.1f,"Menu TURN > rotates 30 degrees without physical turning");
            yaw=Yaw;foreach(var t in Click(ActionKind.TurnAround))yield return t;
            Check(Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(yaw,Yaw))-180)<.1f,"Menu TURN AROUND rotates 180 degrees");
            Check(Vector3.Dot(D.menu.transform.position-P.Head.position,P.Head.forward)>.3f,"The menu turns with the player and stays in front");
            foreach(var t in Click(ActionKind.FaceForward))yield return t;
            Check(Mathf.Abs(Mathf.DeltaAngle(P.Head.eulerAngles.y,D.rooms[0].spawn.eulerAngles.y))<.5f,"FACE FORWARD turns the room's main view back in front of the head");
            foreach(var t in Click(ActionKind.SmoothMotion))yield return t;
            Check(!P.smoothMotion&&D.menu.walking.text.Contains("TELEPORT ONLY"),"Menu MOVEMENT switches to teleport only");
            foreach(var t in Click(ActionKind.CloseMenu))yield return t;
            Check(!D.menu.IsOpen,"Menu CLOSE closes the menu");
            // Teleport only frees the left stick for turning.
            var spot=P.transform.position;yaw=Yaw;foreach(var t in Hold(Key.D,.2f))yield return t;
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,Yaw)-30)<.1f&&Vector3.Distance(spot,P.transform.position)<.05f,"With walking off, the left stick snap turns instead of walking");
            yaw=Yaw;foreach(var t in Hold(Key.S,.2f))yield return t;
            Check(Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(yaw,Yaw))-180)<.1f,"Pulling the stick back turns around once");
            Check(Mathf.Abs(VRPlayer.LandingYaw(new Vector3(0,1.6f,0),new Vector3(3,0,0),0)-90)<.1f&&Mathf.Abs(VRPlayer.LandingYaw(new Vector3(0,1.6f,0),new Vector3(.1f,0,0),45)-45)<.1f,"Seated teleports face the pointed direction, unless the point is underfoot");
            // The vignette narrows the view only while the stick walks.
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.SmoothMotion))yield return t;foreach(var t in MenuKey(false))yield return t;
            Check(P.smoothMotion,"Menu MOVEMENT switches walking back on");
            Check(P.vignette&&!P.vignette.enabled,"No vignette while standing still");
            Queue(keyboard,new KeyboardState(Key.W));yield return .35f;bool shown=P.vignette.enabled;Queue(keyboard,new KeyboardState());yield return 1;
            Check(shown&&!P.vignette.enabled,"The comfort vignette shows while walking and clears after stopping");
            // Walking took the player toward the entrance wall: the menu must still open in front of it and work.
            foreach(var t in MenuKey(true))yield return t;
            Check(D.menu.IsOpen,"The menu opens near the entrance wall (fitted at "+D.menu.transform.localScale.x.ToString("0.00")+" scale); the next press must reach it");
            foreach(var t in Click(ActionKind.Vignette))yield return t;foreach(var t in MenuKey(false))yield return t;
            Queue(keyboard,new KeyboardState(Key.W));yield return .35f;shown=P.vignette.enabled;Queue(keyboard,new KeyboardState());yield return .2f;
            Check(!P.comfortVignette&&!shown,"Menu COMFORT VIGNETTE switches the vignette off");
            foreach(var t in MenuKey(true))yield return t;
            D.Travel(1);yield return .8f;
            Check(!D.menu.IsOpen,"Travel closes the menu");
            foreach(var t in MenuKey(true))yield return t;
            Check(D.menu.lobby.activeSelf&&D.menu.tryAgain.activeSelf&&D.menu.status.text.Contains("OCEAN"),"Chapter menus offer LOBBY and TRY AGAIN and name the room");
            foreach(var t in MenuKey(false))yield return t;
            for(int i=0;i<4;i++){D.ocean.Stroke();yield return .8f;}
            Check(D.ocean.strokes==4,"Short ocean session records four strokes");
            D.Travel(2);yield return .8f;
            var box=D.rooms[2].root.GetComponentsInChildren<Grabbable>().First();
            D.rooms[2].root.GetComponentInChildren<DropZone>().RestoreItem(box);D.AcceptItem(2,box.label);
            D.Travel(3);yield return .8f;D.kitchen.AskForHelp();D.kitchen.cup.transform.position=D.kitchen.nozzle.position;D.kitchen.Brew();yield return .2f;
            Check(D.kitchen.fills==1,"Short kitchen session records one pour and help");
            D.Travel(4);yield return .8f;
            var note=D.rooms[4].root.GetComponentsInChildren<Grabbable>().First();var tray=D.rooms[4].root.GetComponentsInChildren<DropZone>().First(x=>x.category==note.category);
            tray.RestoreItem(note);D.AcceptItem(4,note.label);D.shredder.ShredNow(D.shredder.pages[0]);D.SaveCheckpoint();
            var saved=JsonUtility.FromJson<CheckpointStore.State>(PlayerPrefs.GetString(CheckpointStore.Key));
            Check(saved.strokes==4&&saved.processed.Length==3&&saved.fills==1&&saved.helped,"Checkpoint stores partial progress in all five chapters");
            D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return .8f;
            Check(D.player.seatedMode&&Mathf.Abs(EyeHeight-1.65f)<.03f,"Seated mode and eye-height preference survive reload");
            Check(!P.comfortVignette,"Vignette preference survives reload");
            Check(D.ocean.strokes==4&&Mathf.Abs(D.ocean.horizon.localPosition.z+3.6f)<.01f,"Ocean count and horizon restore before first entry");
            Check(D.rooms[2].accepted==1&&D.rooms[2].root.GetComponentsInChildren<Grabbable>(true).Count(x=>x.processed)==1,"Delivered box restores exactly once");
            Check(D.rooms[4].accepted==1&&D.rooms[4].root.GetComponentsInChildren<Grabbable>(true).Count(x=>x.processed)==1,"Sorted note restores exactly once");
            Check(D.kitchen.helped&&D.kitchen.fills==1,"Kitchen help and partial coffee restore");
            Check(D.rooms[6].accepted==1&&D.shredder.pages.Count(x=>x.processed)==1&&!D.shredder.pages[0].gameObject.activeSelf&&D.shredder.piles[0].activeSelf,"A shredded résumé restores exactly once, and the paper stays in the bin");
            Check(D.lastRoom==4&&D.resumeLabel.text.Contains("ARCHIVE"),"Continue button names the last room");
            Aim(Button(ActionKind.Resume));Press(true);yield return .12f;Press(false);yield return .85f;
            Check(D.currentRoom==4,"Continue input returns to the saved room");
            D.Travel(1);yield return .8f;
            Check(D.ocean.strokes==4&&Mathf.Abs(D.ocean.horizon.localPosition.z+3.6f)<.01f,"First room activation preserves restored ocean position");
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.ResetRoom))yield return t;
            Check(JsonUtility.FromJson<CheckpointStore.State>(PlayerPrefs.GetString(CheckpointStore.Key)).strokes==0&&!D.menu.IsOpen,"Menu TRY AGAIN saves the reset and closes the menu");
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.Rest))yield return t;yield return .8f;
            Check(D.currentRoom==0,"Menu LOBBY returns to the lobby");
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.SeatedMode))yield return t;
            Check(!D.player.seatedMode&&Mathf.Abs(D.player.rig.transform.localPosition.y)<.01f,"Standing view removes the seated offset");
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
            if(D)D.saveEnabled=false;if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Seated-session-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Pointer and keyboard input for the comfort menu, turning, walking and resume; partial-progress fixtures for storage; actual scene reload."}.Concat(lines));Debug.Log("Seated and session validation: "+Status);
        }
    }
}
