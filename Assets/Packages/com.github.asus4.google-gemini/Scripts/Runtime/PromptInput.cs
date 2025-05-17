using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

namespace LlmUI
{
    [UxmlElement]
    public sealed partial class PromptInput : VisualElement
    {

        const string prefix = "prompt-input";

        TextField textInput = null;
        TextField TextInput
            => textInput ??= this.Q<TextField>($"{prefix}__text-field");
        Button sendButton = null;
        Button SendButton
            => sendButton ??= this.Q<Button>($"{prefix}__send-button");

        bool isInitialized = false;
        Action<string> onSend;
        public event Action<string> OnSend
        {
            add
            {
                Initialize();
                onSend += value;
            }
            remove => onSend -= value;
        }

        public string Text
        {
            get => TextInput.value;
            set => TextInput.value = value;
        }

        [CreateProperty]
        public bool CanSend
            => TextInput != null && !string.IsNullOrEmpty(TextInput.value);

        public PromptInput()
        {
            dataSource = this;
        }

        void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            SendButton.clicked += () =>
            {
                var text = TextInput.value;
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                onSend?.Invoke(text);
            };

            isInitialized = true;
        }
    }
}
