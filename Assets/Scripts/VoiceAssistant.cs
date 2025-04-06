using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Windows.Speech;

public class VoiceAssistant : MonoBehaviour
{
    public Button micButton;
    public TMP_Text smallTextBox;
    public GameObject largeTextBoxObject;
    public TMP_Text largeText;

    private Dictionary<string, string> appDict = new();
    private string appsPath;

    private DictationRecognizer dictationRecognizer;
    private string lastResult = "";
    private bool isAwaitingYesNoResponse = false;

    void Start()
    {
        appsPath = Path.Combine(Application.persistentDataPath, "vc_apps.json");
        LoadAppData();

        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.InitialSilenceTimeoutSeconds = 5;
        dictationRecognizer.AutoSilenceTimeoutSeconds = 2;

        dictationRecognizer.DictationResult += (text, confidence) =>
        {
            lastResult = text;
            smallTextBox.text = "You said: " + text;
            dictationRecognizer.Stop();

            if (!isAwaitingYesNoResponse)
            {
                HandleCommand(text.ToLower());
            }
        };

        dictationRecognizer.DictationComplete += (completionCause) =>
        {
            if (completionCause != DictationCompletionCause.Complete)
            {
                smallTextBox.text = "Could not understand.";
            }
        };

        dictationRecognizer.DictationError += (error, hresult) =>
        {
            smallTextBox.text = $"Error: {error}";
        };

        micButton.onClick.AddListener(OnMicClicked);
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

    void OnMicClicked()
    {
        smallTextBox.text = "Listening...";
        lastResult = "";
        dictationRecognizer.Start();
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

                return; // Stop further processing
            }
        }
        else if (command.Contains("time"))
        {
            string timeNow = System.DateTime.Now.ToString("hh:mm tt");
            smallTextBox.text = "The current time is " + timeNow;
            return;
        }
        else if (command.Contains("date"))
        {
            string dateToday = System.DateTime.Now.ToString("dddd, MMMM dd, yyyy");
            smallTextBox.text = "Today's date is " + dateToday;
            return;
        }
        else
        {
            string aiResponse = await AskAI(command);
            largeTextBoxObject.SetActive(true);
            largeText.text = aiResponse;
        }
    }

    async System.Threading.Tasks.Task<bool> PromptYesNo(string promptText)
    {
        isAwaitingYesNoResponse = true;
        smallTextBox.text = promptText;
        lastResult = "";
        dictationRecognizer.Start();
        await WaitForResponse();
        isAwaitingYesNoResponse = false;
        return lastResult.ToLower().Contains("yes");
    }

    async System.Threading.Tasks.Task WaitForResponse()
    {
        while (string.IsNullOrEmpty(lastResult))
        {
            await System.Threading.Tasks.Task.Delay(100);
        }
    }

    async System.Threading.Tasks.Task<string> AskAI(string prompt)
    {
        using var client = new System.Net.Http.HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer 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");

        var data = new
        {
            model = "llama3-70b-8192",
            messages = new[] {
                new { role = "user", content = prompt }
            }
        };

        var content = new System.Net.Http.StringContent(
            JsonConvert.SerializeObject(data), System.Text.Encoding.UTF8, "application/json"
        );

        var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);
        string json = await response.Content.ReadAsStringAsync();
        UnityEngine.Debug.Log("Groq response: " + json);

        try
        {
            var result = JsonConvert.DeserializeObject<GroqResponse>(json);
            if (result?.choices != null && result.choices.Count > 0)
            {
                return result.choices[0].message.content;
            }
            else
            {
                return "No response from AI.";
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError("Error parsing AI response: " + ex.Message);
            return "Error processing response.";
        }
    }

    class GroqResponse
    {
        public List<Choice> choices { get; set; }
        public class Choice
        {
            public Message message { get; set; }
        }
        public class Message
        {
            public string content { get; set; }
        }
    }
}
