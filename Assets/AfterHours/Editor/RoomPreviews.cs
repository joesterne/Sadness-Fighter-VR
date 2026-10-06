using System.IO;
using UnityEditor;
using UnityEngine;
namespace AfterHours.Editor
{
    // Renders stills of every room, the comfort menu and the walking vignette from the player's point of view,
    // so art and comfort changes can be reviewed without a headset.
    public static class RoomPreviews
    {
        const string Folder="Documentation/Previews/";

        [MenuItem("After Hours/Capture room previews",false,100)]
        public static void Capture()
        {
            if(EditorApplication.isPlaying){Debug.LogError("[RoomPreviews] Exit Play Mode first.");return;}
            // Render the saved scene, not editor state left over from a rebuild.
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(!active.isDirty&&active.path==AfterHoursBuilder.ScenePath)UnityEditor.SceneManagement.EditorSceneManager.OpenScene(AfterHoursBuilder.ScenePath);
            var d=Object.FindAnyObjectByType<GameDirector>(FindObjectsInactive.Include);
            if(!d||d.rooms==null){Debug.LogError("[RoomPreviews] Open "+AfterHoursBuilder.ScenePath+" first.");return;}
            var wasActive=new bool[d.rooms.Length];for(int i=0;i<d.rooms.Length;i++)wasActive[i]=d.rooms[i].root.activeSelf;
            var eye=d.player.rig.centerEyeAnchor.GetComponent<Camera>();
            var go=new GameObject("Room preview camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();
            cam.CopyFrom(eye);cam.stereoTargetEye=StereoTargetEyeMask.None;cam.fieldOfView=78;cam.targetTexture=null;
            bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            Directory.CreateDirectory(Folder);int saved=0;
            try
            {
                for(int i=0;i<d.rooms.Length;i++)
                {
                    var spawn=d.rooms[i].spawn.position;float eyeHeight=i==1?1.25f:1.6f;
                    saved+=Shot(d,i,cam,d.rooms[i].title,spawn+Vector3.up*eyeHeight,spawn+new Vector3(0,1.2f,6))?1:0;
                }
                var warehouse=d.rooms[2].root.transform.position;
                saved+=Shot(d,2,cam,"Heavy things - boxes",warehouse+new Vector3(.4f,1.55f,-2.2f),warehouse+new Vector3(0,.95f,.2f))?1:0;
                var kitchen=d.rooms[3].root.transform.position;
                saved+=Shot(d,3,cam,"A small spill - counter",kitchen+new Vector3(-1,1.6f,1.6f),kitchen+new Vector3(-.3f,1.3f,4.8f))?1:0;
                var lobby=d.rooms[0].root.transform.position;
                saved+=Shot(d,0,cam,"The lobby - memory office",lobby+new Vector3(-2.2f,1.6f,7.5f),lobby+new Vector3(-4.6f,1,12))?1:0;
                // The comfort menu as it opens for a seated player, and the walking vignette at full stick.
                saved+=MenuShot(d,cam,0,"Comfort menu - lobby")?1:0;
                saved+=MenuShot(d,cam,3,"Comfort menu - kitchen")?1:0;
                saved+=VignetteShot(d,cam)?1:0;
                // What a seated player sees: the first-visit guide, and each room's destinations lit as if aimed at.
                saved+=GuideShot(d,cam)?1:0;
                saved+=TargetShot(d,cam,2,"Heavy things - send to the truck")?1:0;
                saved+=TargetShot(d,cam,3,"A small spill - send the cup")?1:0;
                saved+=TargetShot(d,cam,4,"The infinite archive - send to a tray")?1:0;
            }
            finally
            {
                if(d.menu)d.menu.gameObject.SetActive(false);
                if(d.intro)d.intro.Hide();
                ShaderUtil.allowAsyncCompilation=async;
                for(int i=0;i<d.rooms.Length;i++)d.rooms[i].root.SetActive(wasActive[i]);
                Object.DestroyImmediate(go);
            }
            Debug.Log("[RoomPreviews] Saved "+saved+" previews to "+Path.GetFullPath(Folder));
        }

        static bool MenuShot(GameDirector d,Camera cam,int room,string name)
        {
            if(!d.menu){Debug.LogError("[RoomPreviews] The scene has no comfort menu. Rebuild it first.");return false;}
            int was=d.currentRoom;d.currentRoom=room;
            try
            {
                var eye=d.rooms[room].spawn.position+Vector3.up*1.65f;var forward=d.rooms[room].spawn.forward;
                d.menu.OpenAt(eye,forward);
                foreach(var text in d.menu.GetComponentsInChildren<TMPro.TMP_Text>(true))text.ForceMeshUpdate();
                return Shot(d,room,cam,name,eye,eye+forward*2+Vector3.down*.5f,"Menu - ");
            }
            finally{d.menu.gameObject.SetActive(false);d.currentRoom=was;}
        }
        static bool GuideShot(GameDirector d,Camera cam)
        {
            if(!d.intro){Debug.LogError("[RoomPreviews] The scene has no first-visit guide. Rebuild it first.");return false;}
            try
            {
                d.intro.Begin();
                foreach(var text in d.intro.GetComponentsInChildren<TMPro.TMP_Text>(true))text.ForceMeshUpdate();
                var eye=d.rooms[0].spawn.position+Vector3.up*1.6f;
                return Shot(d,0,cam,"First visit guide",eye,d.intro.transform.position+Vector3.down*.05f,"Seated - ");
            }
            finally{d.intro.Hide();}
        }
        static bool TargetShot(GameDirector d,Camera cam,int room,string name)
        {
            var targets=d.rooms[room].root.GetComponentsInChildren<SendTarget>(true);
            if(targets.Length==0){Debug.LogError("[RoomPreviews] "+d.rooms[room].title+" has no send targets. Rebuild the scene first.");return false;}
            try
            {
                var look=Vector3.zero;foreach(var t in targets){t.Show(true);look+=t.LandingPoint;}look/=targets.Length;
                var eye=d.rooms[room].spawn.position+Vector3.up*1.6f;
                return Shot(d,room,cam,name,eye,look,"Seated - ");
            }
            finally{foreach(var t in targets)t.Show(false);}
        }
        static bool VignetteShot(GameDirector d,Camera cam)
        {
            var vignette=d.player.vignette;if(!vignette){Debug.LogError("[RoomPreviews] The player has no comfort vignette. Rebuild the scene first.");return false;}
            // Outside Play Mode the eye anchor sits at floor level, so stand the vignette in front of a standing eye instead.
            var quad=vignette.transform;var home=(quad.localPosition,quad.localRotation);bool was=vignette.enabled;
            var eye=d.rooms[0].spawn.position+Vector3.up*1.6f;var forward=d.rooms[0].spawn.forward;
            quad.SetPositionAndRotation(eye+forward*.3f,Quaternion.LookRotation(forward));vignette.enabled=true;
            var block=new MaterialPropertyBlock();block.SetFloat("_Strength",1);vignette.SetPropertyBlock(block);
            try{return Shot(d,0,cam,"Walking vignette",eye,eye+forward*4+Vector3.down*.4f,"Comfort - ");}
            finally{vignette.SetPropertyBlock(null);vignette.enabled=was;quad.localPosition=home.Item1;quad.localRotation=home.Item2;}
        }

        static bool Shot(GameDirector d,int room,Camera cam,string name,Vector3 from,Vector3 to,string prefix="LowPoly - ")
        {
            for(int i=0;i<d.rooms.Length;i++)d.rooms[i].root.SetActive(i==room);
            cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(to-from));
            const int width=1600,height=1000;
            var rt=new RenderTexture(width,height,24);var previous=RenderTexture.active;
            try
            {
                cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
                var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(Folder+prefix+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);return true;
            }
            catch(System.Exception e){Debug.LogError("[RoomPreviews] "+name+": "+e.Message);return false;}
            finally{cam.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);}
        }
    }
}
