import socket
import sys
import io
import traceback
import logging
import argparse
import os
import time
import psutil
from gradio_client import Client, handle_file

# 强制设置标准输出和标准错误的编码为 UTF-8
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')
# 确定ref的位置
script_dir = os.path.dirname(os.path.abspath(__file__))
ref_wav_path = os.path.join(script_dir, 'ref.WAV')
# 配置命令行参数
parser = argparse.ArgumentParser(description='TTStool for Unity TTS')
parser.add_argument('--unity-app', type=str, help='Unity application name')
parser.add_argument('--unique-id', type=str, help='Unique identifier for this process')
args = parser.parse_args()

# 配置详细的日志记录
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout)
    ],
    encoding='utf-8'  # 明确指定编码为 UTF-8
)
logger = logging.getLogger(__name__)

# 打印进程信息
current_pid = os.getpid()
logger.info(f"TTStool PID: {current_pid}")  # Unity 将捕获此行获取PID
if args.unity_app:
    logger.info(f"关联Unity应用: {args.unity_app}")
if args.unique_id:
    logger.info(f"唯一标识符: {args.unique_id}")

# 设置端口
HOST = '127.0.0.1'
PYTHON_RECV_PORT = 31415
UNITY_RECV_PORT = 31416

# 创建发送到Unity的socket
unity_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)


def init_gradio_client():
    """初始化Gradio客户端，重试机制"""
    max_retries = 3  # 增加重试次数
    for attempt in range(max_retries):
        try:
            logger.info(f"尝试连接Gradio服务 (尝试 {attempt + 1}/{max_retries})...")
            client = Client("http://localhost:9872/")

            # 检查是否真正连接成功
            if client is not None:
                logger.info("成功连接到Gradio服务")
                return client
            else:
                logger.warning("客户端初始化成功，但未检测到有效连接，等待重试...")
        except Exception as err:
            logger.error(f"连接失败: {str(err)}")
            logger.error("完整堆栈跟踪:\n" + traceback.format_exc())

        # 增加重试间隔时间
        if attempt < max_retries - 1:
            logger.info("10秒后重试...")
            time.sleep(10)  # 增加重试间隔时间
    raise ConnectionError("无法连接到Gradio服务")


def process_text_message(message):
    """处理文本消息并生成语音"""
    try:
        logger.info(f"开始处理消息: '{message}'")

        # 初始化客户端
        client = init_gradio_client()

        # 语音合成
        result = client.predict(
            text=message,
            text_lang="英文",
            ref_audio_path=handle_file(ref_wav_path),
            aux_ref_audio_paths=[handle_file(ref_wav_path)],
            prompt_text="Good afternoon. I heard your footsteps. My, it certainly is lively outside of the workshop",
            prompt_lang="英文",
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
        logger.info(f"生成音频路径: {audio_path}")
        return audio_path
    except Exception as e:
        logger.error(f"处理过程中出错: {str(e)}")
        logger.error("完整堆栈跟踪:\n" + traceback.format_exc())
        return None


def main():
    """主监听循环"""
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
        s.bind((HOST, PYTHON_RECV_PORT))
        logger.info(f"监听Unity消息 (端口 {PYTHON_RECV_PORT})")
        sys.stdout.flush()

        # 设置心跳检查
        last_activity = time.time()

        while True:
            try:
                # 设置超时以允许定期检查
                s.settimeout(5.0)

                # 接收Unity消息
                try:
                    data, addr = s.recvfrom(1024)
                    message = data.decode('utf-8', errors='ignore')
                    logger.info(f"收到来自Unity的消息: '{message}'")
                    last_activity = time.time()
                except socket.timeout:
                    # 检查Unity是否仍在运行
                    if args.unity_app and not is_unity_running(args.unity_app):
                        logger.info("Unity应用已退出，终止TTStool")
                        return
                    continue

                # 处理消息并生成音频
                audio_path = process_text_message(message)

                if audio_path:
                    # 发送音频路径回Unity
                    try:
                        unity_socket.sendto(audio_path.encode('utf-8'), (HOST, UNITY_RECV_PORT))
                        logger.info(f"发送音频路径到Unity: '{audio_path}'")
                    except Exception as e:
                        logger.error(f"发送回Unity失败: {str(e)}")

                sys.stdout.flush()
            except Exception as e:
                logger.error(f"主循环错误: {str(e)}")
                logger.error("完整堆栈跟踪:\n" + traceback.format_exc())


def is_unity_running(app_name):
    """检查Unity应用是否仍在运行"""
    try:
        for proc in psutil.process_iter(['name']):
            if proc.info['name'] and app_name.lower() in proc.info['name'].lower():
                return True
    except Exception:
        pass
    return False


if __name__ == "__main__":
    try:
        logger.info("TTStool 启动")
        main()
    except KeyboardInterrupt:
        logger.info("程序被用户中断")
    except Exception as e:
        logger.error(f"致命错误: {str(e)}")
        logger.error("完整堆栈跟踪:\n" + traceback.format_exc())
        sys.exit(1)