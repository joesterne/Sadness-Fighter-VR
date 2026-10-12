using TMPro;
using UnityEngine;
namespace AfterHours
{
    // Marks a sign that only guides or reassures (headings, progress boards, instructions). Clean mode switches these off.
    // Labels on buttons, doors and objects are never marked, so everything stays reachable.
    public class Guidance : MonoBehaviour
    {
        TMP_Text text;
        public void Apply(bool clean){if(!text)text=GetComponent<TMP_Text>();if(text)text.enabled=!clean;}
    }
}
