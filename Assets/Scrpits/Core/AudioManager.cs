using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Müzik Kaynakları")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Varsayılan Müzikler")]
    public AudioClip[] backgroundTracks;

    private float _musicVolume = 0.7f;
    private float _sfxVolume = 0.7f;
    private bool _isMusicMuted = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Kaydedilmiş ses seviyelerini yükle
        _musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        _sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 0.7f);

        SetMusicVolume(_musicVolume);
        SetSfxVolume(_sfxVolume);

        // Mute durumunu yükle ve uygula
        _isMusicMuted = PlayerPrefs.GetInt("MusicMuted", 0) == 1;
        if (musicSource != null)
        {
            musicSource.mute = _isMusicMuted;
        }

        // Eğer müzik atanmışsa ve çalmıyorsa ilk müziği döngüsel olarak başlat
        if (backgroundTracks != null && backgroundTracks.Length > 0 && musicSource != null)
        {
            PlayMusic(backgroundTracks[0]);
        }
    }

    private void InitializeAudioSources()
    {
        // Eğer inspector'dan atanmamışsa dinamik olarak ekle
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        
        musicSource.clip = clip;
        musicSource.volume = _musicVolume;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = volume;
        if (musicSource != null)
        {
            musicSource.volume = _musicVolume;
            
            // Eğer ses seviyesi 0'dan büyük yapıldıysa ve şu an sessizdeyse, sessizden çıkar
            if (_musicVolume > 0f && _isMusicMuted)
            {
                SetMusicMute(false);
            }
            // Eğer ses seviyesi sıfır yapıldıysa, sessize al
            else if (_musicVolume <= 0f && !_isMusicMuted)
            {
                SetMusicMute(true);
            }
        }
    }

    public bool IsMusicMuted()
    {
        return _isMusicMuted;
    }

    public void SetMusicMute(bool mute)
    {
        _isMusicMuted = mute;
        if (musicSource != null)
        {
            musicSource.mute = _isMusicMuted;
        }
        PlayerPrefs.SetInt("MusicMuted", _isMusicMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume)
    {
        _sfxVolume = volume;
        if (sfxSource != null)
        {
            sfxSource.volume = _sfxVolume;
        }
    }
}
