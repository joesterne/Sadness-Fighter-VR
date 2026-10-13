using TMPro;
using UnityEngine;
namespace AfterHours
{
    // Marks a sign that only guides or reassures (headings, progress boards, instructions). Clean mode switches these off.
    // Labels on buttons, doors and objects are never marked, so everything stays reachable.
    public class Guidance : MonoBehaviour
    {
        TMP_Text text;
        // A plate that exists only to back this sign goes away with it (the builder keeps it out of the merged scenery).
        public GameObject[] backing;
        public void Apply(bool clean)
        {
            if(!text)text=GetComponent<TMP_Text>();if(text)text.enabled=!clean;
            if(backing!=null)foreach(var plate in backing)if(plate)plate.SetActive(!clean);
        }
    }
}
