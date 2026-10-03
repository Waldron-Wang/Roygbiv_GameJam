using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roygbiv
{
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 5)] public string text;
    }

    /// <summary>A conversation / story fragment. Writers create these in Assets/_Project/Data/Dialogue.</summary>
    [CreateAssetMenu(menuName = "ROYGBIV/Dialogue", fileName = "Dialogue_")]
    public class DialogueData : ScriptableObject
    {
        public List<DialogueLine> lines = new();
    }
}
