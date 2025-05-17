using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using GoogleApis.GenerativeLanguage;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Tool = GoogleApis.GenerativeLanguage.Tool;

[System.Serializable]
public class CodeVariantData
{
    public Transform parent;
    [HideInInspector] public GameObject spawnedObject;
    [HideInInspector] public string shaderName;
    [ReadOnly] [TextArea(10, 30)]
    public string shaderCode;
    [ReadOnly]
    public Material material;
    [HideInInspector] public string materialAssetPath;
}

public class CodeEvolverManager : MonoBehaviour
{
    private const string GENERATED_ASSETS_PATH = "Assets/GeneratedShaders";
    
    [SerializeField]
    private CodeVariantData[] variants = new CodeVariantData[3];
    
    public enum Variant
    {
        One,
        Two,
        Three
    }
    
    [SerializeField]
    private Variant selectedVariantToEvolve = Variant.One;
    [SerializeField][TextArea(10, 30)]
    private string evolvePrompt = "";
    [SerializeField] [TextArea(1, 10)]
    private string systemInstruction = "Generate a simple Unity URP unlit shader in .shader format.";
    
    [Header("Debug")]
    [ReadOnly] [SerializeField]
    [TextArea(10, 30)]
    private string messagesDebug = string.Empty;
    
    public GameObject baseMeshPrefab;
    private GenerativeModel model;
    private readonly List<Content> messages = new();
    private readonly StringBuilder sb = new();
    private readonly StringBuilder sbCode = new();
    private readonly StringBuilder promptBuilder = new();
    
    [ContextMenu("Evolve Code")]
    public void EvolveSelected()
    {
        SendRequestAsync().Forget();
    }

    private async UniTask SendRequestAsync()
    {
        messages.Clear();
        Debug.Log("SendRequestAsync started");
        model ??= GenerativeAIClient.GetModel(Models.Gemini_2_5_Pro_Preview); 
        
        string baseShaderCode = variants[(int)selectedVariantToEvolve].shaderCode;
        string effectiveEvolvePrompt = string.IsNullOrWhiteSpace(evolvePrompt) ? "Generate a new interesting shader." : evolvePrompt;

        for (int i = 0; i < variants.Length; i++)
        {
            Debug.Log($"--- Preparing to generate variant {i + 1} of {variants.Length} ---");
            promptBuilder.Clear();
            promptBuilder.AppendLine(effectiveEvolvePrompt);
            promptBuilder.AppendLine("\n---"); // Separator
            
            if (!string.IsNullOrWhiteSpace(baseShaderCode))
            {
                promptBuilder.AppendLine("Use the following shader code as a starting point or inspiration for the evolutions:");
                promptBuilder.AppendLine("```shader");
                promptBuilder.AppendLine(baseShaderCode);
                promptBuilder.AppendLine("```");
            }

            if (i > 0 && !string.IsNullOrEmpty(variants[i - 1].shaderCode))
            {
                promptBuilder.AppendLine("\n---"); // Separator
                promptBuilder.AppendLine("For this new Variant {i + 1}, please ensure it is distinct and offers a different approach or visual style compared to the *immediately preceding* Variant {i} which was:");
                promptBuilder.AppendLine("```shader");
                promptBuilder.AppendLine(variants[i - 1].shaderCode);
                promptBuilder.AppendLine("```");
                promptBuilder.AppendLine("Focus on creating something new and different while still adhering to the main goal.");
            }
            
            string finalInput = promptBuilder.ToString();
        
            Content content = new(Role.user, finalInput);
            messages.Add(content);
            DisplayDebugMessages();
            
            GenerateContentRequest request = new()
            {
                Contents = messages.ToArray(),
                Tools = new Tool[]
                {
                    new Tool.GoogleSearchRetrieval(),
                },
                SafetySettings = new List<SafetySetting>
                {
                    new ()
                    {
                        category = HarmCategory.HARM_CATEGORY_HARASSMENT,
                        threshold = HarmBlockThreshold.BLOCK_LOW_AND_ABOVE,
                    }
                },
                GenerationConfig = new GenerationConfig // Key change: Request multiple candidates
                {
                    CandidateCount = variants.Length, // Request as many candidates as we have variant slots
                }
            };
            
            if (!string.IsNullOrWhiteSpace(systemInstruction))
            {
                request.SystemInstruction = new Content(new Part[] { systemInstruction });
            }

            GenerateContentResponse response;
            try
            {
                response = await model.GenerateContentAsync(request, destroyCancellationToken);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during GenerativeModel.GenerateContentAsync: {e.Message}\n{e.StackTrace}");
                Clear();
                continue;
            }
            
            if (response.Candidates.Length == 0)
            {
                Debug.LogWarning("No candidates received from the model.");
                if (response.PromptFeedback != null)
                {
                    Debug.LogWarning($"Prompt Feedback: {response.PromptFeedback.BlockReason}");
                }
                continue;
            }

            Content modelContent = response.Candidates[0].Content;
            messages.Add(modelContent); // Add model response to history for potential multi-turn
            string extractedShaderCode = OutputCodeString(modelContent);
            
            if (string.IsNullOrWhiteSpace(extractedShaderCode))
            {
                Debug.LogWarning($"Extracted shader code for candidate {i} is empty. Full model response part(s):");
                foreach(Part part in modelContent.Parts) Debug.LogWarning(part.Text);
                continue;
            }
            
            await CreateVariantAsync(i, extractedShaderCode);
            DisplayDebugMessages(); // Display after each variant is processed
            
            if (i < variants.Length - 1) // Don't delay after the last one
            {
                await UniTask.Delay(System.TimeSpan.FromMilliseconds(500), cancellationToken: destroyCancellationToken); 
            }
        }
        
        Debug.Log("SendRequestAsync (sequential generation) is done.");
    }
    
    private void DisplayDebugMessages()
    {
        sb.Clear();
        foreach (Content message in messages)
        {
            sb.AppendTMPRichText(message);
        }

        messagesDebug = sb.ToString();
    }

    private async UniTask CreateVariantAsync(int index, string shaderCode)
    {
        if (index < 0 || index >= variants.Length)
        {
            Debug.LogError($"Index {index} is out of bounds for variants array.");
            return;
        }
        
        CodeVariantData variant = variants[index];
        if (variant.spawnedObject != null)
        {
            DestroyImmediate(variant.spawnedObject, true);
        }
        if (variant.material != null)
        {
            DestroyImmediate(variant.material, true);
        }

        variant.shaderCode = shaderCode;
        string guidSuffix = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        variant.shaderName = $"Custom/EvolvedShader_{index}_{guidSuffix}";
        
        if (!Directory.Exists(GENERATED_ASSETS_PATH))
        {
            Directory.CreateDirectory(GENERATED_ASSETS_PATH);
            AssetDatabase.Refresh();
            await UniTask.Delay(100, cancellationToken: destroyCancellationToken); // Brief pause for directory creation
        }
        
        string shaderFileName = $"EvolvedShader_{index}_{guidSuffix}.shader";
        string shaderPath = Path.Combine(GENERATED_ASSETS_PATH, shaderFileName);
        
        await File.WriteAllTextAsync(shaderPath, shaderCode);
        AssetDatabase.Refresh(); // Start the import process

        Shader shader = null;
        const int maxRetries = 200; // 200 * 100ms = 20 seconds timeout
        const int retryDelayMilliseconds = 100;
        bool shaderFoundAndCompiled = false;
        
        Debug.Log($"Attempting to load and compile shader: {variant.shaderName} at path: {shaderPath}");

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // It's often better to try loading the asset by path first during compilation,
                // as Shader.Find relies on an internal cache that might not update instantly.
                shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

                if (shader != null)
                {
#if UNITY_EDITOR
                    if (!ShaderUtil.ShaderHasError(shader))
                    {
                        Debug.Log($"Shader '{variant.shaderName}' found and compiled without errors after {attempt + 1} attempts.");
                        shaderFoundAndCompiled = true;
                        break;
                    }
                    else
                    {
                        Debug.LogWarning($"Shader '{variant.shaderName}' found but has compilation errors. Attempt {attempt + 1}/{maxRetries}. Retrying...");
                    }
#else
                    // If not in editor, assume found means compiled (no ShaderUtil available)
                    Debug.Log($"Shader '{variant.shaderName}' found (runtime assumption). Attempt {attempt + 1}.");
                    shaderFoundAndCompiled = true;
                    break;
#endif
                }
                else
                {
                    // If LoadAssetAtPath fails, try Shader.Find as a fallback
                    shader = Shader.Find(variant.shaderName);
                    if (shader != null) {
#if UNITY_EDITOR
                        if (!ShaderUtil.ShaderHasError(shader)) {
                            Debug.Log($"Shader '{variant.shaderName}' found by Shader.Find and compiled without errors after {attempt + 1} attempts.");
                            shaderFoundAndCompiled = true;
                            break;
                        } else {
                             Debug.LogWarning($"Shader '{variant.shaderName}' found by Shader.Find but has compilation errors. Attempt {attempt + 1}/{maxRetries}. Retrying...");
                        }
#else
                        Debug.Log($"Shader '{variant.shaderName}' found by Shader.Find (runtime assumption). Attempt {attempt + 1}.");
                        shaderFoundAndCompiled = true;
                        break;
#endif
                    } else {
                        Debug.LogWarning($"Shader '{variant.shaderName}' not found (LoadAssetAtPath and Shader.Find). Attempt {attempt + 1}/{maxRetries}. Waiting...");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Exception during shader load/check: {ex.Message}");
            }
            
            await UniTask.Delay(retryDelayMilliseconds, cancellationToken: destroyCancellationToken);
            
            // Occasionally, an explicit re-import can help if things get stuck
            if (attempt > 0 && attempt % 20 == 0) 
            { 
                // Every 2 seconds
                AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate);
            }
        }

        if (!shaderFoundAndCompiled)
        {
            Debug.LogError($"Failed to load/compile shader '{variant.shaderName}' at '{shaderPath}' after {maxRetries} attempts. The shader code might be invalid or there was an issue with the import process.");
            Debug.LogError($"Problematic shader code for '{variant.shaderName}':\n{shaderCode}");
            return; // Cannot proceed without a shader
        }

        variant.spawnedObject = Instantiate(baseMeshPrefab, variant.parent);
        variant.spawnedObject.name = $"Variant_{index}_{shaderFileName.Replace(".shader","")}";
        
        variant.material = new Material(shader);
        string materialFileName = $"Mat_EvolvedShader_{index}_{guidSuffix}.mat";
        variant.materialAssetPath = Path.Combine(GENERATED_ASSETS_PATH, materialFileName);

        AssetDatabase.CreateAsset(variant.material, variant.materialAssetPath);
        AssetDatabase.SaveAssets(); // Explicitly save to disk
        AssetDatabase.Refresh(); 
        
        Debug.Log($"Material asset created at: {variant.materialAssetPath}");
        
        Renderer rend = variant.spawnedObject.GetComponent<Renderer>();
        Material materialToApply = AssetDatabase.LoadAssetAtPath<Material>(variant.materialAssetPath);
        
        if (rend != null)
        {
            rend.sharedMaterial = materialToApply;
            variant.material = materialToApply;
        }
        else
        {
            Debug.LogWarning($"Spawned object for variant {index} does not have a Renderer component.");
        }
        
        Debug.Log($"Variant {index} created successfully with shader '{variant.shaderName}'.");
    }

    [ContextMenu("Clear All Variants")]
    public void Clear()
    {
        foreach (CodeVariantData variant in variants)
        {
            if (variant.spawnedObject != null)
            {
                DestroyImmediate(variant.spawnedObject);
                variant.spawnedObject = null;
            }
            if (variant.material != null)
            {
                DestroyImmediate(variant.material);
                variant.material = null;
            }
            variant.shaderCode = string.Empty;
            variant.shaderName = string.Empty;
        }
        
        messages.Clear();
        messagesDebug = string.Empty;
        Debug.Log("All variants cleared.");
    }

    private string OutputCodeString(Content content)
    {
        sbCode.Clear();
        
        foreach (Part part in content.Parts)
        {
            if(string.IsNullOrEmpty(part.Text))
                continue;

            sbCode.AppendLine(part.Text);
        }
        
        string rawOutput = sbCode.ToString().Trim();
        // Regex to extract code from Markdown blocks (e.g., ```shader ... ``` or ``` ... ```)
        // It looks for triple backticks, optionally followed by a language specifier, then captures content, until closing triple backticks.
        // RegexOptions.Singleline allows . to match newline characters.
        Match match = Regex.Match(rawOutput, @"```(?:shader|hlsl|glsl|cg|unityshader)?\s*([\s\S]+?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            // Return the captured group (the code itself)
            string extracted = match.Groups[1].Value.Trim();
            // Sometimes the model might still put "Shader..." inside the block, which is fine.
            // Basic validation: does it look like a shader?
            if (extracted.Contains("Shader") && extracted.Contains("{") && extracted.Contains("}"))
            {
                return extracted;
            }
            else
            {
                // If the extracted block doesn't look like a shader, it might be an explanation.
                // Fall through to see if shader code exists outside the block or if the raw output is better.
                Debug.LogWarning("Markdown block found but content doesn't appear to be a shader. Content: " + extracted);
            }
        }
        
        // Fallback: If no markdown block, or block content was suspicious,
        // try to find "Shader "..." as a starting point in the raw output.
        // This handles cases where the model might provide some introductory text.
        int shaderKeywordIndex = rawOutput.IndexOf("Shader \"");
        if (shaderKeywordIndex != -1)
        {
            // Check if we're inside a larger, unparsed markdown block accidentally
            string potentialShaderCode = rawOutput.Substring(shaderKeywordIndex);
            // A simple check for balanced braces might be too complex here.
            // We assume if "Shader "" is found, the rest is the shader.
            // This could be improved by finding the last '}' of the shader.
            if (potentialShaderCode.Contains("{") && potentialShaderCode.Contains("}")) // Basic sanity
            {
                return potentialShaderCode.Trim();
            }
        }
        
        // If all else fails, return the raw trimmed output.
        // This assumes the model might output just the code, or the above heuristics failed.
        // Final check: if the raw output itself looks like a shader.
        if (rawOutput.Contains("Shader") && rawOutput.Contains("{") && rawOutput.Contains("}"))
        {
            return rawOutput;
        }

        Debug.LogWarning("Could not reliably extract shader code from model response. Returning raw output. Raw: " + rawOutput);
        return rawOutput;
    }
}
