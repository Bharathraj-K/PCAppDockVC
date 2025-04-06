using UnityEngine;
using System.IO;

public class MicrophoneRecorder : MonoBehaviour
{
    private AudioClip clip;

    public void StartRecording()
    {
        clip = Microphone.Start(null, false, 5, 16000);
    }

    public string StopRecording()
    {
        Microphone.End(null);
        string path = Path.Combine(Application.persistentDataPath, "recorded.wav");
        byte[] wavData = WavUtility.FromAudioClip(clip);
        File.WriteAllBytes(path, wavData);
        return path;
    }
}
