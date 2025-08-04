# -*- coding:utf-8 -*-
import sys
import io
import socket
import threading
import logging
import argparse
import os
import time
import traceback
import ssl
# --- 依赖库导入 ---
import psutil
from gradio_client import Client, handle_file
import pyaudio
import websocket
import base64
import hashlib
import hmac
import json
from urllib.parse import urlencode
from datetime import datetime
from time import mktime
from wsgiref.handlers import format_date_time
import _thread as thread

# --- 基础配置 ---
# 强制UTF-8编码，防止日志和输出乱码
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

# --- 日志配置 ---
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[logging.StreamHandler(sys.stdout)],
    encoding='utf-8'
)
logger = logging.getLogger(__name__)

# --- 命令行参数解析 ---
parser = argparse.ArgumentParser(description='Unified STT/TTS Service for Unity')
parser.add_argument('--unity-app', type=str, help='Unity application name to monitor')
parser.add_argument('--unique-id', type=str, help='Unique identifier for this process')
args = parser.parse_args()

# --- 打印进程信息 ---
current_pid = os.getpid()
logger.info(f"Service PID: {current_pid}")
if args.unity_app:
    logger.info(f"Monitoring Unity App: {args.unity_app}")
if args.unique_id:
    logger.info(f"Unique ID: {args.unique_id}")

# --- 网络配置 ---
HOST = '127.0.0.1'
PYTHON_RECV_PORT = 31415
UNITY_RECV_PORT = 31416
unity_address = (HOST, UNITY_RECV_PORT)
unity_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# ==============================================================================
# TTS (Gradio) 部分
# ==============================================================================
script_dir = os.path.dirname(os.path.abspath(__file__))
REF_WAV_PATH = os.path.join(script_dir, 'ref.WAV')


def init_gradio_client():
    """初始化Gradio客户端，包含重试机制。"""
    max_retries = 3
    for attempt in range(max_retries):
        try:
            logger.info(f"Attempting to connect to Gradio service (Attempt {attempt + 1}/{max_retries})...")
            client = Client("http://localhost:9872/")
            if client:
                logger.info("Successfully connected to Gradio service.")
                return client
        except Exception as err:
            logger.error(f"Gradio connection failed: {str(err)}")
        if attempt < max_retries - 1:
            logger.info("Retrying in 10 seconds...")
            time.sleep(10)
    raise ConnectionError("Could not connect to the Gradio service after multiple retries.")


def process_tts_request(message):
    """处理TTS请求，生成语音并返回音频路径。"""
    try:
        logger.info(f"Starting TTS processing for message: '{message}'")
        client = init_gradio_client()
        result = client.predict(
            text=message,
            text_lang="英文",
            ref_audio_path=handle_file(REF_WAV_PATH),
            prompt_text="Good afternoon.",
            prompt_lang="英文",
            aux_ref_audio_paths=handle_file(REF_WAV_PATH),
            top_k=5,
            top_p=1,
            temperature=1,
            text_split_method="凑四句一切",
            batch_size=20,
            speed_factor=1,
            ref_text_free=False,
            split_bucket=True,
            fragment_interval=0.3,
            seed=-1,
            keep_random=True,
            parallel_infer=True,
            repetition_penalty=1.35,
            sample_steps="32",
            super_sampling=False,
            api_name="/inference",
        )
        audio_path = result[0]
        logger.info(f"TTS audio generated at: {audio_path}")
        return audio_path
    except Exception as e:
        logger.error(f"Error during TTS processing: {str(e)}\n{traceback.format_exc()}")
        return None


# ==============================================================================
# STT (iFlytek) 部分
# ==============================================================================
# --- 讯飞STT配置 ---
APPID = 'ae080cee'  # 你的讯飞APPID
APIKey = '76b59b24b1dc95c1d00ef93ca938afe9'  # 你的讯飞APIKey
APISecret = 'MzZhMjc4OWE2Zjg3YWU5NjhlNjc4ODE2'  # 你的讯飞APISecret
STATUS_FIRST_FRAME = 0
STATUS_CONTINUE_FRAME = 1
STATUS_LAST_FRAME = 2

# --- 录音机配置 ---
FORMAT = pyaudio.paInt16
CHANNELS = 1
RATE = 16000
CHUNK = 1024
audio_queue = []
is_recording = False
recognized_text = ""
wsParam = None  # 将wsParam设为全局变量


class Ws_Param:
    def __init__(self, APPID, APIKey, APISecret):
        self.APPID = APPID
        self.APIKey = APIKey
        self.APISecret = APISecret
        self.CommonArgs = {"app_id": self.APPID}
        self.BusinessArgs = {"domain": "iat", "language": "zh_cn", "accent": "mandarin", "vinfo": 1, "vad_eos": 10000}

    def create_url(self):
        url = 'wss://iat-api.xfyun.cn/v2/iat'
        now = datetime.now()
        date = format_date_time(mktime(now.timetuple()))
        signature_origin = f"host: ws-api.xfyun.cn\ndate: {date}\nGET /v2/iat HTTP/1.1"
        signature_sha = hmac.new(self.APISecret.encode('utf-8'), signature_origin.encode('utf-8'),
                                 digestmod=hashlib.sha256).digest()
        signature_sha_base64 = base64.b64encode(signature_sha).decode(encoding='utf-8')
        authorization_origin = f'api_key="{self.APIKey}", algorithm="hmac-sha256", headers="host date request-line", signature="{signature_sha_base64}"'
        authorization = base64.b64encode(authorization_origin.encode('utf-8')).decode(encoding='utf-8')
        v = {"authorization": authorization, "date": date, "host": "ws-api.xfyun.cn"}
        return url + '?' + urlencode(v)


def on_stt_message(ws, message):
    global recognized_text
    try:
        msg_json = json.loads(message)
        code = msg_json.get("code")
        if code != 0:
            logger.error(f"STT Error: {msg_json.get('message', '')} (code: {code})")
            return

        data = msg_json.get("data", {}).get("result", {}).get("ws", [])
        for i in data:
            for w in i.get("cw", []):
                recognized_text += w.get("w", "")

        if msg_json.get("data", {}).get("status", 1) == 2:  # 识别结束
            logger.info(f"STT Final Result: {recognized_text}")
            if recognized_text:
                unity_socket.sendto(f"STT_RESULT:{recognized_text}".encode('utf-8'), unity_address)
            recognized_text = ""  # 重置以便下次使用
    except Exception as e:
        logger.error(f"STT on_message exception: {e}")


def on_stt_error(ws, error):
    logger.error(f"STT Websocket Error: {error}")


def on_stt_close(ws, close_status_code, close_msg):
    logger.info("STT Websocket Closed.")


def on_stt_open(ws):
    def run(*args):
        status = STATUS_FIRST_FRAME
        while True:
            if not audio_queue and not is_recording:
                break  # 如果队列为空且录音已停止，则退出
            if audio_queue:
                buf = audio_queue.pop(0)
                if status == STATUS_FIRST_FRAME:
                    d = {"common": wsParam.CommonArgs, "business": wsParam.BusinessArgs,
                         "data": {"status": 0, "format": "audio/L16;rate=16000",
                                  "audio": str(base64.b64encode(buf), 'utf-8'), "encoding": "raw"}}
                    status = STATUS_CONTINUE_FRAME
                else:
                    d = {"data": {"status": 1, "format": "audio/L16;rate=16000",
                                  "audio": str(base64.b64encode(buf), 'utf-8'), "encoding": "raw"}}
                ws.send(json.dumps(d))
            time.sleep(0.04)

        # 发送最后一帧
        ws.send(json.dumps({"data": {"status": 2, "format": "audio/L16;rate=16000", "audio": "", "encoding": "raw"}}))
        logger.info("STT session finished.")
        time.sleep(1)  # 等待服务器最终响应
        ws.close()

    thread.start_new_thread(run, ())


def start_stt_session():
    global wsParam
    wsParam = Ws_Param(APPID=APPID, APIKey=APIKey, APISecret=APISecret)
    wsUrl = wsParam.create_url()
    ws = websocket.WebSocketApp(wsUrl, on_message=on_stt_message, on_error=on_stt_error, on_close=on_stt_close)
    ws.on_open = on_stt_open
    ws.run_forever(sslopt={"cert_reqs": ssl.CERT_NONE})


def record_audio():
    p = pyaudio.PyAudio()
    stream = p.open(format=FORMAT, channels=CHANNELS, rate=RATE, input=True, frames_per_buffer=CHUNK)
    logger.info("Recording started...")
    while is_recording:
        data = stream.read(CHUNK)
        audio_queue.append(data)
    stream.stop_stream()
    stream.close()
    p.terminate()
    logger.info("Recording stopped.")


# ==============================================================================
# 主服务循环
# ==============================================================================
def is_unity_running(app_name):
    """检查Unity应用是否仍在运行"""
    if not app_name: return True
    try:
        for proc in psutil.process_iter(['name']):
            if proc.info['name'] and app_name.lower() in proc.info['name'].lower():
                return True
    except psutil.NoSuchProcess:
        pass
    return False


def main():
    """主监听循环，分发TTS和STT任务"""
    global is_recording, recognized_text, audio_queue
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
        s.bind((HOST, PYTHON_RECV_PORT))
        logger.info(f"Unified service listening on UDP port {PYTHON_RECV_PORT}")
        sys.stdout.flush()

        while True:
            try:
                # 使用超时来定期检查Unity进程是否存在
                s.settimeout(5.0)
                try:
                    data, addr = s.recvfrom(4096)  # 增加缓冲区大小以防万一
                    message = data.decode('utf-8', errors='ignore')
                except socket.timeout:
                    if not is_unity_running(args.unity_app):
                        logger.info("Unity app has closed. Shutting down service.")
                        break
                    continue

                logger.info(f"Received message from Unity: '{message}'")

                # --- 任务分发 ---
                if message.startswith("TTS_TEXT:"):
                    # 【重要改动】使用 replace 方法确保精确移除前缀
                    text_to_speak = message.replace("TTS_TEXT:", "", 1)
                    audio_path = process_tts_request(text_to_speak)
                    if audio_path:
                        unity_socket.sendto(audio_path.encode('utf-8'), unity_address)
                        logger.info(f"Sent TTS audio path to Unity: '{audio_path}'")

                elif message == "START_RECORD":
                    if not is_recording:
                        is_recording = True
                        recognized_text = ""
                        audio_queue.clear()
                        thread.start_new_thread(record_audio, ())
                        thread.start_new_thread(start_stt_session, ())
                    else:
                        logger.warning("Already recording. 'START_RECORD' command ignored.")

                elif message == "STOP_RECORD":
                    if is_recording:
                        is_recording = False
                    else:
                        logger.warning("'STOP_RECORD' command received but not recording.")

                else:
                    logger.warning(f"Received unknown command format: '{message}'")

            except Exception as e:
                logger.error(f"Error in main loop: {str(e)}\n{traceback.format_exc()}")


if __name__ == "__main__":
    try:
        logger.info("Unified STT/TTS Service starting up...")
        main()
    except KeyboardInterrupt:
        logger.info("Service interrupted by user.")
    except Exception as e:
        logger.error(f"A fatal error occurred: {str(e)}\n{traceback.format_exc()}")
    finally:
        logger.info("Service shutting down.")
        sys.exit(0)
