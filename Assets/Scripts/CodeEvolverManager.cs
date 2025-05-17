using UnityEditor;
using UnityEngine;

[System.Serializable]
public class CodeVariantData
{
    public Transform parent;
    [HideInInspector] public GameObject spawnedObject;
    [HideInInspector] public string shaderName;
    [TextArea(10, 30)]
    public string shaderCode;
    [HideInInspector] public Material material;
}

public class CodeEvolverManager : MonoBehaviour
{
    public CodeVariantData[] variants = new CodeVariantData[3];
    
    public enum Variant
    {
        One,
        Two,
        Three
    }
    
    public Variant selectedVariantToEvolve = Variant.One;
    [TextArea(10, 30)]
    public string evolvePrompt = "";
    
    public GameObject baseMeshPrefab;
    
    public void CreateVariant(int index, string shaderCode)
    {
        CodeVariantData variant = variants[index];
        if (variant.spawnedObject != null)
        {
            DestroyImmediate(variant.spawnedObject);
        }

        variant.shaderCode = shaderCode;
        variant.shaderName = $"Custom/EvolvedShader_{index}_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";

        string shaderPath = $"Assets/Generated/{variant.shaderName.Replace("/", "_")}.shader";
        System.IO.File.WriteAllText(shaderPath, shaderCode);
        AssetDatabase.Refresh();

        Shader shader = Shader.Find(variant.shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"Shader not yet compiled: {variant.shaderName}");
            return;
        }

        variant.material = new Material(shader);
        variant.spawnedObject = Instantiate(baseMeshPrefab, variant.parent);
        variant.spawnedObject.name = $"Variant_{index}";
        variant.spawnedObject.GetComponent<Renderer>().sharedMaterial = variant.material;
    }
    
    [ContextMenu("Evolve Code")]
    public void EvolveSelected()
    {
        string baseShader = variants[(int)selectedVariantToEvolve].shaderCode;
        string prompt = evolvePrompt;

        // 🔧 Simulated LLM responses:
        string[] evolvedCodes = new string[3]
        {
            baseShader + "\n// Evolved #1",
            baseShader + "\n// Evolved #2",
            baseShader + "\n// Evolved #3"
        };

        for (int i = 0; i < variants.Length; i++)
        {
            CreateVariant(i, evolvedCodes[i]);
        }
    }
}
