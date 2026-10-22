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
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key,Wardrobe.Key,Music.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;static string backupFile;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("SeatedValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("SeatedValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
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
        // Every saved value as text (null when absent), and putting such a set back exactly.
        static string[] PrefsNow()=>keys.Select(k=>!PlayerPrefs.HasKey(k)?null:k==CheckpointStore.Key||k==Wardrobe.Key?PlayerPrefs.GetString(k):PlayerPrefs.GetInt(k).ToString()).ToArray();
        static void PrefsPut(string[] set){for(int i=0;i<keys.Length;i++){if(set[i]==null)PlayerPrefs.DeleteKey(keys[i]);else if(keys[i]==CheckpointStore.Key||keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],set[i]);else PlayerPrefs.SetInt(keys[i],int.Parse(set[i]));}PlayerPrefs.Save();}
        static CheckpointStore.State Stored=>JsonUtility.FromJson<CheckpointStore.State>(PlayerPrefs.GetString(CheckpointStore.Key));
        // Selects a menu button with the pointer, exactly as a player would.
        static IEnumerable<float> Click(ActionKind kind){Aim(D.menu.Button(kind));Press(true);yield return .12f;Press(false);yield return .2f;}
        static IEnumerable<float> Hold(Key key,float seconds){Queue(keyboard,new KeyboardState(key));yield return seconds;Queue(keyboard,new KeyboardState());yield return .15f;}
        // With a message showing, the first press only clears it, so opening may take a second press.
        static IEnumerable<float> MenuKey(bool open){for(int attempt=0;attempt<2&&D.menu.IsOpen!=open;attempt++)foreach(var t in Hold(Key.Tab,.1f))yield return t;}
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
            Check(D.menu.gentle&&D.menu.dark&&D.menu.gentle.text.Contains("GENTLE AMBUSH")&&D.menu.gentle.text.Contains("OFF")&&D.menu.dark.text.Contains("DARK OPENING")&&D.menu.dark.text.Contains("ON")&&D.menu.Button(ActionKind.GentleAmbush)&&D.menu.Button(ActionKind.DarkOpening),"The menu offers GENTLE AMBUSH (off) and DARK OPENING (on)");
            var panelButtons=D.menu.GetComponentsInChildren<Interactable>(true).Where(b=>b.gameObject.activeInHierarchy).ToArray();
            float widest=panelButtons.Max(b=>Vector3.Angle(Vector3.ProjectOnPlane(P.Head.forward,Vector3.up),b.transform.position-P.Head.position));
            Check(panelButtons.Length==15&&widest<42f,"Every one of the menu's "+panelButtons.Length+" buttons is within "+widest.ToString("0")+" degrees of straight ahead");
            // Calm background music: the theme in the lobby, a quieter piece in the chapters, and MUSIC in the menu to switch it off.
            // The suites step a paused editor, and a paused editor plays no audio, so a playing piece is checked by its clip and its level.
            var music=D.music;
            Check(music&&music.transform.IsChildOf(D.transform)&&music.source&&music.source.loop&&music.source.spatialBlend==0&&music.theme&&music.theme.name=="Music - After hours theme"&&music.calm&&music.calm.name=="Music - Quiet hours","The game carries its music: the theme and the quiet piece, on the director rather than in a room");
            Check(music.byRoom!=null&&music.byRoom.Length==D.rooms.Length&&new[]{1,2,3,4,6,7,8}.Select(r=>music.ClipFor(r).name).SequenceEqual(new[]{"Music - Slow tide","Music - Quiet hours","Music - Paper lanterns","Music - Night window","Music - Quiet hours","Music - Slow tide","Music - Night window"})&&music.ClipFor(0)==music.theme&&music.ClipFor(5)==music.theme,"Every chapter has its piece: Slow tide, Quiet hours, Paper lanterns, Night window, Quiet hours, Slow tide, Night window; the lobby and rooftop the theme");
            Check(D.rooms.All(r=>r.root.GetComponentsInChildren<AudioSource>(true).All(s=>!s.clip||!s.clip.name.StartsWith("Music"))),"No room plays music of its own, so two pieces never overlap");
            Check(music.on&&music.source.clip==music.theme&&music.Level>Music.Volume*.9f&&D.menu.music&&D.menu.music.text.Contains("MUSIC")&&D.menu.music.text.Contains("ON")&&D.menu.Button(ActionKind.Music),"Music plays in the lobby (the theme), and the menu offers MUSIC (on)");
            foreach(var t in Click(ActionKind.Music))yield return t;yield return Music.FadeSeconds+.3f;
            Check(!music.on&&music.Level==0&&music.Playing==null&&D.menu.music.text.Contains("OFF")&&PlayerPrefs.GetInt(Music.Key,1)==0,"MUSIC fades the music out and pauses it, and the choice is stored");
            Check(D.rooms[0].root.GetComponentsInChildren<Interactable>(true).All(x=>x.kind==ActionKind.Travel||x.kind==ActionKind.Resume||x.kind==ActionKind.IntroNext||x.kind==ActionKind.IntroSkip||x.kind==ActionKind.GoToMirror||x.kind==ActionKind.MirrorTap||x.kind==ActionKind.WardrobePrev||x.kind==ActionKind.WardrobeNext||x.kind==ActionKind.WardrobeBuy),"Only doors, CONTINUE, the mirror (and its glass), its wardrobe (and BUY) and the first-visit guide remain as lobby signs");
            foreach(var t in Click(ActionKind.SeatedMode))yield return t;
            Check(P.seatedMode&&Mathf.Abs(EyeHeight-1.65f)<.03f,"Menu SEATED VIEW enables seated view at 1.65 m");
            foreach(var t in Click(ActionKind.SeatedHeight))yield return t;
            Check(Mathf.Abs(EyeHeight-1.85f)<.03f,"Menu SEATED HEIGHT changes eye height to 1.85 m");
            Check(Mathf.Abs(P.Head.position.y-D.menu.transform.position.y-.16f*D.menu.transform.localScale.y)<.02f,"The open menu moves up with the new eye height");
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
            D.Travel(RageRoom.Room);yield return .8f;var mouse=D.rage.parts[3];D.rage.HitNow(mouse);D.rage.HitNow(mouse);
            Check(mouse.Broken&&D.rooms[RageRoom.Room].accepted==1&&!D.rooms[RageRoom.Room].complete,"Short rage room session breaks the mouse");
            D.darkOpening=false;D.Travel(AmbushRoom.Room);yield return .9f;D.ambush.Feel(0);D.ambush.Feel(4);
            Check(D.ambush.named==2&&D.rooms[AmbushRoom.Room].accepted==2&&!D.rooms[AmbushRoom.Room].complete,"Short ambush session names two feelings");
            D.Travel(4);yield return .8f;
            var note=D.rooms[4].root.GetComponentsInChildren<Grabbable>().First();var tray=D.rooms[4].root.GetComponentsInChildren<DropZone>().First(x=>x.category==note.category);
            tray.RestoreItem(note);D.AcceptItem(4,note.label);D.shredder.ShredNow(D.shredder.pages[0]);D.SaveCheckpoint();
            var saved=JsonUtility.FromJson<CheckpointStore.State>(PlayerPrefs.GetString(CheckpointStore.Key));
            Check(saved.strokes==4&&saved.processed.Length==3&&saved.fills==1&&saved.helped&&saved.rage!=null&&saved.rage.Length==D.rage.parts.Length+D.rage.extras.Length&&saved.rage.Sum()==2&&saved.rage[3]==2&&saved.ambush==17&&!saved.darkOpening&&!saved.gentleAmbush,"Checkpoint stores partial progress in all seven chapters");
            // Autosave: a snapshot as a room is left, another on arriving in the next, and a quiet one now and then; nothing is rewritten when nothing changed.
            string settled=PlayerPrefs.GetString(CheckpointStore.Key);
            backupFile=Path.Combine(Path.GetTempPath(),"AfterHours-save-test-"+Guid.NewGuid().ToString("N")+".json");SaveBackup.PathOverride=backupFile;SaveBackup.Delete();
            int stored=CheckpointStore.Writes,copied=SaveBackup.Writes;
            D.SaveCheckpoint();
            Check(File.Exists(backupFile)&&SaveBackup.Writes==copied+1&&CheckpointStore.Writes==stored,"Saving writes the backup copy once, and leaves an unchanged checkpoint alone");
            var held=JsonUtility.FromJson<SaveBackup.Copy>(File.ReadAllText(backupFile)).entries.ToDictionary(e=>e.key);
            Check(held.ContainsKey(CheckpointStore.Key)&&held[CheckpointStore.Key].value==settled&&held["AfterHours.Seated"].number==1&&held["AfterHours.Vignette"].number==0&&held[IntroGuide.Key].number==1&&held[Music.Key].number==0,"The backup copy holds the checkpoint and the comfort settings (music included), exactly as stored");
            D.SaveCheckpoint();D.SaveCheckpoint();
            Check(CheckpointStore.Writes==stored&&SaveBackup.Writes==copied+1,"Saving again with nothing changed writes nothing at all");
            D.gentleAmbush=true;D.SaveCheckpoint();
            Check(CheckpointStore.Writes==stored+1&&SaveBackup.Writes==copied+2&&Stored.gentleAmbush,"A change is written once, to the checkpoint and to the backup copy");
            D.gentleAmbush=false;D.SaveCheckpoint();
            Check(PlayerPrefs.GetString(CheckpointStore.Key)==settled,"Undoing the change gives back exactly the same checkpoint");
            yield return 1.6f;
            D.gentleAmbush=true;D.autosaveClock=GameDirector.AutosaveSeconds-.2f;yield return .6f;
            Check(Stored.gentleAmbush&&D.autosaveClock<5&&!P.SavedVisible,"The quiet timer saves a change after "+GameDirector.AutosaveSeconds+" seconds, and shows nothing");
            D.gentleAmbush=false;D.SaveCheckpoint();
            D.Travel(0);yield return .1f;D.gentleAmbush=true;yield return .9f;
            Check(D.currentRoom==0&&Stored.gentleAmbush,"Arriving in a room saves too: a change made during the move is kept");
            Check(P.SavedVisible&&P.savedCue.text=="Saved"&&P.savedCue.transform.IsChildOf(P.Head),"A faint \"Saved\" shows after the room change");
            yield return 1.6f;
            Check(!P.SavedVisible,"And it is gone within a couple of seconds");
            D.gentleAmbush=false;D.SaveCheckpoint();
            P.ShowSaved();foreach(var t in MenuKey(true))yield return t;yield return .1f;
            Check(D.menu.IsOpen&&!P.SavedVisible,"An open menu has the cue's place, so the cue steps aside");
            foreach(var t in MenuKey(false))yield return t;
            P.ToggleClean();D.Travel(4);yield return 1;
            Check(P.cleanMode&&!P.SavedVisible,"Clean mode shows no cue after a room change");
            P.ToggleClean();
            Check(PlayerPrefs.GetString(CheckpointStore.Key)==settled&&D.currentRoom==4,"After all of that the saved game is exactly what it was");
            // A fresh install finds the backup copy and takes the progress back; it never replaces progress it already has.
            D.SaveCheckpoint();var before=PrefsNow();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);
            Check(SaveBackup.RestoreIfMissing()&&PrefsNow().SequenceEqual(before),"A fresh install takes the checkpoint, progress and settings back from the backup copy");
            PlayerPrefs.SetString(CheckpointStore.Key,"{\"version\":1,\"lastRoom\":2}");
            Check(!SaveBackup.RestoreIfMissing()&&PlayerPrefs.GetString(CheckpointStore.Key).Contains("\"lastRoom\":2"),"An install with progress of its own is never overwritten");
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);
            File.WriteAllText(backupFile,"{ this is not a save");
            Check(!SaveBackup.RestoreIfMissing()&&!PlayerPrefs.HasKey(CheckpointStore.Key),"A damaged backup copy is ignored");
            File.WriteAllText(backupFile,JsonUtility.ToJson(new SaveBackup.Copy{entries=new[]{new SaveBackup.Entry{key="Not.Ours",number=7}}}));
            Check(!SaveBackup.RestoreIfMissing()&&!PlayerPrefs.HasKey("Not.Ours"),"Only the game's own saved values are ever restored");
            PrefsPut(before);SaveBackup.Delete();SaveBackup.PathOverride=null;
            Check(PrefsNow().SequenceEqual(before),"The saved values are back as they were");
            D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return .8f;
            Check(D.player.seatedMode&&Mathf.Abs(EyeHeight-1.65f)<.03f,"Seated mode and eye-height preference survive reload");
            Check(!P.comfortVignette,"Vignette preference survives reload");
            Check(!D.music.on&&D.music.Level==0&&D.music.Playing==null,"Music off survives reload");
            Check(D.ocean.strokes==4&&Mathf.Abs(D.ocean.horizon.localPosition.z+3.6f)<.01f,"Ocean count and horizon restore before first entry");
            Check(D.rooms[2].accepted==1&&D.rooms[2].root.GetComponentsInChildren<Grabbable>(true).Count(x=>x.processed)==1,"Delivered box restores exactly once");
            Check(D.rooms[4].accepted==1&&D.rooms[4].root.GetComponentsInChildren<Grabbable>(true).Count(x=>x.processed)==1,"Sorted note restores exactly once");
            Check(D.kitchen.helped&&D.kitchen.fills==1,"Kitchen help and partial coffee restore");
            Check(D.rooms[6].accepted==1&&D.shredder.pages.Count(x=>x.processed)==1&&!D.shredder.pages[0].gameObject.activeSelf&&D.shredder.piles[0].activeSelf&&D.shredder.litter[0].activeSelf&&!D.shredder.litter[1].activeSelf,"A shredded résumé restores exactly once, and the paper stays in the bin and on the floor");
            var scrapMeshes=D.shredder.litter.SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).ToArray();var scrapSpots=scrapMeshes.SelectMany(m=>m.vertices).ToArray();
            Check(D.shredder.litter.All(g=>g.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.vertexCount)/24>=100),"Each page leaves at least 100 scraps ("+scrapMeshes.Sum(m=>m.vertexCount)/24+" in all)");
            Check(scrapSpots.Max(v=>v.x)-scrapSpots.Min(v=>v.x)>9f&&scrapSpots.Max(v=>v.z)-scrapSpots.Min(v=>v.z)>11f&&scrapSpots.Any(v=>v.y>.77f&&v.y<.8f)&&scrapSpots.Any(v=>v.y>1.43f&&v.y<1.46f)&&scrapSpots.Any(v=>v.y>.8f&&v.y<.82f),"The scraps lie right across the room, on the desk, on the shredder's top and on the window sill ("+(scrapSpots.Max(v=>v.x)-scrapSpots.Min(v=>v.x)).ToString("0.0")+" by "+(scrapSpots.Max(v=>v.z)-scrapSpots.Min(v=>v.z)).ToString("0.0")+" m)");
            Check(D.shredder.litter.All(g=>g.GetComponentsInChildren<Collider>(true).Length==0)&&D.shredder.litter.All(g=>g.GetComponentsInChildren<MeshRenderer>(true).Length==2),"Scraps have no colliders, and each set is two draws");
            var egg=D.shredder.egg;
            Check(egg&&egg.director==D&&egg.pages.Length==5&&egg.sound&&egg.sound.name=="SFX - Confetti"&&egg.pop&&egg.shower&&egg.transform.IsChildOf(D.rooms[6].root.transform),"The résumé room carries its confetti: a burst, a shower and a pop sound");
            Check(D.shredder.pages.All(x=>x.throwable&&!x.tossable&&x.GetComponent<ConfettiPage>()&&x.GetComponent<ConfettiPage>().egg==egg),"Every page can be thrown (helped toward the shredder), and tells the confetti when it hits something");
            Check(new[]{egg.pop,egg.shower}.All(s=>s.main.startColor.mode==ParticleSystemGradientMode.RandomColor&&s.main.startColor.gradient.colorKeys.Length==7&&s.collision.enabled&&s.collision.planeCount==1&&!s.main.loop&&!s.main.playOnAwake&&s.main.maxParticles>=260&&!s.isPlaying),"The confetti comes in seven colours, rests on the floor plane and plays once when asked");
            Check(new[]{egg.pop,egg.shower}.All(s=>{var r=s.GetComponent<ParticleSystemRenderer>();return r.sharedMaterial==D.shredder.egg.pop.GetComponent<ParticleSystemRenderer>().sharedMaterial&&r.sharedMaterial.name=="28 Confetti paper"&&r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off;}),"Both systems share one flat unlit paper material and cast no shadows");
            var boxBlast=D.boxBlast;var blastSystems=boxBlast?new[]{boxBlast.flash,boxBlast.sparks,boxBlast.smoke,boxBlast.chips}:new ParticleSystem[0];
            Check(boxBlast&&BoxBlast.Room==2&&boxBlast.director==D&&boxBlast.boxes.Length==5&&boxBlast.boxes.All(x=>x&&x.throwable&&x.room==2&&x.transform.IsChildOf(D.rooms[2].root.transform))&&boxBlast.sound&&boxBlast.sound.name=="SFX - Blast"&&boxBlast.transform.IsChildOf(D.rooms[2].root.transform)&&blastSystems.Length==4&&blastSystems.All(x=>x),"The heavy room carries its box blast: the five boxes, a flash, sparks, smoke, chips and a boom");
            Check(blastSystems.All(x=>x.main.startColor.mode==ParticleSystemGradientMode.RandomColor&&!x.main.loop&&!x.main.playOnAwake&&!x.isPlaying&&x.main.simulationSpace==ParticleSystemSimulationSpace.World&&x.emission.burstCount==1&&x.emission.rateOverTime.constant==0)&&new[]{1,70,9,110}.SequenceEqual(blastSystems.Select(x=>(int)x.emission.GetBurst(0).count.constant)),"The blast plays once when asked: a flash, 70 sparks, 9 puffs of smoke and 110 chips, each a single burst");
            Check(boxBlast.chips.collision.enabled&&boxBlast.chips.collision.planeCount==1&&boxBlast.chips.collision.GetPlane(0)&&boxBlast.chips.collision.GetPlane(0).IsChildOf(D.rooms[2].root.transform)&&blastSystems.Count(x=>x.collision.enabled)==1&&boxBlast.chips.main.startColor.gradient.colorKeys.Length==7&&boxBlast.sparks.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Stretch,"The chips land on a floor plane in seven colours, and the sparks are stretched along their flight");
            Check(blastSystems.All(x=>{var r=x.GetComponent<ParticleSystemRenderer>();return r.sharedMaterial==egg.pop.GetComponent<ParticleSystemRenderer>().sharedMaterial&&r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off&&!r.receiveShadows;})&&blastSystems.Sum(x=>x.main.maxParticles)<=300,"The blast shares the flat unlit paper material, casts no shadows and never holds more than 300 bits ("+blastSystems.Sum(x=>x.main.maxParticles)+")");
            // The kitchen: five mugs that can be thrown (and break), six cupboard doors with an orange mug behind one, the machine's lever and steam wand, and the effects of a breaking mug.
            var kitchen=D.kitchen;var kitchenRoot=D.rooms[3].root.transform;var spoutTargets=kitchenRoot.GetComponentsInChildren<SendTarget>(true);
            Check(kitchen.mugs.Length==5&&kitchen.mugs.All(m=>m&&m.item),"The kitchen has five mugs, each with a grabbable");
            Check(kitchen.mugs.All(m=>m.item.tossable&&!m.item.throwable&&m.item.upright&&m.item.category=="mug"&&m.item.room==3),"The mugs are tossed like the ambush paperwork: tossable, never helped to a target, upright, kind 'mug', in the kitchen");
            Check(kitchen.mugs.All(m=>m.coffee&&m.coffee.gameObject.activeSelf==(m.level>0))&&kitchen.mugs.Where(m=>!m.item.Equals(kitchen.cup)).All(m=>m.level==0),"Every mug has its coffee disc, shown only while there is coffee in it, and only your cup carries a saved pour");
            Check(kitchen.mugs.All(m=>m.transform.IsChildOf(kitchenRoot)),"Every mug belongs to the kitchen");
            Check(kitchen.mugs.Count(m=>m.orange)==1&&kitchen.orangeMug&&kitchen.orangeMug==kitchen.mugs.Single(m=>m.orange)&&kitchen.orangeMug.item.label=="Orange mug","One mug is the orange one");
            Check(kitchen.cup==kitchen.mugs[0].item&&kitchen.cup.label=="Your cup","Your cup is the first mug");
            Check(kitchen.orangeMug.GetComponent<Renderer>().sharedMaterial.name=="30 Orange mug"&&kitchen.mugs.Where(m=>!m.orange).All(m=>m.GetComponent<Renderer>().sharedMaterial.name!="30 Orange mug"),"Only the hidden mug is orange");
            Check(spoutTargets.Length==1&&spoutTargets[0].category=="mug"&&!spoutTargets[0].only&&kitchen.mugs.All(m=>spoutTargets[0].Accepts(m.item)),"One spout target takes any mug");
            Check(kitchen.doors.Length==6&&kitchen.doors.Select((x,i)=>x&&x.GetComponentInChildren<Interactable>().kind==ActionKind.Cabinet&&x.GetComponentInChildren<Interactable>().value==i&&x.GetComponent<Rigidbody>().isKinematic&&Mathf.Sign(x.openAngle)==(i<3?1:-1)&&Mathf.Abs(x.openAngle)>=90&&!x.open).All(b=>b),"Six cupboard doors hang on their outer edges, shut, each opened by pointing at it");
            Check(KitchenRoom.OrangeCabinet==4&&kitchen.InCabinet(kitchen.orangeMug)&&!kitchen.mugs.Where(m=>!m.orange).Any(m=>kitchen.InCabinet(m)),"The orange mug waits inside the fifth cupboard, and nothing else is hidden there");
            var orangePin=Outfits.Get("orange-pin");
            Check(orangePin.id=="orange-pin"&&orangePin.slot==OutfitSlot.Pin&&!orangePin.starter&&!string.IsNullOrEmpty(orangePin.hint)&&Outfits.Earnable==12,"The orange mug pin is one of twelve pieces to earn, and its hint is a gentle one");
            var steamMain=kitchen.steam?kitchen.steam.main:default;
            Check(kitchen.lever&&kitchen.lever.IsChildOf(kitchenRoot)&&kitchen.lever.GetComponentInChildren<Interactable>().kind==ActionKind.Brew&&kitchen.steam&&!steamMain.playOnAwake&&steamMain.loop&&kitchen.steam.emission.rateOverTime.constant==0&&kitchenRoot.GetComponentsInChildren<Interactable>(true).Count(x=>x.kind==ActionKind.Steam)==2&&kitchenRoot.GetComponentsInChildren<Interactable>(true).Count(x=>x.kind==ActionKind.Brew)==2,"The machine has a lever that brews, a steam wand and valve that blow steam, and the BREW button besides");
            var breakSystems=new[]{kitchen.shardsCounter,kitchen.shardsFloor,kitchen.dust};
            Check(breakSystems.All(x=>x&&!x.main.loop&&!x.main.playOnAwake&&!x.isPlaying&&x.main.simulationSpace==ParticleSystemSimulationSpace.World&&x.emission.burstCount==1)&&new[]{34,34,6}.SequenceEqual(breakSystems.Select(x=>(int)x.emission.GetBurst(0).count.constant))&&kitchen.shardsCounter.collision.enabled&&kitchen.shardsFloor.collision.enabled&&kitchen.shardsCounter.collision.planeCount==1&&kitchen.shardsFloor.collision.planeCount==1&&kitchen.shardsCounter.collision.GetPlane(0).position.y>kitchen.shardsFloor.collision.GetPlane(0).position.y+.9f&&kitchen.splashes.Length==4&&kitchen.splashes.All(x=>x&&!x.gameObject.activeSelf),"A breaking mug has shards over the counter and over the floor, a puff of dust and four coffee splashes, each waiting to be played");
            var kitchenEffects=breakSystems.Concat(new[]{kitchen.steam}).ToArray();
            Check(kitchenEffects.All(x=>{var r=x.GetComponent<ParticleSystemRenderer>();return r.sharedMaterial.name=="28 Confetti paper"&&r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off&&!r.receiveShadows;})&&kitchenEffects.Sum(x=>x.main.maxParticles)<=300&&kitchen.shatterSound&&kitchen.shatterSound.name=="SFX - Shatter"&&kitchen.cabinetSound&&kitchen.cabinetSound.name=="SFX - Cabinet"&&kitchen.steamSound&&kitchen.steamSound.name=="SFX - Steam","The kitchen's effects share the flat paper material, cast no shadows, never hold more than 300 bits ("+kitchenEffects.Sum(x=>x.main.maxParticles)+") and have their three sounds");
            Check(D.rooms[RageRoom.Room].accepted==1&&D.rage.parts[3].Broken&&D.rage.parts.Take(3).All(x=>x.damage==0)&&!D.rage.parts[3].visual.gameObject.activeSelf,"A broken mouse restores exactly once, still in pieces");
            Check(D.ambush.namedMask==17&&D.ambush.named==2&&D.rooms[AmbushRoom.Room].accepted==2&&!D.darkOpening&&!D.gentleAmbush&&!D.rooms[AmbushRoom.Room].complete,"Two named feelings and the dark-opening choice restore exactly once");
            Check(D.lastRoom==4&&D.resumeLabel.text.Contains("ARCHIVE"),"Continue button names the last room");
            Aim(Button(ActionKind.Resume));Press(true);yield return .12f;Press(false);yield return .85f;
            Check(D.currentRoom==4,"Continue input returns to the saved room");
            D.Travel(1);yield return .8f;
            Check(D.ocean.strokes==4&&Mathf.Abs(D.ocean.horizon.localPosition.z+3.6f)<.01f,"First room activation preserves restored ocean position");
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.ResetRoom))yield return t;
            Check(JsonUtility.FromJson<CheckpointStore.State>(PlayerPrefs.GetString(CheckpointStore.Key)).strokes==0&&!D.menu.IsOpen,"Menu TRY AGAIN saves the reset and closes the menu");
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.Music))yield return t;foreach(var t in MenuKey(false))yield return t;yield return Music.FadeSeconds;
            Check(D.music.on&&D.music.source.clip==D.music.ClipFor(1)&&D.music.ClipFor(1)&&D.music.ClipFor(1).name=="Music - Slow tide"&&D.music.Level>Music.Volume*.9f&&PlayerPrefs.GetInt(Music.Key,0)==1,"MUSIC switches back on in a chapter, which plays its own piece (Slow tide on the ocean)");
            foreach(var t in MenuKey(true))yield return t;foreach(var t in Click(ActionKind.Rest))yield return t;yield return .8f;
            Check(D.currentRoom==0,"Menu LOBBY returns to the lobby");
            Check(D.music.source.clip==D.music.theme,"Back in the lobby the theme plays again");
            // Thumb shortcuts. The editor has no hands, so the gestures are handed over directly.
            var thumbs=P.GetComponent<ThumbShortcuts>();
            Check(thumbs&&thumbs.player==P,"The player carries the thumb shortcuts");
            Check(thumbs.Handle(OVRHand.MicrogestureType.SwipeBackward)==ThumbShortcuts.Outcome.Ignored&&!thumbs.Armed,"In the lobby a thumb swipe back has nowhere to go, so it asks nothing");
            D.Travel(3);yield return .1f;
            Check(P.busy&&thumbs.Handle(OVRHand.MicrogestureType.SwipeBackward)==ThumbShortcuts.Outcome.Ignored&&!thumbs.Armed,"Mid-move, thumb gestures wait");
            yield return .9f;
            Check(thumbs.Handle(OVRHand.MicrogestureType.ThumbTap)==ThumbShortcuts.Outcome.Ignored&&D.currentRoom==3,"A thumb tap on its own does nothing");
            Check(thumbs.Handle(OVRHand.MicrogestureType.SwipeBackward)==ThumbShortcuts.Outcome.Asked&&thumbs.Armed&&P.HintVisible&&P.hint.text.Contains("Back to the lobby?")&&P.hint.text.Contains("Tap your thumb"),"A thumb swipe back asks whether to go back to the lobby");
            Check(thumbs.Handle(OVRHand.MicrogestureType.SwipeLeft)==ThumbShortcuts.Outcome.Stayed&&!thumbs.Armed&&P.hint.text.Contains("Staying here")&&thumbs.Handle(OVRHand.MicrogestureType.ThumbTap)==ThumbShortcuts.Outcome.Ignored&&D.currentRoom==3,"Another swipe means stay, and the tap that follows does nothing");
            thumbs.Handle(OVRHand.MicrogestureType.SwipeBackward);thumbs.armedUntil=Time.time-.1f;
            Check(!thumbs.Armed&&thumbs.Handle(OVRHand.MicrogestureType.ThumbTap)==ThumbShortcuts.Outcome.Ignored&&D.currentRoom==3,"The question lapses on its own after "+ThumbShortcuts.Window+" seconds");
            thumbs.Handle(OVRHand.MicrogestureType.SwipeBackward);
            Check(thumbs.Handle(OVRHand.MicrogestureType.ThumbTap)==ThumbShortcuts.Outcome.Went&&!thumbs.Armed,"A thumb tap right after the question says yes");
            yield return .9f;
            Check(D.currentRoom==0&&D.lastRoom==3&&Stored.lastRoom==3,"Yes goes back to the lobby, with the room saved for CONTINUE");
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
            if(SaveBackup.PathOverride!=null){SaveBackup.Delete();SaveBackup.PathOverride=null;}
            if(D)D.saveEnabled=false;TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Seated-session-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Pointer and keyboard input for the comfort menu, turning, walking and resume; partial-progress fixtures for storage; actual scene reload."}.Concat(lines));Debug.Log("Seated and session validation: "+Status);
        }
    }
}
