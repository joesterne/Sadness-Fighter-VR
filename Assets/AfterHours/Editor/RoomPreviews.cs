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
                    // The rooftop is shown with its sunrise light, so it has its own shots below.
                    if(i==RooftopRoom.Room)continue;
                    var spawn=d.rooms[i].spawn.position;float eyeHeight=i==1?1.25f:1.6f;
                    saved+=Shot(d,i,cam,d.rooms[i].title,spawn+Vector3.up*eyeHeight,spawn+new Vector3(0,1.2f,6))?1:0;
                }
                var warehouse=d.rooms[2].root.transform.position;
                saved+=Shot(d,2,cam,"Heavy things - boxes",warehouse+new Vector3(.4f,1.55f,-2.2f),warehouse+new Vector3(0,.95f,.2f))?1:0;
                var kitchen=d.rooms[3].root.transform.position;
                saved+=Shot(d,3,cam,"A small spill - counter",kitchen+new Vector3(-1,1.6f,1.6f),kitchen+new Vector3(-.3f,1.3f,4.8f))?1:0;
                // The machine with its lever, gauge and steam wand, and the fifth cupboard open on the orange mug.
                saved+=Shot(d,3,cam,"A small spill - the machine",kitchen+new Vector3(.2f,1.5f,2.2f),kitchen+new Vector3(-.1f,1.6f,4.5f))?1:0;
                var cupboard=d.kitchen.doors[KitchenRoom.OrangeCabinet];cupboard.Snap(true);
                try{saved+=Shot(d,3,cam,"A small spill - the orange cupboard",kitchen+new Vector3(1.2f,1.3f,2f),kitchen+new Vector3(1.9f,.4f,4.7f))?1:0;}
                finally{cupboard.Snap(false);}
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
                saved+=TargetShot(d,cam,6,"The old résumé - send to the shredder")?1:0;
                // The boat's oars in their locks, the turning lighthouse light, and the same view at night.
                saved+=OceanShots(d,cam);
                // The rooftop at sunrise: the arrival view, the sun, the skyline both ways, the deck and what stands behind the player.
                saved+=RooftopShots(d,cam);
                // The rage room's computer and bat as a seated player sees them on arrival.
                var rage=d.rooms[RageRoom.Room];
                saved+=Shot(d,RageRoom.Room,cam,"The rage room - the computer",rage.spawn.position+Vector3.up*1.6f,rage.root.transform.TransformPoint(new Vector3(-.15f,.95f,-.2f)),"Seated - ")?1:0;
                // The quiet panel that appears once everything is broken, as the seated player sees it.
                if(d.rage&&d.rage.calm)
                {
                    d.rage.calm.SetActive(true);
                    try{saved+=Shot(d,RageRoom.Room,cam,"The rage room - the quiet",rage.spawn.position+Vector3.up*1.6f,rage.root.transform.TransformPoint(new Vector3(0,1.45f,1.35f)),"Seated - ")?1:0;}
                    finally{d.rage.calm.SetActive(false);}
                }
                // The ambush: the clowns at full size, a third of the way down, once everything is named, after standing up, and in the gentle version.
                saved+=AmbushShots(d,cam);
                // The mirror from where MIRROR on the desk puts you: the starter look, then a look made of earned pieces.
                saved+=MirrorShot(d,cam,"Starter look",new WardrobeState())?1:0;
                saved+=MirrorShot(d,cam,"Earned look",new WardrobeState{skin=4,hair=2,hairColour=2,build=1,top="jumper",head="beanie",neck="scarf",pin="step-pin"})?1:0;
                saved+=MirrorShot(d,cam,"Lanyard and cap",new WardrobeState{skin=0,hair=1,hairColour=3,build=2,top="cardigan",head="cap",neck="lanyard",pin="true-pin"})?1:0;
                saved+=MirrorShot(d,cam,"Orange mug pin",new WardrobeState{skin=2,hair=0,hairColour=1,build=1,top="shirt",head="no-hat",neck="no-neck",pin="orange-pin"})?1:0;
                saved+=MirrorShot(d,cam,"Let it out pin",new WardrobeState{skin=5,hair=4,hairColour=0,build=1,top="jacket",head="no-hat",neck="no-neck",pin="letitout-pin"})?1:0;
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

        static int OceanShots(GameDirector d,Camera cam)
        {
            var ocean=d.ocean;if(!ocean){Debug.LogError("[RoomPreviews] The scene has no ocean room.");return 0;}
            int n=0;var root=d.rooms[1].root.transform.position;var spin=ocean.beacon;var home=spin?spin.transform.localRotation:Quaternion.identity;
            try
            {
                // Turn the light so a beam crosses the view.
                if(spin)spin.transform.localRotation=Quaternion.Euler(0,55,0);
                var eye=root+new Vector3(0,1.6f,-1f);
                n+=Shot(d,1,cam,"Ocean - oars and light",eye,root+new Vector3(.6f,1.6f,6),"Day - ")?1:0;
                n+=Shot(d,1,cam,"Ocean - the right oar",root+new Vector3(.25f,1.65f,-1.25f),root+new Vector3(1.8f,.5f,.6f),"Day - ")?1:0;
                ocean.Preview(true);
                n+=Shot(d,1,cam,"Ocean at night - oars and light",eye,root+new Vector3(.6f,1.6f,6),"Night - ")?1:0;
                n+=Shot(d,1,cam,"Ocean at night - the sky",eye,root+new Vector3(-3,9,14),"Night - ")?1:0;
                n+=Shot(d,1,cam,"Ocean at night - the right oar",root+new Vector3(.25f,1.65f,-1.25f),root+new Vector3(1.8f,.5f,.6f),"Night - ")?1:0;
            }
            finally{ocean.Preview(false);if(spin)spin.transform.localRotation=home;}
            return n;
        }
        static int RooftopShots(GameDirector d,Camera cam)
        {
            var r=d.rooftop;if(!r){Debug.LogError("[RoomPreviews] The scene has no rooftop room.");return 0;}
            int n=0;int room=RooftopRoom.Room;var origin=d.rooms[room].root.transform.position;var spawn=d.rooms[room].spawn.position;var eye=spawn+Vector3.up*1.6f;
            Vector3 At(float x,float y,float z)=>origin+new Vector3(x,y,z);
            try
            {
                r.Preview();
                // The usual arrival view keeps its usual file name.
                n+=Shot(d,room,cam,d.rooms[room].title,eye,spawn+new Vector3(0,1.2f,6))?1:0;
                n+=Shot(d,room,cam,"Rooftop - the sunrise",eye,eye+RooftopRoom.SunDirection*40,"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the skyline on the left",eye,At(-30,6,40),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the skyline on the right",eye,At(34,6,26),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the deck",eye+new Vector3(0,0,2),At(-1,.4f,8),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the board",At(0,1.6f,1),At(0,2.9f,9),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the chairs",At(1.5f,1.5f,1.4f),At(0,.5f,5.3f),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the hut",At(-1.5f,1.6f,-1.2f),At(-6.8f,1.5f,-4.6f),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the water tower",At(2.2f,1.6f,-1.8f),At(6.9f,3.2f,-4.6f),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the balloon",eye,At(33,25,52),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the bunting",At(0,1.6f,0),At(2,5,10),"Sunrise - ")?1:0;
                n+=Shot(d,room,cam,"Rooftop - the sky",eye,At(-10,30,40),"Sunrise - ")?1:0;
            }
            finally{r.EndPreview();}
            return n;
        }
        static int AmbushShots(GameDirector d,Camera cam)
        {
            var a=d.ambush;if(!a){Debug.LogError("[RoomPreviews] The scene has no ambush room.");return 0;}
            int n=0;int room=AmbushRoom.Room;var root=d.rooms[room].root.transform;var eye=d.rooms[room].spawn.position+Vector3.up*1.6f;
            Vector3 At(float x,float y,float z)=>root.TransformPoint(new Vector3(x,y,z));
            try
            {
                a.Preview(0,false,false);
                n+=Shot(d,room,cam,"The ambush - looming",eye,At(0,3.4f,5),"Cool - ")?1:0;
                n+=Shot(d,room,cam,"The ambush - the table",eye,At(.3f,.95f,.7f),"Cool - ")?1:0;
                n+=Shot(d,room,cam,"The ambush - the left clown",eye,At(-2.6f,4.6f,5.5f),"Cool - ")?1:0;
                n+=Shot(d,room,cam,"The ambush - the paperwork",eye,At(1.9f,.9f,.7f),"Cool - ")?1:0;
                a.Preview(3,false,false);
                n+=Shot(d,room,cam,"The ambush - three named",eye,At(0,2.6f,4.5f),"Cool - ")?1:0;
                a.Preview(6,false,false);
                n+=Shot(d,room,cam,"The ambush - all named",eye,At(0,1.5f,3.4f),"Cool - ")?1:0;
                a.Preview(6,true,false);
                n+=Shot(d,room,cam,"The ambush - standing up",eye+Vector3.back*.9f,At(0,1.5f,3.4f),"Warm - ")?1:0;
                n+=Shot(d,room,cam,"The ambush - the way out",eye+Vector3.back*.9f,At(-3.6f,1.5f,9.5f),"Warm - ")?1:0;
                a.Preview(0,false,true);
                n+=Shot(d,room,cam,"The ambush - gentle",eye,At(0,2.6f,4.8f),"Cool - ")?1:0;
            }
            finally{a.EndPreview();}
            return n;
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
        static bool MirrorShot(GameDirector d,Camera cam,string name,WardrobeState look)
        {
            if(!d.wardrobe||!d.mirrorSpot){Debug.LogError("[RoomPreviews] The lobby has no mirror. Rebuild the scene first.");return false;}
            var avatar=d.wardrobe.avatar;
            try
            {
                avatar.Apply(look);
                var eye=d.mirrorSpot.position+Vector3.up*1.61f;var forward=d.mirrorSpot.forward;
                avatar.PoseFor(eye,Quaternion.LookRotation(forward),0);
                foreach(var text in d.wardrobe.GetComponentsInChildren<TMPro.TMP_Text>(true))text.ForceMeshUpdate();
                return Shot(d,0,cam,name,eye,eye+forward*3+Vector3.down*.3f,"Mirror - ");
            }
            finally{avatar.Apply(d.wardrobe.State);}
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
