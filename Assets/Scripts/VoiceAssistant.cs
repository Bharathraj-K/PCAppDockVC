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
using System.Linq;

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
    private bool isPromptingYesNo = false;
    private bool awaitingResponseAfterPrompt = false;

    private List<Dictionary<string, string>> conversationHistory = new();

    void Start()
    {
        chatBubbleCanvasGroup = chatBubbleObject.GetComponent<CanvasGroup>();
        appsPath = Path.Combine(Application.persistentDataPath, "vc_apps.json");
        LoadAppData();

        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.InitialSilenceTimeoutSeconds = 5;
        dictationRecognizer.AutoSilenceTimeoutSeconds = 10;

        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;

        micButton.onClick.AddListener(OnMicClicked);
        chatBubbleObject.SetActive(false);
    }

    void OnMicClicked()
    {
        if (!isListening)
        {
            lastResult = "";
            smallTextBox.text = "Listening...";
            if (dictationRecognizer.Status != SpeechSystemStatus.Running)
                dictationRecognizer.Start();
            isListening = true;
        }
        else
        {
            StopListening("Stopped listening.");
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

    void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        UnityEngine.Debug.Log($"Dictation Result: {text}");
        lastResult = text;
        smallTextBox.text = "You said: " + text;
        StopListening();

        if (!isPromptingYesNo)
        {
            _ = HandleCommand(text.ToLower());
        }
    }

    void OnDictationComplete(DictationCompletionCause cause)
    {
        UnityEngine.Debug.Log("Dictation Complete: " + cause);
        isListening = false;

        if (isPromptingYesNo && !awaitingResponseAfterPrompt)
        {
            smallTextBox.text = "No response. Timed out.";
            isPromptingYesNo = false;
        }
    }

    void OnDictationError(string error, int hresult)
    {
        isListening = false;
        UnityEngine.Debug.LogError($"Dictation Error: {error} ({hresult})");
        smallTextBox.text = $"Error: {error}";
    }

    async System.Threading.Tasks.Task HandleCommand(string command)
    {
        if (command.StartsWith("open "))
        {
            string appName = command.Substring(5).Trim().ToLower();

            if (appDict.ContainsKey(appName))
            {
                Process.Start(appDict[appName]);
                smallTextBox.text = "Opening " + appName;
            }
            else
            {
                await PromptAndAddApp(appName);
            }

        }
        else
        {
            string aiReply = await AskAI(command);
            ShowChatBubble(aiReply);
        }
    }

    async System.Threading.Tasks.Task<bool?> PromptYesNo(string promptText)
    {
        UnityEngine.Debug.Log("PromptYesNo started...");
        isPromptingYesNo = true;
        lastResult = "";
        awaitingResponseAfterPrompt = true;
        smallTextBox.text = promptText;

        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();

        async void BeginListening()
        {
            await System.Threading.Tasks.Task.Delay(500); // wait before starting recognizer

            dictationRecognizer.DictationResult += HandlePromptResult;
            dictationRecognizer.Start();
            isListening = true;
        }

        void HandlePromptResult(string text, ConfidenceLevel confidence)
        {
            UnityEngine.Debug.Log($"Prompt Result: {text}");
            dictationRecognizer.DictationResult -= HandlePromptResult;
            dictationRecognizer.Stop();

            isPromptingYesNo = false;
            isListening = false;
            awaitingResponseAfterPrompt = false;

            text = text.ToLower().Trim();
            if (text == "yes")
                tcs.TrySetResult(true);
            else if (text == "no")
                tcs.TrySetResult(false);
            else
            {
                smallTextBox.text = "Invalid response. Say 'yes' or 'no'.";
                tcs.TrySetResult(null);
            }
        }

        BeginListening();

        var delayTask = System.Threading.Tasks.Task.Delay(10000); // timeout
        var completed = await System.Threading.Tasks.Task.WhenAny(tcs.Task, delayTask);

        if (completed == delayTask)
        {
            dictationRecognizer.DictationResult -= HandlePromptResult;
            dictationRecognizer.Stop();
            smallTextBox.text = "No response. Timed out.";
            isPromptingYesNo = false;
            return null;
        }

        UnityEngine.Debug.Log("PromptYesNo finished.");
        return tcs.Task.Result;
    }

    async System.Threading.Tasks.Task<string> AskAI(string prompt)
    {
        conversationHistory.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", prompt }
        });

        using var client = new HttpClient();
        string apiKey = LoadAPIKey();
        if (string.IsNullOrEmpty(apiKey))
            return "API key not found. Please add it to " + Application.persistentDataPath + "/groq_api.txt";

        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

        var data = new
        {
            model = "llama3-70b-8192",
            messages = conversationHistory
        };

        var content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);
        }
        catch
        {
            return "Network error contacting Groq.";
        }

        var json = await response.Content.ReadAsStringAsync();

        try
        {
            var result = JObject.Parse(json);
            var reply = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "No reply.";

            conversationHistory.Add(new Dictionary<string, string>
            {
                { "role", "assistant" },
                { "content", reply }
            });

            return reply;
        }
        catch
        {
            return "Error parsing AI response.";
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

        while (isHoveringChatBubble)
            yield return null;

        float elapsed = 0f;
        while (elapsed < postHoverDelay)
        {
            if (isHoveringChatBubble)
            {
                elapsed = 0f;
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        LeanTween.alphaCanvas(chatBubbleCanvasGroup, 0f, 0.4f).setOnComplete(() =>
        {
            chatBubbleObject.SetActive(false);
        });
    }

    string LoadAPIKey()
    {
        string path = Path.Combine(Application.persistentDataPath, "groq_api.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    void LoadAppData()
    {
        if (File.Exists(appsPath))
        {
            var json = File.ReadAllText(appsPath);
            appDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                .ToDictionary(kvp => kvp.Key.ToLower(), kvp => kvp.Value);
        }
    }

    void SaveAppData()
    {
        var json = JsonConvert.SerializeObject(appDict, Formatting.Indented);
        File.WriteAllText(appsPath, json);
    }

    async System.Threading.Tasks.Task PromptAndAddApp(string appName)
    {
        await System.Threading.Tasks.Task.Yield();

        var result = await PromptYesNo($"{appName} not found. Add it? Say 'yes' or 'no'.");

        if (result == true)
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
        else if (result == false)
        {
            smallTextBox.text = "Okay, not added.";
        }
        else
        {
            smallTextBox.text = "No response. Timed out.";
        }
    }

    public void OnChatBubbleEnter() => isHoveringChatBubble = true;
    public void OnChatBubbleExit() => isHoveringChatBubble = false;
}
