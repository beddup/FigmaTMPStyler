using UnityEngine;
using TMPro;

namespace FigmaTMPStyler
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class FigmaTextTMPMaterialGenerator : MonoBehaviour
    {
        [SerializeField] public string NodeLink; // Figma text node URL
    }
}
