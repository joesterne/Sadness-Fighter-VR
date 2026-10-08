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
        public Shredder shredder;
        public RageRoom rage;
        public Wardrobe wardrobe;
        public TextMeshPro mirrorLabel;
        public Transform mirrorSpot;
        public AudioClip select, grab, send, place, menuOpen, menuClose, teleport, turn, row, spill, pour, shred, unlock;
        // The six chapters. The lobby is 0 and the rooftop, open from the start, is 5.
        public static readonly int[] Chapters={1,2,3,4,6,7};
        public static bool IsChapter(int room)=>Array.IndexOf(Chapters,room)>=0;
        const float RestForBeanie=20;
        float restTime, lastSay=-10;
        readonly Queue<string> news=new Queue<string>();
        SendTarget[][] targets; Grabbable[][] grabbables;
        // Send targets and movable objects in the current room, for pointing and aim assist.
        public SendTarget[] Targets=>targets!=null&&currentRoom<targets.Length?targets[currentRoom]:System.Array.Empty<SendTarget>();
        public Grabbable[] Grabbables=>grabbables!=null&&currentRoom<grabbables.Length?grabbables[currentRoom]:System.Array.Empty<Grabbable>();
        public int lastRoom;
        bool ready;
        [NonSerialized] public bool saveEnabled=true;
        public int currentRoom;
        public bool gentleAudience;
        public AudioSource audioSource;
        public AudioClip acknowledge, completeSound;
        const string SaveKey="AfterHours.Progress.v1";
        void Awake()
        {
            Instance=this;
            targets=new SendTarget[rooms.Length][];grabbables=new Grabbable[rooms.Length][];
            for(int i=0;i<rooms.Length;i++){targets[i]=rooms[i].root.GetComponentsInChildren<SendTarget>(true);grabbables[i]=rooms[i].root.GetComponentsInChildren<Grabbable>(true);}
            if(wardrobe)wardrobe.Load();
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
            if(intro&&PlayerPrefs.GetInt(IntroGuide.Key,0)==0)intro.Begin();
            else Say("Welcome back. A few minutes is enough. Progress saves after each small step. The menu button (or a left palm pinch) opens seated and comfort options.");
        }
        void Update()
        {
            // Resting on the rooftop for a little while earns the evening beanie.
            if(currentRoom==5&&!player.busy){restTime+=Time.deltaTime;if(restTime>=RestForBeanie)Earn("beanie");}else restTime=0;
            // New pieces are announced one at a time, after whatever the room has just said.
            if(news.Count>0&&!player.busy&&Time.time>lastSay+4){Say(news.Dequeue());PlayUi(unlock,.6f);}
        }
        // Unlocks a wardrobe piece the first time it is earned, and announces it.
        public void Earn(string id)
        {
            if(!wardrobe||!wardrobe.Unlock(id))return;
            news.Enqueue("New to wear: "+Outfits.Get(id).name+". Try it on at the mirror in the lobby.");
        }
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
            StartCoroutine(ChangeRoom(room));
        }
        IEnumerator ChangeRoom(int room)
        {
            player.busy=true;player.ReleaseAll();if(menu)menu.Close();if(intro)intro.Dismiss();if(currentRoom==1)ocean.OnDepart();yield return player.Fade(1,.2f,true);
            rooms[currentRoom].root.SetActive(false);currentRoom=room;rooms[room].root.SetActive(true);
            player.Place(rooms[room].spawn.position,rooms[room].spawn.eulerAngles.y);
            if(room==1)ocean.OnArrive();
            yield return player.Fade(0,.35f,true);player.busy=false;
            string[] messages={"Your progress is saved. Continue when you want to, or stop here for today.","Hold either paddle and pull toward you. Or press ROW. The horizon will wait.","These feelings can travel with you. Pinch a box to pick it up, point at the truck and let go. Five boxes.","Pick up the cup, point at the spout and let go. Then try BREW. You can ask for help at any point. The menu can hide the watching crowd.","Sort six notes into FACT, FEAR, and STILL TRUE: pick one up, point at its tray and let go. A thought does not have to become a verdict.","Stay as long as you like. Your next chapter does not need a title yet.","Pick up an old résumé, point at the shredder and let go. When they are gone, keep one true line.","Point at the bat and pinch to pick it up. It stays in your hand. Swing at the old computer: short swings count, and nothing here can be hurt."};
            Say(messages[room]);
        }
        public void Say(string text){if(player)player.ShowHint(text,9);lastSay=Time.time;}
        public void AcceptItem(int room,string label,string message=null)
        {
            var r=rooms[room];r.accepted++;Play(acknowledge);RefreshProgress();
            Say(message??(room==2?label+" has a place. You do not have to carry everything at once.":"Filed: "+label));
            SmallStep();
            // The shredder room finishes when a true line is chosen, not when the last page goes.
            if(room!=Shredder.Room&&r.accepted>=r.total)CompleteRoom(room);
            SaveCheckpoint();
        }
        public void CompleteRoom(int room)
        {
            bool first=!rooms[room].complete;rooms[room].complete=true;
            int mask=0;foreach(int i in Chapters)if(rooms[i].complete)mask|=1<<i;
            PlayerPrefs.SetInt(SaveKey,mask);PlayerPrefs.Save();RefreshProgress();if(first)Play(completeSound);
            string[] lines={"","You crossed a feeling. It was never the whole ocean.","Processed does not mean erased. You made room to carry on.","A broken machine is not a broken person. Help belongs here.","Losing a role did not erase what is still true about you.","","The old pages are gone. What you bring with you is still yours.","You let some of it out. What you lost mattered. So do you."};Say(lines[room]+"  The menu takes you back to the lobby whenever you are ready.");
            Earn(Outfits.ForChapter(room));if(CompletedCount==Chapters.Length)Earn("jacket");
            SaveCheckpoint();
        }
        public int CompletedCount { get {int n=0;foreach(int i in Chapters)if(rooms[i].complete)n++;return n;} }
        public void RefreshProgress()
        {
            if(resumeLabel)resumeLabel.text=lastRoom>0?"CONTINUE / "+rooms[lastRoom].title.ToUpperInvariant():"BEGIN / OCEAN OF SHAME";
            if(lobbyProgress)lobbyProgress.text=CompletedCount+" / "+Chapters.Length+"   ROOMS EXPLORED\n<color=#ABC4C5>Your pace. No score. No deadline.</color>";
            foreach(int i in Chapters)if(rooms[i].progress)rooms[i].progress.text=rooms[i].complete?"A LITTLE MORE ROOM TO BREATHE":(i==2||i==4)?rooms[i].accepted+" / "+rooms[i].total+"   GIVEN A PLACE":i==Shredder.Room?rooms[i].accepted+" / "+rooms[i].total+"   LET GO":i==RageRoom.Room?rooms[i].accepted+" / "+rooms[i].total+"   BROKEN":"TAKE YOUR TIME";
            if(ending)ending.text=CompletedCount==Chapters.Length?"You are more\nthan your job.":"You can rest\nbefore you are ready.";
        }
        public void RestartCurrentRoom()
        {
            if(!IsChapter(currentRoom))return;
            player.ReleaseAll();foreach(var g in rooms[currentRoom].root.GetComponentsInChildren<Grabbable>(true))g.ResetObject();
            foreach(var z in rooms[currentRoom].root.GetComponentsInChildren<DropZone>(true))z.ResetZone();
            rooms[currentRoom].accepted=0;if(currentRoom==1)ocean.ResetRoom();if(currentRoom==3)kitchen.ResetRoom();if(currentRoom==Shredder.Room&&shredder)shredder.ResetRoom();if(currentRoom==RageRoom.Room&&rage)rage.ResetComputer();RefreshProgress();SaveCheckpoint();Say("This room is ready for another try. Your completed chapters stay saved.");
        }
        public void RefreshComfort(){if(menu)menu.Refresh();}
        public void SaveCheckpoint(){if(!ready||!saveEnabled)return;CheckpointStore.Save(this);RefreshProgress();}
        void OnApplicationPause(bool paused){if(paused)SaveCheckpoint();}
        void OnApplicationFocus(bool focused){if(!focused)SaveCheckpoint();}
        void OnApplicationQuit(){SaveCheckpoint();}
        void Play(AudioClip clip){if(audioSource&&clip)audioSource.PlayOneShot(clip,.5f);}
    }
}
