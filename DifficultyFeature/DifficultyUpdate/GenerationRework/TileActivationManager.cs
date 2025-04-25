using UnityEngine;
using System.Collections.Generic;
using System.Text;
using static LevelGenerator;
using Photon.Pun;
using System.Collections;

namespace DifficultyFeature.DifficultyUpdate.GenerationRework
{
    public class TileActivationManager : MonoBehaviour
    {
        private LevelGenerator levelGenerator;
        private Transform playerTransform;
        private Tile[,] levelGrid;
        private Vector2Int currentTilePos = new Vector2Int(-1, -1);
        private HashSet<Vector2Int> activeTiles = new HashSet<Vector2Int>();
        private Dictionary<Vector2Int, GameObject> tileObjects = new Dictionary<Vector2Int, GameObject>();
        private Dictionary<Vector2Int, LineRenderer> tileBorderRenderers = new Dictionary<Vector2Int, LineRenderer>();

        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private bool debugEnabled = true;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool showTileBorders = false; // Afficher les bordures dans le jeu
        [SerializeField] private float borderHeight = 1f; // Hauteur des bordures
        [SerializeField] private float borderWidth = 0.1f; // Épaisseur des bordures
        [SerializeField] private Color currentTileBorderColor = Color.red; // Couleur tuile actuelle
        [SerializeField] private Color activeTileBorderColor = Color.green; // Couleur tuiles actives

        private float timer = 0f;

        void Awake()
        {
            Debug.Log("TileActivationManager.Awake appelé");
        }

        void Start()
        {
            Debug.Log("TileActivationManager.Start appelé");

            levelGenerator = LevelGenerator.Instance;
            if (levelGenerator == null)
            {
                Debug.LogError("LevelGenerator.Instance est null dans Start !");
                enabled = false;
                return;
            }

            Debug.Log("LevelGenerator trouvé, démarrage WaitForLevelGeneration");
            StartCoroutine(WaitForLevelGeneration());
        }

        private System.Collections.IEnumerator WaitForLevelGeneration()
        {
            Debug.Log("WaitForLevelGeneration démarré");
            while (levelGenerator.State != LevelGenerator.LevelState.Done)
            {
                Debug.Log($"LevelGen pas terminé, état actuel : {levelGenerator.State}");
                yield return null;
            }

            Debug.Log("LevelGen terminé, initialisation...");
            levelGrid = levelGenerator.LevelGrid;
            if (levelGrid == null)
            {
                Debug.LogError("LevelGrid est null !");
                enabled = false;
                yield break;
            }

            InitializeTileObjects();

            // Désactiver toutes les tuiles sauf (0,0)
            Vector2Int spawnTile = new Vector2Int(0, 0);
            foreach (var tile in tileObjects)
            {
                if (tile.Key != spawnTile)
                {
                    SetTileActive(tile.Key, false);
                }
            }
            if (tileObjects.ContainsKey(spawnTile))
            {
                SetTileActive(spawnTile, true);
                activeTiles.Add(spawnTile);
                Debug.Log($"Tuile de spawn (0,0) activée");
            }
            else
            {
                Debug.LogWarning("Tuile (0,0) non trouvée dans tileObjects");
            }

            FindPlayer();

            if (playerTransform != null)
            {
                Vector2Int initialTilePos = WorldToTilePosition(playerTransform.position);
                Debug.Log($"Mise à jour initiale des tuiles pour la position {initialTilePos}");
                UpdateActiveTiles(initialTilePos);
                currentTilePos = initialTilePos;
            }
        }

        private void FindPlayer()
        {
            Debug.Log("FindPlayer appelé");
            foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
            {
                if (player.GetComponent<PhotonView>().IsMine)
                {
                    playerTransform = player.transform;
                    Debug.Log($"Joueur local trouvé : {player.gameObject.name}");
                    return;
                }
            }
            Debug.LogWarning("Aucun joueur local trouvé !");
            enabled = false;
        }

        private void InitializeTileObjects()
        {
            Debug.LogError("InitializeTileObjects appelé");
            Module[] modules = FindObjectsOfType<Module>();
            Debug.LogError($"Nombre de modules trouvés : {modules.Length}");
            foreach (Module module in modules)
            {
                int x = module.GridX;
                int y = module.GridY;
                tileObjects[new Vector2Int(x, y)] = module.gameObject;

                Debug.LogError($"Module ajouté à la tuile ({x}, {y}) : {module.gameObject.name}");
            }
        }

        void Update()
        {
            if (playerTransform == null || levelGrid == null)
            {
                Debug.LogWarning("Update ignoré : playerTransform ou levelGrid est null");
                return;
            }

            // Basculer le débogage avec F6
            if (Input.GetKeyDown(KeyCode.F6))
            {
                debugEnabled = !debugEnabled;
                Debug.Log($"Débogage {(debugEnabled ? "activé" : "désactivé")}");
            }

            // Basculer l'affichage des bordures avec F7
            if (Input.GetKeyDown(KeyCode.F7))
            {
                showTileBorders = !showTileBorders;
                Debug.Log($"Affichage des bordures {(showTileBorders ? "activé" : "désactivé")}");
                UpdateTileBorders();
            }

            timer += Time.deltaTime;
            if (timer < updateInterval)
                return;
            timer = 0f;

            Vector3 playerPos = playerTransform.position;
            Vector2Int newTilePos = WorldToTilePosition(playerPos);

            if (debugEnabled)
            {
                StringBuilder debugLog = new StringBuilder();
                debugLog.AppendLine($"[Debug] Position joueur : {playerPos}");
                debugLog.AppendLine($"[Debug] Tuile actuelle : {newTilePos}");
                debugLog.AppendLine($"[Debug] Tuiles actives : {activeTiles.Count}");
                foreach (Vector2Int tile in activeTiles)
                {
                    debugLog.AppendLine($"  - Tuile active : {tile}, Module : {(tileObjects.ContainsKey(tile) ? tileObjects[tile].name : "Inconnu")}");
                }
                Debug.Log(debugLog.ToString());
            }

            if (newTilePos != currentTilePos)
            {
                Debug.Log($"Changement de tuile : de {currentTilePos} à {newTilePos}");
                UpdateActiveTiles(newTilePos);
                currentTilePos = newTilePos;
                UpdateTileBorders();
            }
        }

        private Vector2Int WorldToTilePosition(Vector3 worldPos)
        {
            float moduleWidth = LevelGenerator.ModuleWidth * LevelGenerator.TileSize;
            float x = (worldPos.x - 7.5f) / moduleWidth;
            float y = worldPos.z / moduleWidth;
            int tileX = Mathf.FloorToInt(x);
            int tileY = Mathf.FloorToInt(y);

            tileX = tileX + 11;
            tileY = Mathf.Clamp(tileY, 0, levelGenerator.LevelHeight - 1);

            if (debugEnabled)
            {
                Debug.Log($"WorldToTilePosition : worldPos {worldPos}, tile ({tileX}, {tileY})");
            }
            return new Vector2Int(tileX, tileY);
        }

        private void UpdateActiveTiles(Vector2Int centerTile)
        {
            if (debugEnabled)
            {
                Debug.Log($"UpdateActiveTiles pour la tuile centrale {centerTile}");
            }
            HashSet<Vector2Int> newActiveTiles = new HashSet<Vector2Int>();

            newActiveTiles.Add(centerTile);
            AddNeighborTiles(centerTile, newActiveTiles);

            foreach (Vector2Int tilePos in activeTiles)
            {
                bool ishere = false;
                foreach (Vector2Int tilePos2 in newActiveTiles)
                {
                    if (tilePos2 == tilePos)
                    {
                        ishere = true; break;
                    }
                }
                if (!ishere)
                {
                    if (debugEnabled)
                    {
                        Debug.Log($"Désactivation de la tuile {tilePos}");
                    }
                    SetTileActive(tilePos, false);
                }
            }

            foreach (Vector2Int tilePos in newActiveTiles)
            {
                if (debugEnabled)
                {
                    Debug.Log($"Activation de la tuile {tilePos}");
                }
                SetTileActive(tilePos, true);
            }

            activeTiles = newActiveTiles;
            if (debugEnabled)
            {
                Debug.Log($"Tuiles actives : {activeTiles.Count}");
            }
        }

        private void AddNeighborTiles(Vector2Int center, HashSet<Vector2Int> activeSet)
        {
            Vector2Int[] neighbors = {
            new Vector2Int(center.x, center.y + 1), // Haut
            new Vector2Int(center.x, center.y - 1), // Bas
            new Vector2Int(center.x + 1, center.y), // Droite
            new Vector2Int(center.x - 1, center.y)  // Gauche
        };
            foreach (var neighbor in neighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;
                if (nx >= 0 && nx < levelGenerator.LevelWidth && ny >= 0 && ny < levelGenerator.LevelHeight)
                {
                    activeSet.Add(new Vector2Int(nx, ny));
                    if (debugEnabled)
                    {
                        Debug.Log($"Voisin ajouté : ({nx}, {ny})");
                    }
                }
            }
        }

        private void SetTileActive(Vector2Int tilePos, bool active)
        {
            if (!tileObjects.ContainsKey(tilePos))
            {
                Debug.LogWarning($"Tuile {tilePos} non trouvée dans tileObjects");
                return;
            }

            GameObject tileObject = tileObjects[tilePos];

            if (debugEnabled)
            {
                Debug.Log($"SetTileActive : tuile {tilePos}, active = {active}");
            }
            if (active)
            {
                StartCoroutine(ActivateRenderersOverTime(tileObject, 2f));
                MeshCollider[] colliders = tileObjects[tilePos].GetComponentsInChildren<MeshCollider>(true);
                foreach (MeshCollider collider in colliders)
                {
                    collider.enabled = true;
                    //if (debugEnabled)
                    //{
                    //    Debug.Log($"MeshCollider activé : {collider.gameObject.name} pour la tuile {tilePos}");
                    //}
                }
            }
            else
            {
                Renderer[] renderers = tileObject.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = false;
                    //if (debugEnabled)
                    //{
                    //    Debug.Log($"Renderer désactivé : {renderer.gameObject.name} pour la tuile {tilePos}");
                    //}
                }

                MeshCollider[] colliders = tileObject.GetComponentsInChildren<MeshCollider>(true);
                foreach (MeshCollider collider in colliders)
                {
                    collider.enabled = true;
                    //if (debugEnabled)
                    //{
                    //    Debug.Log($"MeshCollider activé : {collider.gameObject.name} pour la tuile {tilePos}");
                    //}
                }
            }
        }

        private IEnumerator ActivateRenderersOverTime(GameObject tileObject, float duration)
        {
            Renderer[] renderers = tileObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                if (debugEnabled)
                {
                    Debug.Log($"Aucun Renderer trouvé pour la tuile {tileObject.name}");
                }
                yield break;
            }

            float interval = duration / renderers.Length; // Temps par Renderer
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
                if (debugEnabled)
                {
                    Debug.Log($"Renderer activé : {renderer.gameObject.name} pour la tuile {tileObject.name}");
                }
                yield return new WaitForSeconds(interval);
            }

            if (debugEnabled)
            {
                Debug.Log($"Activation des Renderer terminée pour la tuile {tileObject.name}");
            }
        }

        private void UpdateTileBorders()
        {
            if (!showTileBorders)
            {
                // Désactiver tous les LineRenderer
                foreach (var renderer in tileBorderRenderers.Values)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
                return;
            }

            // Supprimer les bordures des tuiles non actives
            List<Vector2Int> keysToRemove = new List<Vector2Int>();
            foreach (var kvp in tileBorderRenderers)
            {
                if (!activeTiles.Contains(kvp.Key) && kvp.Key != currentTilePos)
                {
                    if (kvp.Value != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                tileBorderRenderers.Remove(key);
            }

            float moduleWidth = LevelGenerator.ModuleWidth * LevelGenerator.TileSize;

            // Bordure pour la tuile actuelle
            if (currentTilePos.x >= 0 && currentTilePos.y >= 0)
            {
                LineRenderer currentTileRenderer = GetOrCreateBorderRenderer(currentTilePos, currentTileBorderColor);
                UpdateBorderRenderer(currentTileRenderer, currentTilePos, moduleWidth);
                currentTileRenderer.enabled = true;
            }

            // Bordures pour les tuiles actives
            foreach (Vector2Int tilePos in activeTiles)
            {
                if (tilePos != currentTilePos) // Éviter de dupliquer la tuile actuelle
                {
                    LineRenderer activeTileRenderer = GetOrCreateBorderRenderer(tilePos, activeTileBorderColor);
                    UpdateBorderRenderer(activeTileRenderer, tilePos, moduleWidth);
                    activeTileRenderer.enabled = true;
                }
            }
        }

        private LineRenderer GetOrCreateBorderRenderer(Vector2Int tilePos, Color color)
        {
            if (tileBorderRenderers.TryGetValue(tilePos, out LineRenderer renderer) && renderer != null)
            {
                return renderer;
            }

            GameObject borderObject = new GameObject($"TileBorder_{tilePos.x}_{tilePos.y}");
            borderObject.transform.SetParent(transform);
            renderer = borderObject.AddComponent<LineRenderer>();
            renderer.positionCount = 5; // 4 coins + 1 pour fermer
            renderer.startWidth = borderWidth;
            renderer.endWidth = borderWidth;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.useWorldSpace = true;

            tileBorderRenderers[tilePos] = renderer;
            return renderer;
        }

        private void UpdateBorderRenderer(LineRenderer renderer, Vector2Int tilePos, float moduleWidth)
        {
            Vector3 center = new Vector3(
                (tilePos.x - levelGenerator.LevelWidth / 2f) * moduleWidth,
                borderHeight,
                (tilePos.y * moduleWidth) + moduleWidth / 2f
            );
            float halfWidth = moduleWidth / 2f;

            Vector3[] positions = new Vector3[5]
            {
            center + new Vector3(-halfWidth, 0, -halfWidth), // Coin bas-gauche
            center + new Vector3(-halfWidth, 0, halfWidth),  // Coin haut-gauche
            center + new Vector3(halfWidth, 0, halfWidth),   // Coin haut-droit
            center + new Vector3(halfWidth, 0, -halfWidth),  // Coin bas-droit
            center + new Vector3(-halfWidth, 0, -halfWidth)  // Retour au départ
            };

            renderer.SetPositions(positions);
        }

        void OnDrawGizmos()
        {
            if (!debugEnabled || !showGizmos || levelGenerator == null || levelGrid == null)
                return;

            float moduleWidth = LevelGenerator.ModuleWidth * LevelGenerator.TileSize;

            if (currentTilePos.x >= 0 && currentTilePos.y >= 0)
            {
                Vector3 tileCenter = new Vector3(
                    (currentTilePos.x - levelGenerator.LevelWidth / 2f) * moduleWidth,
                    1f,
                    (currentTilePos.y * moduleWidth) + moduleWidth / 2f
                );
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(tileCenter, new Vector3(moduleWidth, 1f, moduleWidth));
            }

            Gizmos.color = Color.green;
            foreach (Vector2Int tilePos in activeTiles)
            {
                Vector3 tileCenter = new Vector3(
                    (tilePos.x - levelGenerator.LevelWidth / 2f) * moduleWidth,
                    1f,
                    (tilePos.y * moduleWidth) + moduleWidth / 2f
                );
                Gizmos.DrawWireCube(tileCenter, new Vector3(moduleWidth, 1f, moduleWidth));
            }
        }

        void OnDestroy()
        {
            // Nettoyer les LineRenderer
            foreach (var renderer in tileBorderRenderers.Values)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }
            tileBorderRenderers.Clear();
        }
    } // Bug chaise bizzard et doit trouver comment l'activer a chaque game.
}