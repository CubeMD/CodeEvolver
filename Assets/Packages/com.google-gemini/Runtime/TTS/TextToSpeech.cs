using System.Threading;
using System.Threading.Tasks;

namespace GoogleApis.TTS
{
    /// <summary>
    /// Text to Speech Client using Google Cloud Text-to-Speech API
    /// https://cloud.google.com/text-to-speech/docs/reference/rest
    /// </summary>
    public static class TextToSpeech
    {
        private const string BASE_URL = "https://texttospeech.googleapis.com/v1beta1";
        private static readonly string uriVoicesList;
        private static readonly string uriTextSynthesize;

        static TextToSpeech()
        {
            uriVoicesList = $"{BASE_URL}/voices?key={GoogleApiKey.apiKey}";
            uriTextSynthesize = $"{BASE_URL}/text:synthesize?key={GoogleApiKey.apiKey}";
        }

        /// <summary>
        /// Returns a list of Voice supported for synthesis.
        /// 
        /// https://cloud.google.com/text-to-speech/docs/reference/rest/v1beta1/voices/list
        /// </summary>
        public static async Task<VoicesResponse> ListVoicesAsync(string languageCode, CancellationToken cancellationToken)
        {
            string url = string.IsNullOrWhiteSpace(languageCode)
                ? uriVoicesList
                : $"{uriVoicesList}&languageCode={languageCode}";
            return await Api.GetJsonAsync<VoicesResponse>(url, cancellationToken);
        }

        /// <summary>
        /// https://cloud.google.com/text-to-speech/docs/reference/rest/v1beta1/text/synthesize
        /// </summary>
        /// <param name="message"></param>
        public static async Task<TextSynthesizeResponse> SynthesizeAsync(
            TextSynthesizeRequest requestBody, CancellationToken cancellationToken)
        {
            return await Api.PostJsonAsync<TextSynthesizeRequest, TextSynthesizeResponse>(
                uriTextSynthesize, requestBody, cancellationToken);
        }
    }
}
