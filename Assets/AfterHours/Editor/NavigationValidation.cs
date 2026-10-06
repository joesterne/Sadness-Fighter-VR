using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AfterHours.Editor
{
    public static class NavigationValidation
    {
        static IEnumerator<float> sequence;static Mouse mouse;static Keyboard keyboard;static double next;static float waitUntil;
        static readonly List<string> results=new List<string>();static string checkpoint;static bool hadCheckpoint,hadProgress;static int progress;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        public static void Start(bool remainingOnly=false)
        {
            if(sequence!=null)return;if(!EditorApplication.isPlaying||P.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            checkpoint=PlayerPrefs.GetString(CheckpointStore.Key);hadCheckpoint=PlayerPrefs.HasKey(CheckpointStore.Key);hadProgress=PlayerPrefs.HasKey("AfterHours.Progress.v1");progress=PlayerPrefs.GetInt("AfterHours.Progress.v1");
            D.saveEnabled=false;if(D.intro)D.intro.Hide();mouse=InputSystem.AddDevice<Mouse>("NavigationTestMouse");keyboard=InputSystem.AddDevice<Keyboard>("NavigationTestKeyboard");
            results.Clear();
            if(remainingOnly&&File.Exists("Documentation/Navigation-validation.txt"))
            {
                File.Copy("Documentation/Navigation-validation.txt","Documentation/Navigation-before-fixture-correction.txt",true);
                results.AddRange(File.ReadAllLines("Documentation/Navigation-validation.txt").Where(x=>x.StartsWith("PASS:")));
                results.Add("NOTE: Remaining cases run separately after correcting the test camera's world-space walking direction.");
            }
            Status="Running";waitUntil=0;sequence=remainingOnly?Remaining():Run();EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        // The editor's own mouse and keyboard become "current" whenever they move, so each test event claims the device first.
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool ok,string name){results.Add((ok?"PASS: ":"FAIL: ")+name);if(!ok)throw new Exception(name);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static void Aim(Vector3 at){P.Head.rotation=Quaternion.LookRotation(at-P.Head.position);}
        static Interactable Button(ActionKind action,int destination=-1)=>D.rooms[D.currentRoom].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==action&&(destination<0||x.value==destination));
        static IEnumerator<float> MenuKey(bool open)
        {
            if(D.menu.IsOpen!=open){Queue(keyboard,new KeyboardState(Key.Tab));yield return .1f;Queue(keyboard,new KeyboardState());yield return .15f;}
            Check(D.menu.IsOpen==open,"Menu key "+(open?"opens":"closes")+" the menu in room "+D.currentRoom);
        }
        static void Approach(Interactable button)
        {var pos=button.transform.position-button.transform.forward*1.6f;pos.y=.04f;P.Place(pos,button.transform.eulerAngles.y);Aim(button.transform.position);}
        static void Arrived(int room)
        {
            Check(D.currentRoom==room&&!P.busy,"Room "+room+" arrives with movement unlocked");
            Check(D.rooms.Count(x=>x.root.activeSelf)==1&&D.rooms[room].root.activeSelf,"Room "+room+" is the only active environment");
            Check(P.fade.color.a<.01f,"Room "+room+" fade clears");
            Check(Vector2.Distance(new Vector2(P.Head.position.x,P.Head.position.z),new Vector2(D.rooms[room].spawn.position.x,D.rooms[room].spawn.position.z))<.15f,"Room "+room+" places the head over its safe spawn");
        }
        static IEnumerator<float> Run()
        {
            if(D.currentRoom!=0){D.Travel(0);yield return .8f;}P.smoothMotion=true;
            var key=MenuKey(true);while(key.MoveNext())yield return key.Current;
            Check(!D.menu.lobby.activeSelf&&!D.menu.tryAgain.activeSelf,"The lobby menu has no LOBBY or TRY AGAIN");
            key=MenuKey(false);while(key.MoveNext())yield return key.Current;
            // Every ENTER sign and menu return route, including the rooftop.
            for(int room=1;room<=5;room++)
            {
                var entry=Button(ActionKind.Travel,room);Approach(entry);yield return .2f;Aim(entry.transform.position);Press(true);yield return .12f;Press(false);yield return .85f;Arrived(room);
                key=MenuKey(true);while(key.MoveNext())yield return key.Current;
                var back=D.menu.Button(ActionKind.Rest);Check(back.gameObject.activeInHierarchy,"Room "+room+" menu offers LOBBY");
                if(room<5){Aim(back.transform.position);Press(true);yield return .12f;Press(false);yield return .85f;}
                else {Queue(keyboard,new KeyboardState(Key.Escape));yield return .1f;Queue(keyboard,new KeyboardState());yield return .8f;}
                Arrived(0);Check(!D.menu.IsOpen,"Returning to the lobby closes the menu");
            }
            // A floor gesture follows the aim and only uses the valid release target.
            P.Place(new Vector3(0,.04f,-4),0);yield return .2f;Aim(new Vector3(1.7f,.01f,-3));Press(true);yield return .2f;Aim(new Vector3(3.1f,.01f,-3));yield return .2f;Press(false);yield return .7f;
            Check(P.transform.position.x>2.7f,"Held floor gesture teleports to the updated release target");
            var before=P.transform.position;Aim(new Vector3(4,.01f,-3));Press(true);yield return .2f;Aim(P.Head.position+Vector3.up*3);yield return .2f;Press(false);yield return .5f;
            Check(Vector3.Distance(before,P.transform.position)<.1f,"Invalid release target cancels teleport");
            // A button press cannot change purpose if the aim moves onto the floor while held.
            P.Place(new Vector3(0,.04f,-4),0);yield return .2f;key=MenuKey(true);while(key.MoveNext())yield return key.Current;
            var close=D.menu.Button(ActionKind.CloseMenu);Aim(close.transform.position);Press(true);yield return .2f;before=P.transform.position;
            Check(!D.menu.IsOpen,"Menu CLOSE acts on press");
            Aim(new Vector3(2,.01f,-2.5f));yield return .2f;Press(false);yield return .6f;
            Check(Vector3.Distance(before,P.transform.position)<.1f,"Button hold cannot become a teleport gesture");
            // Teleport into a portal while its enter event occurs during the fade.
            var portal=D.rooms[0].root.GetComponentsInChildren<Portal>().First(x=>x.destination==2);var landing=portal.transform.position;landing.y=.01f;
            var approach=landing-portal.transform.forward*1.5f;approach.y=.04f;P.Place(approach,portal.transform.eulerAngles.y);yield return .2f;Aim(landing);Press(true);yield return .2f;Press(false);yield return 1.3f;Arrived(2);
            // Release carried props before disabling the room; hold input must not act in the destination.
            var box=D.rooms[2].root.GetComponentsInChildren<Grabbable>().First(x=>!x.processed);P.Place(new Vector3(box.transform.position.x,.04f,box.transform.position.z-1.7f),0);yield return .2f;Aim(box.transform.position);Press(true);yield return .2f;
            Check(box.held,"Input picks up an object before travel");
            Queue(keyboard,new KeyboardState(Key.Escape));yield return .12f;Queue(keyboard,new KeyboardState());yield return .8f;
            Check(!box.held&&box.Holder==null,"Travel releases carried objects in their original room");Arrived(0);
            before=P.transform.position;Aim(new Vector3(3,.01f,-3));yield return .2f;Press(false);yield return .5f;
            Check(Vector3.Distance(before,P.transform.position)<.1f,"A trigger held across rooms cannot teleport on release");
            var remaining=Remaining();while(remaining.MoveNext())yield return remaining.Current;
        }
        static IEnumerator<float> Remaining()
        {
            if(D.currentRoom!=0){D.Travel(0);yield return .8f;}P.smoothMotion=true;
            // Walking across a threshold uses the actual CharacterController and trigger.
            var portal=D.rooms[0].root.GetComponentsInChildren<Portal>().First(x=>x.destination==4);var approach=portal.transform.position-portal.transform.forward*1.1f;approach.y=.04f;P.Place(approach,portal.transform.eulerAngles.y);P.Head.rotation=Quaternion.LookRotation(portal.transform.forward);yield return .2f;
            Queue(keyboard,new KeyboardState(Key.W));yield return .7f;Queue(keyboard,new KeyboardState());yield return .8f;Arrived(4);
            D.Travel(-1);D.Travel(99);yield return .1f;Check(D.currentRoom==4&&!P.busy,"Invalid destinations leave the current room intact");
            D.Travel(1);D.Travel(3);yield return .8f;Arrived(1);
            Check(D.lastRoom==1,"Rapid competing travel requests keep the accepted destination");
            D.ocean.RestoreProgress(13);D.rooms[1].complete=false;D.ocean.Stroke();D.Travel(0);yield return .8f;Arrived(0);
            Check(D.ocean.strokes==14&&D.rooms[1].complete,"Leaving during the final paddle stroke preserves completion");
            D.Travel(1);yield return .8f;Check(Mathf.Abs(D.ocean.horizon.localPosition.z+12.6f)<.02f,"Returning after an interrupted row restores the horizon");
            D.Travel(0);yield return .8f;Arrived(0);
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
            if(Time.time<waitUntil)return;
            try{if(!sequence.MoveNext()){Finish("Passed");return;}waitUntil=Time.time+sequence.Current;}catch(Exception e){Finish("Failed: "+e.Message);}
        }
        static void Finish(string status)
        {
            if(sequence==null)return;sequence=null;Status=status;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.isPaused=false;
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(D)D.saveEnabled=false;
            if(hadCheckpoint)PlayerPrefs.SetString(CheckpointStore.Key,checkpoint);else PlayerPrefs.DeleteKey(CheckpointStore.Key);
            if(hadProgress)PlayerPrefs.SetInt("AfterHours.Progress.v1",progress);else PlayerPrefs.DeleteKey("AfterHours.Progress.v1");PlayerPrefs.Save();
            File.WriteAllLines("Documentation/Navigation-validation.txt",new[]{DateTime.UtcNow.ToString("u"),status,"Production mouse/keyboard input and physics, with direct placement for test setup and API calls for race conditions."}.Concat(results));Debug.Log("Navigation validation: "+status);
        }
    }
}
