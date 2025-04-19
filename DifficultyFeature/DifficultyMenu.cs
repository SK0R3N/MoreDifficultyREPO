using DifficultyFeature;
using ExitGames.Client.Photon;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using MenuLib.Structs;
using Photon.Pun;
using Photon.Realtime;
using SingularityGroup.HotReload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using static b;
using static UnityEngine.InputSystem.InputRemoting;

namespace MyMOD
{

    public class DifficultyLabelUI : MonoBehaviour, IOnEventCallback, IInRoomCallbacks
    {
        public static DifficultyLabelUI Instance;
        internal REPOLabel label;

        // Code unique pour l'event RaiseEvent
        public const byte DifficultyEventCode = 101;

        private void Awake()
        {
            Instance = this;

            //PhotonNetwork.AddCallbackTarget(this); 

            var canvas = GameObject.Find("HUD Canvas");
            if (!canvas)
            {
                Debug.LogError("[DifficultyLabelUI] HUD Canvas introuvable.");
                return;
            }

            label = MenuAPI.CreateREPOLabel($"Difficulty: {DifficultyManager.CurrentDifficulty}", canvas.transform, Vector2.zero);
            label.rectTransform.anchorMin = new Vector2(1f, 1f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(1f, 1f);
            StartCoroutine(AdjustDifficultyLabelPosition());

            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this); // Désinscription propre
        }

        public static void SendDifficultyToEveryone(string difficulty)
        {
            Debug.Log("[DifficultyLabelUI] Send Photon");
            PhotonNetwork.RaiseEvent(
                DifficultyEventCode,
                difficulty,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable
            );
        }

        public void OnEvent(EventData photonEvent)
        {
                if (photonEvent.Code == DifficultyEventCode)
                {
                    string difficulty = (string)photonEvent.CustomData;
                    SetLabel(difficulty);
                }
        }

        private void SetLabel(string difficulty)
        {
            Debug.Log("[DifficultyLabelUI] J'ai recu mon rpc");
            if (label == null) return;
            label.labelTMP.text = $"Difficulty: {difficulty}";
            StartCoroutine(AdjustDifficultyLabelPosition());
        }

        public static IEnumerator AdjustDifficultyLabelPosition()
        {
            yield return null;
            float width = Instance.label.labelTMP.preferredWidth;
            float padding = 20f;
            Instance.label.rectTransform.anchoredPosition = new Vector2(-width - padding, -40f);
        }

        public static void SetDifficulty(string difficulty)
        {
            if (Instance?.label == null) return;

            Debug.Log($"[DifficultyLabelUI] Updating label: {difficulty}");
            Instance.SetLabel(difficulty);
            SendDifficultyToEveryone(difficulty);
        }

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log("[DifficultyLabelUI] J'envoieMonRPC");
            if (PhotonNetwork.IsMasterClient)
            {
                Debug.Log("[DifficultyLabelUI] Un joueur a rejoint, envoi de la difficulté dans 1 seconde.");
                StartCoroutine(SendDifficultyDelayed());
            }
        }

        private IEnumerator SendDifficultyDelayed()
        {
            Debug.Log("[DifficultyLabelUI] Début seconde");
            yield return new WaitForSeconds(5f);
            Debug.Log("[DifficultyLabelUI] Fin seconde");
            DifficultyLabelUI.SendDifficultyToEveryone(DifficultyManager.CurrentDifficulty.ToString());
        }

        // Non utilisés ici, mais nécessaires pour IInRoomCallbacks
        public void OnPlayerLeftRoom(Player otherPlayer) { }
        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) { }
        public void OnMasterClientSwitched(Player newMasterClient) { }
    }

    public class DifficultyMenu
    {
        public static string SelectedDifficulty = "Normal";
        private static REPOPopupPage popupRight = null;
        private static REPOPopupPage popupPageDifficulty = null;

        public static void CreateDifficultyButton()
        {
            GameObject menuPageLobby = GameObject.Find("Menu Page Lobby(Clone)");

            if (menuPageLobby == null)
            {
                Debug.LogError("[MyMod] Menu Page Lobby not found!");
                return;
            }

            Vector2 buttonPosition = new Vector2(540f, 320f);

            var button = MenuAPI.CreateREPOButton("Choose Difficulty", OpenDifficultyPopup, menuPageLobby.transform, buttonPosition);
            button.name = "DifficultyButton";

            Debug.Log("[MyMod] Difficulty button added to Lobby!");
        }

        private static void OpenDifficultyPopup()
        {
            var popupPage = MenuAPI.CreateREPOPopupPage("Select Difficulty", REPOPopupPage.PresetSide.Left, shouldCachePage: false, pageDimmerVisibility: true);


            popupPage.AddElementToScrollView(scroll =>
            {
                string[] options = new string[] { "Custom", "Normal", "Hard", "Hardcore", "Nightmare", "IsThatEvenPossible" };

                // CreateREPOSlider(string text, string description, Action<string> onOptionChanged, Transform parent, string[] stringOptions, string defaultOption, Vector2 localPosition = default(Vector2), string prefix = "", string postfix = "", REPOSlider.BarBehavior barBehavior = REPOSlider.BarBehavior.UpdateWithValue)
                var difficultySlider = MenuAPI.CreateREPOSlider(
                    text: "Difficulty",
                    description: "Select the game difficulty.",
                    onOptionChanged: (string difficulty) =>
                    {
                        SelectedDifficulty = difficulty;
                        DifficultyManager.CurrentDifficulty = (DifficultyManager.DifficultyLevel)Enum.Parse(typeof(DifficultyManager.DifficultyLevel), SelectedDifficulty);
                        DifficultySaveManager.SaveDifficulty(SelectedDifficulty);
                        DifficultyLabelUI.SetDifficulty(SelectedDifficulty);
                       

                        Debug.Log($"[MyMod] Difficulty selected: {SelectedDifficulty}");

                        UpdateDifficultyRightPopup(difficulty);
                    },
                    parent: scroll,
                    stringOptions: options,
                    defaultOption: DifficultyManager.CurrentDifficulty.ToString(),
                    localPosition: Vector2.zero,
                    prefix: "",
                    postfix: "",
                    barBehavior: REPOSlider.BarBehavior.UpdateWithValue

                );

                // Apply button
                Vector2 sliderPosition = difficultySlider.rectTransform.anchoredPosition;
                Vector2 buttonOffset = new Vector2(0f, 120f);
                MenuAPI.CreateREPOButton(
                    "Apply",
                    () => popupPage.ClosePage(closePagesAddedOnTop: true),
                    popupPage.transform,
                    localPosition: sliderPosition + buttonOffset
                );

                return difficultySlider.rectTransform;
            });

            // Créer le popup de droite ici et le garder ouvert
            UpdateDifficultyRightPopup(SelectedDifficulty);

            popupPage.OpenPage(openOnTop: true);
        }

        private static void ApplyPreset(string Difficulty)
        {
            switch (Difficulty)
            {
                case "Normal":
                    DifficultyManager.ExtractionMultiplier = 0;
                    DifficultyManager.ExtractionMaxMultiplier = 3;
                    DifficultyManager.PourcentageRoom1 = 100;
                    DifficultyManager.PourcentageRoom2 = 0;
                    DifficultyManager.PourcentageRoom3 = 0;
                    DifficultyManager.EnemyMultiplier = 2;
                    DifficultyManager.ShopMultiplier = 1;
                    DifficultyManager.ValuableMultiplier = 1;
                    break;
                case "Normal+":
                    DifficultyManager.ExtractionMultiplier = 0;
                    DifficultyManager.ExtractionMaxMultiplier = 3;
                    DifficultyManager.PourcentageRoom1 = 70;
                    DifficultyManager.PourcentageRoom2 = 30;
                    DifficultyManager.PourcentageRoom3 = 0;
                    DifficultyManager.EnemyMultiplier = 2;
                    DifficultyManager.ShopMultiplier = 1;
                    DifficultyManager.ValuableMultiplier = 1;
                    break;
                case "Normal++":
                    DifficultyManager.ExtractionMultiplier = 0;
                    DifficultyManager.ExtractionMaxMultiplier = 3;
                    DifficultyManager.PourcentageRoom1 = 40;
                    DifficultyManager.PourcentageRoom2 = 40;
                    DifficultyManager.PourcentageRoom3 = 20;
                    DifficultyManager.EnemyMultiplier = 2;
                    DifficultyManager.ShopMultiplier = 1;
                    DifficultyManager.ValuableMultiplier = 1;
                    break;
                case "Hard":
                    DifficultyManager.ExtractionMultiplier = 1;
                    DifficultyManager.ExtractionMaxMultiplier = 5;
                    DifficultyManager.PourcentageRoom1 = 100;
                    DifficultyManager.PourcentageRoom2 = 0;
                    DifficultyManager.PourcentageRoom3 = 0;
                    DifficultyManager.EnemyMultiplier = 3;
                    DifficultyManager.ShopMultiplier = 1.5f;
                    DifficultyManager.ValuableMultiplier = 2;
                    break;
                case "Hard+":
                    DifficultyManager.ExtractionMultiplier = 1;
                    DifficultyManager.ExtractionMaxMultiplier = 5;
                    DifficultyManager.PourcentageRoom1 = 70;
                    DifficultyManager.PourcentageRoom2 = 30;
                    DifficultyManager.PourcentageRoom3 = 0;
                    DifficultyManager.EnemyMultiplier = 3;
                    DifficultyManager.ShopMultiplier = 1.5f;
                    DifficultyManager.ValuableMultiplier = 2;
                    break;
                case "Hard++":
                    DifficultyManager.ExtractionMultiplier = 1;
                    DifficultyManager.ExtractionMaxMultiplier = 5;
                    DifficultyManager.PourcentageRoom1 = 40;
                    DifficultyManager.PourcentageRoom2 = 40;
                    DifficultyManager.PourcentageRoom3 = 20;
                    DifficultyManager.EnemyMultiplier = 3;
                    DifficultyManager.ShopMultiplier = 1.5f;
                    DifficultyManager.ValuableMultiplier = 2;
                    break;
                case "Hardcore":
                    DifficultyManager.ExtractionMultiplier = 2;
                    DifficultyManager.ExtractionMaxMultiplier = 7;
                    DifficultyManager.PourcentageRoom1 = 40;
                    DifficultyManager.PourcentageRoom2 = 60;
                    DifficultyManager.PourcentageRoom3 = 0;
                    DifficultyManager.EnemyMultiplier = 5;
                    DifficultyManager.ShopMultiplier = 2;
                    DifficultyManager.ValuableMultiplier = 3;
                    break;
                case "Hardcore+":
                    DifficultyManager.ExtractionMultiplier = 2;
                    DifficultyManager.ExtractionMaxMultiplier = 7;
                    DifficultyManager.PourcentageRoom1 = 20;
                    DifficultyManager.PourcentageRoom2 = 50;
                    DifficultyManager.PourcentageRoom3 = 30;
                    DifficultyManager.EnemyMultiplier = 5;
                    DifficultyManager.ShopMultiplier = 2;
                    DifficultyManager.ValuableMultiplier = 3;
                    break;
                case "Nightmare":
                    DifficultyManager.ExtractionMultiplier = 3;
                    DifficultyManager.ExtractionMaxMultiplier = 9;
                    DifficultyManager.PourcentageRoom1 = 30;
                    DifficultyManager.PourcentageRoom2 = 40;
                    DifficultyManager.PourcentageRoom3 = 30;
                    DifficultyManager.EnemyMultiplier = 7;
                    DifficultyManager.ShopMultiplier = 2.5f;
                    DifficultyManager.ValuableMultiplier = 4;
                    break;
                case "IsThatEvenPossible":
                    DifficultyManager.ExtractionMultiplier = 4;
                    DifficultyManager.ExtractionMaxMultiplier = 11;
                    DifficultyManager.PourcentageRoom1 = 10;
                    DifficultyManager.PourcentageRoom2 = 30;
                    DifficultyManager.PourcentageRoom3 = 60;
                    DifficultyManager.EnemyMultiplier = 10;
                    DifficultyManager.ShopMultiplier = 3f;
                    DifficultyManager.ValuableMultiplier = 5;
                    break;
            }

        }

        private static void UpdateSliders()
        {
            // Trouver tous les sliders dans le popup
            var sliders = popupRight.GetComponentsInChildren<REPOSlider>();
           
            foreach (var slider in sliders)
            {
                Debug.Log(slider.name);
                switch (slider.name)
                {
                    case "Float Slider - Extraction Amount":
                        slider.SetValue(DifficultyManager.ExtractionMultiplier, true);
                        break;
                    case "Float Slider - MaxExtraction Amount":
                        slider.SetValue(DifficultyManager.ExtractionMaxMultiplier, true);
                        break;
                    case "Float Slider - Room Difficulty 1 (%)":
                        slider.SetValue(DifficultyManager.PourcentageRoom1, true);
                        break;
                    case "Float Slider - Room Difficulty 2 (%)":
                        slider.SetValue(DifficultyManager.PourcentageRoom2, true);
                        break;
                    case "Float Slider - Room Difficulty 3 (%)":
                        slider.SetValue(DifficultyManager.PourcentageRoom3, true);
                        break;
                    case "Float Slider - Number of Enemy":
                        slider.SetValue(DifficultyManager.EnemyMultiplier, true);
                        break;
                    case "Float Slider - Shop Price Multiplier":
                        slider.SetValue(DifficultyManager.ShopMultiplier, true);
                        break;
                    case "Float Slider - Valuable Multiplier":
                        slider.SetValue(DifficultyManager.ValuableMultiplier, true);
                        break;
                }
            }
        }

        private static void UpdateDifficultyRightPopup(string difficulty)
        {
            if (popupRight != null || popupPageDifficulty != null)
            {
                popupRight?.ClosePage(closePagesAddedOnTop: false);
                popupPageDifficulty?.ClosePage(closePagesAddedOnTop: false);
            }

            popupRight = MenuAPI.CreateREPOPopupPage("Difficulty Details", REPOPopupPage.PresetSide.Right, shouldCachePage: true, pageDimmerVisibility: true, spacing: 2.5f);
            popupPageDifficulty = MenuAPI.CreateREPOPopupPage("Difficulty Details", REPOPopupPage.PresetSide.Right, shouldCachePage: true, pageDimmerVisibility: true);

            popupRight.maskPadding = new Padding(left: 10, top: 10, right: 10, bottom: 0);

            if (popupRight == null || popupPageDifficulty == null)
            {
                Debug.LogWarning("[MyMod] Right popup not initialized yet.");
                return;
            }

            Debug.Log($"[MyMod] Opening: {SelectedDifficulty}");
            if (difficulty == "Custom")
            {
                popupRight.OpenPage(openOnTop: true);
                popupRight.AddElementToScrollView(scroll =>
                {
                    string[] options = new string[] {"None" ,"Normal", "Normal+", "Normal++", "Hard", "Hard+", "Hard++", "Hardcore", "Hardcore+", "Nightmare", "IsThatEvenPossible" };

                    // CreateREPOSlider(string text, string description, Action<string> onOptionChanged, Transform parent, string[] stringOptions, string defaultOption, Vector2 localPosition = default(Vector2), string prefix = "", string postfix = "", REPOSlider.BarBehavior barBehavior = REPOSlider.BarBehavior.UpdateWithValue)
                    var difficultySlider = MenuAPI.CreateREPOSlider(
                        text: "Difficulty Preset",
                        description: "",
                        onOptionChanged: (string difficulty) =>
                        {
                            ApplyPreset(difficulty);
                            UpdateSliders();
                            DifficultyManager.DifficultyPreset = difficulty;
                            DifficultySaveManager.SaveDifficulty("Custom");
                        },
                        parent: scroll,
                        stringOptions: options,
                        defaultOption: DifficultyManager.DifficultyPreset,
                        localPosition: new Vector2(0f, 210f),
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue

                    );

                    MenuAPI.CreateREPOSlider(
                        "Extraction Amount",
                        "",
                        val =>
                        {
                            DifficultyManager.ExtractionMultiplier = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 0,
                        max: 12,
                        localPosition: new Vector2(0f, 190f),
                        precision: 0,
                        defaultValue: DifficultyManager.ExtractionMultiplier,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        "MaxExtraction Amount",
                        "",
                        val =>
                        {
                            DifficultyManager.ExtractionMaxMultiplier = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 0,
                        max: 12,
                        localPosition: new Vector2(0f, 170f),
                        precision: 0,
                        defaultValue: DifficultyManager.ExtractionMaxMultiplier,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        $"Room Difficulty 1 (%)",
                        "",
                        val =>
                        {
                            DifficultyManager.PourcentageRoom1 = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 0,
                        max: 100,
                        precision: 0,
                        localPosition: new Vector2(0f, 150f),
                        defaultValue: DifficultyManager.PourcentageRoom1,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        $"Room Difficulty 2 (%)",
                        "",
                        val =>
                        {
                            DifficultyManager.PourcentageRoom2 = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 0,
                        max: 100,
                        precision: 0,
                        localPosition: new Vector2(0f, 130f),
                        defaultValue: DifficultyManager.PourcentageRoom2,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        $"Room Difficulty 3 (%)",
                        "",
                        val =>
                        {
                            DifficultyManager.PourcentageRoom3 = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 0,
                        max: 100,
                        precision: 0,
                        localPosition: new Vector2(0f, 110f),
                        defaultValue: DifficultyManager.PourcentageRoom3,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        $"Number of Enemy",
                        "",
                        val =>
                        {
                            DifficultyManager.EnemyMultiplier = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 0,
                        max: 20,
                        precision: 0,
                        localPosition: new Vector2(0f, 90f),
                        defaultValue: DifficultyManager.EnemyMultiplier,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        $"Shop Price Multiplier",
                        "",
                        val =>
                        {
                            DifficultyManager.ShopMultiplier = val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 1.0f,
                        max: 10.0f,
                        precision: 1,
                        localPosition: new Vector2(0f, 70f),
                        defaultValue: DifficultyManager.ShopMultiplier,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    MenuAPI.CreateREPOSlider(
                        $"Valuable Multiplier",
                        "",
                        val =>
                        {
                            DifficultyManager.ValuableMultiplier = (int)val;
                            DifficultySaveManager.SaveDifficulty("Custom"); // Save after change
                        },
                        scroll,
                        min: 1,
                        max: 10,
                        precision: 0,
                        localPosition: new Vector2(0f, 50f),
                        defaultValue: DifficultyManager.ValuableMultiplier,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );

                    return scroll as RectTransform;
                });
            }
            else
            {
                popupPageDifficulty.OpenPage(openOnTop: true);
                if (difficulty == "Normal")
                {

                    MenuAPI.CreateREPOLabel($"Enemy Number (2)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 30));
                    MenuAPI.CreateREPOLabel($"Start Number Room (8)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 60));
                    MenuAPI.CreateREPOLabel($"Start Extraction (1)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 90));
                    MenuAPI.CreateREPOLabel($"Max Extraction (4)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 120));
                    MenuAPI.CreateREPOLabel($"Valuable boost (x1)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 150));
                    MenuAPI.CreateREPOLabel($"Shop price (x1)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 180));
                    MenuAPI.CreateREPOLabel($"Average Difficulty room (1)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 210));

                }
                else if (difficulty == "Hard")
                {
                    MenuAPI.CreateREPOLabel($"Enemy Number (3)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 30));
                    MenuAPI.CreateREPOLabel($"Start Number Room (8)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 60));
                    MenuAPI.CreateREPOLabel($"Start Extraction (2)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 90));
                    MenuAPI.CreateREPOLabel($"Max Extraction (6)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 120));
                    MenuAPI.CreateREPOLabel($"Valuable boost (x2)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 150));
                    MenuAPI.CreateREPOLabel($"Shop price (x1.5)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 180));
                    MenuAPI.CreateREPOLabel($"Average Difficulty room (1)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 210));
                }
                else if (difficulty == "Hardcore")
                {
                    MenuAPI.CreateREPOLabel($"Enemy Number (5)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 30));
                    MenuAPI.CreateREPOLabel($"Start Number Room (10)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 60));
                    MenuAPI.CreateREPOLabel($"Start Extraction (3)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 90));
                    MenuAPI.CreateREPOLabel($"Max Extraction (8)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 120));
                    MenuAPI.CreateREPOLabel($"Valuable boost (x3)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 150));
                    MenuAPI.CreateREPOLabel($"Shop price (x2)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 180));
                    MenuAPI.CreateREPOLabel($"Average Difficulty room (1 & 2)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 210));
                }
                else if (difficulty == "Nightmare")
                {
                    MenuAPI.CreateREPOLabel($"Enemy Number (7)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 30));
                    MenuAPI.CreateREPOLabel($"Start Number Room (12)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 60));
                    MenuAPI.CreateREPOLabel($"Start Extraction (4)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 90));
                    MenuAPI.CreateREPOLabel($"Max Extraction (10)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 120));
                    MenuAPI.CreateREPOLabel($"Valuable boost (x4)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 150));
                    MenuAPI.CreateREPOLabel($"Shop price Cx2.5)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 180));
                    MenuAPI.CreateREPOLabel($"Average Difficulty room (1 & 2 & 3)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 210));
                }
                else if (difficulty == "IsThatEvenPossible")
                {
                    MenuAPI.CreateREPOLabel($"Enemy Number (10)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 30));
                    MenuAPI.CreateREPOLabel($"Start Number Room (15)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 60));
                    MenuAPI.CreateREPOLabel($"Start Extraction (5)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 90));
                    MenuAPI.CreateREPOLabel($"Max Extraction (12)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 120));
                    MenuAPI.CreateREPOLabel($"Valuable boost (x5)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 150));
                    MenuAPI.CreateREPOLabel($"Shop price (x3)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 180));
                    MenuAPI.CreateREPOLabel($"Average Difficulty room (2 & 3)", popupPageDifficulty.transform, localPosition: new Vector2(350f, 250f - 210));
                }
            }
        }
    }

}
