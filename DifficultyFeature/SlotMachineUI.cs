using DifficultyFeature;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SlotMachineUI : SemiUI
{
    public static SlotMachineUI instance;

    private bool setup = true;
    private bool isRunning = false;
    private Image slotImage;

    public Sprite defaultSprite;
    public Sprite[] eventSprites;

    private float showDuration = 3f;
    private float timer = 0f;

    private Image slot1;
    private Image slot2;
    private Image slot3;

    private Coroutine[] slotCoroutines = new Coroutine[3];
    public List<Image> slotImages;

    public override void Start()
    {
        base.Start();
        instance = this;

        slot1 = transform.Find("Slot1").GetComponent<Image>();
        slot2 = transform.Find("Slot2").GetComponent<Image>();
        slot3 = transform.Find("Slot3").GetComponent<Image>();

        slotImages = new List<Image> { slot1, slot2, slot3 };

        slot1.enabled = false;
        slot2.enabled = false;
        slot3.enabled = false;
    }

    public override void Update()
    {
        base.Update();

        if (setup)
        {
            if (LevelGenerator.Instance.Generated)
            {
                // HUD prêt → On peut se montrer plus tard
                setup = false;
            }
        }

        if (isRunning)
        {
            timer += Time.deltaTime;
            if (timer >= showDuration)
            {
                slot1.enabled = false;
                slot2.enabled = false;
                slot3.enabled = false;
                isRunning = false;
            }
        }
    }
    public void TriggerSlotAnimation(int eventIndex)
    {
        if (isRunning) return;
        StartCoroutine(SlotSequence(eventIndex));
    }

    private IEnumerator SlotSequence(int eventIndex)
    {
        isRunning = true;
        slot1.enabled = true;
        slot2.enabled = true;
        slot3.enabled = true;

        // 1. Apparition glitchée
        yield return PlayGlitchIntro();

        // 2. Démarrer slots
        StartSlotRoll(0);
        yield return new WaitForSeconds(0.2f);
        StartSlotRoll(1);
        yield return new WaitForSeconds(0.2f);
        StartSlotRoll(2);

        yield return new WaitForSeconds(3f); // Roulement complet

        // 3. Arrêts avec tremblement
        StopSlot(0, eventIndex);
        SemiUISpringShakeY(20f, 10f, 0.3f);
        yield return new WaitForSeconds(0.4f);
        StopSlot(1, eventIndex);
        SemiUISpringShakeY(20f, 10f, 0.3f);
        yield return new WaitForSeconds(0.4f);
        StopSlot(2, eventIndex);
        SemiUISpringShakeY(20f, 10f, 0.3f);

        yield return new WaitForSeconds(1f);


        ISlotEvent e = SlotEventManager.GetRandomEvent();
        Debug.Log(e);
        if (e != null)
        {
            Debug.Log(e.EventName);
            var icon = SlotEventManager.GetIconForEvent(e.EventName);
            e.Execute(); //ligne 114
        }

        // 4. Extinction
        yield return PlayTVOffOutro();


        slot1.enabled = false;
        slot2.enabled = false;
        slot3.enabled = false;
        isRunning = false;

    }

    private IEnumerator PlayGlitchIntro()
    {
        float duration = 0.6f;
        float t = 0f;

        var cg = GetComponent<CanvasGroup>();
        if (cg == null)
            cg = gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        transform.localScale = Vector3.zero;
        SlotAssetLoader.currentInstance.SetActive(true);

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;

            // Glitchy scale / alpha
            float glitch = UnityEngine.Random.Range(0.9f, 1.1f);
            transform.localScale = Vector3.one * progress * glitch;
            cg.alpha = Mathf.Min(1f, progress + UnityEngine.Random.Range(-0.1f, 0.1f));

            yield return null;
        }

        //transform.localScale = Vector3.one;
        //SlotAssetLoader.currentInstance.transform.localScale = Vector3.one;
        cg.alpha = 1f;
    }

    private void StopSlot(int index, int eventIndex)
    {
        if (slotCoroutines[index] != null)
        {
            StopCoroutine(slotCoroutines[index]);
            slotCoroutines[index] = null;
        }

        // Montre le "résultat" (toujours blanc ici)
        slotImages[index].sprite = GenerateWhiteSquareSprite(128);
    }

    private void StartSlotRoll(int index)
    {
        if (slotCoroutines[index] != null)
            StopCoroutine(slotCoroutines[index]);

        slotCoroutines[index] = StartCoroutine(RollingEffect(index));
    }

    private IEnumerator RollingEffect(int index)
    {
        while (true)
        {
            slotImages[index].sprite = GenerateWhiteSquareSprite(UnityEngine.Random.Range(64, 128));
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator PlayTVOffOutro()
    {
        Vector3 originalScale = transform.localScale;
        float duration = 0.3f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float yScale = Mathf.Lerp(originalScale.y, 0f, time / duration);
            transform.localScale = new Vector3(originalScale.x, yScale, originalScale.z);
            yield return null;
        }

        SlotAssetLoader.currentInstance.SetActive(false);
        yield return new WaitForSeconds(0.1f);
        transform.localScale = originalScale;
    }

    public static Sprite GenerateWhiteSquareSprite(int size = 128)
    {


        Texture2D texture = new Texture2D(size, size);
        Color32 white = new Color32(255, 255, 255, 255);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                texture.SetPixel(x, y, white);
            }
        }
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
