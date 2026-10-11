using System.Collections.Generic;
using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Звук игры. Создаётся сам при запуске (как и остальные системы проекта), переживает
    /// перезагрузку сцены между уровнями, поэтому музыка не обрывается.
    ///
    /// Клипы лежат в Assets/Resources/NeonTanks/Audio/:
    ///   SFX/         — звуки по ТЗ (sfx_*.wav)
    ///   SFX/Extras/  — дополнительные звуки
    ///   Music/       — bgm_arena_loop.ogg
    ///
    /// Вызывать из кода: NeonAudio.Play("sfx_tank_hit", worldPos) или готовые методы ниже
    /// (ShootPlayer, Ricochet, TankHit и т.д.). Если какого-то файла нет — тихо пропускается.
    /// </summary>
    public class NeonAudio : MonoBehaviour
    {
        private const string SfxPath = "NeonTanks/Audio/SFX/";
        private const string ExtrasPath = "NeonTanks/Audio/SFX/Extras/";
        private const string MusicPath = "NeonTanks/Audio/Music/bgm_arena_loop";
        private const int PoolSize = 24;

        [Header("Громкость")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Tooltip("Громкость всех эффектов, включая гул двигателей (музыка — отдельно)")]
        [Range(0f, 1f)] public float sfxVolume = 0.78f;
        [Tooltip("Громкость фоновой музыки (0.22 ≈ на 5 дБ тише прежних 0.4)")]
        [Range(0f, 1f)] public float musicVolume = 0.22f;
        [Tooltip("Во сколько раз приглушать музыку на паузе и экране результата")]
        [Range(0f, 1f)] public float musicDuckOnPause = 0.4f;
        [Tooltip("Насколько сильно звук смещается влево/вправо по положению на экране")]
        [Range(0f, 1f)] public float stereoWidth = 0.55f;

        private static NeonAudio _instance;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _missing = new HashSet<string>();
        private readonly Dictionary<string, float> _lastPlayTime = new Dictionary<string, float>();
        private AudioSource[] _pool;
        private float[] _poolStartTime;
        private AudioSource _music;
        private float _listenerCheckTimer;

        /// <summary>Итоговая громкость эффектов (для лупов двигателей).</summary>
        public static float SfxVolume => _instance != null ? _instance.masterVolume * _instance.sfxVolume : 0f;

        // Сколько копий одного звука может звучать одновременно (иначе с тройным выстрелом будет каша)
        private static readonly Dictionary<string, int> VoiceLimit = new Dictionary<string, int>
        {
            { "sfx_bullet_ricochet", 4 },
            { "sfx_mirror_boost", 3 },
            { "sfx_bullet_fizzle", 3 },
            { "sfx_shoot_normal", 3 },
            { "sfx_shoot_rapid", 2 },
            { "sfx_enemy_shoot", 3 },
            { "sfx_turret_shoot", 3 },
            { "sfx_turret_ricochet_shoot", 3 },
            { "sfx_block_crack", 3 },
            { "sfx_gate_warning", 2 },
            { "sfx_gate_toggle", 2 },
        };

        // ------------------------------------------------------------------ запуск

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("NeonAudio");
            DontDestroyOnLoad(go);
            go.AddComponent<NeonAudio>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _pool = new AudioSource[PoolSize];
            _poolStartTime = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i] = CreateSource("Sfx_" + i);
            }

            _music = CreateSource("Music");
            _music.loop = true;
            _music.priority = 0;
            AudioClip musicClip = Resources.Load<AudioClip>(MusicPath);
            if (musicClip != null)
            {
                _music.clip = musicClip;
                _music.volume = 0f;
                _music.Play();
            }
            else
            {
                Debug.LogWarning("[NeonAudio] Музыка не найдена: Resources/" + MusicPath);
            }

            EnsureAudioListener();
        }

        private AudioSource CreateSource(string sourceName)
        {
            GameObject child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // камера смотрит сверху — используем стереопанораму, а не 3D-затухание
            src.dopplerLevel = 0f;
            return src;
        }

        private void Update()
        {
            // Музыка: плавно приглушаем на паузе и на экране результата (там Time.timeScale = 0)
            if (_music != null && _music.clip != null)
            {
                float target = masterVolume * musicVolume * (Time.timeScale < 0.01f ? musicDuckOnPause : 1f);
                _music.volume = Mathf.MoveTowards(_music.volume, target, Time.unscaledDeltaTime * 0.8f);
            }

            // Камеру может создать SimpleArenaBuilder уже после старта — проверяем слушателя раз в секунду
            _listenerCheckTimer -= Time.unscaledDeltaTime;
            if (_listenerCheckTimer <= 0f)
            {
                _listenerCheckTimer = 1f;
                EnsureAudioListener();
            }
        }

        private static void EnsureAudioListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }
        }

        // ------------------------------------------------------------------ базовое API

        private static bool EnsureInstance()
        {
            if (_instance == null && Application.isPlaying) Bootstrap();
            return _instance != null;
        }

        public static AudioClip GetClip(string clipName)
        {
            if (!EnsureInstance() || string.IsNullOrEmpty(clipName)) return null;
            if (_instance._clips.TryGetValue(clipName, out AudioClip cached)) return cached;
            if (_instance._missing.Contains(clipName)) return null;

            AudioClip clip = Resources.Load<AudioClip>(SfxPath + clipName);
            if (clip == null) clip = Resources.Load<AudioClip>(ExtrasPath + clipName);

            if (clip == null)
            {
                _instance._missing.Add(clipName);
                Debug.LogWarning("[NeonAudio] Звук не найден: " + clipName);
                return null;
            }
            _instance._clips[clipName] = clip;
            return clip;
        }

        /// <summary>Проиграть звук. worldPos задаёт стереопанораму (null — по центру).</summary>
        public static void Play(string clipName, float volume = 1f, float pitch = 1f, Vector3? worldPos = null,
            float delay = 0f, float minInterval = 0.02f)
        {
            if (!EnsureInstance()) return;
            _instance.PlayInternal(clipName, volume, pitch, worldPos, delay, minInterval);
        }

        private void PlayInternal(string clipName, float volume, float pitch, Vector3? worldPos, float delay, float minInterval)
        {
            AudioClip clip = GetClip(clipName);
            if (clip == null) return;

            // Защита от «пулемёта» одинаковых звуков в один кадр
            float now = Time.unscaledTime;
            if (minInterval > 0f && _lastPlayTime.TryGetValue(clipName, out float last) && now - last < minInterval) return;

            int limit = VoiceLimit.TryGetValue(clipName, out int l) ? l : 6;
            int playing = 0;
            int oldestSame = -1;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].isPlaying && _pool[i].clip == clip)
                {
                    playing++;
                    if (oldestSame < 0 || _poolStartTime[i] < _poolStartTime[oldestSame]) oldestSame = i;
                }
            }

            int slot;
            if (playing >= limit && oldestSame >= 0)
            {
                slot = oldestSame;               // заменяем самую старую копию этого же звука
            }
            else
            {
                slot = FindFreeSlot();
            }

            AudioSource src = _pool[slot];
            src.Stop();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume) * masterVolume * sfxVolume;
            src.pitch = pitch;
            src.panStereo = worldPos.HasValue ? PanFor(worldPos.Value) : 0f;
            if (delay > 0f) src.PlayDelayed(delay); else src.Play();

            _poolStartTime[slot] = now + delay;
            _lastPlayTime[clipName] = now;
        }

        private int FindFreeSlot()
        {
            int oldest = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].isPlaying) return i;
                if (_poolStartTime[i] < _poolStartTime[oldest]) oldest = i;
            }
            return oldest;
        }

        /// <summary>Стереопанорама по горизонтальному положению объекта на экране.</summary>
        public static float PanFor(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null || _instance == null) return 0f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * _instance.stereoWidth;
        }

        private static float Vary(float amount = 0.05f) => Random.Range(1f - amount, 1f + amount);

        // ------------------------------------------------------------------ события игры

        // Стрельба
        public static void ShootPlayer(bool rapid, Vector3 pos)
        {
            if (rapid) Play("sfx_shoot_rapid", 0.85f, Vary(0.04f), pos);
            else Play("sfx_shoot_normal", 0.8f, Vary(), pos);
        }

        public static void ShootEnemy(bool isTurret, bool ricochetShots, Vector3 pos)
        {
            string clip = !isTurret ? "sfx_enemy_shoot" : (ricochetShots ? "sfx_turret_ricochet_shoot" : "sfx_turret_shoot");
            Play(clip, 0.6f, Vary(), pos);
        }

        // Снаряд. bounceNumber — номер отскока (1, 2, 3...), высота растёт с каждым
        public static void Ricochet(int bounceNumber, bool mirror, Vector3 pos)
        {
            float pitch = 1f + 0.12f * Mathf.Clamp(bounceNumber - 1, 0, 4);
            if (mirror) Play("sfx_mirror_boost", 0.75f, pitch, pos, 0f, 0.01f);
            else Play("sfx_bullet_ricochet", 0.55f, pitch * Vary(0.02f), pos, 0f, 0.01f);
        }

        public static void Fizzle(Vector3 pos) => Play("sfx_bullet_fizzle", 0.5f, Vary(), pos);

        // Танки
        public static void TankHit(bool isPlayer, int healthLeft, Vector3 pos)
        {
            if (isPlayer)
            {
                Play("sfx_tank_hit", 0.95f, Vary(0.03f), pos);
                if (healthLeft == 1) Play("sfx_armor_critical", 0.55f, 1f, pos, 0.18f);
            }
            else
            {
                Play("sfx_tank_hit", 0.6f, 1.15f * Vary(0.03f), pos);
            }
        }

        public static void ShieldBlock(Vector3 pos) => Play("sfx_shield_block", 0.8f, Vary(0.03f), pos);

        public static void ProtectDeflect(Vector3 pos) => Play("sfx_spawn_protect_deflect", 0.5f, Vary(), pos, 0f, 0.1f);

        public static void TankExplode(bool isPlayer, bool hasLivesLeft, Vector3 pos)
        {
            if (isPlayer)
            {
                Play("sfx_tank_explode", 1f, 1f, pos);
                if (hasLivesLeft) Play("sfx_life_lost", 0.65f, 1f, null, 0.65f);
            }
            else
            {
                Play("sfx_enemy_explode", 0.85f, Vary(0.04f), pos);
            }
        }

        public static void Respawn(bool isPlayer, Vector3 pos)
        {
            Play("sfx_tank_respawn", isPlayer ? 0.7f : 0.4f, 1f, pos, 0f, 0.05f);
        }

        public static void RespawnTick() => Play("sfx_respawn_tick", 0.5f);

        public static void BuffEnded(bool speed) => Play(speed ? "sfx_speed_end" : "sfx_rapid_end", 0.5f);

        // Бонусы
        public static void Pickup(PickupType type, bool byPlayer, Vector3 pos)
        {
            string clip;
            switch (type)
            {
                case PickupType.Repair: clip = "sfx_pickup_repair"; break;
                case PickupType.Shield: clip = "sfx_pickup_shield"; break;
                case PickupType.Speed: clip = "sfx_pickup_speed"; break;
                case PickupType.RapidFire: clip = "sfx_pickup_rapid"; break;
                default: clip = "sfx_pickup_take"; break;
            }
            Play(clip, byPlayer ? 0.85f : 0.4f, 1f, pos);
        }

        public static void PickupSpawn(Vector3 pos) => Play("sfx_pickup_spawn", 0.5f, Vary(), pos);

        // Объекты арены
        public static void BlockCrack(int healthLeft, int maxHealth, Vector3 pos)
        {
            float damage = maxHealth > 0 ? 1f - (float)healthLeft / maxHealth : 0f;
            Play("sfx_block_crack", 0.65f, 1f + 0.2f * damage, pos);
        }

        public static void BlockShatter(Vector3 pos) => Play("sfx_block_shatter", 0.85f, Vary(0.03f), pos);

        public static void BlockRebuild(Vector3 pos) => Play("sfx_block_rebuild", 0.45f, 1f, pos);

        public static void GateToggle(bool open, Vector3 pos) => Play("sfx_gate_toggle", 0.55f, open ? 1f : 0.85f, pos, 0f, 0f);

        public static void GateWarning(Vector3 pos) => Play("sfx_gate_warning", 0.3f, 1f, pos, 0f, 0.05f);

        // Интерфейс и ход игры
        public static void UiClick() => Play("sfx_ui_click", 0.5f, 1f, null, 0f, 0.05f);

        public static void UiConfirm() => Play("sfx_ui_confirm", 0.6f, 1f, null, 0f, 0.1f);

        public static void Pause(bool paused) => Play(paused ? "sfx_ui_pause" : "sfx_ui_resume", 0.5f);

        public static void TimerBeep(int secondsLeft) => Play("sfx_timer_beep", 0.6f, secondsLeft <= 1 ? 1.12f : 1f);

        public static void LevelStart() => Play("sfx_level_banner", 0.7f, 1f, null, 0.15f);

        public static void LevelFinished(bool victory, bool campaignDone)
        {
            if (!victory) Play("sfx_match_lose", 0.9f);
            else if (campaignDone) Play("sfx_campaign_complete", 0.9f);
            else Play("sfx_level_complete", 0.9f);
        }

        public static void MatchFinished(bool victory) => Play(victory ? "sfx_match_win" : "sfx_match_lose", 0.9f);
    }
}
