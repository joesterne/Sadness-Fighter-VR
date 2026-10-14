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
        // A target that is a long way off over props (the ambush's clowns, across a table of cards and papers) is not hidden by them: only walls and furniture block the aim.
        public bool seeThrough;
        public string prompt="Point here and let go.";
        public Vector3 LandingPoint=>transform.position+landing;
        // When set, anything of this kind can be sent here (the espresso spout takes any mug).
        public string category;
        public bool Accepts(Grabbable item)=>item&&!item.paddle&&!item.processed&&item.room==room&&(!only||only==item)&&(string.IsNullOrEmpty(category)||category==item.category);
        // Something is being sent here. For a target that takes a kind of object, whatever of that kind already rests at the spot goes back where it started,
        // so two never end up in each other.
        public void Clear(Grabbable incoming)
        {
            var d=GameDirector.Instance;if(!d||string.IsNullOrEmpty(category))return;
            foreach(var g in d.Grabbables)
                if(g&&g!=incoming&&g.isActiveAndEnabled&&Accepts(g)&&!g.held&&!g.sending&&Vector3.Distance(g.transform.position,LandingPoint)<.32f)g.ReturnHome();
        }
        public void Show(bool on){if(highlight&&highlight.activeSelf!=on)highlight.SetActive(on);}
    }
}
