using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.UI;
using TMPro;

namespace AfterHours
{
    public class VRPlayer : MonoBehaviour
    {
        public OVRCameraRig rig;
        public OVRHand leftHand, rightHand;
        public bool smoothMotion=true;
        public float moveSpeed=1.7f;
        public TextMeshPro hint;
        public Image fade;
        public Material rayMaterial;
        public ComfortMenu menu;
        public Renderer vignette;
        public Transform Head => rig.centerEyeAnchor;
        public bool IsXR => UnityEngine.XR.XRSettings.isDeviceActive;
        public bool busy;
        public bool seatedMode { get; private set; }
        public int seatedHeightIndex { get; private set; } = 1;
        public float SeatedEyeHeight => new[]{1.45f,1.65f,1.85f}[seatedHeightIndex];
        public bool comfortVignette { get; private set; } = true;
        Vector3 rigHome, hintHome; bool comfortReady, wasMenu, turnedAround;
        float vignetteLevel; MaterialPropertyBlock vignetteBlock; static readonly int VignetteStrength=Shader.PropertyToID("_Strength");
        CharacterController body; Transform desktopGrip;
        readonly Grabbable[] held=new Grabbable[2];
        readonly bool[] wasSelect=new bool[2], wasGrip=new bool[2];
        readonly LineRenderer[] rays=new LineRenderer[2];
        readonly Transform[] cursors=new Transform[2];
        readonly Vector3[] teleportPoint=new Vector3[2];
        readonly bool[] teleportReady=new bool[2];
        readonly bool[] teleportGesture=new bool[2], inputBlocked=new bool[2];
        readonly GameObject[] controllerModels=new GameObject[2];
        float turnReady, hintUntil; float pitch; bool wasHome;
        public void ShowHint(string text,float duration=6){hint.text=text;hintUntil=Time.time+duration;}
        void Awake()
        {
            // Interaction rays must never target the player's own capsule after a height change.
            gameObject.layer=2;
            rigHome=rig.transform.localPosition;
            seatedMode=PlayerPrefs.GetInt("AfterHours.Seated",0)==1;
            seatedHeightIndex=Mathf.Clamp(PlayerPrefs.GetInt("AfterHours.SeatedHeight",1),0,2);
            comfortVignette=PlayerPrefs.GetInt("AfterHours.Vignette",1)==1;
            if(hint)hintHome=hint.transform.localPosition;if(vignette)vignette.enabled=false;
            body=GetComponent<CharacterController>(); desktopGrip=new GameObject("Desktop carry point").transform; desktopGrip.SetParent(Head,false);desktopGrip.localPosition=new Vector3(0,-.25f,1.35f);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Left pointer":"Right pointer");go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=rayMaterial;line.positionCount=2;line.startWidth=.005f;line.endWidth=.009f;line.useWorldSpace=true;rays[i]=line;
                var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);Object.Destroy(dot.GetComponent<Collider>());dot.GetComponent<MeshFilter>().sharedMesh=Geometry.Get(PrimitiveType.Sphere);dot.name="Pointer target";dot.transform.SetParent(transform,false);dot.transform.localScale=Vector3.one*.035f;dot.GetComponent<Renderer>().sharedMaterial=rayMaterial;cursors[i]=dot.transform;
                var model=GameObject.CreatePrimitive(PrimitiveType.Capsule);Object.Destroy(model.GetComponent<Collider>());model.GetComponent<MeshFilter>().sharedMesh=Geometry.Get(PrimitiveType.Capsule);model.name="Controller grip";model.transform.SetParent(i==0?rig.leftControllerAnchor:rig.rightControllerAnchor,false);model.transform.localScale=new Vector3(.045f,.065f,.045f);model.GetComponent<Renderer>().sharedMaterial=rayMaterial;controllerModels[i]=model;
            }
        }
        void Update()
        {
            bool menuOpen=menu&&menu.IsOpen;
            // Messages move above the open menu so it never hides them.
            if(hint){hint.gameObject.SetActive(Time.time<hintUntil);hint.transform.localPosition=menuOpen?hintHome+Vector3.up*.52f:hintHome;}
            rig.enabled=IsXR; if(!IsXR) DesktopPose();
            if(!comfortReady&&(!IsXR||Head.localPosition.y>.3f))ApplyHeight();
            bool home=IsXR?OVRInput.Get(OVRInput.Button.Two):Keyboard.current!=null&&Keyboard.current.escapeKey.isPressed;
            bool returnPressed=home&&!wasHome;wasHome=home;
            // The left controller's menu button. With tracked hands, Quest reports the left palm pinch as the same button.
            bool menuHeld=IsXR?OVRInput.Get(OVRInput.Button.Start):Keyboard.current!=null&&Keyboard.current.tabKey.isPressed;
            bool menuPressed=menuHeld&&!wasMenu;wasMenu=menuHeld;
            if(busy){HideRays();SetVignette(0,true);return;}
            if(menuPressed&&menu)menu.Toggle();
            Move();
            if(returnPressed)GameDirector.Instance.Travel(0);
            for(int i=0;i<2&&!busy;i++)Interact(i);
        }
        void DesktopPose()
        {
            rig.centerEyeAnchor.localPosition=new Vector3(0,1.65f,0);
            if(Mouse.current!=null&&Mouse.current.rightButton.isPressed)
            { var d=Mouse.current.delta.ReadValue();transform.Rotate(0,d.x*.12f,0);pitch=Mathf.Clamp(pitch-d.y*.12f,-65,65);Head.localRotation=Quaternion.Euler(pitch,0,0); }
            if(Keyboard.current!=null)
            {float turn=(Keyboard.current.eKey.isPressed?1:0)-(Keyboard.current.qKey.isPressed?1:0);transform.Rotate(0,turn*70*Time.deltaTime,0);}
        }
        void Move()
        {
            var local=transform.InverseTransformPoint(Head.position);body.height=Mathf.Clamp(local.y,.8f,2.2f);body.center=new Vector3(local.x,body.height/2,local.z);
            Vector2 left=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,OVRInput.Controller.LTouch);
            Vector2 right=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,OVRInput.Controller.RTouch);
            if(!IsXR&&Keyboard.current!=null)
            {var k=Keyboard.current;left=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));}
            bool aboard=GameDirector.Instance.currentRoom==1;
            bool walking=smoothMotion&&!aboard;float speed=0;
            if(walking)
            {
                var f=Vector3.ProjectOnPlane(Head.forward,Vector3.up).normalized;var r=Vector3.Cross(Vector3.up,f);
                var step=Vector3.ClampMagnitude(f*left.y+r*left.x,1);speed=step.magnitude;
                body.Move((step*moveSpeed+Vector3.down*2)*Time.deltaTime);
            }
            else if(!aboard)body.Move(Vector3.down*2*Time.deltaTime);
            // The comfort vignette softens the edges of the view only while the stick is walking.
            SetVignette(comfortVignette&&speed>.1f?Mathf.Lerp(.6f,1,speed):0,false);
            // Snap turns: the right stick always, and the left stick too whenever it is not walking (teleport only, or in the boat).
            // Pulling a turning stick straight back turns around, so a seated player never has to twist in the chair.
            var turn=right;if(!walking&&left.sqrMagnitude>right.sqrMagnitude)turn=left;
            if(Mathf.Abs(turn.x)>.7f&&Mathf.Abs(turn.x)>-turn.y&&Time.time>turnReady){SnapTurn(Mathf.Sign(turn.x)*30);turnReady=Time.time+.35f;}
            else if(turn.y<-.8f&&Mathf.Abs(turn.x)<.5f&&!turnedAround){SnapTurn(180);turnedAround=true;}
            if(Mathf.Abs(turn.x)<.2f)turnReady=0;
            if(turn.y>-.3f)turnedAround=false;
        }
        void SetVignette(float target,bool instant)
        {
            if(!vignette)return;
            vignetteLevel=instant?target:Mathf.MoveTowards(vignetteLevel,target,Time.deltaTime*(target>vignetteLevel?5:2.5f));
            bool show=vignetteLevel>.005f;if(vignette.enabled!=show)vignette.enabled=show;
            if(show){if(vignetteBlock==null)vignetteBlock=new MaterialPropertyBlock();vignetteBlock.SetFloat(VignetteStrength,vignetteLevel);vignette.SetPropertyBlock(vignetteBlock);}
        }
        void RotateAroundHead(float degrees){transform.RotateAround(Head.position,Vector3.up,degrees);}
        public void SnapTurn(float degrees){RotateAroundHead(degrees);}
        // Turns the player so the room's main view is straight ahead again, wherever the chair points.
        public void FaceForward()
        {
            var d=GameDirector.Instance;RotateAroundHead(Mathf.DeltaAngle(Head.eulerAngles.y,d.rooms[d.currentRoom].spawn.eulerAngles.y));
            ShowHint("The room is in front of you again.",3);
        }
        // In seated view a teleport also turns the player to face where they pointed, so turning never needs the chair.
        public static float LandingYaw(Vector3 head,Vector3 point,float fallback)
        {var flat=Vector3.ProjectOnPlane(point-head,Vector3.up);return flat.sqrMagnitude>.09f?Quaternion.LookRotation(flat).eulerAngles.y:fallback;}
        public void ToggleSeated()
        {
            seatedMode=!seatedMode;ApplyHeight();SaveComfort();
            ShowHint(seatedMode?"Seated view on. Eye height "+EyeText+". Right stick turns; pull it back to turn around. Teleports turn you to face where you point.":"Standing view on. Your tracked height is restored.");
        }
        public void CycleSeatedHeight()
        {
            // From standing, the first press switches on seated view at the saved height.
            if(seatedMode)seatedHeightIndex=(seatedHeightIndex+1)%3;else seatedMode=true;
            ApplyHeight();SaveComfort();
            ShowHint("Seated eye height: "+EyeText+". Change it again whenever you like.");
        }
        public void ToggleVignette()
        {
            comfortVignette=!comfortVignette;SaveComfort();
            ShowHint(comfortVignette?"Comfort vignette on. The edges of your view soften while you walk with the stick.":"Comfort vignette off.",5);
        }
        string EyeText=>SeatedEyeHeight.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" m";
        void ApplyHeight()
        {
            // Raise the entire tracked rig, including hands, while leaving the floor and collision body grounded.
            // Calibrate once per change; do not cancel the player's natural head movement each frame.
            float rawHeight=Head.position.y-transform.position.y-(rig.transform.localPosition.y-rigHome.y);
            float lift=seatedMode?Mathf.Clamp(SeatedEyeHeight-rawHeight,-1.2f,1.5f):0;
            rig.transform.localPosition=rigHome+Vector3.up*lift;comfortReady=true;
            if(GameDirector.Instance)GameDirector.Instance.RefreshComfort();
            // Keep an open menu at the same reach from the new eye height.
            if(menu&&menu.IsOpen)menu.Open();
        }
        void SaveComfort(){PlayerPrefs.SetInt("AfterHours.Seated",seatedMode?1:0);PlayerPrefs.SetInt("AfterHours.SeatedHeight",seatedHeightIndex);PlayerPrefs.SetInt("AfterHours.Vignette",comfortVignette?1:0);PlayerPrefs.Save();}
        void Interact(int index)
        {
            bool desktop=!IsXR;
            if(desktop&&index==0){rays[index].enabled=false;cursors[index].gameObject.SetActive(false);controllerModels[index].SetActive(false);return;}
            OVRHand hand=index==0?leftHand:rightHand;OVRInput.Controller controller=index==0?OVRInput.Controller.LTouch:OVRInput.Controller.RTouch;
            bool trackedHand=!desktop&&hand&&hand.IsTracked&&hand.HandConfidence==OVRHand.TrackingConfidence.High;
            bool trackedController=!desktop&&OVRInput.IsControllerConnected(controller)&&OVRInput.GetControllerPositionTracked(controller);
            Transform grip=desktop?desktopGrip:trackedHand?hand.transform:index==0?rig.leftControllerAnchor:rig.rightControllerAnchor;
            Transform aim=desktop?Head:trackedHand&&hand.IsPointerPoseValid?hand.PointerPose:grip;
            bool valid=desktop||trackedHand||trackedController;
            bool select=valid&&(desktop?(Mouse.current!=null&&Mouse.current.leftButton.isPressed):trackedHand?!hand.IsSystemGestureInProgress&&hand.GetFingerIsPinching(OVRHand.HandFinger.Index):OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,controller)>.65f);
            bool grab=valid&&(trackedHand?select:desktop?select:OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger,controller)>.65f);
            controllerModels[index].SetActive(trackedController&&!trackedHand);
            if(!valid){if(held[index])held[index].Release();held[index]=null;wasGrip[index]=wasSelect[index]=false;teleportReady[index]=teleportGesture[index]=false;inputBlocked[index]=true;rays[index].enabled=false;cursors[index].gameObject.SetActive(false);return;}
            Vector3 origin=aim.position, direction=aim.forward;
            bool hit=Physics.Raycast(origin,direction,out RaycastHit info,14,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            rays[index].enabled=!held[index];rays[index].SetPosition(0,origin);rays[index].SetPosition(1,hit?info.point:origin+direction*5);
            cursors[index].gameObject.SetActive(hit&&!held[index]);if(hit)cursors[index].position=info.point;
            // A held trigger or pinch must be released after travel or tracking recovery before it can act again.
            if(inputBlocked[index])
            {wasSelect[index]=select;wasGrip[index]=grab;if(!select&&!grab)inputBlocked[index]=false;return;}
            var target=hit?info.collider.GetComponentInParent<Interactable>():null;
            var item=hit?info.collider.GetComponentInParent<Grabbable>():null;
            if(!held[index] && grab&&!wasGrip[index])
            {
                Grabbable nearby=null;float closest=.23f;
                foreach(var col in Physics.OverlapSphere(grip.position,.23f,~0,QueryTriggerInteraction.Ignore))
                {var candidate=col.GetComponentInParent<Grabbable>();if(candidate&&!candidate.held&&!candidate.processed){float d=Vector3.Distance(grip.position,col.ClosestPoint(grip.position));if(d<closest){nearby=candidate;closest=d;}}}
                var pick=nearby?nearby:item;
                // Close grabs hold the object where the hand closed on it. Distance grabs bring it along the pointer, just clear of the hand.
                Vector3? offset=desktop?grip.forward*.13f:nearby?(Vector3?)null:pick?direction*(.06f+Extent(pick,direction)):Vector3.zero;
                if(pick&&(nearby||info.distance<(desktop?5:3.5f))&&pick.Take(grip,offset)){held[index]=pick;ShowHint(pick.paddle?"Pull the paddle toward you, then reach forward.":pick.label+"  /  Release to place",3);}
            }
            bool floorTarget=hit&&info.collider.GetComponent<TeleportSurface>()&&info.normal.y>.85f&&GameDirector.Instance.currentRoom!=1;
            if(select&&!wasSelect[index])
            {
                // Lock the purpose of this press. A button that moves the view must never become a floor teleport.
                teleportGesture[index]=!held[index]&&!target&&floorTarget;
                if(!held[index]&&target){target.Activate();Pulse(controller);}
                else if(teleportGesture[index])ShowHint("Release to teleport",2);
            }
            if(teleportGesture[index]&&!busy)
            {
                teleportReady[index]=!held[index]&&floorTarget;
                if(teleportReady[index])teleportPoint[index]=info.point;
                if(!select&&wasSelect[index]&&teleportReady[index])StartCoroutine(Teleport(teleportPoint[index],seatedMode?LandingYaw(Head.position,teleportPoint[index],Head.eulerAngles.y):Head.eulerAngles.y));
            }
            if(!select){teleportReady[index]=false;teleportGesture[index]=false;}
            if(held[index]&&!grab){held[index].Release();held[index]=null;}
            wasSelect[index]=select;wasGrip[index]=grab;
        }
        static float Extent(Grabbable item,Vector3 direction)
        {
            var bounds=new Bounds(item.transform.position,Vector3.zero);foreach(var c in item.GetComponentsInChildren<Collider>())bounds.Encapsulate(c.bounds);
            var e=bounds.extents;return Mathf.Abs(direction.x)*e.x+Mathf.Abs(direction.y)*e.y+Mathf.Abs(direction.z)*e.z;
        }
        public void ReleaseAll(){for(int i=0;i<2;i++){if(held[i])held[i].Release();held[i]=null;teleportReady[i]=teleportGesture[i]=false;inputBlocked[i]=true;}}
        void HideRays(){for(int i=0;i<2;i++){rays[i].enabled=false;cursors[i].gameObject.SetActive(false);}}
        public void Place(Vector3 floor,float yaw)
        {
            ReleaseAll();body.enabled=false;float delta=yaw-Head.eulerAngles.y;transform.RotateAround(Head.position,Vector3.up,delta);
            Vector3 offset=Head.position-transform.position;offset.y=0;transform.position=floor-offset;body.enabled=true;
        }
        public void Shift(Vector3 delta){body.enabled=false;transform.position+=delta;body.enabled=true;}
        IEnumerator Teleport(Vector3 point,float yaw)
        {
            if(busy)yield break;
            // Capsule checks reject tables, props, and walls at the landing point.
            Vector3 feet=point+Vector3.up*.04f;
            foreach(var obstacle in Physics.OverlapCapsule(feet+Vector3.up*.24f,feet+Vector3.up*(Mathf.Max(body.height,1.1f)-.24f),.24f,~0,QueryTriggerInteraction.Ignore))
                if(!obstacle.transform.IsChildOf(transform))yield break;
            busy=true;yield return Fade(1,.12f);Place(point+Vector3.up*.025f,yaw);yield return Fade(0,.18f);busy=false;
        }
        public IEnumerator Fade(float target,float duration)
        {
            if(!fade)yield break;float from=fade.color.a;float t=0;
            while(t<duration){t+=Time.unscaledDeltaTime;fade.color=new Color(.025f,.04f,.07f,Mathf.Lerp(from,target,t/duration));yield return null;}
            fade.color=new Color(.025f,.04f,.07f,target);
        }
        void Pulse(OVRInput.Controller controller){StartCoroutine(Haptic(controller));}
        IEnumerator Haptic(OVRInput.Controller controller){if(IsXR)OVRInput.SetControllerVibration(.25f,.3f,controller);yield return new WaitForSeconds(.06f);if(IsXR)OVRInput.SetControllerVibration(0,0,controller);}
        void OnGUI()
        {
            if(IsXR)return;
            GUI.color=new Color(.07f,.12f,.18f,.94f);GUI.DrawTexture(new Rect(0,Screen.height-46,Screen.width,46),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(20,Screen.height-36,Screen.width-30,30),"AFTER HOURS    |    WASD walk   •   Right mouse look / Q E turn   •   Click interact / hold to carry   •   Click floor to teleport   •   Tab menu   •   Esc lobby");
            GUI.Label(new Rect(Screen.width/2-4,Screen.height/2-10,20,20),"+");
        }
    }
}
