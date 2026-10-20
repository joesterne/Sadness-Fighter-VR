using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace AfterHours
{
    // One add-on as the store describes it. Name and price come from the Meta developer dashboard, never from the game.
    public struct AddOn { public string sku, name, price, description; }
    public enum StoreStart { Ready, NotEntitled, Unavailable }
    public enum PurchaseResult { Bought, AlreadyOwned, Cancelled, Failed, Unavailable }
    // What the player owns. fromCache is true when the store could not be reached and answered from the headset's own copy,
    // which only ever adds: a refund is only believed when the store itself says so.
    public class OwnedAddOns { public List<string> skus=new List<string>(); public bool fromCache; }

    // Where add-ons are bought. On a Quest this is the Meta Horizon Store; in the editor it is a test store that charges nothing.
    public interface IAddOnStore
    {
        string Name { get; }
        Task<StoreStart> Start();
        Task<List<AddOn>> Catalog(string[] skus);
        // Null when nothing could be read at all.
        Task<OwnedAddOns> Owned();
        Task<PurchaseResult> Buy(string sku);
    }

    // Outfit add-ons: checks the player owns the game, fetches the add-on catalog, restores what was bought and buys more.
    // Pieces with a SKU in Outfits.All are the add-ons; the wardrobe treats a bought one like any other piece.
    public class AddOnShop
    {
        // Meta Horizon Store app id for After Hours (Quest). The add-on SKUs are created under it in the developer dashboard.
        public const string AppId="1393348977192544";
        public readonly IAddOnStore store;
        readonly GameDirector director;
        Wardrobe W=>director?director.wardrobe:null;
        public StoreStart State { get; private set; }=StoreStart.Unavailable;
        public bool Ready { get; private set; }
        public readonly Dictionary<string,AddOn> catalog=new Dictionary<string,AddOn>();
        public event Action Changed;

        public AddOnShop(IAddOnStore store,GameDirector director){this.store=store;this.director=director;}
        public static IAddOnStore ForThisDevice()=>Application.platform==RuntimePlatform.Android?new MetaAddOnStore(AppId):(IAddOnStore)new TestAddOnStore();

        // Runs once at launch. Meta requires the ownership check within ten seconds of launch, and a store build to close
        // when it fails. Development builds and the editor only log it, so a sideloaded test build always opens.
        public async Task Begin()
        {
            try
            {
                State=await store.Start();
                if(State!=StoreStart.Ready)
                {
                    Debug.LogWarning("After Hours add-ons: "+store.Name+" "+(State==StoreStart.NotEntitled?"says this account does not own the game":"could not start"));
                    if(State==StoreStart.NotEntitled&&!Application.isEditor&&!Debug.isDebugBuild)Application.Quit();
                    return;
                }
                await Restore();
                await LoadCatalog();
                Ready=true;Changed?.Invoke();
            }
            catch(Exception e){Debug.LogException(e);}
        }
        public async Task LoadCatalog()
        {
            var skus=Outfits.AddOns.Select(p=>p.sku).ToArray();if(skus.Length==0)return;
            var found=await store.Catalog(skus);catalog.Clear();
            foreach(var a in found)catalog[a.sku]=a;
            var missing=skus.Where(s=>!catalog.ContainsKey(s)).ToList();
            if(missing.Count>0)Debug.Log("After Hours add-ons: not on sale yet (no add-on with this SKU in the dashboard): "+string.Join(", ",missing));
            Changed?.Invoke();
        }
        // Puts back everything bought, on this headset or another. Pieces refunded since are taken off.
        public async Task Restore()
        {
            var owned=await store.Owned();if(owned==null||!W)return;
            var ids=owned.skus.Select(Outfits.ForSku).Where(id=>id!=null).ToList();
            foreach(var id in W.SetBought(ids,!owned.fromCache))if(director)director.Announce(id);
            Changed?.Invoke();
        }
        public bool OnSale(string pieceId){var p=Outfits.Get(pieceId);return p.sku!=null&&catalog.ContainsKey(p.sku);}
        public string Price(string pieceId){var p=Outfits.Get(pieceId);return p.sku!=null&&catalog.TryGetValue(p.sku,out var a)?a.price:null;}
        // Opens the store's own checkout for one outfit piece. The piece is added to the wardrobe the moment the store confirms it.
        public async Task<PurchaseResult> Buy(string pieceId)
        {
            var piece=Outfits.Get(pieceId);
            if(piece.sku==null)return PurchaseResult.Failed;
            if(W&&W.Has(pieceId))return PurchaseResult.AlreadyOwned;
            if(State!=StoreStart.Ready)return PurchaseResult.Unavailable;
            PurchaseResult result;
            try{result=await store.Buy(piece.sku);}catch(Exception e){Debug.LogException(e);return PurchaseResult.Failed;}
            if(result==PurchaseResult.Bought||result==PurchaseResult.AlreadyOwned)
            {
                if(W)foreach(var id in W.SetBought(new[]{pieceId},false))if(director)director.Announce(id);
                Changed?.Invoke();
            }
            return result;
        }
    }

    // The Meta Horizon Store, through the Meta XR Platform SDK (com.meta.xr.sdk.platform).
    public class MetaAddOnStore : IAddOnStore
    {
        readonly string appId;
        public MetaAddOnStore(string appId){this.appId=appId;}
        public string Name=>"Meta Horizon Store";
        static string Why(Oculus.Platform.Message m)=>m.GetError()?.Message??"no detail";
        public async Task<StoreStart> Start()
        {
            try
            {
                var init=await Oculus.Platform.Core.AsyncInitialize(appId);
                if(init==null||init.IsError){Debug.LogWarning("After Hours add-ons: Platform SDK did not start: "+(init==null?"no answer":Why(init)));return StoreStart.Unavailable;}
                var entitled=await Oculus.Platform.Entitlements.GetIsViewerEntitled();
                if(entitled==null||entitled.IsError){Debug.LogWarning("After Hours add-ons: entitlement check failed: "+(entitled==null?"no answer":Why(entitled)));return StoreStart.NotEntitled;}
                return StoreStart.Ready;
            }
            catch(Exception e){Debug.LogException(e);return StoreStart.Unavailable;}
        }
        public async Task<List<AddOn>> Catalog(string[] skus)
        {
            var list=new List<AddOn>();
            var msg=await Oculus.Platform.IAP.GetProductsBySKU(skus);
            while(msg!=null&&!msg.IsError&&msg.Data!=null)
            {
                foreach(var p in msg.Data)list.Add(new AddOn{sku=p.Sku,name=p.Name,price=p.FormattedPrice,description=p.Description});
                if(!msg.Data.HasNextPage)break;msg=await Oculus.Platform.IAP.GetNextProductListPage(msg.Data);
            }
            if(msg!=null&&msg.IsError)Debug.LogWarning("After Hours add-ons: catalog: "+Why(msg));
            return list;
        }
        public async Task<OwnedAddOns> Owned()
        {
            var owned=new OwnedAddOns();
            var msg=await Oculus.Platform.IAP.GetViewerPurchases();
            if(msg==null||msg.IsError)
            {
                // Offline: the headset keeps its own copy of durable purchases.
                Debug.LogWarning("After Hours add-ons: purchases from the store failed ("+(msg==null?"no answer":Why(msg))+"), using the headset's copy");
                owned.fromCache=true;msg=await Oculus.Platform.IAP.GetViewerPurchasesDurableCache();
                if(msg==null||msg.IsError)return null;
            }
            while(msg!=null&&!msg.IsError&&msg.Data!=null)
            {
                foreach(var p in msg.Data)owned.skus.Add(p.Sku);
                if(!msg.Data.HasNextPage)break;msg=await Oculus.Platform.IAP.GetNextPurchaseListPage(msg.Data);
            }
            return owned;
        }
        public async Task<PurchaseResult> Buy(string sku)
        {
            var msg=await Oculus.Platform.IAP.LaunchCheckoutFlow(sku);
            if(msg==null)return PurchaseResult.Unavailable;
            if(!msg.IsError)return msg.Data!=null&&msg.Data.Sku==sku?PurchaseResult.Bought:PurchaseResult.Failed;
            var why=Why(msg);
            if(why.IndexOf("cancel",StringComparison.OrdinalIgnoreCase)>=0)return PurchaseResult.Cancelled;
            Debug.LogWarning("After Hours add-ons: checkout for "+sku+" failed: "+why);
            return PurchaseResult.Failed;
        }
    }

    // A store that charges nothing, for the editor and the automated checks. What it sells is every add-on in Outfits.All.
    // Purchases last until the editor closes, so a scene reload restores them the way a real store would.
    public class TestAddOnStore : IAddOnStore
    {
        public static readonly HashSet<string> Bought=new HashSet<string>();
        // For the checks: answer as if the network were down, or as if the player closed the checkout.
        public static bool Offline, CancelNext;
        public static void Reset(){Bought.Clear();Offline=false;CancelNext=false;}
        public string Name=>"Test store";
        public async Task<StoreStart> Start(){await Task.Yield();return StoreStart.Ready;}
        public async Task<List<AddOn>> Catalog(string[] skus)
        {
            await Task.Yield();
            return Outfits.AddOns.Where(p=>skus.Contains(p.sku)).Select(p=>new AddOn{sku=p.sku,name=p.name,price="$0.00 (test)",description="Test store item"}).ToList();
        }
        public async Task<OwnedAddOns> Owned(){await Task.Yield();return Offline?null:new OwnedAddOns{skus=Bought.ToList()};}
        public async Task<PurchaseResult> Buy(string sku)
        {
            await Task.Yield();
            if(Offline)return PurchaseResult.Unavailable;
            if(CancelNext){CancelNext=false;return PurchaseResult.Cancelled;}
            if(!Outfits.AddOns.Any(p=>p.sku==sku))return PurchaseResult.Failed;
            return Bought.Add(sku)?PurchaseResult.Bought:PurchaseResult.AlreadyOwned;
        }
    }
}
