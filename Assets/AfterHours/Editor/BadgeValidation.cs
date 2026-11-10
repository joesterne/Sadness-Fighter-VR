using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Coming back: which days count, that nothing is ever taken away, what each badge asks for, and everything surviving a reload.
    public static class BadgeValidation
    {
        static IEnumerator<float> run;static double next,deadline;
        static readonly List<string> lines=new List<string>();
        static readonly string[] intKeys={"AfterHours.Progress.v1",IntroGuide.Key,"AfterHours.Clean"};
        static readonly string[] stringKeys={CheckpointStore.Key,Wardrobe.Key,Streak.Key};
        static bool[] intExisted,stringExisted;static int[] intValues;static string[] stringValues;
        static DateTime date=new DateTime(2026,11,5);
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static BadgeBoard B=>D.badges;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;
            intExisted=intKeys.Select(PlayerPrefs.HasKey).ToArray();intValues=intKeys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            stringExisted=stringKeys.Select(PlayerPrefs.HasKey).ToArray();stringValues=stringKeys.Select(k=>PlayerPrefs.GetString(k)).ToArray();
            foreach(var key in intKeys.Concat(stringKeys))PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            Streak.Clock=()=>date;
            lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        // A step taken on a later day.
        static void StepAfter(int days){date=date.AddDays(days);D.SmallStep();}
        static void Pure()
        {
            var s=new StreakState();
            Check(Streak.Visit(s,100)&&s.current==1&&s.best==1&&s.days==1,"The first day starts a run of one");
            Check(!Streak.Visit(s,100)&&s.current==1&&s.days==1,"A second step on the same day counts nothing more");
            Check(Streak.Visit(s,101)&&s.current==2&&s.days==2,"The next day makes two");
            Check(Streak.Visit(s,103)&&s.current==3&&s.days==3,"One day off keeps the run going");
            Check(Streak.Running(s,105)&&!Streak.Running(s,106),"The run is still open after one missed day, and not after two");
            Check(Streak.Visit(s,106)&&s.current==1&&s.best==3&&s.days==4,"A longer gap starts a new run, and keeps the best and the total");
            Check(!Streak.Visit(s,105)&&s.current==1&&s.days==4&&s.lastDay==106,"A clock moved back one day, as when crossing time zones, ends nothing and counts nothing");
            Check(Streak.Visit(s,50)&&s.current==1&&s.best==3&&s.lastDay==50&&Streak.Visit(s,51)&&s.current==2,"A clock put right after being far ahead still counts days");
            date=new DateTime(2026,12,31,23,59,0);int late=Streak.Today;date=new DateTime(2027,1,1,0,1,0);
            Check(Streak.Today==late+1,"A day turns over at midnight, across a new year");
            date=new DateTime(2026,11,5);
        }
        static IEnumerator<float> Checks()
        {
            Pure();
            SceneManager.LoadScene("AfterHours");yield return 1f;D.saveEnabled=true;D.intro.Hide();
            Check(B&&BadgeBoard.All.Length==12&&BadgeBoard.All.Select(b=>b.id).Distinct().Count()==12&&B.medals.Length==12,"The board has twelve badges, each with its own medal");
            Check(B.medals.All(m=>m&&m.sharedMaterial==B.locked)&&B.State.days==0,"A new player has none, and opening the game counts no day");
            Check(B.Words(Streak.Today).Contains("ONE SMALL STEP"),"The board says how to begin");
            var plate=B.transform.Find("Badge plate");
            Check(plate&&plate.GetComponentInChildren<Renderer>().bounds.min.y>2.45f,"The board hangs above the lines of sight to the doors below it");

            D.SmallStep();
            Check(B.State.days==1&&B.Has("day-1")&&B.medals[0].sharedMaterial==B.earned,"The first small step counts the day, earns Showed up and lights its medal");
            D.SmallStep();
            Check(B.State.days==1,"More steps that day change nothing");
            Check(B.Words(Streak.Today).Contains("Today counts."),"The board says today counts");
            D.ClearMessages();

            StepAfter(1);
            Check(B.State.current==2&&!B.Has("run-3"),"Two days is not yet three");
            date=date.AddDays(1);
            Check(B.Words(Streak.Today).Contains("One small step counts today")&&B.Words(Streak.Today).Contains("3 in a row is 1 day away"),"The board says what today would bring");
            D.SmallStep();
            Check(B.Has("run-3")&&D.wardrobe.Has("return-pin"),"Three days earns 3 in a row and the Came back pin");
            D.ClearMessages();

            StepAfter(2);
            Check(B.State.current==4,"One day off keeps the run going in the real game");
            date=date.AddDays(4);B.Refresh();
            string after=B.Words(Streak.Today);
            Check(!after.Contains("RUNNING")&&new[]{"lost","broke","missed","ended","sorry"}.All(w=>!after.ToLowerInvariant().Contains(w)),"After a long gap the board does not mention the run that ended: "+after.Replace("\n"," / "));
            Check(B.Has("run-3")&&D.wardrobe.Has("return-pin")&&B.State.best==4,"After a long gap every badge, the pin and the best run are still there");
            D.SmallStep();
            Check(B.State.current==1&&B.State.days==5&&B.State.best==4,"Coming back starts a new run and adds to the total");
            D.ClearMessages();

            for(int i=0;i<6;i++)StepAfter(1);
            Check(B.Has("run-7")&&D.wardrobe.Has("week-pin"),"A week earns 1 week and the Seven days pin");
            Check(B.Has("days-10")==(B.State.days>=10),"10 days follows the total");
            for(int i=0;i<7;i++)StepAfter(1);
            Check(B.Has("days-10"),"Ten days in all earns 10 days, whichever days they were");
            Check(B.Has("run-14")&&!B.Has("run-30"),"Two weeks earns 2 weeks, and not 1 month");
            for(int i=0;i<16;i++)StepAfter(1);
            Check(B.Has("run-30")&&D.wardrobe.Has("month-pin"),"A month earns 1 month and its pin");
            D.ClearMessages();

            Check(!B.Has("room-1"),"No room is finished yet");
            D.CompleteRoom(1);
            Check(B.Has("room-1")&&!B.Has("room-4"),"The first chapter earns First room");
            D.CompleteRoom(2);D.CompleteRoom(3);D.CompleteRoom(4);
            Check(B.Has("room-4")&&!B.Has("room-7"),"Four chapters earns Halfway");
            foreach(int room in GameDirector.Chapters)D.CompleteRoom(room);
            Check(B.Has("room-7"),"All seven chapters earns All seven");
            D.Earn("scarf");D.Earn("beanie");
            Check(B.Has("help")&&B.Has("rest"),"Asking for help and resting on the rooftop earn their badges");
            D.shredder.line="I keep going";D.CompleteRoom(6);
            Check(B.Has("true"),"Keeping a true line earns Still true");
            Check(B.medals.All(m=>m.sharedMaterial==B.earned),"All twelve medals are lit");
            D.ClearMessages();

            foreach(var id in new[]{"return-pin","week-pin","month-pin"})
            {
                D.wardrobe.State.Equip(OutfitSlot.Pin,id);D.wardrobe.Refresh();
                Check(D.wardrobe.avatar.Wearing(id),"The reflection can wear the "+Outfits.Get(id).name);
            }
            int firstEarnable=Outfits.Earnable;
            Check(firstEarnable==15,"The wardrobe has fifteen pieces to earn");

            D.ApplyClean(true);
            Check(!B.gameObject.activeSelf,"Clean mode takes the board away");
            D.ApplyClean(false);
            Check(B.gameObject.activeSelf,"Turning clean mode off brings it back");

            var state=JsonUtility.ToJson(B.State);int days=B.State.days,best=B.State.best;
            string file=Path.Combine(Path.GetTempPath(),"AfterHours-badge-test.json");
            SaveBackup.PathOverride=file;SaveBackup.Delete();
            Check(SaveBackup.Write()&&File.ReadAllText(file).Contains(Streak.Key),"The backup copy of the save carries the streak");
            SaveBackup.Delete();SaveBackup.PathOverride=null;
            D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return 1f;D.saveEnabled=false;
            Check(B.State.days==days&&B.State.best==best&&B.State.badges.Count==12&&B.medals.All(m=>m.sharedMaterial==B.earned),"After a reload: the days, the best run and every badge");

            // Progress from before the badges existed earns them, with no day counted.
            PlayerPrefs.DeleteKey(Streak.Key);PlayerPrefs.DeleteKey(CheckpointStore.Key);PlayerPrefs.SetInt("AfterHours.Progress.v1",1<<1);PlayerPrefs.Save();
            SceneManager.LoadScene("AfterHours");yield return 1f;D.saveEnabled=false;
            Check(B.Has("room-1")&&B.State.days==0,"A save from before the badges earns First room, and counts no day");

            // Tests that do not save do not count days.
            D.SmallStep();
            Check(B.State.days==0,"With saving off, nothing is counted");
            yield return .1f;
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
            if(D)D.saveEnabled=false;Streak.Clock=()=>DateTime.Now;SaveBackup.PathOverride=null;
            for(int i=0;i<intKeys.Length;i++){if(intExisted[i])PlayerPrefs.SetInt(intKeys[i],intValues[i]);else PlayerPrefs.DeleteKey(intKeys[i]);}
            for(int i=0;i<stringKeys.Length;i++){if(stringExisted[i])PlayerPrefs.SetString(stringKeys[i],stringValues[i]);else PlayerPrefs.DeleteKey(stringKeys[i]);}
            PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Badge-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Coming back: which days count, that nothing is taken away, what each badge asks for, and everything surviving a reload. The dates are set by the test."}.Concat(lines));
            Debug.Log("Badge validation: "+Status);
        }
    }
}
