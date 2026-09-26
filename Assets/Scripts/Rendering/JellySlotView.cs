using UnityEngine;

namespace JellyField.Rendering
{
    // A tile and its four-part jelly are already present in the scene.
    public class JellySlotView : MonoBehaviour
    {
        [SerializeField]
        private JellyPieceView jelly;
        public JellyPieceView Jelly => jelly;
    }
}
