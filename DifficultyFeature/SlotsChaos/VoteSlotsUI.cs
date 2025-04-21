using DifficultyFeature.DifficultyUpdate;
using DifficultyFeature.SlotsChaos;
using ExitGames.Client.Photon;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using MenuLib.Structs;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DifficultyFeature.SlotsChaos
{
    public static class VoteSlotsUI
    {
        internal static REPOLabel label;
        internal static List<string> vote;
        internal static List<PlayerAvatar> PlayerVote;
        internal static int voteCountMax;
        internal static REPOPopupPage votePage;

        public static void OpenVoteUi()
        {
            Vector2 vector = new Vector2(-110f, 0f); // Position initiale du popup

            votePage = MenuAPI.CreateREPOPopupPage(
                "Next round: who's going?",
                shouldCachePage: false,
                pageDimmerVisibility: false,
                spacing: 10f,
                localPosition: vector
            );

            votePage.AddElementToScrollView(scroll =>
            {
                float buttonWidth = 100f;
                float buttonHeight = 20f;
                float spacing = 10f;
                float topPadding = 10f;
                float bottomPadding = 10f;

                // Désactiver la barre de défilement si non nécessaire
                Transform scrollBox = scroll.parent?.parent;
                if (scrollBox != null)
                {
                    Scrollbar scrollbar = scrollBox.GetComponentInChildren<Scrollbar>();
                    if (scrollbar != null)
                    {
                        scrollbar.gameObject.SetActive(true); // Activer pour tester
                        Debug.Log($"Scrollbar trouvée : {scrollbar.name}");
                    }

                    // Désactiver les RawImages inutiles
                    RawImage[] rawImages = scrollBox.GetComponentsInChildren<RawImage>(true);
                    foreach (RawImage rawImage in rawImages)
                    {
                        if (!rawImage.transform.IsChildOf(scroll))
                        {
                            rawImage.enabled = false;
                            Debug.Log($"RawImage désactivé : {GetTransformPath(rawImage.transform)}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Menu Scroll Box non trouvé (parent de Mask).");
                }

                int activePlayerCount = GameDirector.instance.PlayerList.Count(p => !p.isDisabled);
                float totalContentHeight = (buttonHeight + spacing) * activePlayerCount + topPadding + bottomPadding;

                // Ajuster la taille du RectTransform du scroll
                RectTransform scrollRect = scroll as RectTransform;
                scrollRect.sizeDelta = new Vector2(scrollRect.sizeDelta.x, totalContentHeight);

                // Positionner les boutons
                float currentY = -topPadding; // Commencer depuis le haut
                foreach (var player in GameDirector.instance.PlayerList)
                {
                    if (!player.isDisabled)
                    {
                        Vector2 buttonPosition = new Vector2(0f, currentY);
                        MenuAPI.CreateREPOButton(
                            player.playerName, () =>
                            VoteForChaos(player.playerName),
                            scroll,
                            localPosition: buttonPosition
                        );

                        currentY -= (buttonHeight + spacing); // Déplacer vers le bas
                    }
                }

                // Mettre à jour le scrollView
                votePage.scrollView.UpdateElements();

                return scrollRect;
            }, topPadding: 10f, bottomPadding: 10f);

            // Ajuster le masque pour correspondre à la taille souhaitée
            votePage.maskPadding = new Padding(10f, 10f, 10f, 10f);
            votePage.OpenPage(openOnTop: true);
        }

        public static void VoteForChaos(string playerName)
        {
            Debug.Log(playerName);
            votePage.ClosePage(true);
            PlayerAvatar playerAvatar = PlayerAvatar.instance;

            object[] eventData = new object[] { playerName };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            PhotonNetwork.RaiseEvent(8, eventData, raiseEventOptions, SendOptions.SendReliable);

        }

        internal static void ExecuteVote()
        {
            PhotonView photonView = PlayerAvatar.instance.GetComponent<PhotonView>();
            vote.Clear();
            PlayerVote.Clear();
            Debug.Log("Execute vote");
            object[] eventData = new object[] { GetWinners(vote) };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(9, eventData, raiseEventOptions, SendOptions.SendReliable);
        }

        public static List<string> GetWinners(List<string> vote)
        {
            List<string> winners = new List<string>();
            int maxVotes = 0;
            Dictionary<string, int> voteCounts = new Dictionary<string, int>();

            // Compter les votes
            foreach (string pseudo in vote)
            {
                if (!string.IsNullOrEmpty(pseudo))
                {
                    if (voteCounts.ContainsKey(pseudo))
                    {
                        voteCounts[pseudo]++;
                    }
                    else
                    {
                        voteCounts[pseudo] = 1;
                    }
                }
            }

            // Trouver le maximum de votes
            maxVotes = voteCounts.Any() ? voteCounts.Values.Max() : 0;

            // Ajouter tous les pseudos qui ont le maximum de votes
            foreach (var pair in voteCounts)
            {
                if (pair.Value == maxVotes)
                {
                    winners.Add(pair.Key);
                }
            }

            return winners;
        }

        private static string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }


    }

    [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
    public class PlayerAvatarDeathPatch
    {
        public static bool voteUI = true;
        public static bool voteStart = false;
        [HarmonyPostfix]
        public static void Postfix(PlayerAvatar __instance)
        {
            // Vérifier si voteUI existe et si les conditions de niveau sont remplies
             // Remplacez par la référence réelle à voteUI si nécessaire
            if (voteUI && PhotonNetwork.InRoom &&
                RunManager.instance.levelCurrent != RunManager.instance.levelShop &&
                RunManager.instance.levelCurrent != RunManager.instance.levelArena &&
                RunManager.instance.levelCurrent != RunManager.instance.levelLobbyMenu)
            {
                CoroutineRunner.instance.StartCoroutine(WaitForSecondes(3f));
            }
        }

        private static IEnumerator WaitForSecondes(float seconde)
        {
            yield return new WaitForSeconds(seconde);

            Debug.Log("InGame");
            int nbr = 0;
            foreach (var player in GameDirector.instance.PlayerList)
            {
                if (player.isDisabled)
                {
                    Debug.Log("PlayerDead");
                    nbr++;
                }
            }

            if (nbr >= GameDirector.instance.PlayerList.Count / 2 && nbr != GameDirector.instance.PlayerList.Count)
            {
                Debug.Log("Launche UI");
                VoteSlotsUI.voteCountMax = nbr;
                voteUI = false;
                if (PlayerAvatar.instance.isDisabled)
                    VoteSlotsUI.OpenVoteUi();

                if (PhotonNetwork.IsMasterClient)
                {

                    DifficultyFeature.Instance.startVote = DateTime.Now;
                    voteStart = true;
                }
            }
        }
    }

}




// Parcourir les joueurs vivants
//foreach (var player in GameDirector.instance.PlayerList)
//{
//    if (!player.isDisabled) // Joueur vivant
//    {
//        Debug.Log(player.playerName);

//        // Vérifier si on dépasse la largeur maximale
//        if (currentX + buttonWidth > maxWidth)
//        {
//            // Passer à la ligne suivante
//            currentX = 0f;
//            currentY -= (buttonHeight + spacing); // Descendre d'une ligne
//        }

//        // Créer le bouton à la position calculée
//        Vector2 buttonPosition = new Vector2(currentX, currentY);
//        MenuAPI.CreateREPOButton(player.playerName, () => Debug.Log($"Bouton cliqué : {player.playerName}"), popupPage.transform, localPosition: buttonPosition);

//        // Mettre à jour la position x pour le prochain bouton
//        currentX += buttonWidth + spacing;
//    }
//}