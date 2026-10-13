using TMPro;
using UnityEngine;
namespace AfterHours
{
    // A sign with one wording for hands and another for controllers. It follows the player's input without being rebuilt.
    public class InputText : MonoBehaviour
    {
        [TextArea] public string hands, controllers;
        TMP_Text text; int seen=-1;
        void OnEnable(){Apply();}
        void Update(){if(seen!=Wording.Version)Apply();}
        void Apply(){if(!text)text=GetComponent<TMP_Text>();if(text)text.text=Wording.Controllers?controllers:hands;seen=Wording.Version;}
    }
}
