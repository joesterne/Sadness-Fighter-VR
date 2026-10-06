using System.Globalization;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // Comfort and room options. The left controller's menu button, a left-hand palm pinch, or Tab on desktop opens and closes it.
    // It opens at arm's length just below eye level, so every option is in reach from a chair, and it moves and turns with the player.
    public class ComfortMenu : MonoBehaviour
    {
        public GameDirector director;
        public TextMeshPro status, seated, height, walking, vignette, crowd;
        public GameObject lobby, tryAgain;
        public bool IsOpen => gameObject.activeSelf;
        const float Distance=.65f, Drop=.2f;

        public void Toggle()
        {
            if(IsOpen){Close();director.PlayUi(director.menuClose,.45f);}
            else{Open();director.PlayUi(director.menuOpen,.45f);}
        }
        public void Open()
        {
            var head=director.player.Head;
            var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
            // Looking straight down: the top of the head still points the way the player faces.
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(head.up,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=director.player.transform.forward;
            OpenAt(head.position,forward);
        }
        public void OpenAt(Vector3 eye,Vector3 forward)
        {
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            // Tilted to face the eye from below, so the whole panel is square to the line of sight.
            var rotation=Quaternion.LookRotation(forward)*Quaternion.Euler(Mathf.Atan2(Drop,Distance)*Mathf.Rad2Deg,0,0);
            var centre=forward*Distance+Vector3.down*Drop;
            // Facing a wall or a counter, the menu comes closer and shrinks to match: it looks the same, but stays in front.
            float fit=1;foreach(var corner in Corners)fit=Mathf.Min(fit,Clearance(eye,centre+rotation*corner));
            transform.SetPositionAndRotation(eye+centre*fit,rotation);transform.localScale=Vector3.one*fit;
            Refresh();gameObject.SetActive(true);
            // The menu travels with the player. Its buttons must never push the player's body.
            var body=director.player.GetComponent<CharacterController>();
            if(body)foreach(var c in GetComponentsInChildren<Collider>())Physics.IgnoreCollision(c,body,true);
        }
        public void Close(){if(IsOpen)gameObject.SetActive(false);}
        // The panel's centre and corners, relative to its pivot.
        static readonly Vector3[] Corners={Vector3.zero,new Vector3(-.27f,.25f,0),new Vector3(.27f,.25f,0),new Vector3(-.27f,-.27f,0),new Vector3(.27f,-.27f,0)};
        float Clearance(Vector3 eye,Vector3 offset)
        {
            float length=offset.magnitude,fit=1;
            foreach(var hit in Physics.RaycastAll(eye,offset/length,length+.05f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(director.player.transform))fit=Mathf.Min(fit,(hit.distance-.05f)/length);
            return Mathf.Clamp(fit,.3f,1);
        }
        public void Refresh()
        {
            var p=director.player;int room=director.currentRoom;
            if(lobby)lobby.SetActive(room!=0);
            if(tryAgain)tryAgain.SetActive(room>=1&&room<=4);
            if(status&&director.rooms!=null&&room<director.rooms.Length)status.text=director.rooms[room].title.ToUpperInvariant();
            string eye=p.SeatedEyeHeight.ToString("0.00",CultureInfo.InvariantCulture)+" m";
            Set(seated,"SEATED VIEW\n"+(p.seatedMode?"ON":"OFF"));
            Set(height,"SEATED HEIGHT\n"+new[]{"LOW","MID","HIGH"}[p.seatedHeightIndex]+"  "+eye);
            Set(walking,"MOVEMENT\n"+(p.smoothMotion?"WALK + TELEPORT":"TELEPORT ONLY"));
            Set(vignette,"COMFORT VIGNETTE\n"+(p.comfortVignette?"ON":"OFF"));
            Set(crowd,"KITCHEN CROWD\n"+(director.gentleAudience?"HIDDEN":"SHOWN"));
        }
        static void Set(TextMeshPro label,string text){if(label)label.text=text;}
        public Interactable Button(ActionKind kind)
        {foreach(var b in GetComponentsInChildren<Interactable>(true))if(b.kind==kind)return b;return null;}
    }
}
