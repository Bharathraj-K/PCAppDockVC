import whisper
import json
import sys
import warnings
import os

# Silence warnings
warnings.filterwarnings("ignore")

# Check file passed as arg
if len(sys.argv) < 2:
    print(json.dumps({"error": "No audio file provided"}))
    sys.exit(1)

audio_file = sys.argv[1]

# Check file exists
if not os.path.exists(audio_file):
    print(json.dumps({"error": f"File not found: {audio_file}"}))
    sys.exit(1)

try:
    model = whisper.load_model("base")
    result = model.transcribe(audio_file)
    print(json.dumps({"text": result["text"]}), flush=True)
except Exception as e:
    print(json.dumps({"error": str(e)}), flush=True)
    sys.exit(1)
