using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using System.Collections;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

public class VoiceAssistant : MonoBehaviour
{
    public Button micButton;
    public TMP_Text smallTextBox;

    public GameObject chatBubbleObject;
    public TMP_Text chatBubbleText;
    private CanvasGroup chatBubbleCanvasGroup;

    private DictationRecognizer dictationRecognizer;
    private string lastResult = "";

    private Dictionary<string, string> appDict = new();
    private string appsPath;

    private Coroutine fadeCoroutine;
    private bool isHoveringChatBubble = false;
    public float postHoverDelay = 2f;

    private bool isListening = false;
    private List<Dictionary<string, string>> conversationHistory = new();


    void Start()
    {
        chatBubbleCanvasGroup = chatBubbleObject.GetComponent<CanvasGroup>();
        appsPath = Path.Combine(Application.persistentDataPath, "vc_apps.json");
        LoadAppData();

        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.InitialSilenceTimeoutSeconds = 5;
        dictationRecognizer.AutoSilenceTimeoutSeconds = 10;

        dictationRecognizer.DictationResult += (text, confidence) =>
        {
            lastResult = text;
            smallTextBox.text = "You said: " + text;
            StopListening();
            HandleCommand(text.ToLower());
        };

        dictationRecognizer.DictationComplete += (completionCause) =>
        {
            isListening = false;
            if (string.IsNullOrEmpty(lastResult))
                smallTextBox.text = "Could not understand.";
        };

        dictationRecognizer.DictationError += (error, hresult) =>
        {
            isListening = false;
            smallTextBox.text = $"Error: {error}";
        };

        micButton.onClick.AddListener(OnMicClicked);

        chatBubbleObject.SetActive(false);
    }

    void OnMicClicked()
    {
        if (!isListening)
        {
            smallTextBox.text = "Listening...";
            lastResult = "";
            dictationRecognizer.Start();
            isListening = true;
        }
        else
        {
            StopListening();
            smallTextBox.text = !string.IsNullOrEmpty(lastResult) ? "You said: " + lastResult : "Stopped listening.";
        }
    }

    void StopListening(string reason = "")
    {
        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            dictationRecognizer.Stop();

        isListening = false;

        if (!string.IsNullOrEmpty(reason))
            smallTextBox.text = reason;
    }

    async void HandleCommand(string command)
    {
        if (command.StartsWith("open "))
        {
            string appName = command.Substring(5).Trim();
            if (appDict.ContainsKey(appName))
            {
                Process.Start(appDict[appName]);
                smallTextBox.text = "Opening " + appName;
            }
            else
            {
                bool add = await PromptYesNo($"{appName} not found. Add it? Say 'yes' or 'no'.");
                if (add)
                {
                    var paths = SFB.StandaloneFileBrowser.OpenFilePanel("Select App", "", "exe", false);
                    if (paths.Length > 0)
                    {
                        appDict[appName] = paths[0];
                        SaveAppData();
                        smallTextBox.text = $"{appName} added!";
                    }
                    else
                    {
                        smallTextBox.text = "No file selected.";
                    }
                }
                else
                {
                    smallTextBox.text = "Okay, not added.";
                }
                return;
            }
        }
        else
        {
            string aiReply = await AskAI(command);
            ShowChatBubble(aiReply);
        }
    }

    async System.Threading.Tasks.Task<bool> PromptYesNo(string promptText)
    {
        smallTextBox.text = promptText;
        lastResult = "";
        dictationRecognizer.Start();
        isListening = true;
        await WaitForResponse();
        isListening = false;
        return lastResult.ToLower().Contains("yes");
    }

    async System.Threading.Tasks.Task WaitForResponse()
    {
        while (string.IsNullOrEmpty(lastResult))
            await System.Threading.Tasks.Task.Delay(100);
    }

    async System.Threading.Tasks.Task<string> AskAI(string prompt)
    {
        // Add user message to history
        conversationHistory.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", prompt }
        });

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer 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");

        var data = new
        {
            model = "llama3-70b-8192",
            messages = conversationHistory
        };

        var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);
        var json = await response.Content.ReadAsStringAsync();

        try
        {
            var result = JObject.Parse(json);
            var reply = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No reply.";

            // Add AI response to history
            conversationHistory.Add(new Dictionary<string, string>
            {
                { "role", "assistant" },
                { "content", reply }
            });

            return reply;
        }
        catch
        {
            return "Error parsing response.";
        }
    }


    void ShowChatBubble(string reply)
    {
        chatBubbleText.text = reply;
        chatBubbleObject.SetActive(true);
        chatBubbleCanvasGroup.alpha = 0f;

        LeanTween.alphaCanvas(chatBubbleCanvasGroup, 1f, 0.4f);

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(AutoFadeChatBubble());
    }

    IEnumerator AutoFadeChatBubble()
    {
        const float minVisibleTime = 5f;
        yield return new WaitForSeconds(minVisibleTime);

        // Wait until the user is not hovering
        while (isHoveringChatBubble)
            yield return null;

        // Once they stop hovering, start post-hover delay
        float elapsed = 0f;
        while (elapsed < postHoverDelay)
        {
            if (isHoveringChatBubble)
            {
                // User re-entered, restart timer
                elapsed = 0f;
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Fade out and hide
        LeanTween.alphaCanvas(chatBubbleCanvasGroup, 0f, 0.4f).setOnComplete(() =>
        {
            chatBubbleObject.SetActive(false);
        });
    }


    void LoadAppData()
    {
        if (File.Exists(appsPath))
        {
            var json = File.ReadAllText(appsPath);
            appDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }

    void SaveAppData()
    {
        var json = JsonConvert.SerializeObject(appDict, Formatting.Indented);
        File.WriteAllText(appsPath, json);
    }

    public void OnChatBubbleEnter() => isHoveringChatBubble = true;
    public void OnChatBubbleExit() => isHoveringChatBubble = false;
}
