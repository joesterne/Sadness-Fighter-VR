using System;
using System.Collections;
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
        public AudioClip select, grab, send, place, menuOpen, menuClose, teleport, turn, row, spill, pour;
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
        }
        public void PlayUi(AudioClip clip,float volume){if(audioSource&&clip)audioSource.PlayOneShot(clip,volume);}
        public void PlayAt(AudioClip clip,Vector3 position,float volume){if(clip)AudioSource.PlayClipAtPoint(clip,position,volume);}
        void Start()
        {
            int mask=PlayerPrefs.GetInt(SaveKey,0);for(int i=1;i<=4;i++)rooms[i].complete=(mask&(1<<i))!=0;
            for(int i=0;i<rooms.Length;i++)rooms[i].root.SetActive(i==0);
            currentRoom=0;player.Place(rooms[0].spawn.position,rooms[0].spawn.eulerAngles.y);CheckpointStore.Load(this);ready=true;RefreshProgress();RefreshComfort();
            if(intro&&PlayerPrefs.GetInt(IntroGuide.Key,0)==0)intro.Begin();
            else Say("Welcome back. A few minutes is enough. Progress saves after each small step. The menu button (or a left palm pinch) opens seated and comfort options.");
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
            string[] messages={"Your progress is saved. Continue when you want to, or stop here for today.","Hold either paddle and pull toward you. Or press ROW. The horizon will wait.","These feelings can travel with you. Pinch a box to pick it up, point at the truck and let go. Five boxes.","Pick up the cup, point at the spout and let go. Then try BREW. You can ask for help at any point. The menu can hide the watching crowd.","Sort six notes into FACT, FEAR, and STILL TRUE: pick one up, point at its tray and let go. A thought does not have to become a verdict.","Stay as long as you like. Your next chapter does not need a title yet."};
            Say(messages[room]);
        }
        public void Say(string text){if(player)player.ShowHint(text,9);}
        public void AcceptItem(int room,string label)
        {
            var r=rooms[room];r.accepted++;Play(acknowledge);RefreshProgress();
            Say(room==2?label+" has a place. You do not have to carry everything at once.":"Filed: "+label);
            if(r.accepted>=r.total)CompleteRoom(room);
            SaveCheckpoint();
        }
        public void CompleteRoom(int room)
        {
            bool first=!rooms[room].complete;rooms[room].complete=true;
            int mask=0;for(int i=1;i<=4;i++)if(rooms[i].complete)mask|=1<<i;
            PlayerPrefs.SetInt(SaveKey,mask);PlayerPrefs.Save();RefreshProgress();if(first)Play(completeSound);
            string[] lines={"","You crossed a feeling. It was never the whole ocean.","Processed does not mean erased. You made room to carry on.","A broken machine is not a broken person. Help belongs here.","Losing a role did not erase what is still true about you."};Say(lines[room]+"  The menu takes you back to the lobby whenever you are ready.");
            SaveCheckpoint();
        }
        public int CompletedCount { get {int n=0;for(int i=1;i<=4;i++)if(rooms[i].complete)n++;return n;} }
        public void RefreshProgress()
        {
            if(resumeLabel)resumeLabel.text=lastRoom>0?"CONTINUE / "+rooms[lastRoom].title.ToUpperInvariant():"BEGIN / OCEAN OF SHAME";
            if(lobbyProgress)lobbyProgress.text=CompletedCount+" / 4   ROOMS EXPLORED\n<color=#ABC4C5>Your pace. No score. No deadline.</color>";
            for(int i=1;i<=4;i++)if(rooms[i].progress)rooms[i].progress.text=rooms[i].complete?"A LITTLE MORE ROOM TO BREATHE":(i==2||i==4)?rooms[i].accepted+" / "+rooms[i].total+"   GIVEN A PLACE":"TAKE YOUR TIME";
            if(ending)ending.text=CompletedCount==4?"You are more\nthan your job.":"You can rest\nbefore you are ready.";
        }
        public void RestartCurrentRoom()
        {
            if(currentRoom<1||currentRoom>4)return;
            player.ReleaseAll();foreach(var g in rooms[currentRoom].root.GetComponentsInChildren<Grabbable>(true))g.ResetObject();
            foreach(var z in rooms[currentRoom].root.GetComponentsInChildren<DropZone>(true))z.ResetZone();
            rooms[currentRoom].accepted=0;if(currentRoom==1)ocean.ResetRoom();if(currentRoom==3)kitchen.ResetRoom();RefreshProgress();SaveCheckpoint();Say("This room is ready for another try. Your completed chapters stay saved.");
        }
        public void RefreshComfort(){if(menu)menu.Refresh();}
        public void SaveCheckpoint(){if(!ready||!saveEnabled)return;CheckpointStore.Save(this);RefreshProgress();}
        void OnApplicationPause(bool paused){if(paused)SaveCheckpoint();}
        void OnApplicationFocus(bool focused){if(!focused)SaveCheckpoint();}
        void OnApplicationQuit(){SaveCheckpoint();}
        void Play(AudioClip clip){if(audioSource&&clip)audioSource.PlayOneShot(clip,.5f);}
    }
}
