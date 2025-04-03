using MenuLib;
using MenuLib.MonoBehaviors;
using MenuLib.Structs;
using SingularityGroup.HotReload;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static b;

namespace MyMOD
{
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

            Vector2 buttonPosition = new Vector2(0f, 0f);

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
                        Debug.Log($"[MyMod] Difficulty selected: {SelectedDifficulty}");

                        UpdateDifficultyRightPopup(difficulty);
                    },
                    parent: scroll,
                    stringOptions: options,
                    defaultOption: SelectedDifficulty,
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

        private static void UpdateDifficultyRightPopup(string difficulty)
        {
            if(popupRight != null || popupPageDifficulty != null)
            {
                popupRight.ClosePage(closePagesAddedOnTop: false);
                popupPageDifficulty.ClosePage(closePagesAddedOnTop: false);
            }

            // Supprimer les anciens éléments
            popupRight = MenuAPI.CreateREPOPopupPage("Difficulty Details", REPOPopupPage.PresetSide.Right, shouldCachePage: true, pageDimmerVisibility: true, spacing: 2.5f);
            popupPageDifficulty = MenuAPI.CreateREPOPopupPage("Difficulty Details", REPOPopupPage.PresetSide.Right, shouldCachePage: true, pageDimmerVisibility: true);

            popupRight.maskPadding = new Padding(left: 10, top: 10, right: 10, bottom: 0);

            if (popupRight == null || popupPageDifficulty == null)
            {
                Debug.LogWarning("[MyMod] Right popup not initialized yet.");
                return;
            }
            Debug.Log($"[MyMod] Oppening: {SelectedDifficulty}");
            if (difficulty == "Custom")
            {
                popupRight.OpenPage(openOnTop: true);
                popupRight.AddElementToScrollView(scroll =>
                {

                    MenuAPI.CreateREPOSlider(
                           "Extraction Amount",
                           "",
                           val => DifficultyManager.ExtractionMultiplier = (int)val,
                           scroll,
                           min: 0,
                           max: 12,
                           localPosition: new Vector2(0f, 210f),
                           precision: 0,
                           defaultValue: DifficultyManager.ExtractionMultiplier,
                           prefix: "",
                           postfix: "",
                           barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                       );

                    MenuAPI.CreateREPOSlider(
                        "MaxExtraction Amount",
                        "",
                        val => DifficultyManager.ExtractionMaxMultiplier = (int)val,
                        scroll,
                        min: 0,
                        max: 12,
                        localPosition: new Vector2(0f, 190f),
                        precision: 0,
                        defaultValue: DifficultyManager.ExtractionMaxMultiplier,
                        prefix: "",
                        postfix: "",
                        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    );


                    //MenuAPI.CreateREPOSlider(
                    //        $"Room Amount",
                    //        description: "Allows you to update the default number of rooms in the dungeon. Note: If you don't put enough rooms for too many extraction rooms, you may find yourself with pregen problems (inaccessible room, door leading to a void, etc.)",
                    //        onValueChanged: val => DifficultyManager.RoomNumber = (int)val,
                    //        parent: scroll,
                    //        min: 8,
                    //        max: 30,
                    //        precision: 0,
                    //        localPosition: new Vector2(0f, 190f),
                    //        defaultValue: DifficultyManager.RoomNumber,
                    //        prefix: "",
                    //        postfix: "",
                    //        barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                    //    );

                    MenuAPI.CreateREPOSlider(
                           $"Room Difficulty 1 (%)",
                           description: "",
                           onValueChanged: val => DifficultyManager.PourcentageRoom1 = (int)val,
                           parent: scroll,
                           min: 0,
                           max: 100,
                           precision: 0,
                           localPosition: new Vector2(0f, 170f),
                           defaultValue: DifficultyManager.PourcentageRoom1,
                           prefix: "",
                           postfix: "",
                           barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                         );

                    MenuAPI.CreateREPOSlider(
                           $"Room Difficulty 2 (%)",
                           description: "",
                           onValueChanged: val => DifficultyManager.PourcentageRoom2 = (int)val,
                           parent: scroll,
                           min: 1,
                           max: 100,
                           precision: 0,
                           localPosition: new Vector2(0f, 150f),
                           defaultValue: DifficultyManager.PourcentageRoom2,
                           prefix: "",
                           postfix: "",
                           barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                         );

                    MenuAPI.CreateREPOSlider(
                           $"Room Difficulty 3 (%)",
                           description: "",
                           onValueChanged: val => DifficultyManager.PourcentageRoom3 = (int)val,
                           parent: scroll,
                           min: 1,
                           max: 100,
                           precision: 0,
                           localPosition: new Vector2(0f, 130f),
                           defaultValue: DifficultyManager.PourcentageRoom3,
                           prefix: "",
                           postfix: "",
                           barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                        );

                    MenuAPI.CreateREPOSlider(
                                $"Enemy Multiplier",
                                description: "",
                                onValueChanged: val => DifficultyManager.EnemyMultiplier = (int)val,
                                parent: scroll,
                                min: 1,
                                max: 10,
                                precision: 0,
                                localPosition: new Vector2(0f, 110f),
                                defaultValue: DifficultyManager.EnemyMultiplier,
                                prefix: "",
                                postfix: "",
                                barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                        );

                    MenuAPI.CreateREPOSlider(
                            $"Shop Price Multiplier",
                            description: "",
                            onValueChanged: val => DifficultyManager.ShopMultiplier = (int)val,
                            parent: scroll,
                            min: 1,
                            max: 10,
                            precision: 0,
                            localPosition: new Vector2(0f, 90f),
                            defaultValue: DifficultyManager.ShopMultiplier,
                            prefix: "",
                            postfix: "",
                            barBehavior: REPOSlider.BarBehavior.UpdateWithValue
                        );

                    MenuAPI.CreateREPOSlider(
                            $"Valuable Multiplier",
                            description: "",
                            onValueChanged: val => DifficultyManager.ValuableMultiplier = (int)val,
                            parent: scroll,
                            min: 1,
                            max: 10,
                            precision: 0,
                            localPosition: new Vector2(0f, 70f),
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
