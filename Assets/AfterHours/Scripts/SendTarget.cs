using UnityEngine;
namespace AfterHours
{
    // Somewhere a held object can be sent from where you sit: point at it while holding the object and let go.
    // The object glides there, so seated players never have to walk anything across the room.
    public class SendTarget : MonoBehaviour
    {
        public int room;
        // When set, only this object can be sent here (the coffee cup to the spout).
        public Grabbable only;
        // Where the object comes to rest, as a world-space offset from this object.
        public Vector3 landing;
        // How forgiving the aim is: the radius around the landing point, in metres, that still counts.
        public float radius=.6f;
        public GameObject highlight;
        public string prompt="Point here and let go.";
        public Vector3 LandingPoint=>transform.position+landing;
        public bool Accepts(Grabbable item)=>item&&!item.paddle&&!item.processed&&item.room==room&&(!only||only==item);
        public void Show(bool on){if(highlight&&highlight.activeSelf!=on)highlight.SetActive(on);}
    }
}
