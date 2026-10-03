using UnityEngine;

namespace Roygbiv
{
    /// <summary>
    /// Everything the game knows about one color. One asset per color in Assets/_Project/Data/Colors.
    /// Designers edit these in the Inspector; code reads them through Game.Config.
    /// </summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Color Data", fileName = "Color_")]
    public class ColorData : ScriptableObject
    {
        public ColorId id;
        public string displayName;
        public Color tint = Color.white;
        [TextArea] public string emotion;

        [Header("Level")]
        [Tooltip("Scene that holds this color's district + boss fight. Must be in Build Settings.")]
        public string sceneName;

        [Header("Reward")]
        public AbilityId grantedAbility;
        [Tooltip("Story fragment played after the color is reclaimed.")]
        public DialogueData storyFragment;
        [Tooltip("Music stem that fades in once this color is restored (optional).")]
        public AudioClip musicLayer;
    }
}
