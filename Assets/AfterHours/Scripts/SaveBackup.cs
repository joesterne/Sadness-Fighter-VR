using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    // A second copy of the saved progress, kept as a small file in the app's own folder on the headset
    // (Android/data/<package>/files/AfterHours-save.json) instead of in the preferences store.
    // Write() runs with every autosave and does nothing unless something changed. RestoreIfMissing() runs once, before the first scene
    // loads: when a fresh install finds no progress of its own but finds this file, it puts the saved values back.
    // Android removes an app's folder when the app is uninstalled, so the file only crosses a reinstall because the installer in the
    // editor copies it off the headset before it uninstalls a differently signed copy and puts it back before the game starts.
    // In the editor nothing is written or restored unless a test points PathOverride at a temporary file.
    public static class SaveBackup
    {
        public const string FileName="AfterHours-save.json";
        public static string PathOverride;
        // How many times the file has actually been written, for the tests.
        public static int Writes;
        [Serializable] public class Entry{public string key;public bool text;public int number;public string value;}
        [Serializable] public class Copy{public int version=1;public Entry[] entries=Array.Empty<Entry>();}
        static readonly string[] Texts={CheckpointStore.Key,Wardrobe.Key};
        static readonly string[] Numbers={CheckpointStore.ProgressKey,IntroGuide.Key,"AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,Music.Key};
        static string folder,lastWritten;
        public static bool Enabled=>PathOverride!=null||(Application.platform==RuntimePlatform.Android&&!Application.isEditor);
        public static string FilePath
        {
            get
            {
                if(PathOverride!=null)return PathOverride;
                if(folder==null)folder=ExternalFolder();
                return string.IsNullOrEmpty(folder)?null:System.IO.Path.Combine(folder,FileName);
            }
        }
        // The app's folder under Android/data, which the installer can reach over USB. The persistent data path is the fallback.
        static string ExternalFolder()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var dir=activity.Call<AndroidJavaObject>("getExternalFilesDir",""))
                {
                    var path=dir?.Call<string>("getAbsolutePath");
                    if(!string.IsNullOrEmpty(path))return path;
                }
            }
            catch(Exception){}
#endif
            return Application.persistentDataPath;
        }
        static bool Allowed(string key)=>Array.IndexOf(Texts,key)>=0||Array.IndexOf(Numbers,key)>=0;
        static Copy Snapshot()
        {
            var entries=new List<Entry>();
            foreach(var key in Texts)if(PlayerPrefs.HasKey(key))entries.Add(new Entry{key=key,text=true,value=PlayerPrefs.GetString(key)});
            foreach(var key in Numbers)if(PlayerPrefs.HasKey(key))entries.Add(new Entry{key=key,number=PlayerPrefs.GetInt(key)});
            return new Copy{entries=entries.ToArray()};
        }
        // Writes the file when the saved values differ from what it already holds. Returns true when it wrote.
        public static bool Write()
        {
            if(!Enabled)return false;
            try
            {
                string path=FilePath;if(string.IsNullOrEmpty(path))return false;
                string json=JsonUtility.ToJson(Snapshot());
                if(json==lastWritten&&File.Exists(path))return false;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string temp=path+".tmp";File.WriteAllText(temp,json);
                if(File.Exists(path)){try{File.Replace(temp,path,null);}catch(Exception){File.Copy(temp,path,true);File.Delete(temp);}}
                else File.Move(temp,path);
                lastWritten=json;Writes++;return true;
            }
            catch(Exception e){Debug.LogWarning("[AfterHours] Could not update the backup copy of the save: "+e.Message);return false;}
        }
        // Puts the saved values back after a fresh install. Does nothing when this install already has progress of its own.
        public static bool RestoreIfMissing()
        {
            try
            {
                if(!Enabled||PlayerPrefs.HasKey(CheckpointStore.Key)||PlayerPrefs.HasKey(CheckpointStore.ProgressKey))return false;
                string path=FilePath;if(string.IsNullOrEmpty(path)||!File.Exists(path))return false;
                var copy=JsonUtility.FromJson<Copy>(File.ReadAllText(path));
                if(copy==null||copy.version!=1||copy.entries==null)return false;
                int restored=0;
                foreach(var entry in copy.entries)
                {
                    if(entry==null||string.IsNullOrEmpty(entry.key)||!Allowed(entry.key)||PlayerPrefs.HasKey(entry.key))continue;
                    if(entry.text)PlayerPrefs.SetString(entry.key,entry.value??"");else PlayerPrefs.SetInt(entry.key,entry.number);
                    restored++;
                }
                if(restored==0)return false;
                PlayerPrefs.Save();lastWritten=null;
                Debug.Log("[AfterHours] A fresh install found the backup copy of the save and restored "+restored+" saved values.");
                return true;
            }
            catch(Exception e){Debug.LogWarning("[AfterHours] The backup copy of the save could not be read: "+e.Message);return false;}
        }
        // Forgets the file, for tests and for clearing the save.
        public static void Delete()
        {
            lastWritten=null;
            try{if(!Enabled)return;var path=FilePath;if(!string.IsNullOrEmpty(path)){if(File.Exists(path))File.Delete(path);if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}}
            catch(Exception){}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot(){RestoreIfMissing();}
    }
}
