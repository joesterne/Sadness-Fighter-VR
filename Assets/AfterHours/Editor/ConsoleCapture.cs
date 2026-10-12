using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace AfterHours
{
    // Writes every warning, error and exception from the Unity Console to Logs/AfterHours-console.txt, so a problem can be read
    // without opening the Console window. The file is cleared each time the editor starts and capped in size.
    [InitializeOnLoad]
    public static class ConsoleCapture
    {
        const string SessionKey="AfterHours.ConsoleCapture.Started";
        const long MaxBytes=400000;
        static readonly string path=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","AfterHours-console.txt");
        static readonly object gate=new object();
        static ConsoleCapture()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // SessionState survives script reloads but not an editor restart, so the log restarts with each editor session.
                if(!SessionState.GetBool(SessionKey,false)){File.WriteAllText(path,"Console log for this editor session\n");SessionState.SetBool(SessionKey,true);}
                Application.logMessageReceivedThreaded-=OnLog;Application.logMessageReceivedThreaded+=OnLog;
            }
            catch(Exception){}
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Log)return;
            try
            {
                lock(gate)
                {
                    if(File.Exists(path)&&new FileInfo(path).Length>MaxBytes)return;
                    var lines=stack==null?"":string.Join("\n    ",stack.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries),0,Math.Min(6,stack.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length));
                    File.AppendAllText(path,DateTime.Now.ToString("HH:mm:ss")+" ["+type+"] "+message+"\n    "+lines+"\n");
                }
            }
            catch(Exception){}
        }
    }
}
