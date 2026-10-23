using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Outfit add-ons against the editor's test store: the catalog (a free piece in every slot, a look for every add-on), trying on
    // at the mirror, BUY (cancelled and finished), a bundle, a reload with the store offline, and a refund.
    // The Meta store itself can only be tried on a headset.
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
        // Steps an outfit row forward until it shows the piece, the way the > button does.
        static void StepTo(string id){var slot=Outfits.Get(id).slot;for(int i=0;i<40&&W.Shown(slot)!=id;i++)W.Cycle((int)slot+4,1);}
        static IEnumerator<float> Checks()
        {
            foreach(var t in Reload())yield return t;
            // The catalog: free to play, so every slot has a free piece, and every add-on has its own SKU and its own look.
            var slots=Enum.GetValues(typeof(OutfitSlot)).Cast<OutfitSlot>().ToList();
            Check(slots.All(slot=>Outfits.All.Any(p=>p.slot==slot&&p.starter)),"Every outfit slot has a free starter piece, so nothing needs buying");
            Check(Wardrobe.Rows.Length==4+slots.Count,"The wardrobe has a row for every outfit slot ("+slots.Count+")");
            var addOns=Outfits.AddOns.ToList();
            Check(addOns.Count>=20&&Outfits.Skus.Distinct().Count()==Outfits.Skus.Count()&&addOns.All(p=>p.sku=="outfit_"+p.id.Replace('-','_')),"Every add-on has its own SKU, named after its id ("+addOns.Count+" add-ons)");
            int Selling(OutfitSlot slot)=>addOns.Count(p=>p.slot==slot);
            Check(Selling(OutfitSlot.Top)>=4&&Selling(OutfitSlot.Head)>=4&&Selling(OutfitSlot.Legs)>=6&&Selling(OutfitSlot.Hands)>=3&&Selling(OutfitSlot.Feet)>=4,"Several add-ons each for tops, hats, trousers and skirts, gloves and shoes");
            var looks=W.avatar.looks.Select(l=>l.id).ToList();
            var bare=new[]{"no-hat","no-neck","no-pin","no-gloves"};
            Check(Outfits.All.Where(p=>!bare.Contains(p.id)).All(p=>looks.Contains(p.id)),"Every piece but the bare ones is built on the reflection");
            Check(W.avatar.looks.Where(l=>Outfits.Get(l.id).slot==OutfitSlot.Legs||Outfits.Get(l.id).slot==OutfitSlot.Hands).All(l=>l.extra!=null&&l.extra.Length>=1),"Trousers, skirts and gloves are built on both legs or both hands");
            Check(Outfits.Earnable==14&&addOns.All(p=>!Outfits.Earned(p)),"Add-ons are not among the fourteen pieces to earn");
            Check(S.Ready&&S.State==StoreStart.Ready&&S.store is TestAddOnStore,"In the editor the shop starts against the test store");
            Check(addOns.All(p=>S.OnSale(p.id)&&!string.IsNullOrEmpty(S.Price(p.id))),"The catalog lists every add-on with a price");
            Check(addOns.All(p=>!W.Has(p.id))&&slots.All(slot=>W.Owned(slot).All(p=>!p.IsAddOn)),"A new player owns no add-ons");
            Check(!W.toFind||addOns.All(p=>!W.toFind.text.Contains(p.name)),"The STILL TO FIND board never lists an add-on");
            Check(!W.Unlock(Hoodie)&&!W.Has(Hoodie),"An add-on cannot be earned by playing");
            Check(W.State.legs=="trousers"&&W.State.hands=="no-gloves"&&W.State.feet=="plain-shoes"&&W.avatar.Wearing("trousers")&&W.avatar.Wearing("plain-shoes"),"A new player wears the free trousers and shoes");

            // Trying on: an outfit row steps onto add-ons, the reflection wears them, and nothing is saved.
            Check(!W.buyButton.activeSelf&&W.status.gameObject.activeSelf,"BUY is hidden until something is tried on");
            StepTo(Hoodie);yield return .2f;
            Check(W.TryingOn==Hoodie&&W.State.top=="tee"&&W.avatar.Wearing(Hoodie)&&!W.avatar.Wearing("tee"),"TOP > steps onto the hoodie: the reflection tries it on, the tee stays what you own and wear");
            Check(W.values[4].text.Contains("Midnight hoodie")&&W.values[4].text.Contains(S.Price(Hoodie)),"The row shows the hoodie and its price");
            Check(W.buyButton.activeSelf&&W.buyLabel.text.Contains("MIDNIGHT HOODIE")&&!W.status.gameObject.activeSelf,"BUY appears with the piece's name, over the status board");
            StepTo("pleated-skirt");StepTo("wellies");yield return .2f;
            Check(W.avatar.Wearing(Hoodie)&&W.avatar.Wearing("pleated-skirt")&&W.avatar.Wearing("wellies")&&!W.avatar.Wearing("trousers")&&W.TryingOn=="wellies","Several pieces can be tried on together; BUY is for the latest");
            Check(W.State.legs=="trousers"&&W.State.feet=="plain-shoes"&&!PlayerPrefs.GetString(Wardrobe.Key).Contains("wellies"),"Nothing tried on is saved");
            StepTo("plain-shoes");yield return .2f;
            Check(!W.avatar.Wearing("wellies")&&W.TryingOn=="pleated-skirt","Stepping back onto a piece you own ends that try-on, and BUY moves to the skirt");

            // Buying at the mirror.
            StepTo(Hoodie);TestAddOnStore.CancelNext=true;D.BuyTryingOn();yield return .5f;
            Check(!W.Has(Hoodie)&&W.TryingOn==Hoodie&&D.player.hint.text.Contains("Nothing was bought"),"Closing the checkout buys nothing, and you can keep trying it on");
            D.BuyTryingOn();yield return .5f;
            Check(W.Has(Hoodie)&&W.State.top==Hoodie&&W.avatar.Wearing(Hoodie)&&D.player.hint.text.Contains("Midnight hoodie is yours"),"BUY: once the store confirms, the hoodie is yours and on");
            Check(W.TryingOn=="pleated-skirt"&&W.buyLabel.text.Contains("PLEATED SKIRT"),"BUY moves on to the next piece still being tried on");
            var again=S.Buy(Hoodie);foreach(var t in Wait(again))yield return t;
            Check(again.Result==PurchaseResult.AlreadyOwned,"Buying it again is refused: it is already owned");
            Check(W.Offered(OutfitSlot.Top).Count(p=>p.id==Hoodie)==1&&W.Owned(OutfitSlot.Top).Any(p=>p.id==Hoodie),"The hoodie now steps like any piece you own");

            // Leaving the lobby takes off what was only tried on.
            D.Travel(1);yield return 1.2f;D.Travel(0);yield return 1.2f;
            Check(W.TryingOn==null&&!W.buyButton.activeSelf&&W.avatar.Wearing("trousers")&&!W.avatar.Wearing("pleated-skirt")&&W.avatar.Wearing(Hoodie),"Leaving the lobby ends every try-on; what you bought stays on");

            // A bundle: one SKU, several pieces.
            Outfits.Bundles.Add(new Outfits.Bundle{id="test-bundle",name="Test bundle",sku="outfit_bundle_test",pieces=new[]{"jeans","boots"}});
            var load=S.LoadCatalog();for(int i=0;i<300&&!load.IsCompleted;i++)yield return .02f;
            var bundle=S.BuyBundle("test-bundle");foreach(var t in Wait(bundle))yield return t;
            Check(bundle.Result==PurchaseResult.Bought&&W.Has("jeans")&&W.Has("boots")&&!string.IsNullOrEmpty(S.BundlePrice("test-bundle")),"A bundle gives every piece in it");

            // A reload with the store unreachable keeps everything, from the headset's own copy.
            TestAddOnStore.Offline=true;
            foreach(var t in Reload())yield return t;
            Check(W.Has(Hoodie)&&W.Has("jeans")&&W.State.top==Hoodie&&W.avatar.Wearing(Hoodie),"With the store offline, a reload still has the hoodie on and the bundle's pieces");
            TestAddOnStore.Offline=false;

            // A refund: the store no longer lists the hoodie, so the next restore takes it off and the starter top goes back on.
            TestAddOnStore.Bought.Remove(Outfits.Get(Hoodie).sku);var restore=S.Restore();
            for(int i=0;i<300&&!restore.IsCompleted;i++)yield return .02f;
            yield return .1f;
            Check(!W.Has(Hoodie)&&W.State.top=="tee"&&!W.avatar.Wearing(Hoodie)&&W.avatar.Wearing("tee")&&W.Has("jeans"),"After a refund the hoodie is gone and the plain tee is back on; the bundle stays");
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
            if(D)D.saveEnabled=false;TestAddOnStore.Reset();Outfits.Bundles.RemoveAll(b=>b.id=="test-bundle");
            for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
            if(introExisted)PlayerPrefs.SetInt(IntroGuide.Key,intro);else PlayerPrefs.DeleteKey(IntroGuide.Key);
            PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/AddOn-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Outfit add-ons against the editor's test store: catalog, trying on, BUY, a bundle, an offline reload and a refund."}.Concat(lines));
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
