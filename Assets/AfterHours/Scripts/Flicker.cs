using UnityEngine;
namespace AfterHours
{
    // A flame that will not hold still: it stretches and leans a little, never in step with the flames beside it.
    // The hat of flames on the mirror uses it.
    public class Flicker : MonoBehaviour
    {
        public float speed=7f, amount=.18f;
        Vector3 home;float seed;bool known;
        void OnEnable(){if(!known){home=transform.localScale;seed=Random.value*100;known=true;}}
        void Update()
        {
            float t=Time.time*speed+seed;
            float tall=1+amount*(Mathf.PerlinNoise(t,0)-.5f)*2,wide=1-amount*.5f*(Mathf.PerlinNoise(0,t)-.5f)*2;
            transform.localScale=new Vector3(home.x*wide,home.y*tall,home.z*wide);
        }
    }
}
