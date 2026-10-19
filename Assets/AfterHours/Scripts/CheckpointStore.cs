using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    public static class CheckpointStore
    {
        public const string Key="AfterHours.Checkpoint.v1";
        // The chapters finished so far, one bit per room, written by the director.
        public const string ProgressKey="AfterHours.Progress.v1";
        // How many times the checkpoint has actually been written, for the tests.
        public static int Writes;
        [Serializable] public class State
        {
            public int version=1,lastRoom,strokes,mistakes,fills;
            public bool helped,gentleAudience,smoothMotion=true;
            // The ambush: which of the six feelings are named (one bit each), and its two options.
            public int ambush;
            public bool gentleAmbush,darkOpening=true;
            public string[] processed=Array.Empty<string>();
            // The line chosen at the end of The old résumé.
            public string line="";
            // How broken each part of the rage room's computer is.
            public int[] rage=Array.Empty<int>();
        }
        static string Id(Grabbable item)=>item.room+"|"+item.label;
        static readonly List<string> done=new List<string>();
        // Takes a snapshot of the whole game. The preferences are written only when the snapshot differs from what is stored, so saving
        // often costs a few microseconds of work and no disk write, and the backup copy is brought up to date at the same time.
        public static bool Save(GameDirector d)
        {
            done.Clear();
            for(int room=0;room<d.rooms.Length;room++)foreach(var item in d.GrabbablesOf(room))if(item&&item.processed)done.Add(Id(item));
            var state=new State{lastRoom=d.lastRoom,strokes=d.ocean.strokes,mistakes=d.kitchen.mistakes,fills=d.kitchen.fills,helped=d.kitchen.helped,gentleAudience=d.gentleAudience,smoothMotion=d.player.smoothMotion,ambush=d.ambush?d.ambush.namedMask:0,gentleAmbush=d.gentleAmbush,darkOpening=d.darkOpening,line=d.shredder?d.shredder.line:"",rage=d.rage?d.rage.Damage():Array.Empty<int>(),processed=done.ToArray()};
            string json=JsonUtility.ToJson(state);
            bool changed=PlayerPrefs.GetString(Key,null)!=json;
            if(changed){PlayerPrefs.SetString(Key,json);PlayerPrefs.Save();Writes++;}
            SaveBackup.Write();
            return changed;
        }
        public static void Load(GameDirector d)
        {
            if(!PlayerPrefs.HasKey(Key))return;
            State state;try{state=JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key));}catch(ArgumentException){return;}
            if(state==null||state.version!=1)return;
            d.lastRoom=Mathf.Clamp(state.lastRoom,0,d.rooms.Length-1);d.gentleAudience=state.gentleAudience;d.gentleAmbush=state.gentleAmbush;d.darkOpening=state.darkOpening;d.player.smoothMotion=state.smoothMotion;
            d.ocean.RestoreProgress(state.strokes);d.kitchen.RestoreProgress(state.helped,state.mistakes,state.fills);
            var processed=state.processed??Array.Empty<string>();
            foreach(int room in new[]{2,4})
            {
                var root=d.rooms[room].root;d.rooms[room].accepted=0;
                var zones=root.GetComponentsInChildren<DropZone>(true);foreach(var zone in zones)zone.ResetZone();
                foreach(var item in root.GetComponentsInChildren<Grabbable>(true))
                {
                    item.ResetObject();
                    if(processed.Contains(Id(item))&&zones.Any(zone=>zone.RestoreItem(item)))d.rooms[room].accepted++;
                }
                if(d.rooms[room].accepted>=d.rooms[room].total)d.rooms[room].complete=true;
            }
            if(d.shredder)d.shredder.Restore(item=>processed.Contains(Id(item)),state.line);
            if(d.rage)d.rage.Restore(state.rage??Array.Empty<int>());
            if(d.ambush)d.ambush.Restore(state.ambush);
            if(d.ocean.strokes>=d.ocean.requiredStrokes)d.rooms[1].complete=true;
            if(d.kitchen.fills>=3)d.rooms[3].complete=true;
            int mask=PlayerPrefs.GetInt(ProgressKey,0);foreach(int i in GameDirector.Chapters)if(d.rooms[i].complete)mask|=1<<i;
            PlayerPrefs.SetInt(ProgressKey,mask);PlayerPrefs.Save();
        }
    }
}
