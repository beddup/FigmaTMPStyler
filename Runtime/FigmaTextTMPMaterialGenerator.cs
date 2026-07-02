using UnityEngine;
using TMPro;

namespace FigmaTMPStyler
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class FigmaTextTMPMaterialGenerator : MonoBehaviour
    {

        public string FigmaToken
        {
            get => PlayerPrefs.GetString("figma_token", string.Empty);
            set => PlayerPrefs.SetString("figma_token", value);
        }
        
        public string MaterialSavePath
        {
            get => PlayerPrefs.GetString("material_save_path", string.Empty);
            set => PlayerPrefs.SetString("material_save_path", value);
        }
        [SerializeField] public string ParentLink; // because there isn't a link for figma text.
    }
}