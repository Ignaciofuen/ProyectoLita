"""
lita_brain.py - VERSIÓN DEFINITIVA
Servidor de cerebro para Lita: Whisper STT + Ollama LLM
"""

import whisper
import ollama
import tempfile
import os
import re
from flask import Flask, request, jsonify
from datetime import datetime

app = Flask(__name__)

# ─────────────────────────────────────────────
# CONFIGURACION
# ─────────────────────────────────────────────

OLLAMA_MODEL = "llama3.2"

SYSTEM_PROMPT = """Eres Lita, una asistente virtual alegre, dulce y un poco tímida que vive en la computadora del usuario.
Respondes siempre en español latino, de forma muy breve y natural (máximo 2 oraciones).
Tienes memoria de conversaciones pasadas y recuerdas detalles del usuario.

REGLAS ESTRICTAS:
- Escribe SOLO lo que vas a decir en voz alta, nada más
- NUNCA uses etiquetas <think> ni expliques tu proceso mental
- NUNCA respondas solo con etiquetas, siempre escribe texto real primero
- Al final de tu respuesta incluye UNA etiqueta de emoción obligatoria
- Opcionalmente puedes incluir UNA etiqueta de animación

Etiquetas de emoción: [happy] [sad] [angry] [surprised] [neutral] [confused]
Etiquetas de animación: [dance] [wave] [bashful] [thankful] [guitar] [texting]

Ejemplos correctos:
- "¡Hola! Me alegra mucho verte hoy. [happy] [wave]"
- "Mmm, eso es muy interesante... [confused]"
- "¡Eso me pone muy feliz! [happy]"

Si el usuario dice su nombre, úsalo en la respuesta.
Responde según tu estado de ánimo indicado en el contexto."""

# ─────────────────────────────────────────────
# CARGAR WHISPER
# ─────────────────────────────────────────────

print("Cargando Whisper...")
whisper_model = whisper.load_model("tiny")
print("Whisper listo!")

# Respuestas de error variadas para que no suene repetitivo
ERROR_RESPONSES = [
    "Estoy aquí. ¿De qué quieres hablar? [happy]",
    "Perdona, me distraje un momento. ¿Qué me decías? [confused]",
    "¡Aquí estoy! Cuéntame algo. [happy]",
    "Hmm, no escuché bien. ¿Puedes repetirlo? [confused]",
]
error_index = 0

# ─────────────────────────────────────────────
# HELPERS
# ─────────────────────────────────────────────

def clean_reply(text: str) -> str:
    """Limpia el texto de etiquetas de pensamiento y caracteres extraños."""
    # Eliminar bloques <think>
    text = re.sub(r'<think>.*?</think>', '', text, flags=re.DOTALL)
    # Eliminar asteriscos de markdown
    text = re.sub(r'\*+', '', text)
    # Eliminar múltiples espacios y saltos de línea
    text = re.sub(r'\s+', ' ', text)
    return text.strip()

def has_real_text(text: str) -> bool:
    """Verifica que el texto tenga contenido real además de etiquetas."""
    cleaned = re.sub(r'\[.*?\]', '', text).strip()
    return len(cleaned) >= 3

def ensure_emotion_tag(text: str) -> str:
    """Asegura que siempre haya una etiqueta de emoción."""
    emotions = ["[happy]", "[sad]", "[angry]", "[surprised]", "[neutral]", "[confused]"]
    if not any(e in text.lower() for e in emotions):
        text += " [neutral]"
    return text

def get_error_response() -> str:
    """Devuelve una respuesta de error variada."""
    global error_index
    response = ERROR_RESPONSES[error_index % len(ERROR_RESPONSES)]
    error_index += 1
    return response

# ─────────────────────────────────────────────
# ENDPOINTS
# ─────────────────────────────────────────────

@app.route("/transcribe", methods=["POST"])
def transcribe():
    if "audio" not in request.files:
        return jsonify({"error": "No audio file"}), 400

    audio_file = request.files["audio"]

    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        audio_file.save(tmp.name)
        tmp_path = tmp.name

    try:
        result = whisper_model.transcribe(tmp_path, language="es")
        text   = result["text"].strip()

        # Filtrar ruido y textos muy cortos
        if len(text) < 2:
            return jsonify({"text": ""})

        # Filtrar transcripciones de silencio comunes de Whisper
        noise_phrases = ["gracias", "suscríbete", "subtítulos", "música"]
        if any(p in text.lower() for p in noise_phrases) and len(text) < 15:
            return jsonify({"text": ""})

        print(f"[Whisper] Transcripcion: '{text}'")
        return jsonify({"text": text})

    except Exception as e:
        print(f"[Whisper] Error: {e}")
        return jsonify({"error": str(e)}), 500
    finally:
        if os.path.exists(tmp_path):
            os.unlink(tmp_path)


@app.route("/chat", methods=["POST"])
def chat():
    data = request.get_json(force=True)

    if not data or "message" not in data:
        return jsonify({"error": "No message"}), 400

    message = data.get("message", "").strip()
    history = data.get("history", [])

    if not message:
        return jsonify({"error": "Mensaje vacío"}), 400

    # Construir mensajes con historial
    messages = [{"role": "system", "content": SYSTEM_PROMPT}]

    recent_history = history[-5:] if len(history) > 5 else history
    for h in recent_history:
        role    = h.get("role", "user")
        content = h.get("content", "")
        if role in ("user", "assistant") and content:
            messages.append({"role": role, "content": content})

    messages.append({"role": "user", "content": message})

    print(f"[Ollama] Procesando: '{message[:50]}...' " if len(message) > 50 else f"[Ollama] Procesando: '{message}'")

    try:
        response = ollama.chat(
            model=OLLAMA_MODEL,
            messages=messages,
            think=False,  # Desactiva pensamiento extendido de qwen
            options={
                "temperature": 0.75,
                "top_p":       0.9,
                "num_predict": 90,  # Respuestas más cortas = más rápido
                "repeat_penalty": 1.1  # Evita repeticiones
            }
        )

        reply = clean_reply(response["message"]["content"])
        print(f"[Ollama] Respuesta raw: '{reply}'")

        # Salvavidas si no hay texto real
        if not has_real_text(reply):
            print("[Ollama] Salvavidas activado")
            reply = get_error_response()
        else:
            reply = ensure_emotion_tag(reply)

        print(f"[Ollama] Respuesta final: '{reply}'")
        return jsonify({"reply": reply})

    except Exception as e:
        print(f"[Ollama] Error: {e}")
        return jsonify({"error": str(e)}), 500


@app.route("/reset", methods=["POST"])
def reset():
    return jsonify({"status": "ok", "message": "Historial reseteado"})


@app.route("/health", methods=["GET"])
def health():
    return jsonify({
        "status":  "ok",
        "model":   OLLAMA_MODEL,
        "whisper": "base",
        "time":    datetime.now().strftime("%H:%M:%S")
    })


@app.route("/models", methods=["GET"])
def list_models():
    try:
        models = ollama.list()
        return jsonify({"models": [m["name"] for m in models.get("models", [])]})
    except Exception as e:
        return jsonify({"error": str(e)}), 500


@app.route("/change_model", methods=["POST"])
def change_model():
    global OLLAMA_MODEL
    data  = request.get_json(force=True)
    model = data.get("model", "").strip()
    if not model:
        return jsonify({"error": "No model specified"}), 400
    OLLAMA_MODEL = model
    return jsonify({"status": "ok", "model": OLLAMA_MODEL})


# ─────────────────────────────────────────────
# INICIO
# ─────────────────────────────────────────────

if __name__ == "__main__":
    print("=" * 60)
    print("  LITA BRAIN SERVER")
    print(f"  Modelo  : {OLLAMA_MODEL}")
    print(f"  Whisper : base")
    print(f"  URL     : http://localhost:5000")
    print("=" * 60)
    app.run(host="0.0.0.0", port=5000, debug=False)