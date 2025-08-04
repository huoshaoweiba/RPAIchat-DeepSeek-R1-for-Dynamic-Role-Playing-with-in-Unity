from gradio_client import Client,handle_file

client = Client("http://localhost:9872/")
message = "Hello,my dear."

result = client.predict(
            text= message,
            text_lang="英文",
            ref_audio_path=handle_file('ref.WAV'),
            aux_ref_audio_paths=[handle_file('ref.WAV')],
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
print(result[0])

