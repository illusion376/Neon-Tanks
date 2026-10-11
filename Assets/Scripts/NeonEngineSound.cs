using UnityEngine;

namespace DemoRicochet
{
    /// <summary>
    /// Гул двигателя танка (зацикленный). Громкость и высота зависят от скорости,
    /// на паузе, после гибели и пока танк стоит — затихает.
    /// Добавляется из кода: SimpleBoxTank (игрок) и EnemyTankAI (только ездящие враги).
    /// </summary>
    public class NeonEngineSound : MonoBehaviour
    {
        public string clipName = "sfx_tank_move_loop";
        [Range(0f, 1f)] public float maxVolume = 0.25f;
        [Tooltip("Скорость, при которой гул звучит в полную силу")]
        public float fullSpeed = 8.2f;
        public float minPitch = 0.85f;
        public float maxPitch = 1.15f;

        private AudioSource _source;
        private Rigidbody _rb;
        private TankHealth _health;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<TankHealth>();

            AudioClip clip = NeonAudio.GetClip(clipName);
            if (clip == null)
            {
                enabled = false;
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.volume = 0f;
            _source.Play();
            // разные танки стартуют с разного места лупа, чтобы не гудеть в унисон
            _source.time = Random.Range(0f, clip.length * 0.9f);
        }

        private void Update()
        {
            if (_source == null) return;

            float speed = 0f;
            if (_rb != null)
            {
                Vector3 v = _rb.linearVelocity;
                v.y = 0f;
                speed = v.magnitude;
            }

            bool silent = Time.timeScale < 0.01f || (_health != null && _health.IsDead);
            float t = silent ? 0f : Mathf.Clamp01(speed / Mathf.Max(0.1f, fullSpeed));

            float targetVolume = t * maxVolume * NeonAudio.SfxVolume;
            _source.volume = Mathf.MoveTowards(_source.volume, targetVolume, Time.unscaledDeltaTime * 1.5f);
            _source.pitch = Mathf.Lerp(minPitch, maxPitch, t);
            _source.panStereo = NeonAudio.PanFor(transform.position);
        }
    }
}
