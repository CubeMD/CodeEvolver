using System.Text;
using System.Threading.Tasks;
using GoogleApis.GenerativeLanguage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoogleApis.Example
{
    /// <summary>
    /// Audio understanding example introduced in Gemini 1.5 Pro
    /// </summary>
    public sealed class AudioExample : MonoBehaviour
    {
        [SerializeField]
        private AudioClip audioClip;

        [SerializeField]
        private TextMeshProUGUI resultLabel;

        [SerializeField]
        private Button sendButton;

        [SerializeField]
        [Multiline(10)]
        private string inputText;

        private readonly StringBuilder sb = new();
        private GenerativeModel model;

        private void Start()
        {
            model = GenerativeAIClient.GetModel(Models.Gemini_2_0_Flash);

            // Setup UIs
            sendButton.onClick.AddListener(async () => await SendRequest());
        }

        private async Task SendRequest()
        {
            // Add audio data to the message
            byte[] audioData = audioClip.ConvertToWav();
            var blob = new Blob("audio/wav", audioData);
            Content[] messages = { new(Role.user, inputText, blob), };

            sb.AppendTMPRichText(messages[0]);
            resultLabel.SetText(sb);

            var response = await model.GenerateContentAsync(messages, destroyCancellationToken);
            Debug.Log($"Response: {response}");

            if (response.Candidates.Length > 0)
            {
                var modelContent = response.Candidates[0].Content;
                sb.AppendTMPRichText(modelContent);
                resultLabel.SetText(sb);
            }
        }
    }
}
