using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    [Serializable] public class StreakState
    {
        public int version=1,lastDay=-1,current,best,days;
        public List<string> badges=new List<string>();
    }

    // Coming back. A day counts when the player does something in a room, not when they only open the game.
    // Nothing is ever taken away: one day off keeps a run going, a longer gap quietly starts a new one, and best, days and badges stay.
    public static class Streak
    {
        public const string Key="AfterHours.Streak.v1";
        // Days that can be missed without ending a run.
        public const int RestDays=1;
        // A test can set the date.
        public static Func<DateTime> Clock=()=>DateTime.Now;
        public static int Today=>(int)(Clock().Date-new DateTime(2026,1,1)).TotalDays;
        // A clock moved back a day, as when crossing time zones, ends nothing.
        public static bool Running(StreakState s,int today)=>s.lastDay>=0&&today-s.lastDay>=-1&&today-s.lastDay<=1+RestDays;
        public static bool Counted(StreakState s,int today)=>s.lastDay>=0&&(today==s.lastDay||today==s.lastDay-1);
        // True the first time a day is counted.
        public static bool Visit(StreakState s,int today)
        {
            if(Counted(s,today))return false;
            s.current=Running(s,today)?s.current+1:1;s.days++;s.best=Mathf.Max(s.best,s.current);s.lastDay=today;return true;
        }
    }

    // The board in the lobby: the run so far, and twelve badges. Ids are saved on the player's headset, so an id must never be renamed.
    public class BadgeBoard : MonoBehaviour
    {
        public struct Badge { public string id,name,token,reward; public int run; public Func<GameDirector,StreakState,bool> earned; }
        static Badge Run(string id,string name,int days,string reward=null)=>new Badge{id=id,name=name,token=days.ToString(),reward=reward,run=days,earned=(d,s)=>s.best>=days};
        static Badge Done(string id,string name,string token,Func<GameDirector,StreakState,bool> earned)=>new Badge{id=id,name=name,token=token,earned=earned};
        public static readonly Badge[] All=
        {
            Run("day-1","Showed up",1),
            Run("run-3","3 in a row",3,"return-pin"),
            Run("run-7","1 week",7,"week-pin"),
            Run("run-14","2 weeks",14),
            Run("run-30","1 month",30,"month-pin"),
            Done("days-10","10 days","10",(d,s)=>s.days>=10),
            Done("room-1","First room","1/7",(d,s)=>d.CompletedCount>=1),
            Done("room-4","Halfway","4/7",(d,s)=>d.CompletedCount>=4),
            Done("room-7","All seven","7/7",(d,s)=>d.CompletedCount>=GameDirector.Chapters.Length),
            Done("help","Asked for help","",(d,s)=>d.wardrobe&&d.wardrobe.Has("scarf")),
            Done("rest","Rested","",(d,s)=>d.wardrobe&&d.wardrobe.Has("beanie")),
            Done("true","Still true","",(d,s)=>d.shredder&&!string.IsNullOrEmpty(d.shredder.line)),
        };
        public GameDirector director;
        public Renderer[] medals;
        public TextMeshPro[] tokens;
        public TextMeshPro status;
        public Material earned,locked;
        public Color tokenEarned,tokenLocked;
        public StreakState State { get; private set; }=new StreakState();
        public bool Has(string id)=>State.badges.Contains(id);

        public void Load()
        {
            StreakState loaded=null;
            if(PlayerPrefs.HasKey(Streak.Key))try{loaded=JsonUtility.FromJson<StreakState>(PlayerPrefs.GetString(Streak.Key));}catch(ArgumentException){}
            State=loaded!=null&&loaded.version==1?loaded:new StreakState();
            State.badges=(State.badges??new List<string>()).Where(id=>All.Any(b=>b.id==id)).Distinct().ToList();
            Refresh();
        }
        void Save(){PlayerPrefs.SetString(Streak.Key,JsonUtility.ToJson(State));PlayerPrefs.Save();}
        // Counts the day, the first time it is called on that day.
        public bool Count(int today)
        {
            if(!Streak.Visit(State,today))return false;
            Save();if(State.current>=2&&director)director.Announce("Day "+State.current+" in a row. It all counts.");
            Check(true);Refresh();return true;
        }
        // Awards whatever has been earned. Progress from before a badge existed earns it quietly.
        public void Check(bool announce)
        {
            bool any=false;
            foreach(var badge in All)
            {
                if(Has(badge.id)||!badge.earned(director,State))continue;
                State.badges.Add(badge.id);any=true;
                if(announce)director.Announce("New badge: "+badge.name+". It is on the board in the lobby.");
                if(badge.reward!=null)director.Earn(badge.reward);
            }
            if(any){Save();Refresh();}
        }
        // What the board says. A run that has ended is simply not mentioned.
        public string Words(int today)
        {
            if(State.days==0)return "YOUR DAYS HERE START WITH ONE SMALL STEP\n<color=#ABC4C5>Any room. Any size. Then come back when you like.</color>";
            bool running=Streak.Running(State,today);
            string top=(running&&State.current>=2?State.current+" DAYS RUNNING   /   ":"")+"BEST "+State.best+"   /   "+State.days+(State.days==1?" DAY":" DAYS")+" IN ALL";
            string next=Streak.Counted(State,today)?"Today counts.":"One small step counts today.";
            int run=running?State.current:0;
            foreach(var badge in All)if(badge.run>0&&!Has(badge.id)){int left=badge.run-run;next+="  "+badge.name+" is "+left+(left==1?" day away.":" days away.");break;}
            return top+"\n<color=#ABC4C5>"+next+"</color>";
        }
        public void Refresh()
        {
            if(status)status.text=Words(Streak.Today);
            if(medals==null)return;
            for(int i=0;i<medals.Length&&i<All.Length;i++)
            {
                bool has=Has(All[i].id);
                if(medals[i])medals[i].sharedMaterial=has?earned:locked;
                if(tokens!=null&&i<tokens.Length&&tokens[i])tokens[i].color=has?tokenEarned:tokenLocked;
            }
        }
    }
}
