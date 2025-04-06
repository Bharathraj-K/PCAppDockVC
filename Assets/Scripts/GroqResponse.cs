[System.Serializable]
public class GroqResponse
{
    public Choice[] choices;

    [System.Serializable]
    public class Choice
    {
        public Message message;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }
}
