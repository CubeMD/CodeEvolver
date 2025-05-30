using System.Collections.Generic;
using System.Text;
using GoogleApis.GenerativeLanguage;
using TMPro;
using UnityEngine;

namespace GoogleApis.Example
{
    public sealed class JsonResponseExample : MonoBehaviour
    {
        /// <summary>
        /// JSON Schema of the response data.
        /// </summary>
        public class ResponseData
        {
            /// <summary>
            /// List of recipes.
            /// </summary>
            public Recipe[] recipes;
        }

        /// <summary>
        /// Recipe.
        /// </summary>
        public class Recipe
        {
            public string name;
            public string[] ingredients;
            public string[] instructions;
        }

        [SerializeField]
        [TextArea(3, 20)]
        private string prompt =
@"List a few popular cookie recipes in JSON with the following format:

## JSON Format:
{
    recipes: [
        {
            name: 'Chocolate Chip Cookies',
            ingredients: ['flour', 'sugar', 'chocolate chips'],
            instructions: [
                'Mix flour and sugar',
                'Add chocolate chips',
                'Bake'
            ]
        },
        {
            name: 'Peanut Butter Cookies',
            ingredients: ['flour', 'sugar', 'peanut butter'],
            instructions: [
                'Mix flour and sugar',
                'Add peanut butter',
                'Bake'
            ]
        }
    ]    
}";

        [SerializeField]
        private TextMeshProUGUI messageLabel;

        private GenerativeModel model;
        private static readonly StringBuilder sb = new();

        private async void Start()
        {
            model = GenerativeAIClient.GetModel(Models.Gemini_2_0_Flash);

            // Send request
            var config = new GenerationConfig();
            config.SetJsonMode(typeof(ResponseData));

            var request = new GenerateContentRequest()
            {
                Contents = new List<Content>()
                {
                    new(Role.user, prompt)
                },
                GenerationConfig = config,
            };

            var response = await model.GenerateContentAsync(request, destroyCancellationToken);
            if (response.Candidates.Length > 0)
            {
                var modelContent = response.Candidates[0].Content;
                sb.AppendTMPRichText(modelContent);
            }
            else
            {
                sb.Append("No response");
            }
            messageLabel.SetText(sb);
        }
    }
}
