using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Outfit add-ons against the editor's test store: the catalog, a cancelled and a finished checkout, wearing what was bought,
    // a reload with the store offline, and a refund. The Meta store itself can only be tried on a headset.
    public static class AddOnValidation
    {
        static IEnumerator<float> run;static double next,deadline;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,Wardrobe.Key};
        static bool[] existed;static string[] values;static bool introExisted;static int intro;
        public static string Status="Not run";
        const string Hoodie="midnight-hoodie";
        static GameDirector D=>GameDirector.Instance;
        static Wardrobe W=>D.wardrobe;
        static AddOnShop S=>D.shop;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;
            existed=keys.Select(PlayerPrefs.HasKey).ToArray();values=keys.Select(k=>PlayerPrefs.GetString(k)).ToArray();
            introExisted=PlayerPrefs.HasKey(IntroGuide.Key);intro=PlayerPrefs.GetInt(IntroGuide.Key);
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;TestAddOnStore.Reset();
            lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        // Waits for a store call, a frame at a time.
        static IEnumerable<float> Wait<T>(System.Threading.Tasks.Task<T> task){for(int i=0;i<300&&!task.IsCompleted;i++)yield return .02f;if(!task.IsCompleted)throw new Exception("The store did not answer");}
        static IEnumerable<float> Reload(){D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return 1f;for(int i=0;i<100&&!S.Ready;i++)yield return .05f;}
        static IEnumerator<float> Checks()
        {
            foreach(var t in Reload())yield return t;
            var piece=Outfits.Get(Hoodie);
            Check(piece.IsAddOn&&piece.slot==OutfitSlot.Top&&Outfits.ForSku(piece.sku)==Hoodie,"The placeholder outfit is an add-on top with its own SKU ("+piece.sku+")");
            Check(Outfits.Earnable==12&&Outfits.AddOns.All(p=>!Outfits.Earned(p)),"Add-ons are not among the twelve pieces to earn");
            Check(S.Ready&&S.State==StoreStart.Ready&&S.store is TestAddOnStore,"In the editor the shop starts against the test store");
            Check(S.OnSale(Hoodie)&&!string.IsNullOrEmpty(S.Price(Hoodie)),"The catalog lists the hoodie with a price ("+S.Price(Hoodie)+")");
            Check(!W.Has(Hoodie)&&!W.Owned(OutfitSlot.Top).Any(p=>p.id==Hoodie),"Before buying, the hoodie is not in the wardrobe");
            Check(!W.toFind||!W.toFind.text.Contains("hoodie"),"The STILL TO FIND board never lists an add-on");
            Check(!W.Unlock(Hoodie)&&!W.Has(Hoodie),"An add-on cannot be earned by playing");

            TestAddOnStore.CancelNext=true;var buy=S.Buy(Hoodie);foreach(var t in Wait(buy))yield return t;
            Check(buy.Result==PurchaseResult.Cancelled&&!W.Has(Hoodie),"Closing the checkout buys nothing");
            buy=S.Buy(Hoodie);foreach(var t in Wait(buy))yield return t;
            Check(buy.Result==PurchaseResult.Bought&&W.Has(Hoodie)&&W.State.fresh.Contains(Hoodie),"A finished checkout puts the hoodie in the wardrobe, marked new");
            yield return 6f;
            Check(D.player.hint.gameObject.activeSelf&&D.player.hint.text.Contains("Midnight hoodie"),"The purchase is announced like an earned piece");
            buy=S.Buy(Hoodie);foreach(var t in Wait(buy))yield return t;
            Check(buy.Result==PurchaseResult.AlreadyOwned,"Buying it again is refused: it is already owned");

            for(int i=0;i<W.Owned(OutfitSlot.Top).Count&&W.State.top!=Hoodie;i++)W.Cycle(4,1);
            yield return .2f;
            Check(W.State.top==Hoodie&&W.avatar.Wearing(Hoodie)&&!W.State.fresh.Contains(Hoodie),"The hoodie can be tried on at the mirror and the reflection wears it");

            // A reload with the store unreachable keeps the hoodie, from the headset's own copy.
            TestAddOnStore.Offline=true;
            foreach(var t in Reload())yield return t;
            Check(W.Has(Hoodie)&&W.State.top==Hoodie&&W.avatar.Wearing(Hoodie),"With the store offline, a reload still has the hoodie on");
            TestAddOnStore.Offline=false;

            // A refund: the store no longer lists it, so the next restore takes it off and the starter top goes back on.
            TestAddOnStore.Bought.Clear();var restore=S.Restore();
            for(int i=0;i<300&&!restore.IsCompleted;i++)yield return .02f;
            yield return .1f;
            Check(!W.Has(Hoodie)&&W.State.top=="tee"&&!W.avatar.Wearing(Hoodie)&&W.avatar.Wearing("tee"),"After a refund the hoodie is gone and the plain tee is back on");
            Check(W.FoundCount==0&&W.Owned(OutfitSlot.Top).Count==2,"Earned pieces are untouched by buying and refunding");
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
            if(D)D.saveEnabled=false;TestAddOnStore.Reset();
            for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
            if(introExisted)PlayerPrefs.SetInt(IntroGuide.Key,intro);else PlayerPrefs.DeleteKey(IntroGuide.Key);
            PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/AddOn-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Outfit add-ons against the editor's test store: catalog, checkout, wearing, an offline reload and a refund."}.Concat(lines));
            Debug.Log("Add-on validation: "+Status);
        }
    }

    // Try add-ons by hand in Play Mode without the Meta store: the test store sells every add-on for nothing until the editor closes.
    static class AddOnTestStoreMenu
    {
        const string Buy="After Hours/Add-ons/Buy every add-on in the test store (Play Mode)",Refund="After Hours/Add-ons/Refund the test store's add-ons (Play Mode)";
        [MenuItem(Buy,false,2100)] static async void BuyAll(){foreach(var p in Outfits.AddOns.ToList())Debug.Log("Test store: "+p.name+": "+await GameDirector.Instance.shop.Buy(p.id));}
        [MenuItem(Refund,false,2101)] static async void RefundAll(){TestAddOnStore.Bought.Clear();await GameDirector.Instance.shop.Restore();Debug.Log("Test store: add-ons refunded");}
        [MenuItem(Buy,true)][MenuItem(Refund,true)] static bool Ready()=>EditorApplication.isPlaying&&GameDirector.Instance&&GameDirector.Instance.shop!=null&&GameDirector.Instance.shop.store is TestAddOnStore;
    }
}
