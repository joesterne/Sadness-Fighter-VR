using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AfterHours
{
    [Serializable] public class MindRoom
    {
        public string title;
        public GameObject root;
        public Transform spawn;
        public TextMeshPro progress;
        public bool complete;
        public int accepted;
        public int total;
    }
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }
        public VRPlayer player;
        public MindRoom[] rooms;
        public OceanRoom ocean;
        public KitchenRoom kitchen;
        public TextMeshPro lobbyProgress, ending;
        public TextMeshPro resumeLabel;
        public ComfortMenu menu;
        public IntroGuide intro;
        public Shredder shredder; public BoxBlast boxBlast;
        public RageRoom rage;
        public AmbushRoom ambush;
        public RooftopRoom rooftop;
        public ArchiveRoom archive;
        public Wardrobe wardrobe;
        // Outfit add-ons from the store. Started at launch, so the store's ownership check runs in the first seconds.
        [NonSerialized] public AddOnShop shop;
        public TextMeshPro mirrorLabel;
        public Transform mirrorSpot;
        public AudioClip select, grab, send, place, menuOpen, menuClose, teleport, turn, row, spill, pour, shred, unlock;
        // The seven chapters. The lobby is 0 and the rooftop, open from the start, is 5.
        public static readonly int[] Chapters={1,2,3,4,6,7,8};
        public static bool IsChapter(int room)=>Array.IndexOf(Chapters,room)>=0;
        const float RestForBeanie=20;
        float restTime, lastSay=-10;
        readonly Queue<string> news=new Queue<string>();
        SendTarget[][] targets; Grabbable[][] grabbables; Guidance[][] guidance;
        // Send targets and movable objects in the current room, for pointing and aim assist.
        public SendTarget[] Targets=>targets!=null&&currentRoom<targets.Length?targets[currentRoom]:System.Array.Empty<SendTarget>();
        public Grabbable[] Grabbables=>grabbables!=null&&currentRoom<grabbables.Length?grabbables[currentRoom]:System.Array.Empty<Grabbable>();
        // Every movable object in one room, as found when the scene woke. Saving walks these instead of searching the scene.
        public Grabbable[] GrabbablesOf(int room)=>grabbables!=null&&room>=0&&room<grabbables.Length?grabbables[room]:System.Array.Empty<Grabbable>();
        public int lastRoom;
        bool ready;
        [NonSerialized] public bool saveEnabled=true;
        public int currentRoom;
        public bool gentleAudience;
        // The ambush's two options: smaller, upright clowns from the start, and whether the room opens in the dark.
        public bool gentleAmbush;
        public bool darkOpening=true;
        public AudioSource audioSource;
        public AudioClip acknowledge, completeSound;
        const string SaveKey=CheckpointStore.ProgressKey;
        // Saving happens by itself: as the player leaves a room, as they arrive in the next one, and now and then in between.
        // The quiet saves show nothing. A room change shows a brief "Saved".
        public const float AutosaveSeconds=45;
        [NonSerialized] public float autosaveClock;
        void Awake()
        {
            Instance=this;
            targets=new SendTarget[rooms.Length][];grabbables=new Grabbable[rooms.Length][];guidance=new Guidance[rooms.Length][];
            for(int i=0;i<rooms.Length;i++){targets[i]=rooms[i].root.GetComponentsInChildren<SendTarget>(true);grabbables[i]=rooms[i].root.GetComponentsInChildren<Grabbable>(true);guidance[i]=rooms[i].root.GetComponentsInChildren<Guidance>(true);}
            if(wardrobe)wardrobe.Load();
            shop=new AddOnShop(AddOnShop.ForThisDevice(),this);shop.Changed+=()=>{if(wardrobe)wardrobe.Refresh();};_=shop.Begin();
        }
        public void PlayUi(AudioClip clip,float volume){if(audioSource&&clip)audioSource.PlayOneShot(clip,volume);}
        public void PlayAt(AudioClip clip,Vector3 position,float volume){if(clip)AudioSource.PlayClipAtPoint(clip,position,volume);}
        void Start()
        {
            int mask=PlayerPrefs.GetInt(SaveKey,0);foreach(int i in Chapters)rooms[i].complete=(mask&(1<<i))!=0;
            for(int i=0;i<rooms.Length;i++)rooms[i].root.SetActive(i==0);
            currentRoom=0;player.Place(rooms[0].spawn.position,rooms[0].spawn.eulerAngles.y);CheckpointStore.Load(this);ready=true;RefreshProgress();RefreshComfort();
            // Progress from before the wardrobe existed still earns its pieces.
            foreach(int i in Chapters)if(rooms[i].complete)Earn(Outfits.ForChapter(i));
            if(CompletedCount==Chapters.Length)Earn("jacket");if(kitchen.helped)Earn("scarf");
            ApplyClean(player.cleanMode);
            if(intro&&PlayerPrefs.GetInt(IntroGuide.Key,0)==0&&!player.cleanMode)intro.Begin();
            else Say("Welcome back. A few minutes is enough. Progress saves after each small step. "+Wording.Pick("A left palm pinch","The menu button")+" opens seated and comfort options.");
        }
        void Update()
        {
            // Resting on the rooftop for a little while earns the evening beanie.
            if(currentRoom==5&&!player.busy){restTime+=Time.deltaTime;if(restTime>=RestForBeanie)Earn("beanie");}else restTime=0;
            // The quiet timer. Nothing is written unless something changed since the last save, so it never causes a hitch.
            if(ready&&saveEnabled){autosaveClock+=Time.deltaTime;if(autosaveClock>=AutosaveSeconds&&!player.busy)Autosave(false);}
            // New pieces are announced one at a time, after whatever the room has just said.
            if(news.Count>0&&!player.busy&&Time.time>lastSay+4){Say(news.Dequeue());PlayUi(unlock,.6f);}
        }
        // Unlocks a wardrobe piece the first time it is earned, and announces it.
        public void Earn(string id)
        {
            if(!wardrobe||!wardrobe.Unlock(id))return;
            Announce(id);
        }
        // BUY at the mirror: the store's checkout for the add-on being tried on. Once bought it is put on straight away.
        bool buying;
        public async void BuyTryingOn()
        {
            if(buying||!wardrobe||shop==null||wardrobe.TryingOn==null)return;
            var id=wardrobe.TryingOn;var name=Outfits.Get(id).name;buying=true;PurchaseResult result;
            try{result=await shop.Buy(id);}finally{buying=false;}
            if(!this||!wardrobe)return;
            switch(result)
            {
                case PurchaseResult.Bought:case PurchaseResult.AlreadyOwned:wardrobe.Wear(id);Say(name+" is yours. It is on now, and stays in your wardrobe.");PlayUi(unlock,.6f);break;
                case PurchaseResult.Cancelled:Say("Nothing was bought. You can keep trying it on.");break;
                default:Say("The store could not be reached just now. Nothing was charged.");break;
            }
        }
        public void Announce(string id){news.Enqueue("New to wear: "+Outfits.Get(id).name+". Try it on at the mirror in the lobby.");}
        // The first small step anywhere earns a pin. Every step still counts the same.
        public void SmallStep(){Earn("step-pin");}
        public void GoToMirror()
        {
            if(player.busy||currentRoom!=0||!mirrorSpot)return;
            player.TeleportTo(mirrorSpot.position,mirrorSpot.eulerAngles.y);
        }
        public void RefreshMirrorButton()
        {
            if(!mirrorLabel||!wardrobe)return;int fresh=wardrobe.State.fresh.Count;
            mirrorLabel.text=fresh>0?"MIRROR  /  "+fresh+" NEW TO WEAR":"MIRROR  /  YOUR WARDROBE";
        }
        public void Travel(int room)
        {
            if(room<0||room>=rooms.Length||player.busy||room==currentRoom)return;
            if(room>0)lastRoom=room;SaveCheckpoint();
            if(currentRoom==0&&wardrobe)wardrobe.StopTrying();
            StartCoroutine(ChangeRoom(room));
        }
        IEnumerator ChangeRoom(int room)
        {
            player.busy=true;player.ReleaseAll();if(menu)menu.Close();if(intro)intro.Dismiss();if(currentRoom==1)ocean.OnDepart();yield return player.Fade(1,.2f,true);
            int from=currentRoom;if(from==0&&wardrobe&&wardrobe.avatar)wardrobe.avatar.SetXray(false);rooms[currentRoom].root.SetActive(false);
            // The sea's night sky and light belong to the ocean room alone. They go before the next room switches on, so a room that
            // takes the scene's light as it opens (the rooftop) takes the day's, not the sea's.
            if(from==1&&ocean)ocean.SetEnvironment(false);
            currentRoom=room;rooms[room].root.SetActive(true);
            player.Place(rooms[room].spawn.position,rooms[room].spawn.eulerAngles.y);
            if(room==1)ocean.OnArrive();
            if(room==AmbushRoom.Room&&ambush)ambush.OnArrive();
            yield return player.Fade(0,.35f,true);player.busy=false;
            Autosave(true);
            string[] messages={"Your progress is saved. Continue when you want to, or stop here for today.","Hold either oar and push it forward through the water, then draw it back. Or press ROW. The horizon will wait.","These feelings can travel with you. "+Wording.Pick("Pinch","Squeeze the trigger on")+" a box to pick it up. Point at the truck and let go, or throw it. Five boxes.","Pick up a cup, point at the spout and let go. Then try BREW. You can ask for help at any point. The menu can hide the watching crowd.","Sort six notes into FACT, FEAR, and STILL TRUE: pick one up, point at its tray and let go. A thought does not have to become a verdict.","Stay as long as you like. Your next chapter does not need a title yet.","Pick up an old résumé, point at the shredder and let go. When they are gone, keep one true line.","Point at the bat and "+Wording.Pick("pinch","squeeze the trigger")+" to pick it up. It stays in your hand. Swing at the old computer: short swings count, and nothing here can be hurt.",""};
            if(room==AmbushRoom.Room&&ambush)ambush.Announce();else Say(messages[room]);
        }
        public void Say(string text){if(player)player.ShowHint(text,9);lastSay=Time.time;}
        public void AcceptItem(int room,string label,string message=null)
        {
            var r=rooms[room];r.accepted++;Play(acknowledge);RefreshProgress();
            Say(message??(room==2?label+" has a place. You do not have to carry everything at once.":"Filed: "+label));
            SmallStep();
            // The shredder room finishes when a true line is chosen, not when the last page goes.
            if(room!=Shredder.Room&&room!=AmbushRoom.Room&&r.accepted>=r.total)CompleteRoom(room);
            SaveCheckpoint();
        }
        public void CompleteRoom(int room)
        {
            bool first=!rooms[room].complete;rooms[room].complete=true;
            int mask=0;foreach(int i in Chapters)if(rooms[i].complete)mask|=1<<i;
            PlayerPrefs.SetInt(SaveKey,mask);PlayerPrefs.Save();RefreshProgress();if(first)Play(completeSound);
            string[] lines={"","You crossed a feeling. It was never the whole ocean.","Processed does not mean erased. You made room to carry on.","A broken machine is not a broken person. Help belongs here.","Losing a role did not erase what is still true about you.","","The old pages are gone. What you bring with you is still yours.","You let some of it out. What you lost mattered. So do you.","You named it, and then you stood up. Leaving a room like that is allowed. So is coming back."};Say(lines[room]+"  The menu takes you back to the lobby whenever you are ready.");
            Earn(Outfits.ForChapter(room));if(CompletedCount==Chapters.Length)Earn("jacket");
            SaveCheckpoint();
        }
        public int CompletedCount { get {int n=0;foreach(int i in Chapters)if(rooms[i].complete)n++;return n;} }
        public void RefreshProgress()
        {
            if(resumeLabel)resumeLabel.text=lastRoom>0?"CONTINUE / "+rooms[lastRoom].title.ToUpperInvariant():"BEGIN / OCEAN OF SHAME";
            if(lobbyProgress)lobbyProgress.text=CompletedCount+" / "+Chapters.Length+"   ROOMS EXPLORED\n<color=#ABC4C5>Your pace. No score. No deadline.</color>";
            foreach(int i in Chapters)if(rooms[i].progress)rooms[i].progress.text=rooms[i].complete?"A LITTLE MORE ROOM TO BREATHE":(i==2||i==4)?rooms[i].accepted+" / "+rooms[i].total+"   GIVEN A PLACE":i==Shredder.Room?rooms[i].accepted+" / "+rooms[i].total+"   LET GO":i==RageRoom.Room?rooms[i].accepted+" / "+rooms[i].total+"   BROKEN":i==AmbushRoom.Room?rooms[i].accepted+" / "+rooms[i].total+"   NAMED":"TAKE YOUR TIME";
            if(ending)ending.text=CompletedCount==Chapters.Length?"You are more\nthan your job.":"You can rest\nbefore you are ready.";
        }
        public void RestartCurrentRoom()
        {
            if(!IsChapter(currentRoom))return;
            player.ReleaseAll();foreach(var g in rooms[currentRoom].root.GetComponentsInChildren<Grabbable>(true))g.ResetObject();
            foreach(var z in rooms[currentRoom].root.GetComponentsInChildren<DropZone>(true))z.ResetZone();
            rooms[currentRoom].accepted=0;if(currentRoom==1)ocean.ResetRoom();if(currentRoom==3)kitchen.ResetRoom();if(currentRoom==Shredder.Room&&shredder)shredder.ResetRoom();if(currentRoom==BoxBlast.Room&&boxBlast)boxBlast.ResetRoom();if(currentRoom==RageRoom.Room&&rage)rage.ResetComputer();if(currentRoom==AmbushRoom.Room&&ambush)ambush.ResetRoom();RefreshProgress();SaveCheckpoint();Say("This room is ready for another try. Your completed chapters stay saved.");
        }
        public void RefreshComfort(){if(menu)menu.Refresh();}
        // Clean mode: every sign that only guides or reassures goes, along with spoken lines and the first-visit guide.
        public void ApplyClean(bool clean)
        {
            if(guidance!=null)foreach(var room in guidance)foreach(var g in room)if(g)g.Apply(clean);
            if(clean){if(player)player.HideHint();if(intro&&intro.IsShowing)intro.Hide();news.Clear();}
            RefreshComfort();
        }
        // The menu button's second job: clear every message at once, including announcements still waiting their turn.
        public void ClearMessages(){news.Clear();if(player)player.HideHint();}
        public void SaveCheckpoint(){if(!ready||!saveEnabled)return;autosaveClock=0;CheckpointStore.Save(this);RefreshProgress();}
        // A snapshot with nothing else to refresh. With cue set it also shows the player a faint "Saved".
        public void Autosave(bool cue){if(!ready||!saveEnabled)return;autosaveClock=0;CheckpointStore.Save(this);if(cue&&player)player.ShowSaved();}
        void OnApplicationPause(bool paused){if(paused)SaveCheckpoint();}
        void OnApplicationFocus(bool focused){if(!focused)SaveCheckpoint();}
        void OnApplicationQuit(){SaveCheckpoint();}
        void Play(AudioClip clip){if(audioSource&&clip)audioSource.PlayOneShot(clip,.5f);}
    }
}
