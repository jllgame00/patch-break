using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small persistent 2D audio service for PATCH//BREAK. It owns only clip
/// playback and scene music policy; gameplay components keep their existing
/// timing and call the static SFX methods after their normal authorization.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class PersistentAudioManager : MonoBehaviour
{
    private const string BattleSceneName = "Battle";
    private const string KnightBattleSceneName = "KnightBattle";
    private const string DebuggerBattleSceneName = "DebuggerBattle";
    private const string EndingSceneName = "Ending";

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    [SerializeField] private AudioClip battleBgm;
    [SerializeField] private AudioClip debuggerBgm;
    [SerializeField] private AudioClip cityAmbience;

    [Header("UI")]
    [SerializeField] private AudioClip[] typingClips = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip briefingAppearClip;

    [Header("Combat")]
    [SerializeField] private AudioClip swordSwingClip;
    [SerializeField] private AudioClip projectileClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField, Range(0f, 1f)]
    private float hitVolumeMultiplier = 0.1f;

    [Header("Mix")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.13f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float typingVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float briefingVolume = 0.5f;
    [SerializeField, Min(0.01f)] private float musicFadeDuration = 0.4f;

    [Header("Typing")]
    [SerializeField, Min(0f)] private float typingMinimumInterval = 0.035f;

    private static PersistentAudioManager instance;

    private Coroutine bgmTransition;
    private Coroutine ambienceTransition;
    private float nextTypingTime;

    public static PersistentAudioManager Instance => instance;
    public AudioSource BgmSource => bgmSource;
    public AudioSource AmbienceSource => ambienceSource;
    public AudioSource SfxSource => sfxSource;
    public AudioClip BattleBgm => battleBgm;
    public AudioClip DebuggerBgm => debuggerBgm;
    public AudioClip CityAmbience => cityAmbience;
    public AudioClip[] TypingClips => typingClips;
    public AudioClip BriefingAppearClip => briefingAppearClip;
    public AudioClip SwordSwingClip => swordSwingClip;
    public AudioClip ProjectileClip => projectileClip;
    public AudioClip HitClip => hitClip;
    public float HitVolumeMultiplier => hitVolumeMultiplier;
    public float BgmVolume => bgmVolume;
    public float AmbienceVolume => ambienceVolume;
    public float SfxVolume => sfxVolume;
    public float TypingVolume => typingVolume;
    public float BriefingVolume => briefingVolume;
    public float TypingMinimumInterval => typingMinimumInterval;
    public float MusicFadeDuration => musicFadeDuration;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ConfigureSourcePolicies();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void Start()
    {
        ApplySceneAudioPolicy(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void Configure(
        AudioSource configuredBgmSource,
        AudioSource configuredAmbienceSource,
        AudioSource configuredSfxSource,
        AudioClip configuredBattleBgm,
        AudioClip configuredDebuggerBgm,
        AudioClip configuredCityAmbience,
        AudioClip[] configuredTypingClips,
        AudioClip configuredBriefingAppear,
        AudioClip configuredSwordSwing,
        AudioClip configuredProjectile,
        AudioClip configuredHit)
    {
        bgmSource = configuredBgmSource;
        ambienceSource = configuredAmbienceSource;
        sfxSource = configuredSfxSource;
        battleBgm = configuredBattleBgm;
        debuggerBgm = configuredDebuggerBgm;
        cityAmbience = configuredCityAmbience;
        typingClips = configuredTypingClips ?? Array.Empty<AudioClip>();
        briefingAppearClip = configuredBriefingAppear;
        swordSwingClip = configuredSwordSwing;
        projectileClip = configuredProjectile;
        hitClip = configuredHit;
        ConfigureSourcePolicies();
    }

    public static void NotifyUserGesture()
    {
        // Unity/WebGL handles the actual browser audio-context unlock. This
        // explicit menu gesture makes the first subsequent battle playback a
        // user-initiated transition without a browser-policy workaround.
        instance?.ApplySceneAudioPolicy(SceneManager.GetActiveScene());
    }

    public static void PlayTyping()
    {
        instance?.PlayTypingInternal();
    }

    public static void PlayBriefingAppear()
    {
        instance?.PlaySfx(
            instance.briefingAppearClip,
            instance.briefingVolume,
            1f,
            1f
        );
    }

    public static void PlaySwordSwing()
    {
        instance?.PlaySfx(
            instance.swordSwingClip,
            1f,
            0.98f,
            1.02f
        );
    }

    public static void PlayProjectile()
    {
        instance?.PlaySfx(
            instance.projectileClip,
            1f,
            1f,
            1f
        );
    }

    public static void PlayHit()
    {
        instance?.PlaySfx(
            instance.hitClip,
            instance.hitVolumeMultiplier,
            0.97f,
            1.03f
        );
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        ApplySceneAudioPolicy(next);
    }

    private void ApplySceneAudioPolicy(Scene scene)
    {
        switch (scene.name)
        {
            case BattleSceneName:
            case KnightBattleSceneName:
                RequestLoopingClip(
                    bgmSource,
                    battleBgm,
                    bgmVolume,
                    ref bgmTransition
                );
                RequestLoopingClip(
                    ambienceSource,
                    cityAmbience,
                    ambienceVolume,
                    ref ambienceTransition
                );
                break;

            case DebuggerBattleSceneName:
                RequestLoopingClip(
                    bgmSource,
                    debuggerBgm,
                    bgmVolume,
                    ref bgmTransition
                );
                RequestLoopingClip(
                    ambienceSource,
                    cityAmbience,
                    ambienceVolume,
                    ref ambienceTransition
                );
                break;

            case EndingSceneName:
            default:
                RequestLoopingClip(
                    bgmSource,
                    null,
                    bgmVolume,
                    ref bgmTransition
                );
                RequestLoopingClip(
                    ambienceSource,
                    null,
                    ambienceVolume,
                    ref ambienceTransition
                );
                break;
        }
    }

    private void ConfigureSourcePolicies()
    {
        ConfigureLoopSource(bgmSource);
        ConfigureLoopSource(ambienceSource);
        ConfigureSfxSource(sfxSource);
    }

    private static void ConfigureLoopSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.pitch = 1f;
    }

    private static void ConfigureSfxSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.pitch = 1f;
    }

    private void RequestLoopingClip(
        AudioSource source,
        AudioClip targetClip,
        float targetVolume,
        ref Coroutine transition)
    {
        if (source == null)
        {
            return;
        }

        if (source.clip == targetClip &&
            ((targetClip == null && !source.isPlaying) ||
             (targetClip != null && source.isPlaying)))
        {
            source.volume = targetClip == null ? 0f : targetVolume;
            return;
        }

        if (transition != null)
        {
            StopCoroutine(transition);
        }

        transition = StartCoroutine(
            TransitionLoopingClip(source, targetClip, targetVolume)
        );
    }

    private IEnumerator TransitionLoopingClip(
        AudioSource source,
        AudioClip targetClip,
        float targetVolume)
    {
        float fadeDuration = Mathf.Max(0.01f, musicFadeDuration);
        float startingVolume = source.volume;

        if (source.isPlaying && startingVolume > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(
                    startingVolume,
                    0f,
                    elapsed / fadeDuration
                );
                yield return null;
            }
        }

        source.Stop();
        source.clip = targetClip;

        if (targetClip == null)
        {
            source.volume = 0f;
            yield break;
        }

        source.loop = true;
        source.volume = 0f;
        source.Play();

        float fadeInElapsed = 0f;
        while (fadeInElapsed < fadeDuration)
        {
            fadeInElapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(
                0f,
                targetVolume,
                fadeInElapsed / fadeDuration
            );
            yield return null;
        }

        source.volume = targetVolume;
    }

    private void PlayTypingInternal()
    {
        if (typingClips == null || typingClips.Length == 0 ||
            Time.unscaledTime < nextTypingTime)
        {
            return;
        }

        AudioClip clip = typingClips[UnityEngine.Random.Range(
            0,
            typingClips.Length
        )];
        if (clip == null)
        {
            return;
        }

        nextTypingTime = Time.unscaledTime + typingMinimumInterval;
        float pitch = UnityEngine.Random.Range(0.95f, 1.05f);
        PlaySfx(clip, typingVolume, pitch, pitch);
    }

    private void PlaySfx(
        AudioClip clip,
        float relativeVolume,
        float minPitch,
        float maxPitch)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, sfxVolume * relativeVolume);
        sfxSource.pitch = 1f;
    }
}
