using UnityEngine;

namespace Roygbiv
{
    /// <summary>Drop in a scene to set its music. The audio designer's only touch point per scene.</summary>
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] AudioClip music;

        void Start()
        {
            if (music) Game.Audio.PlayMusic(music);
        }
    }
}
