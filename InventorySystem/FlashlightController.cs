using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using VContainer;

/// <summary>
/// FlashlightController — pasang pada Player.
///
/// Fase 4 — Migrasi VContainer:
///   - Hapus Instance singleton dan singleton Awake pattern
///   - Hapus RegisterPersistCallback / UnregisterPersistCallback
///   - [Inject] PlayerEquipment dan PlayerBatteryInventory — tidak ada .Instance lagi
///   - Subscribe ke PlayerEquipment events di Awake (setelah inject) bukan Start
///
/// Mechanic:
///   - Tahan F → senter nyala | Lepas F → langsung mati
///   - Overheat threshold — tahan terlalu lama → overheat → cooldown
///   - Battery habis → isi ulang dengan R, ada progress bar
/// </summary>
public class FlashlightController : MonoBehaviour, IPersistable
{
    public static FlashlightController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Light flashlightLight;

    [Header("Battery Override")]
    [SerializeField] private bool  useOverride             = false;
    [SerializeField] private float batteryDurationOverride = 120f;
    [Range(0f, 0.5f)]
    [SerializeField] private float flickerThresholdOverride = 0.15f;

    [Header("Overheat")]
    [Tooltip("Berapa detik tahan F sebelum overheat")]
    [SerializeField] private float overheatThreshold = 5f;
    [Tooltip("Berapa detik cooldown setelah overheat")]
    [SerializeField] private float overheatCooldown  = 4f;
    [Tooltip("Detik sebelum overheat mulai flicker cepat")]
    [SerializeField] private float overheatWarningAt = 1f;

    [Header("Recharge")]
    [Tooltip("Berapa detik proses mengisi 1 baterai")]
    [SerializeField] private float rechargeDuration = 3f;

    [Header("Flicker")]
    [SerializeField] private float flickerMinInterval   = 0.05f;
    [SerializeField] private float flickerMaxInterval   = 0.2f;
    [SerializeField] private float flickerMinIntensity  = 0.1f;
    [SerializeField] private float overheatFlickerSpeed = 0.03f;

    [Header("Spam Detection")]
    [Tooltip("Berapa kali F ditekan dalam spamWindow detik sebelum dianggap spam")]
    [SerializeField] private int   spamPressLimit     = 5;
    [Tooltip("Window waktu untuk deteksi spam (detik)")]
    [SerializeField] private float spamWindow         = 2f;
    [Tooltip("Berapa detik senter rusak akibat spam")]
    [SerializeField] private float spamBrokenDuration = 30f;

    [Header("Noise")]
    [Tooltip("Noise per detik selama flashlight dinyalakan (hold)")]
    [SerializeField] private float noiseWhileOn        = 3f;
    [Tooltip("Noise saat flashlight dinyalakan")]
    [SerializeField] private float noiseOnTurnOn       = 2f;
    [Tooltip("Noise saat flashlight dimatikan")]
    [SerializeField] private float noiseOnTurnOff      = 1f;
    [Tooltip("Noise saat baterai habis")]
    [SerializeField] private float noiseOnDepleted     = 5f;
    [Tooltip("Noise saat overheat mulai")]
    [SerializeField] private float noiseOnOverheat     = 8f;
    [Tooltip("Noise saat overheat selesai (cooldown selesai)")]
    [SerializeField] private float noiseOnOverheatEnd  = 2f;
    [Tooltip("Noise saat recharge selesai")]
    [SerializeField] private float noiseOnRecharge     = 3f;
    [Tooltip("Noise saat flashlight rusak karena spam")]
    [SerializeField] private float noiseOnBroken       = 10f;

    [Header("Events")]
    public UnityEvent          onFlashlightOn;
    public UnityEvent          onFlashlightOff;
    public UnityEvent          onBatteryDepleted;
    public UnityEvent<float>   onBatteryChanged;
    public UnityEvent          onOverheatStart;
    public UnityEvent          onOverheatEnd;
    public UnityEvent<float>   onOverheatProgress;
    public UnityEvent<float>   onRechargeProgress;
    public UnityEvent          onRechargeComplete;
    public UnityEvent<float>   onBrokenStart;
    public UnityEvent          onBrokenEnd;

    // Fase 4 — Inject dari SceneLifetimeScope
    [Inject] private PlayerEquipment        _equipment;
    [Inject] private PlayerBatteryInventory _batteryInventory;

    // ── State ──
    private bool      _isOn;
    private bool      _hasFlashlight;
    private bool      _isOverheated;
    private bool      _isRecharging;
    private float     _holdTime;
    private float     _batteryRemaining;
    private float     _activeBatteryDuration;
    private float     _activeFlickerThreshold = 0.15f;
    private float     _baseIntensity;
    private Coroutine _flickerRoutine;
    private Coroutine _batteryRoutine;
    private Coroutine _cooldownRoutine;
    private Coroutine _rechargeRoutine;
    private bool      _fHeld;
    private int       _inputIgnoreFrames; // skip input N frame pertama setelah scene load
    // ── Spam Detection ──
    private int       _spamPressCount;
    private float     _spamWindowTimer;
    private bool      _isBroken;
    private Coroutine _brokenRoutine;

    private float     _overheatTimeRemaining;
    private float     _brokenTimeRemaining;

    public struct FlashlightState
    {
        public bool  isOverheated;
        public float overheatTimeRemaining;
        public bool  isBroken;
        public float brokenTimeRemaining;
        public float batteryRemaining;
    }

    public bool  IsOn             => _isOn;
    public bool  IsOverheated     => _isOverheated;
    public bool  IsRecharging     => _isRecharging;
    public float BatteryRemaining => _batteryRemaining;
    public float BatteryPercent   => _activeBatteryDuration > 0 ? _batteryRemaining / _activeBatteryDuration : 1f;
    public float OverheatPercent  => Mathf.Clamp01(_holdTime / overheatThreshold);
    public bool  IsBroken         => _isBroken;

    // ──────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────

    private void Awake()
    {
        // Fase 4: Singleton Awake dihapus. Instance sebagai shim sementara.
        Instance = this;

        if (flashlightLight != null)
        {
            _baseIntensity          = flashlightLight.intensity;
            flashlightLight.enabled = false;
        }

        // RegisterPersistCallback DIHAPUS — FlashlightController tidak implement IPersistable
        // karena state-nya sudah di-persist oleh PersistFlashlight() yang dipanggil
        // dari GameSave.RegisterPersistCallback... WAIT: lihat catatan di bawah.
        //
        // CATATAN: FlashlightController.PersistFlashlight() tetap dipanggil
        // melalui GameSave.RegisterPersistCallback() dari versi lama.
        // Fase 4: Implement IPersistable juga agar GameSaveService bisa flush.
        // Untuk sekarang, PersistFlashlight() tetap dipakai via GameSave callback
        // sampai GameSaveService sepenuhnya replace GameSave.Save() di Fase 4 akhir.
        GameSave.RegisterPersistCallback(PersistFlashlight);

        // FIX: Cancel hold state saat input dikunci (pause, dialog, scene transition).
        // Tanpa ini, _fHeld stuck true jika player release F saat IsInputLocked = true,
        // karena Update() skip wasReleasedThisFrame check saat locked.
        GameState.OnInputLockChanged += OnInputLockChanged;

        _inputIgnoreFrames = 2;

        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void Start()
    {
        // Fase 4: Subscribe ke PlayerEquipment events via injected field.
        // Tidak ada lagi PlayerEquipment.Instance.
        if (_equipment != null)
        {
            _equipment.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
            _equipment.onFlashlightUnequipped.AddListener(OnFlashlightUnequipped);
        }
        else
        {
            // Fallback shim jika inject belum aktif (editor testing)
            var equip = PlayerEquipment.Instance;
            if (equip != null)
            {
                equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
                equip.onFlashlightUnequipped.AddListener(OnFlashlightUnequipped);
            }
        }
    }

    private void OnDestroy()
    {
        GameState.OnInputLockChanged -= OnInputLockChanged;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        GameSave.UnregisterPersistCallback(PersistFlashlight);

        var equip = _equipment ?? PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onFlashlightEquipped.RemoveListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.RemoveListener(OnFlashlightUnequipped);
        }
    }

    private void Update()
    {
        if (!_hasFlashlight) return;

        if (UnityEngine.InputSystem.Keyboard.current != null && !GameState.IsInputLocked)
        {
            if (_inputIgnoreFrames > 0)
            {
                _inputIgnoreFrames--;
            }
            else
            {
                var key = UnityEngine.InputSystem.Keyboard.current.fKey;
                if (key.wasPressedThisFrame)  OnToggleFlashlightStarted();
                if (key.wasReleasedThisFrame) OnToggleFlashlightCanceled();
            }
        }

        if (_spamPressCount > 0)
        {
            _spamWindowTimer += Time.deltaTime;
            if (_spamWindowTimer >= spamWindow)
            {
                _spamPressCount  = 0;
                _spamWindowTimer = 0f;
            }
        }

        if (_isBroken || _isOverheated || _isRecharging) return;
        if (_batteryRemaining <= 0 && _activeBatteryDuration > 0) return;

        if (_fHeld)
        {
            _holdTime += Time.deltaTime;
            onOverheatProgress.Invoke(OverheatPercent);
            NoiseReporter.Add(noiseWhileOn * Time.deltaTime);

            float timeLeft = overheatThreshold - _holdTime;
            if (timeLeft <= overheatWarningAt && _flickerRoutine == null)
                _flickerRoutine = StartCoroutine(OverheatWarningFlicker());

            if (_holdTime >= overheatThreshold)
                TriggerOverheat();
        }
    }

    // ──────────────────────────────────────────
    // Input
    // ──────────────────────────────────────────

    public void OnToggleFlashlightStarted()
    {
        if (!_hasFlashlight) return;
        if (_isBroken || _isOverheated || _isRecharging) return;
        if (_batteryRemaining <= 0 && _activeBatteryDuration > 0) return;

        _spamPressCount++;
        if (_spamPressCount >= spamPressLimit) { TriggerBroken(); return; }

        _fHeld = true;
        TurnOn();
    }

    public void OnToggleFlashlightCanceled()
    {
        if (_isBroken) return;
        _fHeld    = false;
        _holdTime = 0f;
        onOverheatProgress.Invoke(0f);
        StopOverheatWarning();
        TurnOff();
    }

    public void OnToggleFlashlight(InputValue value)
    {
        // Guard: cegah fire saat input locked (pause, dialog, cutscene).
        if (GameState.IsInputLocked) return;

        if (value.isPressed) OnToggleFlashlightStarted();
        else                 OnToggleFlashlightCanceled();
    }

    public void OnRecharge(InputValue value)
    {
        if (!value.isPressed) return;
        if (!_hasFlashlight) return;
        if (_isOn || _isOverheated || _isRecharging) return;
        if (_batteryRemaining > 0 && _activeBatteryDuration > 0) return;

        // Fase 4: Pakai injected _batteryInventory, tidak ada .Instance
        var inv = _batteryInventory ?? PlayerBatteryInventory.Instance;
        if (inv == null || inv.IsEmpty) return;

        StartRecharge(inv);
    }

    // ──────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────

    public FlashlightState GetState() => new FlashlightState
    {
        isOverheated          = _isOverheated,
        overheatTimeRemaining = _overheatTimeRemaining,
        isBroken              = _isBroken,
        brokenTimeRemaining   = _brokenTimeRemaining,
        batteryRemaining      = _batteryRemaining,
    };

    public void RestoreState(FlashlightState state)
    {
        _batteryRemaining = state.batteryRemaining;
        onBatteryChanged.Invoke(BatteryPercent);

        if (state.isOverheated && state.overheatTimeRemaining > 0f)
        {
            _isOverheated          = true;
            _overheatTimeRemaining = state.overheatTimeRemaining;
            onOverheatStart.Invoke();
            if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = StartCoroutine(CooldownRoutine());
        }

        if (state.isBroken && state.brokenTimeRemaining > 0f)
        {
            _isBroken            = true;
            _brokenTimeRemaining = state.brokenTimeRemaining;
            onBrokenStart.Invoke(state.brokenTimeRemaining);
            if (_brokenRoutine != null) StopCoroutine(_brokenRoutine);
            _brokenRoutine = StartCoroutine(BrokenRoutine());
        }
    }

    public void TurnOn()
    {
        if (_isOn || !_hasFlashlight || flashlightLight == null) return;
        if (_isOverheated || _isRecharging) return;
        if (_batteryRemaining <= 0 && _activeBatteryDuration > 0) return;

        _isOn                     = true;
        flashlightLight.enabled   = true;
        flashlightLight.intensity = _baseIntensity;
        onFlashlightOn.Invoke();
        NoiseReporter.Add(noiseOnTurnOn);

        if (_activeBatteryDuration > 0)
        {
            if (_batteryRoutine != null) StopCoroutine(_batteryRoutine);
            _batteryRoutine = StartCoroutine(BatteryDrainRoutine());
        }
    }

    public void TurnOff()
    {
        if (!_isOn) return;
        _isOn = false;
        if (flashlightLight != null) flashlightLight.enabled = false;

        StopOverheatWarning();
        if (_batteryRoutine != null) { StopCoroutine(_batteryRoutine); _batteryRoutine = null; }
        if (_flickerRoutine != null) { StopCoroutine(_flickerRoutine); _flickerRoutine = null; }

        onFlashlightOff.Invoke();
        NoiseReporter.Add(noiseOnTurnOff);
    }

    public void RechargeBattery(float amount)
    {
        if (!_hasFlashlight) return;
        _batteryRemaining = _activeBatteryDuration > 0
            ? Mathf.Min(_batteryRemaining + amount, _activeBatteryDuration)
            : float.MaxValue;
        onBatteryChanged.Invoke(BatteryPercent);
    }

    public void SetBattery(float remaining)
    {
        _batteryRemaining = _activeBatteryDuration > 0
            ? Mathf.Clamp(remaining, 0f, _activeBatteryDuration)
            : remaining;
        onBatteryChanged.Invoke(BatteryPercent);
    }

    // ──────────────────────────────────────────
    // Overheat
    // ──────────────────────────────────────────

    private void TriggerOverheat()
    {
        _fHeld = false; _holdTime = 0f; _isOverheated = true;
        TurnOff();
        onOverheatProgress.Invoke(1f);
        onOverheatStart.Invoke();
        NoiseReporter.Add(noiseOnOverheat);
        _cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        _overheatTimeRemaining = overheatCooldown;
        while (_overheatTimeRemaining > 0f) { _overheatTimeRemaining -= Time.deltaTime; yield return null; }
        _overheatTimeRemaining = 0f;
        _isOverheated = false;
        onOverheatProgress.Invoke(0f);
        onOverheatEnd.Invoke();
        NoiseReporter.Add(noiseOnOverheatEnd);
    }

    private IEnumerator OverheatWarningFlicker()
    {
        while (_isOn && _fHeld && flashlightLight != null)
        {
            float t        = Mathf.Clamp01((_holdTime - (overheatThreshold - overheatWarningAt)) / overheatWarningAt);
            float interval = Mathf.Lerp(flickerMaxInterval, overheatFlickerSpeed, t);

            yield return new WaitForSeconds(interval);
            if (!_isOn || !_fHeld) break;

            flashlightLight.intensity = Random.Range(flickerMinIntensity, _baseIntensity);
            yield return new WaitForSeconds(interval * 0.5f);
            if (_isOn) flashlightLight.intensity = _baseIntensity;
        }
        _flickerRoutine = null;
    }

    private void StopOverheatWarning()
    {
        if (_flickerRoutine != null) { StopCoroutine(_flickerRoutine); _flickerRoutine = null; }
        if (flashlightLight != null) flashlightLight.intensity = _baseIntensity;
    }

    // ──────────────────────────────────────────
    // Recharge
    // ──────────────────────────────────────────

    private void StartRecharge(PlayerBatteryInventory inv)
    {
        if (_rechargeRoutine != null) StopCoroutine(_rechargeRoutine);
        _rechargeRoutine = StartCoroutine(RechargeRoutine(inv));
    }

    private IEnumerator RechargeRoutine(PlayerBatteryInventory inv)
    {
        _isRecharging = true;
        float elapsed = 0f;
        while (elapsed < rechargeDuration)
        {
            elapsed += Time.deltaTime;
            onRechargeProgress.Invoke(Mathf.Clamp01(elapsed / rechargeDuration));
            yield return null;
        }

        float amount = inv.UseBattery();
        if (amount > 0) RechargeBattery(amount);

        _isRecharging = false;
        onRechargeProgress.Invoke(0f);
        onRechargeComplete.Invoke();
        NoiseReporter.Add(noiseOnRecharge);
    }

    // ──────────────────────────────────────────
    // Battery Drain
    // ──────────────────────────────────────────

    private IEnumerator BatteryDrainRoutine()
    {
        while (_isOn && _batteryRemaining > 0)
        {
            _batteryRemaining -= Time.deltaTime;
            _batteryRemaining  = Mathf.Max(0f, _batteryRemaining);
            onBatteryChanged.Invoke(BatteryPercent);

            bool shouldFlicker = BatteryPercent <= _activeFlickerThreshold && !_fHeld;
            if (shouldFlicker && _flickerRoutine == null)
                _flickerRoutine = StartCoroutine(BatteryLowFlicker());

            if (_batteryRemaining <= 0)
            {
                TurnOff(); _fHeld = false;
                onBatteryDepleted.Invoke();
                NoiseReporter.Add(noiseOnDepleted);
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator BatteryLowFlicker()
    {
        while (_isOn && flashlightLight != null && BatteryPercent <= _activeFlickerThreshold)
        {
            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            if (!_isOn) break;

            flashlightLight.intensity = Random.Range(flickerMinIntensity, _baseIntensity);
            yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
            if (_isOn) flashlightLight.intensity = _baseIntensity;
        }
        _flickerRoutine = null;
    }

    // ── IPersistable ──────────────────────────────────────────────────────────

    /// Dipanggil oleh GameSaveService saat Save(). Alias untuk PersistFlashlight.
    public void Persist() => PersistFlashlight();

    // ──────────────────────────────────────────
    // Save / Load
    // ──────────────────────────────────────────

    private void OnInputLockChanged(bool isLocked)
    {
        // Saat input dikunci (pause, dialog, cutscene), force cancel hold state.
        // Ini mencegah _fHeld stuck true jika player release F saat locked,
        // karena Update() tidak membaca wasReleasedThisFrame saat IsInputLocked = true.
        if (isLocked && _fHeld)
        {
            _fHeld    = false;
            _holdTime = 0f;
            onOverheatProgress.Invoke(0f);
            StopOverheatWarning();
            TurnOff();
        }
    }

    private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
    {
        // FIX: Flush battery ke disk setiap kali scene di-unload.
        // Ini menangkap kasus: player pindah scene / quit tanpa melewati checkpoint.
        // ForceWrite() aman dipanggil di sini karena masih di main thread.
        if (_hasFlashlight)
        {
            PersistFlashlight();
            SaveFile.ForceWrite();
        }
    }

    public void PersistFlashlight()
    {
        var d = SaveFile.Data;
        d.flashlightBattery           = _batteryRemaining;
        d.flashlightOverheat          = _isOverheated;
        d.flashlightOverheatRemaining = _overheatTimeRemaining;
        d.flashlightBroken            = _isBroken;
        d.flashlightBrokenRemaining   = _brokenTimeRemaining;
        SaveFile.MarkDirty();
    }

    private void LoadFromSave()
    {
        var d = SaveFile.Data;
        if (d.flashlightBattery < 0f) return;

        _batteryRemaining = d.flashlightBattery;
        onBatteryChanged.Invoke(BatteryPercent);

        if (d.flashlightOverheat && d.flashlightOverheatRemaining > 0f)
        {
            _isOverheated = true; _overheatTimeRemaining = d.flashlightOverheatRemaining;
            onOverheatStart.Invoke();
            if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = StartCoroutine(CooldownRoutine());
        }

        if (d.flashlightBroken && d.flashlightBrokenRemaining > 0f)
        {
            _isBroken = true; _brokenTimeRemaining = d.flashlightBrokenRemaining;
            onBrokenStart.Invoke(d.flashlightBrokenRemaining);
            if (_brokenRoutine != null) StopCoroutine(_brokenRoutine);
            _brokenRoutine = StartCoroutine(BrokenRoutine());
        }
    }

    // ──────────────────────────────────────────
    // Equipment Callbacks
    // ──────────────────────────────────────────

    private void OnFlashlightEquipped(FlashlightItem item)
    {
        _hasFlashlight = true;

        float duration  = batteryDurationOverride;
        float threshold = flickerThresholdOverride;

        if (!useOverride && item != null)
        {
            duration  = item.batteryDuration;
            threshold = item.flickerThreshold;
        }

        _activeFlickerThreshold = threshold;
        _batteryRemaining       = duration > 0 ? duration : float.MaxValue;
        _activeBatteryDuration  = duration;

        if (flashlightLight != null)
            _baseIntensity = flashlightLight.intensity;

        LoadFromSave();
    }

    private void OnFlashlightUnequipped()
    {
        TurnOff();
        _hasFlashlight = false; _fHeld = false; _holdTime = 0f;
        _isRecharging  = false; _spamPressCount = 0; _spamWindowTimer = 0f;

        if (_cooldownRoutine != null) { StopCoroutine(_cooldownRoutine); _cooldownRoutine = null; }
        if (_rechargeRoutine != null) { StopCoroutine(_rechargeRoutine); _rechargeRoutine = null; }
        if (_brokenRoutine   != null) { StopCoroutine(_brokenRoutine);   _brokenRoutine   = null; }

        _isOverheated = false;
        _isBroken     = false;
    }

    // ──────────────────────────────────────────
    // Broken (Spam)
    // ──────────────────────────────────────────

    private void TriggerBroken()
    {
        _isBroken = true; _spamPressCount = 0; _spamWindowTimer = 0f;
        _fHeld    = false; _holdTime = 0f;
        TurnOff();
        onBrokenStart.Invoke(spamBrokenDuration);
        NoiseReporter.Add(noiseOnBroken);

        if (_brokenRoutine != null) StopCoroutine(_brokenRoutine);
        _brokenRoutine = StartCoroutine(BrokenRoutine());
    }

    private IEnumerator BrokenRoutine()
    {
        _brokenTimeRemaining = spamBrokenDuration;
        while (_brokenTimeRemaining > 0f) { _brokenTimeRemaining -= Time.deltaTime; yield return null; }
        _brokenTimeRemaining = 0f;
        _isBroken = false;
        onBrokenEnd.Invoke();
    }
}