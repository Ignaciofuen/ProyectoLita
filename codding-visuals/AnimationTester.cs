using UnityEngine;

public class AnimationTester : MonoBehaviour
{
    AvatarMaster avatar;

    void Start()
    {
        avatar = GetComponent<AvatarMaster>();
    }

    void Update()
    {
        if (avatar == null) return;

        // --- ANIMACIONES (Triggers) ---
        if (Input.GetKeyDown(KeyCode.Alpha1)) avatar.PlayAnimation("wave");
        if (Input.GetKeyDown(KeyCode.Alpha2)) avatar.PlayAnimation("bashful");
        if (Input.GetKeyDown(KeyCode.Alpha3)) avatar.PlayAnimation("surprised");
        if (Input.GetKeyDown(KeyCode.Alpha4)) avatar.PlayAnimation("thankful");
        if (Input.GetKeyDown(KeyCode.Alpha5)) avatar.PlayAnimation("yawn");
        if (Input.GetKeyDown(KeyCode.Alpha6)) avatar.PlayAnimation("angry");
        if (Input.GetKeyDown(KeyCode.Alpha7)) avatar.PlayRandomDance();

        // --- ANIMACIÓN DE PENSAR (Bools) ---
        // Usa las funciones dedicadas para cambiar el Bool a true/false
        if (Input.GetKeyDown(KeyCode.Alpha8)) avatar.OnThinking();
        if (Input.GetKeyDown(KeyCode.T)) avatar.OnStopThinking();

        if (Input.GetKeyDown(KeyCode.Alpha9)) avatar.PlayAnimation("guitar");
        if (Input.GetKeyDown(KeyCode.Alpha0)) avatar.PlayAnimation("texting");

        // --- EMOCIONES ---
        if (Input.GetKeyDown(KeyCode.H)) avatar.SetEmotion("happy");
        if (Input.GetKeyDown(KeyCode.S)) avatar.SetEmotion("sad");
        if (Input.GetKeyDown(KeyCode.A)) avatar.SetEmotion("angry");
        if (Input.GetKeyDown(KeyCode.N)) avatar.SetEmotion("neutral");

        // --- ESTADO DE ÁNIMO ---
        if (Input.GetKeyDown(KeyCode.B)) avatar.ImproveMood(-1f);

        // --- RESET ---
        if (Input.GetKeyDown(KeyCode.R)) avatar.PlayAnimation("greetingMorning");
    }
}