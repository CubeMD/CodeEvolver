using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoogleApis.TTS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace GoogleApis.Example
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class TextToSpeechExample : MonoBehaviour
    {
        [Header("UI references")]
        [SerializeField]
        private TMP_InputField inputField;
        [SerializeField]
        private Button sendButton;

        [SerializeField]
        private string languageCode = "en-US";


        private VoiceSelectionParams voiceSelectionParams;
        private AudioConfig audioConfig;
        private AudioSource audioSource;

        private async void Start()
        {
            audioSource = GetComponent<AudioSource>();

            voiceSelectionParams = new VoiceSelectionParams(languageCode, null, null);
            audioConfig = new AudioConfig()
            {
                audioEncoding = AudioEncoding.MP3_64_KBPS,
            };

            // Setup UIs
            sendButton.onClick.AddListener(async () => await SendRequest());
            inputField.onSubmit.AddListener(async _ => await SendRequest());
            // for Debug
            inputField.text = "I have 57 cats, each owns 44 mittens, how many mittens is that in total?";

            // Check all available voices
            // https://cloud.google.com/text-to-speech/docs/voices
            VoicesResponse voices = await TextToSpeech.ListVoicesAsync(string.Empty, destroyCancellationToken);
            Debug.Log($"Voices: {voices}");
        }

        private async Task SendRequest()
        {
            var input = inputField.text;
            if (string.IsNullOrEmpty(input))
            {
                return;
            }
            inputField.text = string.Empty;

            TextSynthesizeRequest requestBody = new(
                input: input,
                voice: voiceSelectionParams,
                audioConfig: audioConfig
            );
            var response = await TextToSpeech.SynthesizeAsync(requestBody, destroyCancellationToken);
            Debug.Log($"response: {response}");

            // Play audio
            if (audioSource.clip != null)
            {
                Destroy(audioSource.clip);
            }
            var audioClip = await response.ToAudioClipAsync(destroyCancellationToken);
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }
}
