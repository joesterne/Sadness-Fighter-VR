using System;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    public static class CheckpointStore
    {
        public const string Key="AfterHours.Checkpoint.v1";
        [Serializable] public class State
        {
            public int version=1,lastRoom,strokes,mistakes,fills;
            public bool helped,gentleAudience,smoothMotion=true;
            public string[] processed=Array.Empty<string>();
        }
        static string Id(Grabbable item)=>item.room+"|"+item.label;
        public static void Save(GameDirector d)
        {
            var state=new State{lastRoom=d.lastRoom,strokes=d.ocean.strokes,mistakes=d.kitchen.mistakes,fills=d.kitchen.fills,helped=d.kitchen.helped,gentleAudience=d.gentleAudience,smoothMotion=d.player.smoothMotion,
                processed=d.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Grabbable>(true)).Where(g=>g.processed).Select(Id).ToArray()};
            PlayerPrefs.SetString(Key,JsonUtility.ToJson(state));PlayerPrefs.Save();
        }
        public static void Load(GameDirector d)
        {
            if(!PlayerPrefs.HasKey(Key))return;
            State state;try{state=JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key));}catch(ArgumentException){return;}
            if(state==null||state.version!=1)return;
            d.lastRoom=Mathf.Clamp(state.lastRoom,0,5);d.gentleAudience=state.gentleAudience;d.player.smoothMotion=state.smoothMotion;
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
            if(d.ocean.strokes>=d.ocean.requiredStrokes)d.rooms[1].complete=true;
            if(d.kitchen.fills>=3)d.rooms[3].complete=true;
            int mask=0;for(int i=1;i<=4;i++)if(d.rooms[i].complete)mask|=1<<i;
            PlayerPrefs.SetInt("AfterHours.Progress.v1",mask);PlayerPrefs.Save();
        }
    }
}
