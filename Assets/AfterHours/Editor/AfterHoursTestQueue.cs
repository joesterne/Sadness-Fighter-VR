using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace AfterHours.Editor
{
    // Runs the automated checks and builds without the menu. Temp/AfterHours-run.txt lists jobs, one per line, and the first
    // one is taken off the list whenever the editor is ready for it. Progress goes to Logs/AfterHours-queue.txt.
    //   Suites (need desktop Play Mode and no other suite running): smoke, comfort, navigation, seat, mirror, ocean, throw, text.
    //   Editor jobs (need Play Mode off): clearprefs, buildscene, previews, buildrelease, installrelease, refresh, play, push (commit and push as the Push window would, using Temp/AfterHours-commit-message.txt).
    //   stop: leaves Play Mode once no suite is running.
    [InitializeOnLoad]
    public static class AfterHoursTestQueue
    {
        const string File_="Temp/AfterHours-run.txt",Log="Logs/AfterHours-queue.txt";
        static readonly string[] EditorJobs={"clearprefs","buildscene","previews","buildrelease","installrelease","refresh","play","push"};
        static double next;
        static AfterHoursTestQueue(){EditorApplication.update+=Pump;Note("runner loaded"+(EditorApplication.isPlaying?" (in Play Mode)":""));}
        static bool Busy=>new[]{AfterHoursSmoke.Status,ComfortValidation.Status,NavigationValidation.Status,SeatedReachValidation.Status,WardrobeValidation.Status,OceanValidation.Status,ThrowValidation.Status,TextVisibilityValidation.Status}.Any(s=>s=="Running");
        static void Note(string text){try{Directory.CreateDirectory("Logs");System.IO.File.AppendAllText(Log,DateTime.Now.ToString("HH:mm:ss")+" "+text+"\n");}catch{}}
        static void ClearPrefs()
        {
            foreach(var key in new[]{CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,Wardrobe.Key})PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(IntroGuide.Key,1);PlayerPrefs.Save();
        }
        static void Pop(System.Collections.Generic.List<string> names)
        {
            names.RemoveAt(0);if(names.Count==0)System.IO.File.Delete(File_);else System.IO.File.WriteAllLines(File_,names);
        }
        static void Pump()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!System.IO.File.Exists(File_))return;
            var names=System.IO.File.ReadAllLines(File_).Select(l=>l.Trim().ToLowerInvariant()).Where(l=>l.Length>0).ToList();
            if(names.Count==0){System.IO.File.Delete(File_);return;}
            var name=names[0];
            if(EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isPlaying)return;
            if(EditorJobs.Contains(name))
            {
                if(EditorApplication.isPlaying)return;
                Pop(names);Note("start "+name);
                try
                {
                    switch(name)
                    {
                        case "clearprefs":ClearPrefs();break;
                        case "buildscene":AfterHoursBuilder.BuildScene();break;
                        case "previews":RoomPreviews.Capture();break;
                        case "buildrelease":AfterHoursBuilder.BuildQuestRelease();break;
                        case "installrelease":QuestInstall.InstallRelease();break;
                        case "refresh":AssetDatabase.Refresh();break;
                        case "play":EditorApplication.isPlaying=true;break;
                        case "push":GitHubPush.Begin(System.IO.File.Exists("Temp/AfterHours-commit-message.txt")?System.IO.File.ReadAllText("Temp/AfterHours-commit-message.txt").Replace("\r\n","\n").Trim():"Update After Hours",EditorPrefs.GetString("AfterHours.GitName",""),EditorPrefs.GetString("AfterHours.GitEmail",""));break;
                    }
                    Note("finished "+name);
                }
                catch(Exception e){Note("failed "+name+": "+e.Message);}
                return;
            }
            if(!EditorApplication.isPlaying||Busy)return;
            if(name=="stop"){Pop(names);Note("stop");EditorApplication.isPlaying=false;return;}
            var d=GameDirector.Instance;
            // After Play Mode starts, give the scene a few seconds to settle before the first suite.
            if(!d||!d.player||Time.timeSinceLevelLoad<4)return;
            Pop(names);
            try
            {
                Note("start "+name+" (room "+d.currentRoom+", last room "+d.lastRoom+", walking "+d.player.smoothMotion+")");
                switch(name)
                {
                    case "smoke":AfterHoursSmoke.Start();break;
                    case "comfort":ComfortValidation.Start();break;
                    case "navigation":NavigationValidation.Start();break;
                    case "seat":SeatedReachValidation.Start();break;
                    case "mirror":WardrobeValidation.Start();break;
                    case "ocean":OceanValidation.Start();break;
                    case "throw":ThrowValidation.Start();break;
                    case "text":TextVisibilityValidation.Start();break;
                    default:Note("unknown job "+name);break;
                }
            }
            catch(Exception e){Note("could not start "+name+": "+e.Message);}
        }
    }
}
