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
    // Regression test for putting rooms back the way they started: RESET ROOM on every lobby door (which asks for a second press), the menu's
    // paper binder (its rings, its pages and the arrows that turn them, with the page-turn sound), and page 2's room ticks, ALL ROOMS and
    // RESET TICKED ROOMS. Rooms are given some progress first (strokes rowed, coffee poured, feelings named, the computer broken), and each
    // reset must take that away while finished chapters stay finished.
    public static class ResetValidation
    {
        static IEnumerator<float> run;static double next;static float deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key};
        static bool[] existed;static string checkpoint;static int[] values;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        static ComfortMenu M=>D.menu;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||!D||!D.player||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);values=keys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("ResetValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("ResetValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Info(string text){lines.Add("INFO: "+text);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static void Aim(Component c)
        {
            P.Head.rotation=Quaternion.LookRotation(c.transform.position-P.Head.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||!hit.collider.transform.IsChildOf(c.transform))
                throw new Exception("Pointer target obstructed: "+c.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        // Selects a button with the pointer, exactly as a player would.
        static IEnumerable<float> Click(Interactable b){Check(b,"The button to press exists");Aim(b);Press(true);yield return .12f;Press(false);yield return .2f;}
        static IEnumerable<float> Hold(Key key,float seconds){Queue(keyboard,new KeyboardState(key));yield return seconds;Queue(keyboard,new KeyboardState());yield return .15f;}
        // With a message showing, the first press only clears it, so opening may take a second press.
        static IEnumerable<float> MenuKey(bool open){for(int attempt=0;attempt<2&&M.IsOpen!=open;attempt++)foreach(var t in Hold(Key.Tab,.1f))yield return t;}
        static IEnumerable<float> Turn(int step){foreach(var t in Click(M.Button(ActionKind.MenuPage,step)))yield return t;yield return ComfortMenu.FlipSeconds*2+.15f;}
        static RoomResetButton Door(int room)=>D.rooms[0].root.GetComponentsInChildren<RoomResetButton>(true).FirstOrDefault(b=>b.room==room);
        // Stands the player in the lobby a little way back from a door, facing it.
        static void StandBefore(RoomResetButton door)
        {
            var doorway=door.transform.parent;var front=-doorway.forward;var spot=doorway.position+front*2.4f;spot.y=0;
            P.Place(spot,Quaternion.LookRotation(-front).eulerAngles.y);
        }
        // Some progress in four rooms, made directly, as a saved game would bring it back.
        static void MakeProgress()
        {
            D.ocean.RestoreProgress(5);
            D.kitchen.RestoreProgress(true,2,2);
            D.ambush.Restore(3);
            var damage=new int[D.rage.Damage().Length];damage[0]=1;D.rage.Restore(damage);
        }
        static bool Fresh(int room)
        {
            switch(room)
            {
                case 1:return D.ocean.strokes==0;
                case 3:return D.kitchen.fills==0&&D.kitchen.mistakes==0&&!D.kitchen.helped;
                case AmbushRoom.Room:return D.ambush.namedMask==0&&D.ambush.named==0&&D.rooms[room].accepted==0;
                case RageRoom.Room:return D.rage.Damage().All(x=>x==0)&&D.rooms[room].accepted==0;
                default:return D.rooms[room].accepted==0;
            }
        }

        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;D.saveEnabled=false;
            if(D.currentRoom!=0){D.Travel(0);yield return .9f;}
            Check(D.currentRoom==0,"The checks start in the lobby");

            // ---- RESET ROOM on the lobby doors
            var doors=D.rooms[0].root.GetComponentsInChildren<RoomResetButton>(true);
            Check(doors.Length==8&&new[]{1,2,3,4,6,RageRoom.Room,AmbushRoom.Room,WebRoom.Room}.All(r=>doors.Count(b=>b.room==r)==1),"Each of the eight lobby doors has one RESET ROOM button for its own room ("+doors.Length+")");
            Check(doors.All(b=>b.label&&b.label.text==RoomResetButton.Idle&&b.GetComponent<Interactable>()&&b.GetComponent<Interactable>().kind==ActionKind.ResetRoomAt&&b.GetComponent<Interactable>().value==b.room),"Every door button reads RESET ROOM and resets the room behind its door");
            Check(D.ambush.exitDoor&&!D.ambush.exitDoor.GetComponentInChildren<RoomResetButton>(true),"The ambush's way out back to the lobby has no reset button");
            Check(!GameDirector.CanReset(0)&&!D.ResetRoom(0),"The lobby itself is never reset");

            MakeProgress();D.rooms[1].complete=true;
            Check(!Fresh(1)&&!Fresh(3)&&!Fresh(AmbushRoom.Room)&&!Fresh(RageRoom.Room),"Progress made in the ocean, the kitchen, the ambush and the rage room");
            var ocean=Door(1);StandBefore(ocean);yield return .3f;
            int resets=D.RoomResets;
            foreach(var t in Click(ocean.GetComponent<Interactable>()))yield return t;
            Check(ocean.Armed&&ocean.label.text==RoomResetButton.Confirm&&D.ocean.strokes==5&&D.RoomResets==resets,"The first press asks first (TAP AGAIN TO RESET) and resets nothing");
            foreach(var t in Click(ocean.GetComponent<Interactable>()))yield return t;
            Check(!ocean.Armed&&ocean.label.text==RoomResetButton.Idle&&Fresh(1)&&D.RoomResets==resets+1,"The second press puts the ocean back: no strokes rowed, and the button reads RESET ROOM again");
            Check(D.rooms[1].complete&&D.currentRoom==0&&!Fresh(3),"The finished ocean chapter stays finished, the player stays in the lobby, and other rooms are untouched");
            foreach(var t in Click(ocean.GetComponent<Interactable>()))yield return t;
            Check(ocean.Armed,"Pressed once more, it asks again");
            yield return RoomResetButton.ConfirmSeconds+.3f;
            Check(!ocean.Armed&&ocean.label.text==RoomResetButton.Idle,"Left alone, the question lapses after "+RoomResetButton.ConfirmSeconds+" seconds");
            var ambushDoor=Door(AmbushRoom.Room);StandBefore(ambushDoor);yield return .3f;
            foreach(var t in Click(ambushDoor.GetComponent<Interactable>()))yield return t;
            foreach(var t in Click(ambushDoor.GetComponent<Interactable>()))yield return t;
            Check(Fresh(AmbushRoom.Room),"RESET ROOM on the ambush door un-names every feeling, so the clowns are big again");
            P.Place(D.rooms[0].spawn.position,D.rooms[0].spawn.eulerAngles.y);yield return .3f;

            // ---- The binder
            Check(D.pageTurn&&D.pageTurn.name=="SFX - Page turn","The page-turn sound is loaded (python Tools/make_audio.py Assets/AfterHours/Audio, then Build scene)");
            Check(M.pages!=null&&M.pages.Length==2&&M.PageCount==2,"The menu is a binder with two pages");
            var rings=M.transform.Find("Binder rings");
            Check(rings&&rings.GetComponent<MeshFilter>().sharedMesh&&rings.GetComponent<MeshFilter>().sharedMesh.vertexCount>0&&M.transform.Find("Binder cover"),"It has a cover and curly rings along the top (one mesh)");
            foreach(var t in MenuKey(true))yield return t;
            Check(M.IsOpen&&M.Page==0&&M.pages[0].activeSelf&&!M.pages[1].activeSelf,"The menu opens on page 1");
            var back=M.Button(ActionKind.MenuPage,-1);var forward=M.Button(ActionKind.MenuPage,1);
            Check(back&&forward&&!back.gameObject.activeInHierarchy&&forward.gameObject.activeInHierarchy,"On page 1 only the > arrow shows");
            var shown=M.GetComponentsInChildren<Interactable>(true).Where(b=>b.gameObject.activeInHierarchy).ToArray();
            float widest=shown.Max(b=>Vector3.Angle(Vector3.ProjectOnPlane(P.Head.forward,Vector3.up),b.transform.position-P.Head.position));
            Check(widest<42f,"Every button on page 1 and the arrow are within "+widest.ToString("0")+" degrees of straight ahead");
            foreach(var t in Turn(1))yield return t;
            Check(M.Page==1&&!M.pages[0].activeSelf&&M.pages[1].activeSelf&&M.pages[1].transform.localScale==Vector3.one,"The > arrow turns to page 2, which ends up lying flat");
            Check(back.gameObject.activeInHierarchy&&!forward.gameObject.activeInHierarchy,"On page 2 only the < arrow shows");
            shown=M.GetComponentsInChildren<Interactable>(true).Where(b=>b.gameObject.activeInHierarchy).ToArray();
            widest=shown.Max(b=>Vector3.Angle(Vector3.ProjectOnPlane(P.Head.forward,Vector3.up),b.transform.position-P.Head.position));
            Check(widest<42f,"Every button on page 2 is within "+widest.ToString("0")+" degrees of straight ahead ("+shown.Length+" buttons)");

            // ---- Page 2: ticks, ALL ROOMS, RESET TICKED ROOMS
            Check(GameDirector.Resettable.All(r=>M.resetToggles[r]&&M.Button(ActionKind.ResetToggle,r)&&M.resetToggles[r].text.Contains(D.rooms[r].title.ToUpperInvariant())&&M.resetToggles[r].text.Contains("KEEP AS IS")),"Page 2 lists every room, the rooftop too, each starting as KEEP AS IS");
            Check(M.resetAll&&M.resetAll.text.Contains("ALL ROOMS")&&M.Button(ActionKind.ResetAllToggle)&&M.Button(ActionKind.ResetChosen),"Page 2 has ALL ROOMS and RESET TICKED ROOMS");
            foreach(var t in Click(M.Button(ActionKind.ResetChosen)))yield return t;
            Check(M.resetNote.text.Contains("Tick a room first")&&M.Ticked.Count==0,"RESET TICKED ROOMS with nothing ticked says to tick a room first");
            MakeProgress();resets=D.RoomResets;
            foreach(var t in Click(M.Button(ActionKind.ResetToggle,RageRoom.Room)))yield return t;
            Check(M.Ticked.Count==1&&M.resetToggles[RageRoom.Room].text.Contains("WILL RESET")&&M.resetToggles[1].text.Contains("KEEP AS IS"),"Ticking the rage room marks it WILL RESET, and only it");
            foreach(var t in Click(M.Button(ActionKind.ResetToggle,1)))yield return t;foreach(var t in Click(M.Button(ActionKind.ResetToggle,1)))yield return t;
            Check(M.Ticked.Count==1&&M.resetToggles[1].text.Contains("KEEP AS IS"),"Ticking a room twice un-ticks it");
            foreach(var t in Click(M.Button(ActionKind.ResetChosen)))yield return t;
            Check(Fresh(RageRoom.Room)&&!Fresh(1)&&!Fresh(3)&&!Fresh(AmbushRoom.Room)&&D.RoomResets==resets+1,"RESET TICKED ROOMS gives the rage room a whole computer and leaves the other rooms alone");
            Check(M.Ticked.Count==0&&M.resetNote.text.StartsWith("1 room reset")&&M.resetToggles[RageRoom.Room].text.Contains("KEEP AS IS")&&M.IsOpen,"Afterwards nothing is ticked, the page says what happened, and the menu stays open");
            foreach(var t in Click(M.Button(ActionKind.ResetAllToggle)))yield return t;
            Check(M.AllTicked&&M.resetAll.text.Contains("WILL RESET")&&GameDirector.Resettable.All(r=>M.resetToggles[r].text.Contains("WILL RESET")),"ALL ROOMS ticks every room");
            foreach(var t in Click(M.Button(ActionKind.ResetAllToggle)))yield return t;
            Check(M.Ticked.Count==0&&M.resetAll.text.Contains("KEEP AS IS"),"ALL ROOMS again clears every tick");
            foreach(var t in Click(M.Button(ActionKind.ResetAllToggle)))yield return t;
            int towers=D.rooftop.tower.Towers;D.rooms[1].complete=true;
            foreach(var t in Click(M.Button(ActionKind.ResetChosen)))yield return t;
            Check(Fresh(1)&&Fresh(3)&&Fresh(AmbushRoom.Room)&&Fresh(RageRoom.Room)&&GameDirector.Resettable.All(Fresh),"Resetting all rooms takes away every room's progress");
            Check(D.RoomResets==resets+1+GameDirector.Resettable.Length&&D.rooftop.tower.Towers==towers+1&&!D.rooftop.tower.Fallen,"Every room is reset once, and the rooftop gets a new tower");
            Check(D.rooms[1].complete&&M.resetNote.text.StartsWith(GameDirector.Resettable.Length+" rooms reset"),"Finished chapters stay finished, and the page says how many rooms were reset");
            foreach(var t in Turn(-1))yield return t;
            Check(M.Page==0&&M.pages[0].activeSelf&&!M.pages[1].activeSelf,"The < arrow turns back to page 1");
            foreach(var t in Turn(1))yield return t;foreach(var t in Click(M.Button(ActionKind.ResetToggle,3)))yield return t;
            foreach(var t in MenuKey(false))yield return t;foreach(var t in MenuKey(true))yield return t;
            Check(M.IsOpen&&M.Page==0&&M.Ticked.Count==0,"Closed and opened again, the menu is back on page 1 with nothing ticked");
            foreach(var t in MenuKey(false))yield return t;

            // ---- Inside a room: TRY AGAIN still resets just that room, and a reset of the room you are in releases what you hold
            D.Travel(3);yield return .9f;
            D.kitchen.RestoreProgress(true,1,1);
            Check(D.ResetRooms(new[]{3})==1&&Fresh(3)&&D.currentRoom==3&&!P.busy,"Resetting the room you are standing in works there and then");
            D.Travel(0);yield return .9f;
            Info("Rooms reset during the run: "+D.RoomResets);
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
            if(D){D.saveEnabled=false;if(D.menu)D.menu.Close();}
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Reset-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Room resets: RESET ROOM on every lobby door (asks first), the menu binder's pages and arrows, and page 2's room ticks, ALL ROOMS and RESET TICKED ROOMS."}.Concat(lines));Debug.Log("Reset validation: "+Status+" ("+lines.Count+" checks)");
        }
    }
}
