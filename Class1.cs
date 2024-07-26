using MelonLoader;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Il2Cpp;
using System.Threading.Tasks;
namespace SlapshotReplayMod
{
    public class ReplayMod : MelonMod
    {
        private GameObject gamePuck;
        private GameObject[] playbackPlayers;
        private bool fetchedPlayers = false;

        private bool _isGuiVisible;
        private bool _isPopupVisible;
        private bool _isPopupActive;
        private Rect _guiWindowRect = new Rect(20, 20, 300, 400);
        private List<string> _replayFiles = new List<string>();
        private Vector2 _scrollPosition;
        private string _selectedReplay = null;

        private bool isRecording = false;
        private bool isPlaying = false;
        private bool hasFinishedPlaying = false;
        private bool isPaused = false;
        private List<FrameData> recordedFrames = new List<FrameData>();
        private List<FrameData> circularBuffer = new List<FrameData>();
        private int currentFrameIndex = 0;
        private float recordingInterval = 1.0f / 120.0f;
        private float playbackInterval = 1.0f / 120.0f;
        private float lastRecordedTime = 0.0f;
        private float lastPlaybackTime = 0.0f;
        private float bufferDuration = 15.0f;

        private string popupMessage = "";
        private float popupDuration = 3.0f;
        private float popupStartTime;
        private int maxBufferFrames => Mathf.CeilToInt(bufferDuration / recordingInterval);

        private string replayDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlapshotReboundReplays");

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Replay Mod Loaded");

            if (!Directory.Exists(replayDirectory))
            {
                Directory.CreateDirectory(replayDirectory);
            }
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _isPopupVisible = true;
                popupMessage = "Started Recording";
                popupStartTime = Time.time;
                StartRecording();
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                _isPopupVisible = true;
                popupMessage = "Saved Recording";
                popupStartTime = Time.time;
                StopRecording();
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _isPopupVisible = true;
                popupMessage = "Clip Saved";
                popupStartTime = Time.time;
                SaveCircularBuffer();
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                _isGuiVisible = !_isGuiVisible;
                if (_isGuiVisible)
                {
                    LoadReplayFiles();
                }
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                TogglePause();
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SeekPlayback(5);
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SeekPlayback(-5);
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                StopPlaybackAndReset();
            }

            float currentTime = Time.time;

            if (isRecording && currentTime - lastRecordedTime >= recordingInterval)
            {
                RecordFrame();
                lastRecordedTime = currentTime;
            }

            if (currentTime - lastRecordedTime >= recordingInterval)
            {
                RecordToBuffer();
                lastRecordedTime = currentTime;
            }

            if (isPlaying && !isPaused && currentTime - lastPlaybackTime >= playbackInterval)
            {
                if (currentFrameIndex < recordedFrames.Count)
                {
                    PlaybackFrame(recordedFrames[currentFrameIndex]);
                    currentFrameIndex++;
                    lastPlaybackTime = currentTime;
                }
                else
                {
                    isPlaying = false;
                    hasFinishedPlaying = true;
                    MelonLogger.Msg("Playback finished.");
                }
            }
        }

        public override void OnGUI()
        {
            if (_isGuiVisible)
            {
                _guiWindowRect = GUI.Window(0, _guiWindowRect, (GUI.WindowFunction)GuiWindowFunction, "Replay Mod");
            }

            if (_isPopupVisible)
            {
                DisplayPopup(popupMessage);

                if (Time.time - popupStartTime >= popupDuration)
                {
                    _isPopupVisible = false;
                }
            }
        }

        private void GuiWindowFunction(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(20);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(_guiWindowRect.width), GUILayout.Height(_guiWindowRect.height - 50));

            if (_replayFiles.Count == 0)
            {
                GUILayout.Label("No replays found.");
            }
            else
            {
                foreach (var replayFile in _replayFiles)
                {
                    if (GUILayout.Button(Path.GetFileNameWithoutExtension(replayFile)))
                    {
                        MelonLogger.Msg(Path.GetFileNameWithoutExtension(replayFile));
                        gamePuck = GameObject.Find("puck(Clone)");
                        _selectedReplay = replayFile;
                        LoadSpecificReplay(_selectedReplay);
                        _isGuiVisible = false;
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close"))
            {
                _isGuiVisible = false;
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void LoadReplayFiles()
        {
            _replayFiles = Directory.GetFiles(replayDirectory, "*.dat").OrderByDescending(f => File.GetCreationTime(f)).ToList();
        }

        private void LoadSpecificReplay(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    IFormatter formatter = new BinaryFormatter();
                    recordedFrames = (List<FrameData>)formatter.Deserialize(stream);
                }
                MelonLogger.Msg($"Replay loaded successfully from {filePath}.");
                PlayRecording();
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Failed to load replay: " + ex.Message);
            }
        }
        private void StartRecording()
        {
            isRecording = true;
            isPlaying = false;
            hasFinishedPlaying = false;
            recordedFrames.Clear();
            lastRecordedTime = Time.time;
            MelonLogger.Msg("Recording started.");
        }

        private void StopRecording()
        {
            isRecording = false;
            SaveRecording();
            MelonLogger.Msg("Recording stopped and saved.");
        }

        private void SaveCircularBuffer()
        {
            recordedFrames = new List<FrameData>(circularBuffer);
            SaveRecording();
            MelonLogger.Msg("Circular buffer saved as recording.");
        }

        private void PlayRecording()
        {

            if (recordedFrames.Count > 0)
            {
                isPlaying = true;
                currentFrameIndex = 0;
                lastPlaybackTime = Time.time;
                MelonLogger.Msg("Playback started.");
                MelonCoroutines.Start(TogglePlayersCoroutine());
            }
            else
            {
                MelonLogger.Warning("No recorded frames to play.");
            }
        }

        private void TogglePause()
        {
            if (isPlaying)
            {
                isPaused = !isPaused;
                MelonLogger.Msg(isPaused ? "Playback paused." : "Playback resumed.");
            }
        }

        private void SeekPlayback(int seconds)
        {
            if (isPlaying)
            {
                int framesToSeek = (int)(seconds * (1.0f / playbackInterval));
                currentFrameIndex = Mathf.Clamp(currentFrameIndex + framesToSeek, 0, recordedFrames.Count - 1);
                lastPlaybackTime = Time.time;
                isPaused = false;
                isPlaying = true;
                MelonLogger.Msg($"Playback seeked to frame {currentFrameIndex}.");
            }
        }

        private void RecordFrame()
        {
            FrameData frameData = new FrameData
            {
                PlayersData = new List<PlayerData>()
            };

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                PlayerData playerData = RecordPlayerData(player);
                frameData.PlayersData.Add(playerData);
            }

            GameObject puck = GameObject.Find("puck(Clone)");
            if (puck != null)
            {
                frameData.PuckPosition = new SerializableVector3(puck.transform.position);
                frameData.PuckRotation = new SerializableQuaternion(puck.transform.rotation);
            }

            recordedFrames.Add(frameData);
        }

        private void RecordToBuffer()
        {
            FrameData frameData = new FrameData
            {
                PlayersData = new List<PlayerData>()
            };

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                PlayerData playerData = RecordPlayerData(player);
                frameData.PlayersData.Add(playerData);
            }

            GameObject puck = GameObject.Find("puck(Clone)");
            if (puck != null)
            {
                frameData.PuckPosition = new SerializableVector3(puck.transform.position);
                frameData.PuckRotation = new SerializableQuaternion(puck.transform.rotation);
            }

            if (circularBuffer.Count >= maxBufferFrames)
            {
                circularBuffer.RemoveAt(0);
            }
            circularBuffer.Add(frameData);
        }

        private PlayerData RecordPlayerData(GameObject player)
        {
            PlayerData playerData = new PlayerData
            {
                Position = new SerializableVector3(player.transform.position),
                Rotation = new SerializableQuaternion(player.transform.rotation),
                CosmeticsData = new List<CosmeticData>(),
                ChildObjectsData = new List<ChildObjectData>(),
                Teams = new List<Team>(),
                Usernames = new List<string>(),
                IsRightHanded = new List<bool>()
            };

            var teamComponent = player.GetComponent<Player>();
            if (teamComponent != null)
            {
                playerData.Teams.Add(teamComponent.Team);
                playerData.Usernames.Add(teamComponent.Username);
                playerData.IsRightHanded.Add(teamComponent.RightHandedness);
            }

            var customization = player.GetComponent<PlayerCustomization>();
            if (customization != null)
            {
                foreach (var cosmetic in customization.currentCosmeticFullKey)
                {
                    string[] itemVariant = cosmetic.Value.Split('/');
                    CosmeticData cosmeticData = new CosmeticData
                    {
                        Type = cosmetic.Key,
                        Item = itemVariant[0],
                        Variant = itemVariant.Length == 2 ? itemVariant[1] : "default"
                    };
                    playerData.CosmeticsData.Add(cosmeticData);
                }
            }

            for (int i = 0; i < player.transform.childCount; i++)
            {
                GameObject child = player.transform.GetChild(i).gameObject;
                ChildObjectData childData = new ChildObjectData
                {
                    Name = child.name,
                    Position = new SerializableVector3(child.transform.position),
                    Rotation = new SerializableQuaternion(child.transform.rotation)
                };
                playerData.ChildObjectsData.Add(childData);
            }

            return playerData;
        }

        private void PlaybackFrame(FrameData frameData)
        {
            if (fetchedPlayers == false)
            {
                playbackPlayers = GameObject.FindGameObjectsWithTag("Player");
            }

            if (playbackPlayers.Length == frameData.PlayersData.Count)
            {
                fetchedPlayers = true;
            }

            for (int i = 0; i < playbackPlayers.Length && i < frameData.PlayersData.Count; i++)
            {
                GameObject playerObject = playbackPlayers[i];
                PlayerData playerData = frameData.PlayersData[i];

                playerObject.transform.position = playerData.Position.ToVector3();
                playerObject.transform.rotation = playerData.Rotation.ToQuaternion();

                foreach (var childData in playerData.ChildObjectsData)
                {
                    Transform childTransform = playerObject.transform.Find(childData.Name);
                    if (childTransform != null)
                    {
                        childTransform.position = childData.Position.ToVector3();
                        childTransform.rotation = childData.Rotation.ToQuaternion();
                    }
                }
            }

            if (gamePuck != null)
            {
                gamePuck.transform.position = frameData.PuckPosition.ToVector3();
                gamePuck.transform.rotation = frameData.PuckRotation.ToQuaternion();
            }
        }

        private void ApplyInitialCosmetics()
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

            if (recordedFrames.Count > 0)
            {
                FrameData initialFrame = recordedFrames[0];

                for (int i = 0; i < players.Length && i < initialFrame.PlayersData.Count; i++)
                {
                    GameObject playerObject = players[i];
                    PlayerData playerData = initialFrame.PlayersData[i];
                    if (playerObject.name == "player(Clone)")
                    {
                        // Check for PlayerCustomization component
                        var customization = playerObject.GetComponent<PlayerCustomization>();
                        if (customization != null)
                        {
                            List<string> cosmeticTypes = new List<string>() { "hat", "gloves", "pants", "stick", "jersey", "back", "hairstyle", "mouth_decal", "face_decal", "eyes_decal" };
                            foreach (string type in cosmeticTypes)
                            {
                                customization.DestroyCosmeticsOfType(type);
                            }

                            customization.currentCosmeticFullKey.Clear();

                            foreach (var cosmetic in playerData.CosmeticsData)
                            {
                                customization.LoadCosmetic(cosmetic.Type, cosmetic.Item, cosmetic.Variant);
                            }
                        }
                        else
                        {
                            MelonLogger.Error("PlayerCustomization component not found on player object.");
                        }

                        // Check for Player component
                        var teamComponent = playerObject.GetComponent<Player>();
                        if (teamComponent != null)
                        {
                            if (playerData.Teams.Count > 0)
                            {
                                teamComponent.Team = playerData.Teams[0];
                            }

                            if (playerData.Usernames.Count > 0)
                            {
                                teamComponent.Username = playerData.Usernames[0];
                            }

                            if (playerData.IsRightHanded.Count > 0)
                            {
                                teamComponent.SetRightHandedness(playerData.IsRightHanded[0]);
                            }
                        }
                        else
                        {
                            MelonLogger.Error("Player component not found on player object.");
                        }

                        // Handle missing child objects
                        foreach (var childData in playerData.ChildObjectsData)
                        {
                            Transform childTransform = playerObject.transform.Find(childData.Name);
                            if (childTransform != null)
                            {
                                childTransform.position = childData.Position.ToVector3();
                                childTransform.rotation = childData.Rotation.ToQuaternion();
                            }
                            else
                            {
                                MelonLogger.Error($"Child object '{childData.Name}' not found on player object.");
                            }
                        }
                    }
                }
            }
        }


        private void StopPlaybackAndReset()
        {
            isPlaying = false;
            isPaused = false;
            hasFinishedPlaying = false;
            currentFrameIndex = 0;
            lastPlaybackTime = 0.0f;
            MelonLogger.Msg("Playback stopped and reset.");
        }

        private void DisplayPopup(string message)
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2 - 100, Screen.height - 100, 200, 50), GUI.skin.box);
            GUILayout.Label(message);
            GUILayout.EndArea();
        }

        private IEnumerator TogglePlayersCoroutine()
        {
            GameObject hockeyGamemode = GameObject.Find("HockeyGamemode");

            if (hockeyGamemode != null)
            {
                GameObject game = hockeyGamemode.transform.Find("Game").gameObject;

                if (game != null)
                {
                    Game gameComponent = game.GetComponent<Game>();

                    if (gameComponent != null)
                    {
                        GameObject playerPrefab = gameComponent.playerPrefab;

                        if (playerPrefab != null)
                        {
                            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                            foreach (GameObject player in players)
                            {
                                GameObject.Destroy(player);
                            }

                            yield return new WaitForSeconds(0.1f);

                            if (recordedFrames.Count > 0)
                            {
                                FrameData initialFrame = recordedFrames[0];
                                int halfPlayerCount = initialFrame.PlayersData.Count / 2;

                                for (int i = 0; i < halfPlayerCount; i++)
                                {
                                    GameObject newPlayer = GameObject.Instantiate(playerPrefab);
                                    Transform newPlayerTransform = newPlayer.transform;
                                    GameObject bodyObject = newPlayerTransform.Find("body").gameObject;
                                    PlayerController playerController = bodyObject.GetComponent<PlayerController>();
                                    playerController.enabled = false;
                                }

                                ApplyInitialCosmetics();
                            }

                            GameObject mainCameraParent = GameObject.Find("HockeyGamemode");
                            if (mainCameraParent != null)
                            {
                                Transform spectatorCameraTransform = mainCameraParent.transform.Find("SpectatorCamera");
                                if (spectatorCameraTransform != null)
                                {
                                    GameObject spectatorCamera = spectatorCameraTransform.gameObject;
                                    spectatorCamera.SetActive(true);
                                    MelonLogger.Msg("SpectatorCamera has been set to active.");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogError("Player Prefab is not assigned in the Il2cpp.Game component.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Il2cpp.Game component is not found in the Game object.");
                    }
                }
                else
                {
                    Debug.LogError("Game object is not found in the HockeyGamemode object.");
                }
            }
            else
            {
                Debug.LogError("HockeyGamemode object is not found.");
            }
        }

        private async Task SaveRecording()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"replay_{timestamp}.dat";
                string path = Path.Combine(replayDirectory, filename);
                await Task.Run(() =>
                {
                    using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        IFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, recordedFrames);
                    }
                });
                MelonLogger.Msg($"Recording saved successfully as {filename}.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Failed to save recording: " + ex.Message);
            }
        }
    }

    [Serializable]
    public class FrameData
    {
        public List<PlayerData> PlayersData { get; set; }
        public SerializableVector3 PuckPosition { get; set; }
        public SerializableQuaternion PuckRotation { get; set; }
    }

    [Serializable]
    public class PlayerData
    {
        public SerializableVector3 Position { get; set; }
        public SerializableQuaternion Rotation { get; set; }
        public List<CosmeticData> CosmeticsData { get; set; }
        public List<ChildObjectData> ChildObjectsData { get; set; }
        public List<Team> Teams { get; set; }
        public List<string> Usernames { get; set; }
        public List<bool> IsRightHanded { get; set; }
    }

    [Serializable]
    public class CosmeticData
    {
        public string Type { get; set; }
        public string Item { get; set; }
        public string Variant { get; set; }
    }

    [Serializable]
    public class ChildObjectData
    {
        public string Name { get; set; }
        public SerializableVector3 Position { get; set; }
        public SerializableQuaternion Rotation { get; set; }
    }

    [Serializable]
    public struct SerializableVector3
    {
        public float x, y, z;

        public SerializableVector3(float rX, float rY, float rZ)
        {
            x = rX;
            y = rY;
            z = rZ;
        }

        public SerializableVector3(Vector3 vector3)
        {
            x = vector3.x;
            y = vector3.y;
            z = vector3.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public struct SerializableQuaternion
    {
        public float x, y, z, w;

        public SerializableQuaternion(float rX, float rY, float rZ, float rW)
        {
            x = rX;
            y = rY;
            z = rZ;
            w = rW;
        }

        public SerializableQuaternion(Quaternion quaternion)
        {
            x = quaternion.x;
            y = quaternion.y;
            z = quaternion.z;
            w = quaternion.w;
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }
}

//Make replays transferrable between versions
//Troubleshoot problem where replays continously play 
//Troubleshoot pressing different buttons play same replay
//Body object also have Player tag so get rid of them some
//PlaybackFrame gets tags every frame (TOP PRIORITY)