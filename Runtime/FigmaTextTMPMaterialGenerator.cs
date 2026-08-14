using UnityEngine;
using TMPro;

namespace FigmaTMPStyler
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class FigmaTextTMPMaterialGenerator : MonoBehaviour
    {
        #if UNITY_EDITOR
        public string FigmaToken
        {
            get => UnityEditor.EditorPrefs.GetString("editor_figma_token", string.Empty);
            set => UnityEditor.EditorPrefs.SetString("editor_figma_token", value);
        }
        
        public string MaterialSavePath
        {
            get => UnityEditor.EditorPrefs.GetString("editor_material_save_path", string.Empty);
            set => UnityEditor.EditorPrefs.SetString("editor_material_save_path", value);
        }
        #endif

        [SerializeField] public string NodeLink; // Figma text node URL
    }
}
