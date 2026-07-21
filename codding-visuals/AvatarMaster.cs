using UnityEngine;
using UniVRM10;
using System.IO;
using System.Collections.Generic;
using URandom = UnityEngine.Random;

public class AvatarMaster : MonoBehaviour
{
    Vrm10Instance vrm;
    Animator animator;
    Transform headBone;
    Transform eyeBoneL;
    Transform eyeBoneR;
    SkinnedMeshRenderer faceMesh;

    // =====================
    // CONSTANTES
    // =====================
    const float WINDOW_SHAKE_THRESHOLD = 150f;
    const float MOUSE_ACTIVE_THRESHOLD = 2f;
    const float BLINK_DOUBLE_PAUSE     = 0.08f;
    const float MOOD_MAX               = 1f;
    const float MOOD_MIN               = -1f;
    const float EXPRESSION_SPEED       = 3f;
    const float FACIAL_SPEED          = 4f;

    const string BROW_ANGRY    = "Fcl_BRW_Angry";
    const string BROW_SURPRISE = "Fcl_BRW_Fun";
    const string MOUTH_OPEN    = "Fcl_MTH_A";
    const string CHEEK_PUFF    = "Fcl_ALL_Angry";
    const string EYE_SQUINT    = "Fcl_EYE_Close_L";

    // =====================
    // INSPECTOR
    // =====================
    [Header("Personalidad (0=nada, 1=maximo)")]
    [Range(0f, 1f)] public float shyness       = 0.5f;
    [Range(0f, 1f)] public float cheerfulness   = 0.7f;
    [Range(0f, 1f)] public float curiosity      = 0.6f;
    [Range(0f, 1f)] public float sensitivity    = 0.5f;

    [Header("Usuario")]
    public string userName = "";

    [Header("Parpadeo")]
    public float blinkIntervalMin  = 2f;
    public float blinkIntervalMax  = 5f;
    public float blinkSpeed        = 10f;
    public float doubleBlinkChance = 0.3f;

    [Header("Mirada")]
    public float lookAroundInterval  = 4f;
    public float maxLookAngle        = 8f;
    public float lookTransitionSpeed = 2f;
    public float thinkingLookUpAngle = 15f;

    [Header("Cabeza")]
    public float headTiltInterval = 6f;
    public float maxHeadTilt      = 4f;
    public float headNodInterval  = 8f;
    public float maxHeadNod       = 3f;

    [Header("Estado de animo")]
    public float moodDecayRate = 0.01f;

    [Header("Aburrimiento")]
    public float boredTimeLimit     = 180f;
    public float veryBoredTimeLimit = 300f;

    [Header("Bostezo y estornudo")]
    public float yawnInterval   = 120f;
    public float sneezeInterval = 180f;

    [Header("Estiramiento")]
    public float stretchInterval = 240f;

    [Header("Deteccion de ausencia")]
    public float awayTimeLimit     = 60f;
    public float veryAwayTimeLimit = 300f;

    [Header("Micro expresiones")]
    public float microExprInterval = 15f;

    [Header("Cola de emociones")]
    public float emotionQueueInterval = 2f;

    [Header("Musica")]
    public AudioSource musicSource;
    public float musicBobSpeed  = 2f;
    public float musicBobAmount = 3f;

    // =====================
    // ESTADO INTERNO
    // =====================
    public bool isTalking { get; private set; }
    bool isThinking;
    bool isBored;
    bool isVeryBored;
    bool isAway;
    bool isVeryAway;
    bool isNight;
    bool isMorning;
    bool isEvening;
    bool isDawnSleep;
    bool isDayGood;
    bool isHoliday;
    bool isNodding;
    bool blinking;
    bool doubleBlink;
    bool doingMicroExpr;
    bool isPlayingTriggerAnim;

    string holidayName = "";
    string savedMoodPath;
    string savedDatePath;
    string currentEmotion = "neutral";

    float currentMood;
    float moodSmooth;
    float idleTimer;
    float awayTimer;
    float yawnTimer;
    float sneezeTimer;
    float stretchTimer;
    float microExprTimer;
    float microExprDuration;
    float clickCooldown;
    float emotionQueueTimer;
    float musicBobTimer;
    float sleepWeight;
    float blinkTimer;
    float blinkValue;
    int   blinkCount;

    // Mirada
    float lookTimer;
    float targetYaw;
    float targetPitch;
    float currentYaw;
    float currentPitch;

    // Cabeza
    float headTiltTimer;
    float targetHeadTilt;
    float currentHeadTilt;
    float headNodTimer;
    float targetHeadNod;
    float currentHeadNod;

    // Posicion mouse
    Vector3 lastMousePos;
    Vector3 lastMousePosAway;

    // Cola de emociones
    Queue<string> emotionQueue = new Queue<string>();

    // =====================
    // EXPRESIONES — targets y actuales
    // =====================
    float happyTarget,     happyCurrent;
    float angryTarget,     angryCurrent;
    float sadTarget,       sadCurrent;
    float surprisedTarget, surprisedCurrent;
    float neutralTarget,   neutralCurrent;

    // Faciales avanzados
    float blushTarget,       blushCurrent;
    float cheekPuffTarget,   cheekPuffCurrent;
    float mouthOpenTarget,   mouthOpenCurrent;
    float browAngryTarget,   browAngryCurrent;
    float browSurpriseTarget, browSurpriseCurrent;
    float squintTarget,      squintCurrent;
    float confusedTarget,    confusedCurrent;

    // Indices BlendShape cacheados
    int idxBrowAngry    = -1;
    int idxBrowSurprise = -1;
    int idxMouthOpen    = -1;
    int idxCheekPuff    = -1;
    int idxSquint       = -1;

    // Triggers disponibles
    static readonly string[] danceTriggers = { "dance", "hipHopDance", "twerk" };
    static readonly string[] idleTriggers  = { "guitar", "texting", "textingStanding" };

    // =====================
    // INICIO
    // =====================
    void Start()
    {
        vrm      = GetComponent<Vrm10Instance>();
        animator = GetComponent<Animator>();

        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            eyeBoneL = anim.GetBoneTransform(HumanBodyBones.LeftEye);
            eyeBoneR = anim.GetBoneTransform(HumanBodyBones.RightEye);
        }

        // Cachear SkinnedMeshRenderer y BlendShape indices
        faceMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        if (faceMesh != null)
        {
            idxBrowAngry    = faceMesh.sharedMesh.GetBlendShapeIndex(BROW_ANGRY);
            idxBrowSurprise = faceMesh.sharedMesh.GetBlendShapeIndex(BROW_SURPRISE);
            idxMouthOpen    = faceMesh.sharedMesh.GetBlendShapeIndex(MOUTH_OPEN);
            idxCheekPuff    = faceMesh.sharedMesh.GetBlendShapeIndex(CHEEK_PUFF);
            idxSquint       = faceMesh.sharedMesh.GetBlendShapeIndex(EYE_SQUINT);
        }

        savedMoodPath = Application.persistentDataPath + "/mood.txt";
        savedDatePath = Application.persistentDataPath + "/lastopen.txt";

        LoadMood();
        CheckLastOpenDate();
        CheckCalendar();
        CheckSchedule();
        RollDailyMood();
        ApplyPersonalityToDefaults();
        PlayGreeting();

        ResetBlinkTimer();
        lookTimer      = URandom.Range(0f, lookAroundInterval);
        headTiltTimer  = URandom.Range(0f, headTiltInterval);
        headNodTimer   = URandom.Range(0f, headNodInterval);
        yawnTimer      = URandom.Range(30f, yawnInterval);
        sneezeTimer    = URandom.Range(60f, sneezeInterval);
        stretchTimer   = URandom.Range(stretchInterval * 0.5f, stretchInterval);
        microExprTimer = URandom.Range(microExprInterval * 0.5f, microExprInterval * 1.5f);
        lastMousePos     = Input.mousePosition;
        lastMousePosAway = Input.mousePosition;
    }

    void Update()
    {
        if (vrm == null) return;

        CheckSchedule();
        HandleBlink();
        HandleBoredom();
        HandleRandomLook();
        HandleHeadTilt();
        HandleWindowShake();
        HandleClickReaction();
        HandleYawnAndSneeze();
        HandleStretch();
        HandleAwayDetection();
        HandleMicroExpressions();
        HandleEmotionQueue();
        HandleMusicReaction();
        HandleSleepiness();
        UpdateMood();
        UpdateExpressions();
        UpdateFacialBlendShapes();
    }

    void OnApplicationQuit()
    {
        SaveMood();
        SaveOpenDate();
    }

    // =====================
    // PERSONALIDAD
    // =====================
    void ApplyPersonalityToDefaults()
    {
        blinkIntervalMin   = Mathf.Lerp(2f,   1.2f, shyness);
        blinkIntervalMax   = Mathf.Lerp(5f,   3f,   shyness);
        doubleBlinkChance  = Mathf.Lerp(0.2f, 0.5f, shyness);
        maxLookAngle       = Mathf.Lerp(6f,   14f,  curiosity);
        lookAroundInterval = Mathf.Lerp(6f,   2f,   curiosity);
        boredTimeLimit     = Mathf.Lerp(300f, 100f, sensitivity);
        veryBoredTimeLimit = Mathf.Lerp(500f, 180f, sensitivity);
    }

    // =====================
    // CALENDARIO
    // =====================
    void CheckCalendar()
    {
        var now     = System.DateTime.Now;
        int month   = now.Month;
        int day     = now.Day;
        var weekday = now.DayOfWeek;

        isHoliday   = false;
        holidayName = "";

        if      (month == 12 && day == 25) { isHoliday = true; holidayName = "navidad"; }
        else if (month == 1  && day == 1)  { isHoliday = true; holidayName = "anionuevo"; }
        else if (month == 10 && day == 31) { isHoliday = true; holidayName = "halloween"; }

        bool isFriday = weekday == System.DayOfWeek.Friday;
        bool isMonday = weekday == System.DayOfWeek.Monday;

        if (isFriday)  currentMood = Mathf.Clamp(currentMood + 0.3f, MOOD_MIN, MOOD_MAX);
        if (isMonday)  currentMood = Mathf.Clamp(currentMood - 0.2f, MOOD_MIN, MOOD_MAX);
        if (isHoliday) currentMood = MOOD_MAX;
    }

    // =====================
    // DIA BUENO O MALO
    // =====================
    void RollDailyMood()
    {
        string todayKey = System.DateTime.Now.ToString("yyyy-MM-dd");
        string savedKey = PlayerPrefs.GetString("DailyMoodDate", "");

        if (savedKey != todayKey)
        {
            isDayGood = URandom.value > 0.4f;
            PlayerPrefs.SetString("DailyMoodDate", todayKey);
            PlayerPrefs.SetInt("DailyMoodGood", isDayGood ? 1 : 0);
        }
        else
        {
            isDayGood = PlayerPrefs.GetInt("DailyMoodGood", 1) == 1;
        }

        float moodShift = isDayGood
            ? 0.2f  * cheerfulness
            : -0.2f * sensitivity;
        currentMood = Mathf.Clamp(currentMood + moodShift, MOOD_MIN, MOOD_MAX);
    }

    // =====================
    // ULTIMA VEZ ABIERTA
    // =====================
    void CheckLastOpenDate()
    {
        if (!File.Exists(savedDatePath)) return;

        string lastOpen = File.ReadAllText(savedDatePath).Trim();
        if (!System.DateTime.TryParse(lastOpen, out System.DateTime lastDate)) return;

        int daysSince = (System.DateTime.Now - lastDate).Days;
        if (daysSince >= 3)
            currentMood = Mathf.Clamp(currentMood - 0.3f * sensitivity, MOOD_MIN, MOOD_MAX);
        else if (daysSince == 0)
            currentMood = Mathf.Clamp(currentMood + 0.1f, MOOD_MIN, MOOD_MAX);
    }

    void SaveOpenDate()
    {
        File.WriteAllText(savedDatePath, System.DateTime.Now.ToString("yyyy-MM-dd"));
    }

    // =====================
    // HORARIO
    // =====================
    void CheckSchedule()
    {
        int hour    = System.DateTime.Now.Hour;
        isMorning   = hour >= 7  && hour < 12;
        isEvening   = hour >= 18 && hour < 22;
        isNight     = hour >= 22 || (hour >= 0 && hour < 3);
        isDawnSleep = hour >= 3  && hour < 7;

        SafeSetBool("isNight",   isNight);
        SafeSetBool("isMorning", isMorning);
    }

    // =====================
    // SOMNOLENCIA
    // =====================
    void HandleSleepiness()
    {
        float target = isDawnSleep ? 0.85f : 0f;
        sleepWeight  = Mathf.Lerp(sleepWeight, target, Time.deltaTime * 0.5f);
    }

    // =====================
    // SALUDO
    // =====================
    void PlayGreeting()
    {
        CancelInvoke("ResetToNeutral");

        if (isHoliday)
        {
            SafeTrigger("happy"); SetEmotion("happy");
        }
        else if (isDawnSleep)
        {
            SafeTrigger("yawn"); SetEmotion("sad");
        }
        else if (System.DateTime.Now.DayOfWeek == System.DayOfWeek.Monday)
        {
            SafeTrigger("greetingAfternoon"); SetEmotion("sad");
        }
        else if (System.DateTime.Now.DayOfWeek == System.DayOfWeek.Friday)
        {
            SafeTrigger("happy"); SetEmotion("happy");
        }
        else
        {
            int hour = System.DateTime.Now.Hour;
            if      (hour >= 7  && hour < 12) { SafeTrigger("greetingMorning");   SetEmotion("happy"); }
            else if (hour >= 12 && hour < 18) { SafeTrigger("greetingAfternoon"); SetEmotion("happy"); }
            else                              { SafeTrigger("greetingEvening");   SetEmotion("neutral"); }
        }

        Invoke("ResetToNeutral", 3f);
    }

    // =====================
    // PARPADEO
    // =====================
    void ResetBlinkTimer()
    {
        float multiplier = (isNight || isDawnSleep) ? 2f : 1f;
        blinkTimer = URandom.Range(blinkIntervalMin, blinkIntervalMax) * multiplier;
    }

    void HandleBlink()
    {
        blinkTimer -= Time.deltaTime;

        if (blinkTimer <= 0 && !blinking)
        {
            blinking    = true;
            blinkCount  = 0;
            doubleBlink = URandom.value < doubleBlinkChance;
            ResetBlinkTimer();
        }

        if (!blinking) return;

        blinkValue += Time.deltaTime * blinkSpeed;
        float blink    = Mathf.Clamp01(Mathf.Sin(blinkValue * Mathf.PI));
        float minBlink = isDawnSleep ? sleepWeight * 0.6f : 0f;
        blink = Mathf.Max(blink, minBlink);

        vrm.Runtime.Expression.SetWeight(ExpressionKey.Blink, blink);

        if (blinkValue < 1f) return;

        blinkValue = 0f;
        blinkCount++;

        if (doubleBlink && blinkCount < 2)
        {
            blinkTimer = BLINK_DOUBLE_PAUSE;
            blinking   = false;
        }
        else
        {
            blinking = false;
            float finalBlink = isDawnSleep ? sleepWeight * 0.5f : 0f;
            vrm.Runtime.Expression.SetWeight(ExpressionKey.Blink, finalBlink);
        }
    }

    // =====================
    // MIRADA
    // =====================
    void HandleRandomLook()
    {
        if (eyeBoneL == null || eyeBoneR == null) return;

        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0)
        {
            if (isThinking)
            {
                targetYaw   = URandom.Range(-3f, 3f);
                targetPitch = thinkingLookUpAngle;
            }
            else if (isDawnSleep)
            {
                targetYaw   = URandom.Range(-2f, 2f);
                targetPitch = URandom.Range(-5f, -2f);
            }
            else
            {
                targetYaw   = URandom.Range(-maxLookAngle, maxLookAngle);
                targetPitch = URandom.Range(-maxLookAngle * 0.4f, maxLookAngle * 0.4f);
            }
            lookTimer = URandom.Range(lookAroundInterval * 0.5f, lookAroundInterval * 1.5f);
        }

        currentYaw   = Mathf.Lerp(currentYaw,   targetYaw,   Time.deltaTime * lookTransitionSpeed);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * lookTransitionSpeed);

        Quaternion eyeRot = Quaternion.Euler(-currentPitch, currentYaw, 0f);
        float eyeSpeed    = Time.deltaTime * lookTransitionSpeed * 2f;
        eyeBoneL.localRotation = Quaternion.Slerp(eyeBoneL.localRotation, eyeRot, eyeSpeed);
        eyeBoneR.localRotation = Quaternion.Slerp(eyeBoneR.localRotation, eyeRot, eyeSpeed);
    }

    // =====================
    // CABEZA
    // =====================
    void HandleHeadTilt()
    {
        if (headBone == null) return;

        headTiltTimer -= Time.deltaTime;
        if (headTiltTimer <= 0)
        {
            targetHeadTilt = URandom.Range(-maxHeadTilt, maxHeadTilt);
            headTiltTimer  = URandom.Range(headTiltInterval * 0.5f, headTiltInterval * 1.5f);
        }
        currentHeadTilt = Mathf.Lerp(currentHeadTilt, targetHeadTilt, Time.deltaTime * 1.5f);

        headNodTimer -= Time.deltaTime;
        if (headNodTimer <= 0)
        {
            targetHeadNod = URandom.Range(-maxHeadNod, maxHeadNod);
            headNodTimer  = URandom.Range(headNodInterval * 0.5f, headNodInterval * 1.5f);
        }
        currentHeadNod = Mathf.Lerp(currentHeadNod, targetHeadNod, Time.deltaTime * 1.2f);

        Vector3 euler = headBone.localEulerAngles;
        float tilt    = euler.z > 180 ? euler.z - 360 : euler.z;
        float nod     = euler.x > 180 ? euler.x - 360 : euler.x;

        headBone.localEulerAngles = new Vector3(
            Mathf.Lerp(nod,  currentHeadNod,  Time.deltaTime * 1.5f),
            euler.y,
            Mathf.Lerp(tilt, currentHeadTilt, Time.deltaTime * 1.5f)
        );
    }

    // =====================
    // ABURRIMIENTO
    // =====================
    void HandleBoredom()
    {
        if (isTalking || isPlayingTriggerAnim)
        {
            idleTimer = 0f;
            if (!isBored && !isVeryBored) return;

            isBored     = false;
            isVeryBored = false;
            SafeSetBool("isBored",     false);
            SafeSetBool("isVeryBored", false);
            SetEmotion("neutral");
            return;
        }

        idleTimer += Time.deltaTime;

        if (idleTimer >= boredTimeLimit && !isBored)
        {
            isBored      = true;
            currentMood  = Mathf.Clamp(currentMood - 0.2f, MOOD_MIN, MOOD_MAX);
            SafeSetBool("isBored", true);
            SetEmotion("sad");
        }

        if (idleTimer >= veryBoredTimeLimit && !isVeryBored)
        {
            isVeryBored  = true;
            currentMood  = Mathf.Clamp(currentMood - 0.3f, MOOD_MIN, MOOD_MAX);
            SafeSetBool("isVeryBored", true);
        }
    }

    // =====================
    // BOSTEZO Y ESTORNUDO
    // =====================
    void HandleYawnAndSneeze()
    {
        if (isNight || isDawnSleep)
        {
            yawnTimer -= Time.deltaTime;
            if (yawnTimer <= 0)
            {
                SafeTrigger("yawn");
                mouthOpenTarget = 0.8f;
                CancelInvoke("ResetMouthOpen");
                Invoke("ResetMouthOpen", 2f);
                yawnTimer = URandom.Range(yawnInterval * 0.8f, yawnInterval * 1.2f);
            }
        }

        if (isNight)
        {
            sneezeTimer -= Time.deltaTime;
            if (sneezeTimer <= 0)
            {
                SafeTrigger("surprised");
                SetEmotion("surprised");
                CancelInvoke("ResetToNeutral");
                Invoke("ResetToNeutral", 2f);
                sneezeTimer = URandom.Range(sneezeInterval * 0.8f, sneezeInterval * 1.5f);
            }
        }
    }

    void ResetMouthOpen() => mouthOpenTarget = 0f;

    // =====================
    // ESTIRAMIENTO
    // =====================
    void HandleStretch()
    {
        if (isTalking || isBored || isPlayingTriggerAnim) return;

        stretchTimer -= Time.deltaTime;
        if (stretchTimer <= 0)
        {
            SafeTrigger("thankful");
            stretchTimer = URandom.Range(stretchInterval * 0.8f, stretchInterval * 1.5f);
        }
    }

    // =====================
    // MUSICA
    // =====================
    void HandleMusicReaction()
    {
        if (musicSource == null || !musicSource.isPlaying)
        {
            isNodding = false;
            return;
        }

        isNodding = true;

        if (headBone == null) return;

        musicBobTimer += Time.deltaTime * musicBobSpeed;
        float bob      = Mathf.Sin(musicBobTimer) * musicBobAmount;

        Vector3 euler = headBone.localEulerAngles;
        float nod     = euler.x > 180 ? euler.x - 360 : euler.x;
        headBone.localEulerAngles = new Vector3(
            Mathf.Lerp(nod, bob, Time.deltaTime * 3f),
            euler.y,
            euler.z
        );
    }

    // =====================
    // AUSENCIA
    // =====================
    void HandleAwayDetection()
    {
        float delta      = Vector3.Distance(Input.mousePosition, lastMousePosAway);
        lastMousePosAway = Input.mousePosition;
        bool userActive  = delta > MOUSE_ACTIVE_THRESHOLD || Input.anyKey;

        if (userActive)
        {
            awayTimer = 0f;
            if (!isAway && !isVeryAway) return;

            isAway     = false;
            isVeryAway = false;
            SafeSetBool("isAway",     false);
            SafeSetBool("isVeryAway", false);
            SetEmotion("happy");
            SafeTrigger("wave");
            CancelInvoke("ResetToNeutral");
            Invoke("ResetToNeutral", 2f);
            currentMood = Mathf.Clamp(currentMood + 0.15f, MOOD_MIN, MOOD_MAX);
            return;
        }

        awayTimer += Time.deltaTime;

        if (awayTimer >= awayTimeLimit && !isAway)
        {
            isAway = true;
            SafeSetBool("isAway", true);
        }

        if (awayTimer >= veryAwayTimeLimit && !isVeryAway)
        {
            isVeryAway  = true;
            currentMood = Mathf.Clamp(currentMood - 0.2f, MOOD_MIN, MOOD_MAX);
            SafeSetBool("isVeryAway", true);
            SetEmotion("sad");
        }
    }

    // =====================
    // MICRO EXPRESIONES
    // =====================
    void HandleMicroExpressions()
    {
        if (doingMicroExpr)
        {
            microExprDuration -= Time.deltaTime;
            if (microExprDuration <= 0)
            {
                doingMicroExpr = false;
                ResetToNeutral();
            }
            return;
        }

        microExprTimer -= Time.deltaTime;
        if (microExprTimer > 0) return;

        microExprTimer = URandom.Range(microExprInterval * 0.5f, microExprInterval * 1.5f);

        float roll = URandom.value;
        if      (roll < cheerfulness * 0.4f)                          TriggerMicroExpression("happy");
        else if (roll < cheerfulness * 0.4f + shyness * 0.3f)        TriggerMicroExpression("surprised");
        else                                                           TriggerMicroExpression("neutral");
    }

    void TriggerMicroExpression(string emotion)
    {
        doingMicroExpr    = true;
        microExprDuration = URandom.Range(0.3f, 0.7f);
        if (emotion == "surprised" && shyness > 0.5f)
            blushTarget = shyness;
        SetEmotion(emotion);
    }

    // =====================
    // COLA DE EMOCIONES
    // =====================
    void HandleEmotionQueue()
    {
        if (emotionQueue.Count == 0) return;

        emotionQueueTimer -= Time.deltaTime;
        if (emotionQueueTimer > 0) return;

        SetEmotion(emotionQueue.Dequeue());
        emotionQueueTimer = emotionQueueInterval;
    }

    // =====================
    // VENTANA
    // =====================
    void HandleWindowShake()
    {
        float delta  = Vector3.Distance(Input.mousePosition, lastMousePos);
        lastMousePos = Input.mousePosition;

        if (delta <= WINDOW_SHAKE_THRESHOLD) return;

        SetEmotion("surprised");
        SafeTrigger("surprised");
        if (shyness > 0.5f) blushTarget = shyness * 0.5f;
        CancelInvoke("ResetToNeutral");
        Invoke("ResetToNeutral", 1.5f);
    }

    // =====================
    // CLICK
    // =====================
    void HandleClickReaction()
    {
        if (clickCooldown > 0)
        {
            clickCooldown -= Time.deltaTime;
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;

        clickCooldown = 2f;

        if (shyness > 0.6f)
        {
            SafeTrigger("bashful");
            SetEmotion("surprised");
            blushTarget  = shyness;
            currentMood  = Mathf.Clamp(currentMood + 0.05f, MOOD_MIN, MOOD_MAX);
        }
        else if (cheerfulness > 0.6f)
        {
            SafeTrigger("wave");
            SetEmotion("happy");
            currentMood = Mathf.Clamp(currentMood + 0.15f, MOOD_MIN, MOOD_MAX);
        }
        else
        {
            string[] reactions = { "wave", "thankful", "bashful" };
            SafeTrigger(reactions[URandom.Range(0, reactions.Length)]);
            SetEmotion("happy");
            currentMood = Mathf.Clamp(currentMood + 0.1f, MOOD_MIN, MOOD_MAX);
        }

        CancelInvoke("ResetToNeutral");
        Invoke("ResetToNeutral", 2f);
    }

    // =====================
    // MOOD
    // =====================
    void UpdateMood()
    {
        currentMood = Mathf.Clamp(currentMood, MOOD_MIN, MOOD_MAX);
        moodSmooth  = Mathf.Lerp(moodSmooth, currentMood, Time.deltaTime * 0.5f);
        currentMood = Mathf.MoveTowards(currentMood, 0f, moodDecayRate * Time.deltaTime);
        blushTarget = Mathf.MoveTowards(blushTarget, 0f, Time.deltaTime * 0.3f);
        cheekPuffTarget = Mathf.MoveTowards(cheekPuffTarget, 0f, Time.deltaTime * 0.5f);
    }

    void SaveMood()
    {
        File.WriteAllText(savedMoodPath, currentMood.ToString());
    }

    void LoadMood()
    {
        if (!File.Exists(savedMoodPath)) { currentMood = 0f; return; }

        string saved = File.ReadAllText(savedMoodPath);
        if (float.TryParse(saved, out float mood))
            currentMood = Mathf.Clamp(mood * 0.5f, -0.5f, 0.5f);
        else
            currentMood = 0f;
    }

    // =====================
    // EXPRESIONES BASE
    // =====================
    void UpdateExpressions()
    {
        float speed = Time.deltaTime * EXPRESSION_SPEED;

        happyCurrent     = Mathf.Lerp(happyCurrent,     happyTarget,     speed);
        angryCurrent     = Mathf.Lerp(angryCurrent,     angryTarget,     speed);
        sadCurrent       = Mathf.Lerp(sadCurrent,       sadTarget,       speed);
        surprisedCurrent = Mathf.Lerp(surprisedCurrent, surprisedTarget, speed);
        neutralCurrent   = Mathf.Lerp(neutralCurrent,   neutralTarget,   speed);

        vrm.Runtime.Expression.SetWeight(ExpressionKey.Happy,     happyCurrent);
        vrm.Runtime.Expression.SetWeight(ExpressionKey.Angry,     angryCurrent);
        vrm.Runtime.Expression.SetWeight(ExpressionKey.Sad,       sadCurrent);
        vrm.Runtime.Expression.SetWeight(ExpressionKey.Surprised, surprisedCurrent);
        vrm.Runtime.Expression.SetWeight(ExpressionKey.Neutral,   neutralCurrent);
    }

    // =====================
    // FACIALES AVANZADOS
    // =====================
    void UpdateFacialBlendShapes()
    {
        if (faceMesh == null) return;

        float speed = Time.deltaTime * FACIAL_SPEED;

        blushCurrent       = Mathf.Lerp(blushCurrent,       blushTarget,       speed);
        cheekPuffCurrent   = Mathf.Lerp(cheekPuffCurrent,   cheekPuffTarget,   speed);
        mouthOpenCurrent   = Mathf.Lerp(mouthOpenCurrent,   mouthOpenTarget,   speed);
        browAngryCurrent   = Mathf.Lerp(browAngryCurrent,   browAngryTarget,   speed);
        browSurpriseCurrent = Mathf.Lerp(browSurpriseCurrent, browSurpriseTarget, speed);
        confusedCurrent    = Mathf.Lerp(confusedCurrent,    confusedTarget,    speed);

        float sleepSquint = isDawnSleep ? sleepWeight * 0.7f : 0f;
        squintCurrent     = Mathf.Lerp(squintCurrent, Mathf.Max(squintTarget, sleepSquint), speed);

        SafeSetBlendShape(idxBrowAngry,    browAngryCurrent);
        SafeSetBlendShape(idxBrowSurprise, browSurpriseCurrent);
        SafeSetBlendShape(idxMouthOpen,    mouthOpenCurrent);
        SafeSetBlendShape(idxCheekPuff,    cheekPuffCurrent);
        SafeSetBlendShape(idxSquint,       squintCurrent);
    }

    void SafeSetBlendShape(int index, float value)
    {
        if (index >= 0)
            faceMesh.SetBlendShapeWeight(index, value * 100f);
    }

    // =====================
    // HELPERS ANIMATOR
    // =====================
    void SafeSetBool(string param, bool value)
    {
        if (animator != null && HasParameter(param, AnimatorControllerParameterType.Bool))
            animator.SetBool(param, value);
    }

    void SafeTrigger(string param)
    {
        if (animator != null && HasParameter(param, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(param);
            animator.SetTrigger(param);
        }
    }

    bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == type) return true;
        return false;
    }

    // =====================
    // EMOCIONES
    // =====================
    void ResetAllEmotionTargets()
    {
        happyTarget = angryTarget = sadTarget = surprisedTarget = neutralTarget = 0f;
        browAngryTarget = browSurpriseTarget = cheekPuffTarget = confusedTarget = 0f;
        mouthOpenTarget = squintTarget = 0f;
    }

    void ResetToNeutral() => SetEmotion("neutral");

    // =====================
    // METODOS PUBLICOS
    // =====================
    public void SetEmotion(string emotion)
    {
        currentEmotion = emotion.ToLower();
        ResetAllEmotionTargets();

        switch (currentEmotion)
        {
            case "happy":
                happyTarget        = Mathf.Clamp01(0.7f + moodSmooth * 0.3f + cheerfulness * 0.2f);
                browSurpriseTarget = 0.3f;
                cheekPuffTarget    = 0.2f * cheerfulness;
                currentMood        = Mathf.Clamp(currentMood + 0.05f, MOOD_MIN, MOOD_MAX);
                break;

            case "angry":
                angryTarget      = 1f;
                browAngryTarget  = 1f;
                currentMood      = Mathf.Clamp(currentMood - 0.1f * sensitivity, MOOD_MIN, MOOD_MAX);
                SafeTrigger("angry");
                break;

            case "sad":
                sadTarget   = 1f;
                currentMood = Mathf.Clamp(currentMood - 0.05f * sensitivity, MOOD_MIN, MOOD_MAX);
                break;

            case "surprised":
                surprisedTarget    = 1f;
                browSurpriseTarget = 1f;
                mouthOpenTarget    = 0.5f;
                if (shyness > 0.5f) blushTarget = shyness * 0.7f;
                CancelInvoke("ResetMouthOpen");
                Invoke("ResetMouthOpen", 1.5f);
                break;

            case "confused":
                confusedTarget = 1f;
                neutralTarget  = 0.5f;
                browAngryTarget = 0.3f;
                break;

            case "squint":
                squintTarget  = 0.8f;
                neutralTarget = 1f;
                break;

            default:
                neutralTarget = 1f;
                break;
        }
    }

    public void OnStartTalking()
    {
        isTalking       = true;
        isThinking      = false;
        idleTimer       = 0f;
        mouthOpenTarget = 0.3f;
        SafeSetBool("isTalking", true);
    }

    public void OnStopTalking()
    {
        isTalking       = false;
        mouthOpenTarget = 0f;
        SafeSetBool("isTalking", false);
        SetEmotion("neutral");
    }

    public void OnThinking()
    {
        isThinking     = true;
        confusedTarget = 0.5f;
        SafeSetBool("isThinking", true);
    }

    public void OnStopThinking()
    {
        isThinking     = false;
        confusedTarget = 0f;
        SafeSetBool("isThinking", false);
    }

    public void OnAIResponse(string text, string emotion)
    {
        OnStopThinking();
        OnStartTalking();
        EnqueueEmotion(emotion);
    }

    public void OnAIFinished()     => OnStopTalking();
    public void OnAIThinking()     => OnThinking();

    public void EnqueueEmotion(string emotion) => emotionQueue.Enqueue(emotion);

    public void PlayAnimation(string animationName) => SafeTrigger(animationName);

    public void PlayRandomDance()
    {
        SafeTrigger(danceTriggers[URandom.Range(0, danceTriggers.Length)]);
    }

    public void PlayRandomIdleAction()
    {
        SafeTrigger(idleTriggers[URandom.Range(0, idleTriggers.Length)]);
    }

    public void TriggerSquint()
    {
        squintTarget = 0.8f;
        CancelInvoke("ResetSquint");
        Invoke("ResetSquint", 2f);
    }

    public void TriggerCheekPuff()  => cheekPuffTarget = 1f;
    public void TriggerConfused()   { SetEmotion("confused"); CancelInvoke("ResetToNeutral"); Invoke("ResetToNeutral", 3f); }

    public void SetUserName(string name)
    {
        userName = name;
        PlayerPrefs.SetString("UserName", name);
    }

    public string GetUserName()
    {
        if (string.IsNullOrEmpty(userName))
            userName = PlayerPrefs.GetString("UserName", "");
        return userName;
    }

    public void ImproveMood(float amount) =>
        currentMood = Mathf.Clamp(currentMood + amount * cheerfulness, MOOD_MIN, MOOD_MAX);

    public void WorsenMood(float amount) =>
        currentMood = Mathf.Clamp(currentMood - amount * sensitivity, MOOD_MIN, MOOD_MAX);

    void ResetSquint() => squintTarget = 0f;

    public float  GetMood()        => moodSmooth;
    public bool   IsAway()         => isAway;
    public bool   IsDayGood()      => isDayGood;
    public bool   IsHoliday()      => isHoliday;
    public string GetHolidayName() => holidayName;
    public string GetEmotion()     => currentEmotion;
}