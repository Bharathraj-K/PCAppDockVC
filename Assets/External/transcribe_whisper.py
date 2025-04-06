import sys
import whisper

def transcribe(audio_path):
    model = whisper.load_model("base")
    result = model.transcribe(audio_path)
    print(result["text"])

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python transcribe_whisper.py path_to_audio.wav")
        sys.exit(1)

    transcribe(sys.argv[1])
