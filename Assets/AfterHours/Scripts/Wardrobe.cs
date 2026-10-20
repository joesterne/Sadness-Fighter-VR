using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    public enum OutfitSlot { Top, Head, Neck, Pin }

    // Everything the avatar can wear. Starter pieces are always available; most of the rest are earned by playing.
    // Pieces with a SKU are add-ons, bought from the store (see AddOnShop) and never earned.
    // Ids are saved on the player's headset and SKUs live in the Meta dashboard, so neither may ever be renamed.
    public static class Outfits
    {
        public struct Piece { public string id, name, hint, sku; public OutfitSlot slot; public bool starter; public bool IsAddOn=>sku!=null; }
        static Piece P(string id,string name,OutfitSlot slot,string hint=null)=>new Piece{id=id,name=name,slot=slot,hint=hint,starter=hint==null};
        static Piece Sold(string id,string name,OutfitSlot slot,string sku)=>new Piece{id=id,name=name,slot=slot,sku=sku};
        public static readonly Piece[] All=
        {
            P("tee","Plain tee",OutfitSlot.Top),
            P("shirt","Work shirt",OutfitSlot.Top),
            P("jumper","Harbour jumper",OutfitSlot.Top,"Reach the shore in Ocean of shame"),
            P("cardigan","Warm cardigan",OutfitSlot.Top,"Finish A small spill"),
            P("jacket","Sunrise jacket",OutfitSlot.Top,"Finish all seven chapters"),
            P("no-hat","No hat",OutfitSlot.Head),
            P("cap","Mover's cap",OutfitSlot.Head,"Finish Heavy things"),
            P("beanie","Evening beanie",OutfitSlot.Head,"Rest on the rooftop for a little while"),
            P("no-neck","Open collar",OutfitSlot.Neck),
            P("scarf","Kind scarf",OutfitSlot.Neck,"Ask for help in the kitchen"),
            P("lanyard","Your own lanyard",OutfitSlot.Neck,"Finish The old résumé"),
            P("no-pin","No pin",OutfitSlot.Pin),
            P("step-pin","Small step pin",OutfitSlot.Pin,"Take one small step in any chapter"),
            P("true-pin","Still true pin",OutfitSlot.Pin,"Finish The infinite archive"),
            P("letitout-pin","Let it out pin",OutfitSlot.Pin,"Finish The rage room"),
            P("orange-pin","Orange mug pin",OutfitSlot.Pin,"Something orange is hiding in the kitchen"),
            P("standup-pin","Stood up pin",OutfitSlot.Pin,"Finish The ambush"),
            // Add-ons. A placeholder until real outfits go on sale: it only appears once the store says it was bought.
            Sold("midnight-hoodie","Midnight hoodie",OutfitSlot.Top,"outfit_midnight_hoodie"),
        };
        public static Piece Get(string id)=>All.FirstOrDefault(p=>p.id==id);
        public static bool Exists(string id)=>All.Any(p=>p.id==id);
        public static bool Earned(Piece p)=>!p.starter&&!p.IsAddOn;
        public static int Earnable=>All.Count(Earned);
        public static IEnumerable<Piece> AddOns=>All.Where(p=>p.IsAddOn);
        public static string ForSku(string sku)=>All.FirstOrDefault(p=>p.IsAddOn&&p.sku==sku).id;
        // The piece each chapter gives the first time it is finished.
        public static string ForChapter(int room)=>room switch{1=>"jumper",2=>"cap",3=>"cardigan",4=>"true-pin",6=>"lanyard",7=>"letitout-pin",8=>"standup-pin",_=>null};
    }

    [Serializable] public class WardrobeState
    {
        public int version=1, skin=2, hair, hairColour=1, build=1;
        public string top="tee", head="no-hat", neck="no-neck", pin="no-pin", line="";
        // bought is the headset's copy of the add-ons the store says are owned, so they show at once and offline.
        public List<string> earned=new List<string>(), fresh=new List<string>(), bought=new List<string>();
        public string Equipped(OutfitSlot slot)=>slot switch{OutfitSlot.Top=>top,OutfitSlot.Head=>head,OutfitSlot.Neck=>neck,_=>pin};
        public void Equip(OutfitSlot slot,string id){switch(slot){case OutfitSlot.Top:top=id;break;case OutfitSlot.Head:head=id;break;case OutfitSlot.Neck:neck=id;break;default:pin=id;break;}}
        public bool Owns(string id){if(!Outfits.Exists(id))return false;var p=Outfits.Get(id);return p.starter||(p.IsAddOn?bought:earned).Contains(id);}
    }

    // The wardrobe beside the lobby mirror: what the player has earned, what they wear, and how their avatar looks.
    // It saves on its own key, so trying a chapter again never takes anything away.
    public class Wardrobe : MonoBehaviour
    {
        public const string Key="AfterHours.Wardrobe.v1";
        public static readonly string[] Rows={"SKIN TONE","HAIR","HAIR COLOUR","BUILD","TOP","HAT","NECK","PIN"};
        public static readonly string[] HairStyles={"Short","Curls","Long","Bun","Shaved"},HairColours={"Black","Brown","Auburn","Blond","Silver"},Builds={"Narrow","Medium","Broad"};
        public const int SkinTones=6;
        public GameDirector director;
        public MirrorAvatar avatar;
        public TextMeshPro[] values;
        public TextMeshPro status, toFind, nameplate;
        public WardrobeState State { get; private set; }=new WardrobeState();
        public int FoundCount=>State.earned.Count;

        public void Load()
        {
            WardrobeState loaded=null;
            if(PlayerPrefs.HasKey(Key))try{loaded=JsonUtility.FromJson<WardrobeState>(PlayerPrefs.GetString(Key));}catch(ArgumentException){}
            State=loaded!=null&&loaded.version==1?loaded:new WardrobeState();
            State.earned=(State.earned??new List<string>()).Where(id=>Outfits.Exists(id)&&Outfits.Earned(Outfits.Get(id))).Distinct().ToList();
            State.bought=(State.bought??new List<string>()).Where(id=>Outfits.Exists(id)&&Outfits.Get(id).IsAddOn).Distinct().ToList();
            State.fresh=(State.fresh??new List<string>()).Where(id=>State.earned.Contains(id)||State.bought.Contains(id)).Distinct().ToList();
            KeepWearable();
            State.skin=Wrap(State.skin,SkinTones);State.hair=Wrap(State.hair,HairStyles.Length);State.hairColour=Wrap(State.hairColour,HairColours.Length);State.build=Wrap(State.build,Builds.Length);
            State.line=State.line??"";
            Refresh();
        }
        // Anything worn that is no longer owned (a refunded add-on) goes back to the starter piece for its slot.
        void KeepWearable()
        {
            foreach(OutfitSlot slot in Enum.GetValues(typeof(OutfitSlot)))
                if(!State.Owns(State.Equipped(slot))||Outfits.Get(State.Equipped(slot)).slot!=slot)State.Equip(slot,Outfits.All.First(p=>p.slot==slot&&p.starter).id);
        }
        void Save(){PlayerPrefs.SetString(Key,JsonUtility.ToJson(State));PlayerPrefs.Save();}
        public bool Has(string id)=>State.Owns(id);
        // True the first time a piece is earned.
        public bool Unlock(string id)
        {
            if(string.IsNullOrEmpty(id)||!Outfits.Exists(id)||!Outfits.Earned(Outfits.Get(id))||State.earned.Contains(id))return false;
            State.earned.Add(id);State.fresh.Add(id);Save();Refresh();return true;
        }
        // The add-ons the store says are owned. With replace, anything missing from ids was refunded and is taken off;
        // without it (a purchase, or the headset's offline copy) pieces are only added. Returns the pieces new to this headset.
        public List<string> SetBought(IEnumerable<string> ids,bool replace)
        {
            var owned=ids.Where(id=>Outfits.Exists(id)&&Outfits.Get(id).IsAddOn).Distinct().ToList();
            var added=owned.Where(id=>!State.bought.Contains(id)).ToList();
            var removed=replace?State.bought.Where(id=>!owned.Contains(id)).ToList():new List<string>();
            if(added.Count==0&&removed.Count==0)return added;
            State.bought.AddRange(added);State.fresh.AddRange(added);
            foreach(var id in removed){State.bought.Remove(id);State.fresh.Remove(id);}
            KeepWearable();Save();Refresh();return added;
        }
        public void SetLine(string line){State.line=line??"";Save();Refresh();}
        static OutfitSlot SlotOf(int row)=>(OutfitSlot)(row-4);
        public List<Outfits.Piece> Owned(OutfitSlot slot)=>Outfits.All.Where(p=>p.slot==slot&&State.Owns(p.id)).ToList();
        // One wardrobe row forward or back. Outfit rows only offer pieces the player owns.
        public void Cycle(int row,int step)
        {
            switch(row)
            {
                case 0:State.skin=Wrap(State.skin+step,SkinTones);break;
                case 1:State.hair=Wrap(State.hair+step,HairStyles.Length);break;
                case 2:State.hairColour=Wrap(State.hairColour+step,HairColours.Length);break;
                case 3:State.build=Wrap(State.build+step,Builds.Length);break;
                default:
                    if(row<4||row>7)return;
                    var slot=SlotOf(row);var owned=Owned(slot);int index=owned.FindIndex(p=>p.id==State.Equipped(slot));
                    var next=owned[Wrap(index+step,owned.Count)];State.Equip(slot,next.id);State.fresh.Remove(next.id);break;
            }
            Save();Refresh();
        }
        static int Wrap(int value,int count)=>((value%count)+count)%count;
        public string Value(int row)=>row switch
        {
            0=>"Tone "+(State.skin+1),1=>HairStyles[State.hair],2=>HairColours[State.hairColour],3=>Builds[State.build],
            _=>Outfits.Get(State.Equipped(SlotOf(row))).name
        };
        public void Refresh()
        {
            if(values!=null)
                for(int i=0;i<values.Length&&i<Rows.Length;i++)
                {
                    if(!values[i])continue;string text=Value(i);
                    if(i>=4&&Owned(SlotOf(i)).Any(p=>State.fresh.Contains(p.id)))text+="\n<size=70%><color=#E4BD7E>NEW TO TRY</color></size>";
                    values[i].text=text;
                }
            if(status)status.text=FoundCount+" OF "+Outfits.Earnable+" PIECES FOUND"+(State.fresh.Count>0?"\n<color=#E4BD7E>New: "+string.Join(", ",State.fresh.Select(id=>Outfits.Get(id).name))+"</color>":"");
            if(toFind)
            {
                var missing=Outfits.All.Where(p=>Outfits.Earned(p)&&!State.earned.Contains(p.id)).Select(p=>p.hint).Take(4).ToList();
                toFind.text=missing.Count==0?"EVERYTHING FOUND\nThank you for taking your time.":"STILL TO FIND\n"+string.Join("\n",missing);
            }
            if(nameplate)nameplate.text=string.IsNullOrEmpty(State.line)?"YOUR WARDROBE":"STILL TRUE  /  "+State.line;
            if(avatar)avatar.Apply(State);
            if(director)director.RefreshMirrorButton();
        }
    }
}
