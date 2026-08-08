using System;
using UnityEngine;

/// <summary>
/// Lightweight sprite-only sequence playback. It never changes transform,
/// physics, material, sorting, facing, or SpriteRenderer.color.
/// </summary>
public sealed class SpriteSequencePlayer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;

    private Sprite[] activeFrames;
    private float framesPerSecond;
    private float frameElapsed;
    private int frameIndex;
    private bool isLooping;
    private bool isPlaying;
    private Action onComplete;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public bool IsPlaying => isPlaying;

    public void SetStatic(Sprite sprite)
    {
        Stop();

        if (targetRenderer != null && sprite != null)
        {
            targetRenderer.sprite = sprite;
        }
    }

    public void PlayLoop(Sprite[] frames, float fps)
    {
        Begin(frames, fps, loop: true, null);
    }

    public void PlayOnce(
        Sprite[] frames,
        float fps,
        Action completed = null)
    {
        Begin(frames, fps, loop: false, completed);
    }

    public void Stop()
    {
        activeFrames = null;
        framesPerSecond = 0f;
        frameElapsed = 0f;
        frameIndex = 0;
        isLooping = false;
        isPlaying = false;
        onComplete = null;
    }

    private void OnDisable()
    {
        Stop();
    }

    private void Update()
    {
        if (!isPlaying || activeFrames == null || activeFrames.Length <= 1)
        {
            return;
        }

        float frameDuration = 1f / framesPerSecond;
        frameElapsed += Time.deltaTime;

        int framesToAdvance = Mathf.FloorToInt(
            frameElapsed / frameDuration
        );
        if (framesToAdvance <= 0)
        {
            return;
        }

        frameElapsed -= framesToAdvance * frameDuration;
        frameIndex += framesToAdvance;

        if (isLooping)
        {
            frameIndex %= activeFrames.Length;
            ApplyCurrentFrame();
            return;
        }

        if (frameIndex < activeFrames.Length)
        {
            ApplyCurrentFrame();
            return;
        }

        frameIndex = activeFrames.Length - 1;
        ApplyCurrentFrame();
        isPlaying = false;

        Action completed = onComplete;
        onComplete = null;
        completed?.Invoke();
    }

    private void Begin(
        Sprite[] frames,
        float fps,
        bool loop,
        Action completed)
    {
        Stop();

        if (targetRenderer == null || frames == null || frames.Length == 0)
        {
            completed?.Invoke();
            return;
        }

        activeFrames = frames;
        framesPerSecond = Mathf.Max(0.01f, fps);
        isLooping = loop;
        isPlaying = frames.Length > 1;
        onComplete = completed;
        ApplyCurrentFrame();

        if (!isPlaying && !isLooping)
        {
            Action immediateCompletion = onComplete;
            onComplete = null;
            immediateCompletion?.Invoke();
        }
    }

    private void ApplyCurrentFrame()
    {
        Sprite frame = activeFrames[frameIndex];
        if (targetRenderer != null && frame != null)
        {
            targetRenderer.sprite = frame;
        }
    }
}
